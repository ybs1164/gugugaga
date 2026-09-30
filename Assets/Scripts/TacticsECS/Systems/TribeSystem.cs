using System.Collections.Generic;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 종족(Tribes.csv)을 팀에 적용한다: 시작 골드, 연구 가능한 기술(기술 그룹), 처음부터 해금된 기술, 시작 유닛.
    /// 지형 쪽(종족 바이옴 / 수도 주변 시작 조건)은 BattleController가 RegionBiome/StartCondition으로 꺼내 지형 생성에 넘긴다.
    /// 자체 상태는 없다 — 결과는 EconomyWorld(Resources/Tech/StartUnitIds)에만 남는다.
    ///
    /// 원문과 다른 점(위키 Tribes): Luxidoor의 "레벨 3 수도로 시작"은 도시 레벨 시작값이 표에 없어 아직 반영하지 않았다(골드 2만 적용).
    /// 종족 고유 지형 배수는 바이옴(BiomeIndex)으로 근사한다 — 기본 바이옴 표에는 평원/사막/고지대 3개뿐이라 여러 종족이 같은 바이옴을 쓴다.
    /// </summary>
    public static class TribeSystem
    {
        public static bool IsValid(int tribeIndex) => tribeIndex >= 0 && tribeIndex < GameTables.Tribes.Length;

        /// <summary>econ.Tech[team]/Resources[team]이 이미 만들어진 뒤 부른다. econ.TechNodes는 배열형 기술 표(TechGroupSystem.BuildTechNodes)여야
        /// Index가 맞는다. unitRows는 StartUnit Index가 가리키는 유닛 표(행 순서).</summary>
        public static void Apply(EconomyWorld econ, Team team, TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var res = econ.Resources[team];
            res.Gold = tribe.StartGold;
            econ.Resources[team] = res;

            var tech = econ.Tech[team];
            if (tribe.TechGroupIndex >= 0 && tribe.TechGroupIndex < GameTables.TechGroups.Length)
                tech.Allowed = TechGroupSystem.GroupTechIds(GameTables.TechGroups[tribe.TechGroupIndex], GameTables.Techs);
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) tech.Unlocked.Add(GameTables.Techs[t].Id);
            econ.Tech[team] = tech;

            econ.StartUnitIds[team] = StartUnitIds(tribe, unitRows);
        }

        public static string[] StartUnitIds(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var ids = new List<string>();
            foreach (var u in tribe.StartUnits ?? new int[0])
                if (unitRows != null && u >= 0 && u < unitRows.Count) ids.Add(unitRows[u].Id);
            return ids.ToArray();
        }

        /// <summary>종족의 바이옴(범위 밖이면 0번 바이옴).</summary>
        public static BiomeCsvRow RegionBiome(TribeRow tribe, IReadOnlyList<BiomeCsvRow> biomes) =>
            biomes[tribe.BiomeIndex >= 0 && tribe.BiomeIndex < biomes.Count ? tribe.BiomeIndex : 0];

        /// <summary>종족의 시작 조건(없으면 null).</summary>
        public static StartConditionRow? StartCondition(TribeRow tribe) =>
            tribe.StartConditionIndex >= 0 && tribe.StartConditionIndex < GameTables.StartConditions.Length
                ? GameTables.StartConditions[tribe.StartConditionIndex] : (StartConditionRow?)null;

        /// <summary>샌드박스 정보 칸용 요약 한 줄: 바이옴/시작 기술/골드/유닛/시작 조건.</summary>
        public static string Summary(TribeRow tribe, IReadOnlyList<BiomeCsvRow> biomes, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var sb = new StringBuilder();
            sb.Append(tribe.Name).Append(": ");
            sb.Append(biomes != null && biomes.Count > 0 ? RegionBiome(tribe, biomes).Name : "?").Append(" / ");
            var techs = new List<string>();
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) techs.Add(GameTables.Techs[t].Name);
            sb.Append(techs.Count > 0 ? string.Join(",", techs) : "기술 없음").Append(" / 골드 ").Append(tribe.StartGold);
            var units = new List<string>();
            foreach (var u in tribe.StartUnits ?? new int[0])
                if (unitRows != null && u >= 0 && u < unitRows.Count) units.Add(unitRows[u].Name);
            if (units.Count > 0) sb.Append(" / ").Append(string.Join(",", units));
            var cond = StartCondition(tribe);
            if (cond != null && cond.Value.Rules != null && cond.Value.Rules.Length > 0) sb.Append(" / ").Append(cond.Value.Name);
            return sb.ToString();
        }
    }
}
