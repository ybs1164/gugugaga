using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 배열형 테이블 사이의 Index 참조가 범위 안인지 확인한다(파서는 칸 형식만 본다). 경고 목록만 돌려주고 불러오기는 막지 않는다.
    ///   - TechSlots 선행 슬롯, TechTreeLayout 슬롯별 기술, Techs.Unlock*, TechGroups.Tech*, 종족/시작 조건 참조
    ///   - 시작 기술이 그 종족 기술 그룹 안에 있는지(밖이면 트리에 안 보이는 기술이 해금된 채 시작한다)
    ///   - biomeCount/unitCount를 주면 BiomeIndex/StartUnit*도(바이옴·유닛 표는 샌드박스에서 바뀔 수 있어 호출자가 넘긴다, 음수면 생략)
    ///   - 시작 조건 규칙의 거리/개수 값
    /// 자체 상태는 없다.
    /// </summary>
    public static class ArrayTableValidationSystem
    {
        public static List<string> Validate(TechUnlockRow[] unlocks, TechRow[] techs, TechGroupRow[] groups, TribeRow[] tribes,
            StartConditionRow[] conditions, StartConditionRuleRow[] rules, int biomeCount = -1, int unitCount = -1)
        {
            var w = new List<string>();
            var techIds = new HashSet<string>();
            foreach (var t in techs)
            {
                if (!techIds.Add(t.Id)) w.Add($"Techs[{t.Index}]: 중복 Id '{t.Id}'");
                if (string.IsNullOrEmpty(t.Id)) w.Add($"Techs[{t.Index}]: Id가 비어 있음");
                if (t.CostBase < 0 || t.CostPerCity < 0) w.Add($"Techs[{t.Index}] {t.Id}: 비용이 음수");
                foreach (var u in t.Unlocks ?? new int[0])
                    if (!InRange(u, unlocks.Length)) w.Add($"Techs[{t.Index}] {t.Id}: Unlock {u}이(가) TechUnlocks 범위 밖");
            }
            foreach (var u in unlocks)
            {
                if (string.IsNullOrEmpty(u.Category)) w.Add($"TechUnlocks[{u.Index}]: Category가 비어 있음");
                if (!System.Enum.IsDefined(typeof(TechUnlockKind), u.Kind)) w.Add($"TechUnlocks[{u.Index}]: Kind가 잘못됨");
            }

            foreach (var g in groups)
                foreach (var t in g.Techs ?? new int[0])
                    if (!InRange(t, techs.Length)) w.Add($"TechGroups[{g.Index}] {g.Id}: Tech {t}이(가) Techs 범위 밖");

            foreach (var tr in tribes)
            {
                string who = $"Tribes[{tr.Index}] {tr.Id}";
                if (biomeCount >= 0 && !InRange(tr.BiomeIndex, biomeCount)) w.Add($"{who}: BiomeIndex {tr.BiomeIndex}이(가) 바이옴 {biomeCount}개 범위 밖");
                bool groupOk = InRange(tr.TechGroupIndex, groups.Length);
                if (!groupOk) w.Add($"{who}: TechGroupIndex {tr.TechGroupIndex} 범위 밖");
                foreach (var t in tr.StartTechs ?? new int[0])
                {
                    if (!InRange(t, techs.Length)) w.Add($"{who}: StartTech {t}이(가) Techs 범위 밖");
                    else if (groupOk && System.Array.IndexOf(groups[tr.TechGroupIndex].Techs ?? new int[0], t) < 0)
                        w.Add($"{who}: 시작 기술 {techs[t].Id}이(가) 기술 그룹 {groups[tr.TechGroupIndex].Id}에 없음");
                }
                if (unitCount >= 0)
                    foreach (var u in tr.StartUnits ?? new int[0])
                        if (!InRange(u, unitCount)) w.Add($"{who}: StartUnit {u}이(가) 유닛 {unitCount}개 범위 밖");
                if (tr.StartConditionIndex != -1 && !InRange(tr.StartConditionIndex, conditions.Length))
                    w.Add($"{who}: StartConditionIndex {tr.StartConditionIndex} 범위 밖");
            }

            foreach (var c in conditions)
                foreach (var r in c.Rules ?? new int[0])
                    if (!InRange(r, rules.Length)) w.Add($"StartConditions[{c.Index}] {c.Id}: Rule {r}이(가) StartConditionRules 범위 밖");

            foreach (var r in rules)
            {
                string who = $"StartConditionRules[{r.Index}] {r.Id}";
                if (string.IsNullOrEmpty(r.Target)) w.Add($"{who}: Target이 비어 있음");
                if (r.Count < 0) w.Add($"{who}: Count가 음수");
                if (r.MinDistance < 1 || r.MaxDistance < r.MinDistance) w.Add($"{who}: 거리 {r.MinDistance}~{r.MaxDistance}가 잘못됨(1 이상, Min ≤ Max)");
                if (r.Kind == StartRuleKind.Structure && System.Array.FindIndex(StructureDefinition.All, s => s.Id == r.Target) < 0)
                    w.Add($"{who}: 구조물 '{r.Target}'이(가) 없음");
            }
            return w;
        }

        /// <summary>슬롯 구조와 배치를 검사한다. 기술 개수는 슬롯보다 많아도 되지만 슬롯마다 서로 다른 기술 하나가 필요하다.</summary>
        public static List<string> ValidateTechLayout(TechRow[] techs, TechSlotRow[] slots, TechTreeLayoutRow[] layout)
        {
            var w = new List<string>();
            var ids = new HashSet<string>();
            var techIds = new HashSet<string>();
            for (int i = 0; i < techs.Length; i++)
                if (string.IsNullOrEmpty(techs[i].Id) || !techIds.Add(techs[i].Id))
                    w.Add($"Techs[{i}]: Id가 비어 있거나 중복됨");
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                string who = $"TechSlots[{i}] {s.Id}";
                if (s.Index != i) w.Add($"{who}: Index가 배열 위치와 다름");
                if (string.IsNullOrEmpty(s.Id) || !ids.Add(s.Id)) w.Add($"{who}: Id가 비어 있거나 중복됨");
                if (s.Tier < 1 || s.Tier > 3 || s.Slot < 0 || s.Slot > 1) w.Add($"{who}: Tier/Slot 배치 범위 밖");
                if (s.ParentIndex == -1)
                {
                    if (s.Tier != 1 || s.Slot != 0) w.Add($"{who}: 루트는 Tier 1 / Slot 0이어야 함");
                }
                else if (!InRange(s.ParentIndex, slots.Length)) w.Add($"{who}: ParentIndex 범위 밖");
                else
                {
                    var parent = slots[s.ParentIndex];
                    if (parent.Tier != s.Tier - 1) w.Add($"{who}: 선행 슬롯 Tier는 현재 Tier보다 1 작아야 함");
                    if (s.Tier == 3 && parent.Slot != s.Slot) w.Add($"{who}: 3티어 Slot이 선행 슬롯과 다름");
                }
                var seen = new HashSet<int>();
                int current = i;
                while (InRange(current, slots.Length))
                {
                    if (!seen.Add(current)) { w.Add($"{who}: 선행 슬롯 순환"); break; }
                    current = slots[current].ParentIndex;
                }
            }
            // 같은 부모에서 같은 방향으로 분기하면 UI 위치가 겹친다.
            var branches = new HashSet<string>();
            foreach (var s in slots)
                if (s.ParentIndex >= 0 && !branches.Add(s.ParentIndex + ":" + s.Slot))
                    w.Add($"TechSlots[{s.Index}]: 같은 선행 슬롯의 Slot 위치 중복");
            var occupied = new HashSet<int>();
            var assigned = new HashSet<int>();
            foreach (var p in layout)
            {
                string who = $"TechTreeLayout[{p.Index}]";
                if (!InRange(p.SlotIndex, slots.Length)) w.Add($"{who}: SlotIndex 범위 밖");
                else if (!occupied.Add(p.SlotIndex)) w.Add($"{who}: 슬롯 중복 배치");
                if (!InRange(p.TechIndex, techs.Length)) w.Add($"{who}: TechIndex 범위 밖");
                else if (!assigned.Add(p.TechIndex)) w.Add($"{who}: 기술 중복 배치");
            }
            for (int i = 0; i < slots.Length; i++)
                if (!occupied.Contains(i)) w.Add($"TechSlots[{i}]: 기술 배치 누락");
            return w;
        }

        /// <summary>GameTables에 올라간 표 전부를 검사한다.</summary>
        public static List<string> ValidateLoaded(int biomeCount = -1, int unitCount = -1)
        {
            var w = Validate(GameTables.TechUnlocks, GameTables.Techs, GameTables.TechGroups, GameTables.Tribes,
                GameTables.StartConditions, GameTables.StartConditionRules, biomeCount, unitCount);
            w.AddRange(ValidateTechLayout(GameTables.Techs, GameTables.TechSlots, GameTables.TechTreeLayout));
            var placed = new HashSet<int>();
            foreach (var p in GameTables.TechTreeLayout) placed.Add(p.TechIndex);
            foreach (var tr in GameTables.Tribes)
                foreach (var t in tr.StartTechs ?? new int[0])
                    if (!placed.Contains(t)) w.Add($"Tribes[{tr.Index}] {tr.Id}: 시작 기술 {t}이(가) 트리에 배치되지 않음");
            return w;
        }

        private static bool InRange(int i, int count) => i >= 0 && i < count;
    }
}
