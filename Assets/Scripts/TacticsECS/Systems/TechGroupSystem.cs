using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 배열형 기술 표(Techs/TechUnlocks/TechGroups)를 기존 기술 로직(TechSystem/TechTreeHud)이 쓰는 TechNodeData로 바꾸고,
    /// 종족별 기술 그룹을 팀의 연구 가능 목록(TechTreeData.Allowed)으로 만든다. 자체 상태는 없다.
    /// 해금 판정은 여전히 "해금 키"(TechUnlockRow.Category + "." + Target) 문자열로 한다 — 건물/유닛/이동 등 기존 시스템은 바뀌지 않는다.
    /// </summary>
    public static class TechGroupSystem
    {
        /// <summary>해금 내역 하나의 해금 키. Target이 비면 Category 자체(예: "Literacy").</summary>
        public static string Key(TechUnlockRow unlock) =>
            string.IsNullOrEmpty(unlock.Target) ? unlock.Category : unlock.Category + "." + unlock.Target;

        /// <summary>고정 슬롯 순서로 기술을 배치한다. 선행 기술은 선행 슬롯에 현재 배치된 기술로 결정한다.
        /// 잘못된 배치를 부분적으로 적용하면 선행 조건이 사라질 수 있으므로 빈 트리를 반환한다.</summary>
        public static List<TechNodeData> BuildTechNodes(TechRow[] techs, TechUnlockRow[] unlocks,
            TechSlotRow[] slots, TechTreeLayoutRow[] layout, List<string> errors = null)
        {
            var nodes = new List<TechNodeData>(slots.Length);
            var warnings = ArrayTableValidationSystem.ValidateTechLayout(techs, slots, layout);
            if (warnings.Count > 0)
            {
                errors?.AddRange(warnings);
                return nodes;
            }
            var techBySlot = new int[slots.Length];
            foreach (var placement in layout) techBySlot[placement.SlotIndex] = placement.TechIndex;
            foreach (var slot in slots)
            {
                var t = techs[techBySlot[slot.Index]];
                var keys = new List<string>();
                foreach (var u in t.Unlocks ?? new int[0])
                    if (u >= 0 && u < unlocks.Length) keys.Add(Key(unlocks[u]));
                nodes.Add(new TechNodeData
                {
                    Id = t.Id,
                    Name = t.Name,
                    Branch = techs[techBySlot[RootIndex(slots, slot.Index)]].Id,
                    ParentId = slot.ParentIndex >= 0 ? techs[techBySlot[slot.ParentIndex]].Id : string.Empty,
                    Tier = slot.Tier,
                    Slot = slot.Slot,
                    Icon = t.Icon,
                    CostBase = t.CostBase,
                    CostPerCity = t.CostPerCity,
                    Unlocks = keys.ToArray(),
                    Effect = t.Description,
                });
            }
            return nodes;
        }

        public static List<TechNodeData> BuildTechNodes() =>
            BuildTechNodes(GameTables.Techs, GameTables.TechUnlocks, GameTables.TechSlots, GameTables.TechTreeLayout);

        /// <summary>기술 그룹에 든 기술 Id 집합(범위 밖 Index는 무시).</summary>
        public static HashSet<string> GroupTechIds(TechGroupRow group, TechRow[] techs)
        {
            var ids = new HashSet<string>();
            foreach (var i in group.Techs ?? new int[0])
                if (i >= 0 && i < techs.Length) ids.Add(techs[i].Id);
            return ids;
        }

        /// <summary>allowed에 든 노드만(트리 표시용). allowed가 null이면 전부.</summary>
        public static List<TechNodeData> Filter(IReadOnlyList<TechNodeData> nodes, HashSet<string> allowed)
        {
            var result = new List<TechNodeData>();
            foreach (var n in nodes)
                if (allowed == null || allowed.Contains(n.Id)) result.Add(n);
            return result;
        }

        private static int RootIndex(TechSlotRow[] slots, int index)
        {
            while (slots[index].ParentIndex >= 0) index = slots[index].ParentIndex;
            return index;
        }
    }
}
