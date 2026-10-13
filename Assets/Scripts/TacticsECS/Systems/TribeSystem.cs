using System.Collections.Generic;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 종족(Tribes.csv)을 팀에 적용한다: 시작 별, 연구 가능한 기술(기술 그룹), 처음부터 해금된 기술, 시작 유닛.
    /// 지형 쪽(팀마다 종족 지형 묶음 / 수도 주변 시작 조건)은 RegionBiomes/StartCondition으로 꺼내 지형 생성에 넘긴다.
    /// 자체 상태는 없다 — 결과는 EconomyWorld(Resources/Tech/StartUnitIds)에만 남는다.
    /// 규칙: docs/spec/game/tribe.md
    /// </summary>
    public static class TribeSystem
    {
        public static bool IsValid(int tribeIndex) => tribeIndex >= 0 && tribeIndex < GameTables.Tribes.Length;

        /// <summary>econ.Tech[team]/Resources[team]이 이미 만들어진 뒤 부른다. econ.TechNodes는 배열형 기술 표(TechGroupSystem.BuildTechNodes)여야
        /// Index가 맞는다. unitRows는 이번 전투의 유닛 표 — StartUnit(Units Index)의 Id로 찾는다.</summary>
        public static void Apply(EconomyWorld econ, Team team, TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var res = econ.Resources[team];
            res.Stars = tribe.StartStars;
            econ.Resources[team] = res;

            var tech = econ.Tech[team];
            if (tribe.TechGroupIndex >= 0 && tribe.TechGroupIndex < GameTables.TechGroups.Length)
                tech.Allowed = TechGroupSystem.GroupTechIds(GameTables.TechGroups[tribe.TechGroupIndex], GameTables.Techs);
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) tech.Unlocked.Add(GameTables.Techs[t].Id);
            econ.Tech[team] = tech;

            econ.StartUnitIds[team] = StartUnitIds(tribe, unitRows);
            econ.StartCapitalLevels[team] = System.Math.Max(1, tribe.StartCapitalLevel);
        }

        public static string[] StartUnitIds(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var ids = new List<string>();
            foreach (var row in StartUnitRows(tribe, unitRows)) ids.Add(row.Id);
            return ids.ToArray();
        }

        /// <summary>StartUnit{n}(Units Index)이 가리키는 유닛을 unitRows에서 Id로 찾는다 — 불러온 유닛 표에 그 Id가 없으면 빠진다.</summary>
        public static List<UnitCsvRow> StartUnitRows(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var rows = new List<UnitCsvRow>();
            foreach (var u in tribe.StartUnits ?? new int[0])
            {
                if (unitRows == null || u < 0 || u >= GameTables.Units.Length) continue;
                string id = GameTables.Units[u].Id;
                foreach (var row in unitRows)
                    if (row.Id == id) { rows.Add(row); break; }
            }
            return rows;
        }

        /// <summary>TribeTerrain.csv에서 종족을 고르지 않은 팀(또는 묶음이 없는 종족)이 쓰는 묶음 Id.</summary>
        public const string DefaultTerrainId = "Default";

        /// <summary>팀마다 영역 하나(CitySystem.Teams 순서) — 그 팀 종족의 지형 묶음(TribeTerrain.csv). 종족이 없거나 묶음이 없으면
        /// Default 묶음, 그것도 없으면 첫 묶음.</summary>
        public static List<BiomeCsvRow> RegionBiomes(IReadOnlyList<TribeRow?> tribesPerTeam, IReadOnlyList<BiomeCsvRow> terrains)
        {
            var regions = new List<BiomeCsvRow>();
            if (terrains == null || terrains.Count == 0) return regions;
            foreach (var tribe in tribesPerTeam)
                regions.Add(Terrain(tribe?.Id, terrains));
            return regions;
        }

        /// <summary>Id가 같은 지형 묶음 → Default 묶음 → 첫 묶음.</summary>
        public static BiomeCsvRow Terrain(string tribeId, IReadOnlyList<BiomeCsvRow> terrains)
        {
            BiomeCsvRow fallback = null;
            foreach (var b in terrains)
            {
                if (tribeId != null && b.Id == tribeId) return b;
                if (fallback == null && b.Id == DefaultTerrainId) fallback = b;
            }
            return fallback ?? terrains[0];
        }

        /// <summary>종족의 시작 조건(없으면 null).</summary>
        public static StartConditionRow? StartCondition(TribeRow tribe) =>
            tribe.StartConditionIndex >= 0 && tribe.StartConditionIndex < GameTables.StartConditions.Length
                ? GameTables.StartConditions[tribe.StartConditionIndex] : (StartConditionRow?)null;

        /// <summary>샌드박스 정보 칸용 요약 한 줄: 시작 기술/별/유닛/시작 조건.</summary>
        public static string Summary(TribeRow tribe, IReadOnlyList<UnitCsvRow> unitRows)
        {
            var sb = new StringBuilder();
            sb.Append(tribe.Name).Append(": ");
            var techs = new List<string>();
            foreach (var t in tribe.StartTechs ?? new int[0])
                if (t >= 0 && t < GameTables.Techs.Length) techs.Add(GameTables.Techs[t].Name);
            sb.Append(techs.Count > 0 ? string.Join(",", techs) : LocalizationSystem.T("UI.Tribe.NoTech"))
              .Append(" / ").Append(LocalizationSystem.F("UI.Tribe.Stars", tribe.StartStars));
            var units = new List<string>();
            foreach (var row in StartUnitRows(tribe, unitRows)) units.Add(row.Name);
            if (units.Count > 0) sb.Append(" / ").Append(string.Join(",", units));
            var cond = StartCondition(tribe);
            if (cond != null && cond.Value.Rules != null && cond.Value.Rules.Length > 0) sb.Append(" / ").Append(cond.Value.Name);
            return sb.ToString();
        }
    }
}
