using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 적 팀의 경제 턴(이동/전투 전에 실행) — EnemyAI와 같은 아주 단순한 규칙 기반. 자체 상태 없음.
    ///   1. 마을/적 도시 위에서 턴을 시작한 유닛은 점령한다.
    ///   2. 밀린 레벨업 보상은 첫 번째 선택지를 고른다.
    ///   3. 연구 가능한 기술 중 가장 싼 것 하나를 연구한다.
    ///   4. 도시마다 (인구 상한/골드가 허락하면) 살 수 있는 가장 비싼 유닛을 하나씩 훈련한다.
    ///   5. 남은 골드로 "골드당 인구"가 가장 좋은 채집/건설부터 반복한다.
    /// 유닛 스폰은 View가 필요해서 여기서 하지 않고, Kind=Train(SpawnUnitId 포함) 기록으로 돌려주면
    /// BattleController가 이어서 스폰한다.
    /// </summary>
    public static class EconomyAI
    {
        private const int MaxImprovementsPerTurn = 12;

        public static List<EconomyLogEntry> RunTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var log = new List<EconomyLogEntry>();
            if (econ == null) return log;

            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id) && world.Get<Team>(id) == team && CitySystem.CanCapture(grid, world, econ, id))
                    CitySystem.Capture(grid, world, econ, id, log);

            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id) && world.Get<Team>(id) == team && RuinSystem.CanExplore(grid, world, econ, id))
                    RuinSystem.Explore(grid, world, econ, id, log);

            for (int i = 0; i < econ.Cities.Count; i++)
            {
                while (econ.Cities[i].Owner == team && econ.Cities[i].PendingRewards > 0)
                {
                    var options = CitySystem.RewardOptions(CitySystem.PendingRewardLevel(econ.Cities[i]));
                    if (!CitySystem.ApplyReward(grid, econ, i, options[0], log)) break;
                }
            }

            Research(econ, team, log);
            Train(grid, world, econ, team, log);
            Improve(grid, econ, team, log);
            return log;
        }

        private static void Research(EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            int cityCount = CitySystem.CountCities(econ, team);
            var tech = econ.Tech[team];
            string bestId = null;
            int bestCost = int.MaxValue;
            foreach (var node in econ.TechNodes)
            {
                if (!TechSystem.CanUnlock(econ.TechNodes, tech, econ.Resources[team], cityCount, node.Id)) continue;
                int cost = TechSystem.Cost(econ.TechNodes, tech, node, cityCount);
                if (cost < bestCost || (cost == bestCost && CitySystem.NextRandom(econ, 2) == 0)) { bestCost = cost; bestId = node.Id; }
            }
            if (bestId == null) return;

            var res = econ.Resources[team];
            if (TechSystem.Unlock(econ.TechNodes, tech, ref res, cityCount, bestId))
            {
                econ.Resources[team] = res;
                log.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Research, Subject = TechSystem.Find(econ.TechNodes, bestId).Value.Name, CityIndex = -1 });
            }
        }

        private static void Train(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            if (econ.UnitRows.Count == 0) return;
            int pending = 0;
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                if (econ.Cities[i].Owner != team) continue;
                if (CityResourceSystem.CountPopulation(world, team) + pending >= CitySystem.UnitCapacity(econ, team)) return;

                UnitCsvRow best = null;
                foreach (var row in econ.UnitRows)
                    if (CitySystem.CanTrain(grid, world, econ, team, i, row, out _) && (best == null || row.Cost > best.Cost)) best = row;
                if (best == null || !CitySystem.PayForTraining(grid, world, econ, team, i, best)) continue;

                pending++;
                log.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Train, Subject = best.Name, SpawnUnitId = best.Id, Position = econ.Cities[i].Position, CityIndex = i });
            }
        }

        private static void Improve(GridWorld grid, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            for (int n = 0; n < MaxImprovementsPerTurn; n++)
            {
                (Vector2Int Pos, TileOption Option)? best = null;
                float bestScore = 0f;
                foreach (var candidate in TileImprovementSystem.GetAllOptions(grid, econ, team))
                {
                    if (!candidate.Option.Enabled) continue;
                    float score = Score(grid, team, candidate.Pos, candidate.Option);
                    if (score > bestScore) { bestScore = score; best = candidate; }
                }
                if (best == null) return;
                if (!TileImprovementSystem.Execute(grid, econ, team, best.Value.Pos, best.Value.Option.Id, log)) return;
            }
        }

        /// <summary>골드 1당 얻는 인구(불가사리처럼 골드를 주는 행동은 큰 점수). 도로/벌목/화전/숲 조성/파괴는
        /// 지형을 바꾸거나 즉시 이득이 없어 AI는 쓰지 않는다(0점).</summary>
        private static float Score(GridWorld grid, Team team, Vector2Int pos, TileOption option)
        {
            int pop;
            if (option.IsBuilding)
            {
                var info = TileImprovementSystem.FindBuilding(option.Id).Value;
                if (info.IsRoad || info.ProducesGoldFromAdjacent) return 0f;
                pop = info.Population + TileImprovementSystem.ProcessorPopulation(grid, pos, info, team);
            }
            else
            {
                var info = TileImprovementSystem.FindAction(option.Id).Value;
                if (info.Kind != TileActionKind.Harvest) return 0f;
                if (info.GoldGain > 0) return 100f;
                pop = info.Population;
            }
            return pop <= 0 ? 0f : pop / (float)Mathf.Max(1, option.Cost);
        }
    }
}
