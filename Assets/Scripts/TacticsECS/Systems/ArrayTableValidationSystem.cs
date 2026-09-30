using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 배열형 테이블 사이의 Index 참조가 범위 안인지 확인한다(파서는 칸 형식만 본다). 경고 목록만 돌려주고 불러오기는 막지 않는다.
    ///   - Techs.ParentIndex / Unlock*, TechGroups.Tech*, Tribes의 TechGroupIndex/StartTech*/StartConditionIndex, StartConditions.Rule*
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
                if (t.ParentIndex != -1 && !InRange(t.ParentIndex, techs.Length)) w.Add($"Techs[{t.Index}] {t.Id}: ParentIndex {t.ParentIndex} 범위 밖");
                else if (t.ParentIndex == t.Index) w.Add($"Techs[{t.Index}] {t.Id}: 선행 기술이 자기 자신");
                foreach (var u in t.Unlocks ?? new int[0])
                    if (!InRange(u, unlocks.Length)) w.Add($"Techs[{t.Index}] {t.Id}: Unlock {u}이(가) TechUnlocks 범위 밖");
            }
            foreach (var u in unlocks)
                if (string.IsNullOrEmpty(u.Category)) w.Add($"TechUnlocks[{u.Index}]: Category가 비어 있음");

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

        /// <summary>GameTables에 올라간 표 전부를 검사한다.</summary>
        public static List<string> ValidateLoaded(int biomeCount = -1, int unitCount = -1) =>
            Validate(GameTables.TechUnlocks, GameTables.Techs, GameTables.TechGroups, GameTables.Tribes,
                GameTables.StartConditions, GameTables.StartConditionRules, biomeCount, unitCount);

        private static bool InRange(int i, int count) => i >= 0 && i < count;
    }
}
