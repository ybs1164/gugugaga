namespace TacticsECS
{
    /// <summary>
    /// 기술트리 해금 판정/소모를 담당하는 순수 함수형 시스템. CityResourceSystem과 마찬가지로 자체 상태는
    /// 없다 — TechTreeData/CityResourceData 값을 인자로 받아 다음 값을 계산해 반환할 뿐이고, 실제 보관은
    /// 호출자(BattleController)가 맡는다.
    ///
    /// 각 기술 노드가 실제로 여는 효과(산악 신전 건설, 현자 훈련 등 TechNodeData.Effect에 적힌 것들)는
    /// 건물/유닛 훈련/지형 시스템 자체가 아직 없어 구현하지 않았다 — 여기서는 "이 기술이 해금됐는가"라는
    /// 사실만 관리하고, IsUnlocked가 그 질의 지점이다. 해당 시스템들이 생기면 각자 IsUnlocked를 참조해
    /// 실제 효과를 적용하면 된다(WaitAction의 IsInOwnTerritory, CityResourceSystem.IsConnectedToCapital과
    /// 같은 이유의 플레이스홀더).
    /// </summary>
    public static class TechSystem
    {
        public static TechNodeData? Find(TechId id)
        {
            foreach (var node in TechTreeDefinition.Nodes)
            {
                if (node.Id == id) return node;
            }
            return null;
        }

        public static bool IsUnlocked(TechTreeData tech, TechId id) => tech.Unlocked.Contains(id);

        /// <summary>선행 기술이 해금되어 있어(또는 1티어 루트라 선행 기술이 없어) 해금을 시도할 수 있는
        /// 상태인지. 비용 충분 여부는 별도(CanUnlock)다.</summary>
        public static bool IsAvailable(TechTreeData tech, TechId id)
        {
            if (IsUnlocked(tech, id)) return false;

            var node = Find(id);
            if (node == null) return false;

            return node.Value.ParentId == TechId.None || IsUnlocked(tech, node.Value.ParentId);
        }

        public static bool CanUnlock(TechTreeData tech, CityResourceData city, TechId id)
        {
            if (!IsAvailable(tech, id)) return false;

            var node = Find(id);
            return node != null && city.Development >= node.Value.Cost;
        }

        /// <summary>해금 가능(CanUnlock)할 때만 실제로 상태를 바꾼다 — 아니면 인자로 받은 값을 그대로
        /// 돌려준다. 해금과 동시에 도시 발전도를 소모하므로, 갱신된 CityResourceData를 out으로 함께
        /// 돌려준다. TechTreeData.Unlocked는 참조 타입(HashSet)이라, 해금에 성공하면 인자로 받은 tech
        /// 자체가 제자리에서 바뀐다(CityResourceData처럼 값 전체가 새로 만들어지지는 않는다).</summary>
        public static TechTreeData Unlock(TechTreeData tech, CityResourceData city, TechId id, out CityResourceData updatedCity)
        {
            updatedCity = city;
            if (!CanUnlock(tech, city, id)) return tech;

            var node = Find(id).Value;
            updatedCity.Development -= node.Cost;
            tech.Unlocked.Add(id);
            return tech;
        }
    }
}
