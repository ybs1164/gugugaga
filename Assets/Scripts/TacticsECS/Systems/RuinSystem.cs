using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛이 쓰는 경제 행동 중 도시/타일 쪽이 아닌 것 — 유적 탐험과 해산(자유 영혼). 순수 함수형, 자체 상태 없음.
    ///   - 유적 탐험: 유적 칸에서 턴을 시작한(이번 턴 이동·행동 전) 유닛이 행동을 써서 탐험하면 유적이 사라지고 보상 하나(폴리토피아 위키
    ///     Ruins: 골드 10 / 무료 기술 / 수도 인구 +3 / 탐험가 / 유닛)를 조건이 맞는 것 중 균등하게 무작위로 받는다.
    ///     기술은 트리가 남았을 때, 인구는 수도가 있을 때, 탐험가는 유적 주변 5x5에 구름이 남았을 때만 후보가 된다.
    ///     원문과 다른 점: "New Friends"(베테랑 검사)는 베테랑 시스템이 없어 가장 싼 유닛으로, 물 위 유적의 충각선 보상은
    ///     물 위 유적이 생성되지 않아 넣지 않았다.
    ///   - 해산: "Ability.Disband" 해금 시 이번 턴 이동도 행동도 하지 않은 자기 유닛을 없애고 훈련 비용 절반(내림)을 골드로 돌려받는다
    ///     (위키 Disband — 슈퍼 유닛은 비용 10이라 5, 배는 태운 유닛 비용의 절반이고 배 업그레이드 비용은 돌려받지 않는다:
    ///     UnitTypeId가 태운 육지 유닛 Id라 자연히 그렇게 된다).
    /// </summary>
    public static class RuinSystem
    {
        public const string DisbandKey = "Ability.Disband";

        public static bool CanExplore(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId) =>
            econ != null && UnitQueries.IsAlive(world, unitId) && !world.Get<HasMoved>(unitId).Value && !world.Get<HasActed>(unitId).Value &&
            grid.GetStructure(world.Get<GridPosition>(unitId).Value) == StructureGenerationSystem.RuinStructureId;

        public static bool Explore(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, List<EconomyLogEntry> log)
        {
            if (!CanExplore(grid, world, econ, unitId)) return false;
            var team = world.Get<Team>(unitId);
            var pos = world.Get<GridPosition>(unitId).Value;
            grid.SetStructure(pos, string.Empty);
            world.Set(unitId, new HasActed { Value = true });

            var entry = new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Explore, Position = pos, CityIndex = -1 };

            var techCandidates = new List<TechNodeData>();
            foreach (var n in econ.TechNodes)
                if (TechSystem.IsAvailable(econ.TechNodes, econ.Tech[team], n.Id)) techCandidates.Add(n);
            int capital = CitySystem.FindCapital(econ, team);
            string unit = CheapestUnitId(econ);

            var rewards = new List<RuinReward> { RuinReward.Gold };
            if (techCandidates.Count > 0) rewards.Add(RuinReward.Tech);
            if (capital >= 0) rewards.Add(RuinReward.Population);
            if (HasFogAround(grid, team, pos, 2)) rewards.Add(RuinReward.Explorer);
            if (!string.IsNullOrEmpty(unit)) rewards.Add(RuinReward.Unit);

            switch (rewards[CitySystem.NextRandom(econ, rewards.Count)])
            {
                case RuinReward.Tech:
                {
                    var pick = techCandidates[CitySystem.NextRandom(econ, techCandidates.Count)];
                    econ.Tech[team].Unlocked.Add(pick.Id);
                    entry.Subject = $"기술 {pick.Name}";
                    break;
                }
                case RuinReward.Population:
                    entry.Subject = $"인구 +{GameRules.Ruin.Population} ({econ.Cities[capital].Name})";
                    entry.CityIndex = capital;
                    log?.Add(entry);
                    CitySystem.AddPopulation(econ, capital, GameRules.Ruin.Population, log);
                    return true;
                case RuinReward.Explorer:
                    entry.Subject = "탐험가";
                    log?.Add(entry);
                    VisionSystem.RunExplorer(grid, econ, team, pos, log);
                    return true;
                case RuinReward.Unit:
                    entry.Subject = "유닛";
                    entry.SpawnUnitId = unit;
                    break;
                default:
                {
                    var res = econ.Resources[team];
                    res.Gold += GameRules.Ruin.Gold;
                    econ.Resources[team] = res;
                    entry.Subject = $"골드 +{GameRules.Ruin.Gold}";
                    break;
                }
            }
            log?.Add(entry);
            return true;
        }

        private enum RuinReward { Gold, Tech, Population, Explorer, Unit }

        /// <summary>center 주변 반경 radius 안에 team에게 아직 구름인 칸이 있는지(위키: 탐험가 보상 조건 5x5).</summary>
        private static bool HasFogAround(GridWorld grid, Team team, Vector2Int center, int radius)
        {
            if (!grid.FogEnabled) return false;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var p = center + new Vector2Int(dx, dy);
                if (grid.InBounds(p) && !VisionSystem.IsExplored(grid, team, p)) return true;
            }
            return false;
        }

        private static string CheapestUnitId(EconomyWorld econ)
        {
            UnitCsvRow best = null;
            foreach (var row in econ.UnitRows)
                if (best == null || row.Cost < best.Cost) best = row;
            return best?.Id ?? string.Empty;
        }

        public static bool CanDisband(EntityWorld world, EconomyWorld econ, int unitId) =>
            econ != null && UnitQueries.IsAlive(world, unitId) && !world.Get<HasMoved>(unitId).Value && !world.Get<HasActed>(unitId).Value &&
            TechSystem.HasUnlock(econ.TechNodes, econ.Tech[world.Get<Team>(unitId)], DisbandKey);

        public static int DisbandRefund(EntityWorld world, EconomyWorld econ, int unitId)
        {
            var row = CitySystem.FindUnitRow(econ, world.Get<UnitTypeId>(unitId).Value);
            return (row?.Cost ?? 0) / Mathf.Max(1, GameRules.Unit.DisbandRefundDivisor);
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
