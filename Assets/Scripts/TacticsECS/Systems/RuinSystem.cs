using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛이 쓰는 경제 행동 중 도시/타일 쪽이 아닌 것 — 유적 탐험과 해산(자유 영혼). 순수 함수형, 자체 상태 없음.
    ///   - 유적 탐험: 유적 칸에 선 유닛이 이번 턴 행동을 써서 탐험하면 유적이 사라지고 보상 하나(폴리토피아
    ///     Ruins: 별 10 / 무료 기술 / 인구 +3 / 유닛)를 무작위로 받는다.
    ///   - 해산: "Ability.Disband" 해금 시 자기 유닛을 없애고 훈련 비용 절반(내림)을 골드로 돌려받는다.
    /// </summary>
    public static class RuinSystem
    {
        public const int GoldReward = 10;
        public const int PopulationReward = 3;
        public const string DisbandKey = "Ability.Disband";

        public static bool CanExplore(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId) =>
            econ != null && UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value &&
            grid.GetStructure(world.Get<GridPosition>(unitId).Value) == StructureGenerationSystem.RuinStructureId;

        public static bool Explore(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, List<EconomyLogEntry> log)
        {
            if (!CanExplore(grid, world, econ, unitId)) return false;
            var team = world.Get<Team>(unitId);
            var pos = world.Get<GridPosition>(unitId).Value;
            grid.SetStructure(pos, string.Empty);
            world.Set(unitId, new HasActed { Value = true });

            var entry = new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Explore, Position = pos, CityIndex = -1 };
            var res = econ.Resources[team];
            int roll = CitySystem.NextRandom(econ, 4);

            if (roll == 1)
            {
                var candidates = new List<TechNodeData>();
                foreach (var n in econ.TechNodes)
                    if (TechSystem.IsAvailable(econ.TechNodes, econ.Tech[team], n.Id)) candidates.Add(n);
                if (candidates.Count > 0)
                {
                    var pick = candidates[CitySystem.NextRandom(econ, candidates.Count)];
                    econ.Tech[team].Unlocked.Add(pick.Id);
                    entry.Subject = $"기술 {pick.Name}";
                }
                else roll = 0;
            }
            if (roll == 2)
            {
                int city = NearestOwnCity(econ, team, pos);
                if (city >= 0)
                {
                    entry.Subject = $"인구 +{PopulationReward} ({econ.Cities[city].Name})";
                    entry.CityIndex = city;
                    log?.Add(entry);
                    CitySystem.AddPopulation(econ, city, PopulationReward, log);
                    return true;
                }
                roll = 0;
            }
            if (roll == 3)
            {
                string unit = CheapestUnitId(econ);
                if (!string.IsNullOrEmpty(unit)) { entry.Subject = "유닛"; entry.SpawnUnitId = unit; }
                else roll = 0;
            }
            if (roll == 0)
            {
                res.Gold += GoldReward;
                econ.Resources[team] = res;
                entry.Subject = $"골드 +{GoldReward}";
            }
            log?.Add(entry);
            return true;
        }

        private static int NearestOwnCity(EconomyWorld econ, Team team, Vector2Int pos)
        {
            int best = -1, bestDist = int.MaxValue;
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                if (econ.Cities[i].Owner != team) continue;
                int d = PathfindingSystem.Distance(econ.Cities[i].Position, pos);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        private static string CheapestUnitId(EconomyWorld econ)
        {
            UnitCsvRow best = null;
            foreach (var row in econ.UnitRows)
                if (best == null || row.Cost < best.Cost) best = row;
            return best?.Id ?? string.Empty;
        }

        public static bool CanDisband(EntityWorld world, EconomyWorld econ, int unitId) =>
            econ != null && UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value &&
            TechSystem.HasUnlock(econ.TechNodes, econ.Tech[world.Get<Team>(unitId)], DisbandKey);

        public static int DisbandRefund(EntityWorld world, EconomyWorld econ, int unitId)
        {
            var row = CitySystem.FindUnitRow(econ, world.Get<UnitTypeId>(unitId).Value);
            return (row?.Cost ?? 0) / 2;
        }

        /// <summary>유닛을 죽은 것과 같은 방식(Hp=0 + 그리드에서 제거)으로 치우고 환급한다.</summary>
        public static bool Disband(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, List<EconomyLogEntry> log)
        {
            if (!CanDisband(world, econ, unitId)) return false;
            var team = world.Get<Team>(unitId);
            int refund = DisbandRefund(world, econ, unitId);
            var pos = world.Get<GridPosition>(unitId).Value;

            grid.RemoveOccupant(pos);
            world.Set(unitId, new Hp { Value = 0 });
            var res = econ.Resources[team];
            res.Gold += refund;
            econ.Resources[team] = res;
            log?.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Disband, Subject = $"골드 +{refund}", Position = pos, CityIndex = -1 });
            return true;
        }
    }
}
