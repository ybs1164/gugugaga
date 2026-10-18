using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 경제가 켜진 전투의 시작 상태를 만든다: 팀별 자원·기술·신·종족 적용(Create), 그다음 시야를 켜고 수도를 세우고
    /// 시작 유닛을 요청한다(Start). 시작 유닛 스폰은 View가 필요해 호출자가 반환된 기록으로 마무리한다.
    /// 규칙: docs/spec/game/city.md, docs/spec/game/tribe.md
    /// </summary>
    public static class EconomySetupSystem
    {
        /// <summary>샌드박스에서 유닛 표를 고르지 않았을 때 쓰는 유닛 행(GameTables.Units 복사본).</summary>
        public static List<UnitCsvRow> DefaultUnitRows() => new List<UnitCsvRow>(GameTables.Units);

        /// <summary>unitRows가 비어 있으면 DefaultUnitRows를 쓴다. tribeByTeam에 값이 있는 팀은 TribeSystem.Apply로
        /// 시작 별·기술 그룹·시작 기술·시작 유닛을 받는다.</summary>
        public static EconomyWorld Create(List<TechNodeData> techNodes, List<UnitCsvRow> unitRows,
            IReadOnlyDictionary<Team, int> godByTeam, IReadOnlyDictionary<Team, TribeRow?> tribeByTeam)
        {
            var econ = new EconomyWorld { TechNodes = techNodes, UnitRows = unitRows ?? new List<UnitCsvRow>() };
            if (econ.UnitRows.Count == 0) econ.UnitRows = DefaultUnitRows();
            foreach (var team in CitySystem.Teams)
            {
                var res = CityResourceData.Create(0, 0, false);
                res.Stars = GameRules.Economy.StartingStars;
                econ.Resources[team] = res;
                econ.Tech[team] = TechTreeData.CreateEmpty();
                econ.GodIds[team] = godByTeam.TryGetValue(team, out int god) ? god : -1;
                if (tribeByTeam.TryGetValue(team, out var tribe) && tribe is TribeRow row) TribeSystem.Apply(econ, team, row, econ.UnitRows);
            }
            TaskSystem.Init(econ);
            return econ;
        }

        /// <summary>시야(구름)를 켜고(경제가 켜진 전투에서만 쓴다), 수도를 세운 뒤, 유닛 없이 시작한 팀의 수도에 줄
        /// 기본 유닛 요청(CitySystem.StartingUnitRequests)을 돌려준다.</summary>
        public static List<EconomyLogEntry> Start(GridWorld grid, EntityWorld world, EconomyWorld econ,
            IReadOnlyDictionary<Team, Vector2Int> tribeCapitals, IReadOnlyDictionary<Team, TribeRow?> tribeByTeam)
        {
            grid.FogEnabled = true;
            FoundTribeCapitals(grid, econ, tribeCapitals, tribeByTeam);
            CitySystem.InitializeCapitals(grid, world, econ, includeUnitlessTeams: true);
            return CitySystem.StartingUnitRequests(world, econ);
        }

        /// <summary>생성한 지형이면 팀마다 자기 영역의 수도 자리(tribeCapitals)를 수도로 세운다 — 시작 조건이 그 수도 주변에
        /// 적용됐으므로. 지형을 생성하지 않았으면 InitializeCapitals의 기본 규칙(유닛 무게중심에서 가장 가까운 수도)을 따른다.</summary>
        private static void FoundTribeCapitals(GridWorld grid, EconomyWorld econ,
            IReadOnlyDictionary<Team, Vector2Int> tribeCapitals, IReadOnlyDictionary<Team, TribeRow?> tribeByTeam)
        {
            foreach (var kv in tribeCapitals)
            {
                var pos = kv.Value;
                if (!grid.InBounds(pos) || grid.GetStructure(pos) != StructureGenerationSystem.CapitalStructureId) continue;
                if (CitySystem.FindCapital(econ, kv.Key) >= 0 || CitySystem.FindCityAt(econ, pos) >= 0) continue;
                tribeByTeam.TryGetValue(kv.Key, out var tribe);
                string name = tribe != null ? LocalizationSystem.F("UI.Common.TribeCapital", tribe.Value.Name)
                    : LocalizationSystem.T(kv.Key == Team.Player ? "UI.Common.AllyCapital" : "UI.Common.EnemyCapital");
                CitySystem.FoundCity(grid, econ, pos, kv.Key, true, name);
                VisionSystem.Reveal(grid, kv.Key, pos, GameRules.Vision.StartRevealRadius);
            }
        }
    }
}
