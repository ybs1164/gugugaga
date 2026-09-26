using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 기술트리 해금 판정/비용/효과 조회를 담당하는 순수 함수형 시스템. 자체 상태는 없다 — 기술 정의
    /// 목록(EconomyWorld.TechNodes, CSV에서 파싱)과 팀의 TechTreeData/CityResourceData를 인자로 받는다.
    ///
    /// 비용은 폴리토피아 공식 "(티어) x (도시 수) + 4"를 CSV의 CostBase/CostPerCity로 일반화한 값이고, 해금
    /// 키 "Literacy"를 가진 팀은 폴리토피아 Literacy와 같이 1/3을 깎는다(올림 — 위키 비용 표와 일치).
    /// 기술이 여는 실제 효과는 기술 이름이 아니라 해금 키로만 조회한다(HasUnlock) — 건물 표(BuildingInfo.
    /// UnlockKey), 타일 행동 표, 유닛 훈련("Unit.&lt;유닛 CSV Id&gt;"), 지형 이동/방어("Move.*"/"Defense.*"),
    /// 자원 공개("Reveal.&lt;StructureId&gt;")가 모두 같은 방식이다.
    /// </summary>
    public static class TechSystem
    {
        public const string LiteracyKey = "Literacy";
        public const string UnitKeyPrefix = "Unit.";
        public const string RevealKeyPrefix = "Reveal.";

        public static TechNodeData? Find(IReadOnlyList<TechNodeData> nodes, string id)
        {
            if (string.IsNullOrEmpty(id) || nodes == null) return null;
            foreach (var node in nodes)
                if (node.Id == id) return node;
            return null;
        }

        public static bool IsUnlocked(TechTreeData tech, string id) => tech.Unlocked != null && tech.Unlocked.Contains(id);

        /// <summary>선행 기술이 해금되어 있어(또는 1티어 루트라 선행 기술이 없어) 해금을 시도할 수 있는
        /// 상태인지. 비용 충분 여부는 별도(CanUnlock)다.</summary>
        public static bool IsAvailable(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, string id)
        {
            if (IsUnlocked(tech, id)) return false;
            var node = Find(nodes, id);
            if (node == null) return false;
            return string.IsNullOrEmpty(node.Value.ParentId) || IsUnlocked(tech, node.Value.ParentId);
        }

        /// <summary>도시 수를 반영한 실제 연구 비용. cityCount가 0이어도 CostBase는 든다.</summary>
        public static int Cost(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, TechNodeData node, int cityCount)
        {
            int cost = node.CostBase + node.CostPerCity * (cityCount < 0 ? 0 : cityCount);
            if (HasUnlock(nodes, tech, LiteracyKey))
                cost = (cost * 2 + 2) / 3; // ceil(cost * 2/3)
            return cost < 0 ? 0 : cost;
        }

        public static bool CanUnlock(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, CityResourceData resources, int cityCount, string id)
        {
            if (!IsAvailable(nodes, tech, id)) return false;
            var node = Find(nodes, id);
            return node != null && resources.Development >= Cost(nodes, tech, node.Value, cityCount);
        }

        /// <summary>해금 가능(CanUnlock)할 때만 발전도를 소모하고 해금 집합에 추가한다. TechTreeData.Unlocked는
        /// 참조 타입(HashSet)이라 성공하면 인자로 받은 tech 자체가 제자리에서 바뀐다.</summary>
        public static bool Unlock(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, ref CityResourceData resources, int cityCount, string id)
        {
            if (!CanUnlock(nodes, tech, resources, cityCount, id)) return false;
            resources.Development -= Cost(nodes, tech, Find(nodes, id).Value, cityCount);
            tech.Unlocked.Add(id);
            return true;
        }

        /// <summary>해금된 기술 중 하나라도 이 해금 키를 가지고 있는지.</summary>
        public static bool HasUnlock(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, string key)
        {
            if (nodes == null || tech.Unlocked == null) return false;
            foreach (var node in nodes)
            {
                if (!tech.Unlocked.Contains(node.Id) || node.Unlocks == null) continue;
                foreach (var k in node.Unlocks)
                    if (k == key) return true;
            }
            return false;
        }

        /// <summary>트리 어딘가에 이 해금 키를 가진 노드가 있는지(있으면 그 기술 없이는 잠겨 있다).</summary>
        public static bool IsKeyGated(IReadOnlyList<TechNodeData> nodes, string key)
        {
            if (nodes == null) return false;
            foreach (var node in nodes)
            {
                if (node.Unlocks == null) continue;
                foreach (var k in node.Unlocks)
                    if (k == key) return true;
            }
            return false;
        }

        /// <summary>이 유닛 CSV Id를 훈련할 수 있는지 — 트리가 "Unit.&lt;id&gt;"로 잠그지 않은 유닛(보병 등 기본
        /// 유닛)은 항상 가능하고, 잠근 유닛은 그 기술을 해금해야 한다.</summary>
        public static bool CanTrainUnitType(IReadOnlyList<TechNodeData> nodes, TechTreeData tech, string unitId)
        {
            string key = UnitKeyPrefix + unitId;
            return !IsKeyGated(nodes, key) || HasUnlock(nodes, tech, key);
        }

        /// <summary>이 팀에게 아직 보이지 않는 구조물 Id 집합("Reveal.X" 키가 트리에 있는데 해금 안 됨).</summary>
        public static HashSet<string> HiddenStructures(IReadOnlyList<TechNodeData> nodes, TechTreeData tech)
        {
            var hidden = new HashSet<string>();
            if (nodes == null) return hidden;
            foreach (var node in nodes)
            {
                if (node.Unlocks == null) continue;
                foreach (var k in node.Unlocks)
                {
                    if (!k.StartsWith(RevealKeyPrefix)) continue;
                    if (!HasUnlock(nodes, tech, k)) hidden.Add(k.Substring(RevealKeyPrefix.Length));
                }
            }
            return hidden;
        }
    }
}
