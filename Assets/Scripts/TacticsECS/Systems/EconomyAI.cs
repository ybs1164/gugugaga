using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 한 팀의 경제 턴(이동/전투 전에 실행) — 규칙 기반, 어느 팀이든 돌릴 수 있다. 자체 상태 없음.
    ///   1. 마을/적 도시 위에서 턴을 시작한 유닛은 점령하고, 적 도시 옆의 침투 유닛은 가장 레벨 높은 도시에 침투하고, 유적 위 유닛은 탐험한다.
    ///   2. 밀린 레벨업 보상을 상황에 맞게 고른다(구름이 남았으면 탐험가, 위협받으면 성벽/슈퍼 유닛 ...).
    ///   3. 연구 가능한 기술 중 가장 싼 것 하나를 연구한다.
    ///   4. 위협(보이는 적 유닛이 자기 도시 3칸 안)이 있으면 훈련 먼저, 없으면 건설 먼저 하고 남은 별로 훈련한다.
    ///   5. 건설: 달성한 기념물(무료) → 수도와 미연결 도시를 잇는 도로/다리(연결 계획) → "별당 가치"가 좋은 채집/건설 순.
    ///      가치 = 즉시 인구 + 이 건물 덕에 옆 가공 건물(풍차/제재소/대장간)이 더 얻는 인구, 시장은 늘어날 별 수입,
    ///      첫 항구는 바다 진출 가치, 신전은 점수용(낮은 우선순위).
    ///   6. 자기 영토 안의 뗏목은 살 수 있는 가장 좋은 배로 업그레이드한다.
    /// 유닛 스폰은 View가 필요해서 여기서 하지 않고, Kind=Train(SpawnUnitId 포함) 기록으로 돌려주면 호출자가
    /// (BattleController는 View와 함께, 헤드리스 시뮬레이션은 UnitFactorySystem으로) 스폰한다.
    /// </summary>
    public static class EconomyAI
    {
        public static List<EconomyLogEntry> RunTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var log = new List<EconomyLogEntry>();
            if (econ == null) return log;
            TaskSystem.Refresh(grid, econ, log);

            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id) && world.Get<Team>(id) == team && !UnitActionQueries.IsWild(world, id) && CitySystem.CanCapture(grid, world, econ, id))
                    CitySystem.Capture(grid, world, econ, id, log);

            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id) || world.Get<Team>(id) != team || UnitActionQueries.IsWild(world, id)) continue;
                // 약탈: 수입이 가장 큰 도시부터.
                var plunder = PlunderSystem.Targets(grid, world, econ, id);
                if (plunder.Count > 0)
                {
                    plunder.Sort((a, b) => CitySystem.CityStarsIncome(grid, world, econ.Cities[CitySystem.FindCityAt(econ, b)])
                        .CompareTo(CitySystem.CityStarsIncome(grid, world, econ.Cities[CitySystem.FindCityAt(econ, a)])));
                    PlunderSystem.Plunder(grid, world, econ, id, plunder[0], log);
                    continue;
                }
                var targets = InfiltrationSystem.Targets(grid, world, econ, id);
                if (targets.Count == 0) continue;
                targets.Sort((a, b) => econ.Cities[CitySystem.FindCityAt(econ, b)].Level.CompareTo(econ.Cities[CitySystem.FindCityAt(econ, a)].Level));
                InfiltrationSystem.Infiltrate(grid, world, econ, id, targets[0], null, log); // Dagger View는 호출자가 붙인다
            }

            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id) && world.Get<Team>(id) == team && !UnitActionQueries.IsWild(world, id) && RuinSystem.CanExplore(grid, world, econ, id))
                    RuinSystem.Explore(grid, world, econ, id, log);

            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id) && world.Get<Team>(id) == team && !UnitActionQueries.IsWild(world, id) && RuinSystem.CanHarvestStarfish(grid, world, econ, id))
                    RuinSystem.HarvestStarfish(grid, world, econ, id, log);

            bool threatened = IsThreatened(grid, world, econ, team);
            ChooseRewards(grid, econ, team, threatened, log);
            FaithAI.RunEarly(grid, world, econ, team, log); // 대신전은 연구·건설·훈련보다 먼저(별을 빼앗기기 전에)
            int faithReserve = FaithAI.StarReserve(grid, world, econ, team); // 못 지었으면 그 비용을 남겨 둔다
            // 학습한 AI(난이도)는 아래 결정마다 규칙 AI의 후보 중 하나를 고른다 — PolicySystem. 규칙 AI만 도는 팀은 global이 null.
            float[] global = PolicySystem.Wants(econ, team) ? PolicySystem.Global(grid, world, econ, team) : null;
            Research(econ, team, faithReserve, log, global);

            bool trainFirst = threatened;
            if (global != null)
                trainFirst = PolicySystem.Choose(econ, team, PolicyDecisionKind.Priority, global,
                    new List<float[]> { PriorityFeatures(true, threatened), PriorityFeatures(false, threatened) }, threatened ? 0 : 1) == 0;
            if (trainFirst)
            {
                Train(grid, world, econ, team, 0, log, global); // 위협받을 땐 방어가 먼저
                Improve(grid, world, econ, team, faithReserve, log, global);
            }
            else
            {
                Improve(grid, world, econ, team, TrainingReserve(world, econ, team) + faithReserve, log, global);
                Train(grid, world, econ, team, faithReserve, log, global);
            }
            UpgradeRafts(grid, world, econ, team, log);
            FaithAI.RunTurn(grid, world, econ, team, log); // 신을 고른 팀만 — docs/spec/game/ai.md
            TaskSystem.Refresh(grid, econ, log);
            return log;
        }

        /// <summary>보이는 적 유닛이 자기 도시 ThreatRadius 칸 안에 있는지.</summary>
        public static bool IsThreatened(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id) || world.Get<Team>(id) == team) continue;
                var p = world.Get<GridPosition>(id).Value;
                if (!VisionSystem.IsExplored(grid, team, p)) continue;
                foreach (var c in econ.Cities)
                    if (c.Owner == team && PathfindingSystem.Distance(c.Position, p) <= GameRules.AI.ThreatRadius) return true;
            }
            return false;
        }

        /// <summary>유닛이 도시 수보다 적으면 가장 싼 유닛 값만큼은 건설에 쓰지 않고 남겨둔다.</summary>
        private static int TrainingReserve(EntityWorld world, EconomyWorld econ, Team team)
        {
            if (CityResourceSystem.CountPopulation(world, team) >= CitySystem.CountCities(econ, team)) return 0;
            int cheapest = int.MaxValue;
            foreach (var row in econ.UnitRows)
                if (CitySystem.IsTrainable(row) && TechSystem.CanTrainUnitType(econ.TechNodes, econ.Tech[team], row.Id)) cheapest = Mathf.Min(cheapest, row.Cost);
            return cheapest == int.MaxValue ? 0 : cheapest;
        }

        private static void ChooseRewards(GridWorld grid, EconomyWorld econ, Team team, bool threatened, List<EconomyLogEntry> log)
        {
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                while (econ.Cities[i].Owner == team && econ.Cities[i].PendingRewards > 0)
                {
                    var city = econ.Cities[i];
                    var options = CitySystem.RewardOptions(CitySystem.PendingRewardLevel(city));
                    var pick = options[0];
                    foreach (var o in options)
                    {
                        if (o == CityRewardType.Explorer && FogWithin(grid, team, city.Position, GameRules.Vision.ExplorerMoves / 2)) pick = o;
                        if (o == CityRewardType.CityWall && threatened) pick = o;
                        if (o == CityRewardType.BorderGrowth && city.BorderRadius < CitySystem.RewardAmount(CityRewardType.BorderGrowth)) pick = o;
                        if (o == CityRewardType.SuperUnit && threatened) pick = o;
                    }
                    if (!CitySystem.ApplyReward(grid, econ, i, pick, log)) break;
                }
            }
        }

        private static bool FogWithin(GridWorld grid, Team team, Vector2Int center, int radius)
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

        private static void Research(EconomyWorld econ, Team team, int reserve, List<EconomyLogEntry> log, float[] global = null)
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
            int cheapest = bestCost;
            // 신을 고른 팀은 신전·대신전 과업을 여는 기술을 먼저 연구한다(살 수 있을 때).
            string preferred = FaithAI.PreferredTech(econ, team);
            if (preferred != null && TechSystem.CanUnlock(econ.TechNodes, tech, econ.Resources[team], cityCount, preferred))
            {
                bestId = preferred;
                bestCost = TechSystem.Cost(econ.TechNodes, tech, TechSystem.Find(econ.TechNodes, preferred).Value, cityCount);
            }
            if (bestId != null && reserve > 0 && econ.Resources[team].Stars - bestCost < reserve) bestId = null; // 대신전 비용을 남긴다

            if (global != null)
            {
                // 후보: 살 수 있는(남길 별을 지키는) 기술 전부 + 연구하지 않음(마지막).
                var ids = new List<string>();
                var features = new List<float[]>();
                int stars = econ.Resources[team].Stars, rule = -1;
                foreach (var node in econ.TechNodes)
                {
                    if (!TechSystem.CanUnlock(econ.TechNodes, tech, econ.Resources[team], cityCount, node.Id)) continue;
                    int cost = TechSystem.Cost(econ.TechNodes, tech, node, cityCount);
                    if (reserve > 0 && stars - cost < reserve) continue;
                    if (node.Id == bestId) rule = ids.Count;
                    ids.Add(node.Id);
                    var f = new float[PolicySystem.CandidateSize];
                    f[1] = cost / 20f; f[2] = (stars - cost) / 20f; f[3] = node.Tier / 4f;
                    f[4] = node.Id == bestId ? 1f : 0f; f[5] = node.Id == preferred ? 1f : 0f; f[6] = cost == cheapest ? 1f : 0f;
                    features.Add(f);
                }
                var skip = new float[PolicySystem.CandidateSize];
                skip[0] = 1f; skip[2] = stars / 20f; skip[4] = bestId == null ? 1f : 0f;
                features.Add(skip);
                int pick = PolicySystem.Choose(econ, team, PolicyDecisionKind.Research, global, features, rule >= 0 ? rule : ids.Count);
                bestId = pick >= 0 && pick < ids.Count ? ids[pick] : null;
            }
            if (bestId == null) return;

            var res = econ.Resources[team];
            if (TechSystem.Unlock(econ.TechNodes, tech, ref res, cityCount, bestId))
            {
                econ.Resources[team] = res;
                log.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Research, Subject = TechSystem.Find(econ.TechNodes, bestId).Value.Name, CityIndex = -1 });
            }
        }

        private static float[] PriorityFeatures(bool trainFirst, bool threatened)
        {
            var f = new float[PolicySystem.CandidateSize];
            f[0] = trainFirst ? 1f : 0f;
            f[1] = threatened ? 1f : 0f;
            f[4] = trainFirst == threatened ? 1f : 0f;
            return f;
        }

        private static void Train(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int reserve, List<EconomyLogEntry> log, float[] global = null)
        {
            if (econ.UnitRows.Count == 0) return;
            int pending = 0;
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                if (econ.Cities[i].Owner != team) continue;
                int population = CityResourceSystem.CountPopulation(world, team) + pending, capacity = CitySystem.UnitCapacity(econ, team);
                if (population >= capacity) return;

                UnitCsvRow best = null;
                foreach (var row in econ.UnitRows)
                    if (CitySystem.CanTrain(grid, world, econ, team, i, row, out _) && (best == null || row.Cost > best.Cost)) best = row;
                if (best != null && econ.Resources[team].Stars - best.Cost < reserve) best = null;
                if (global != null && best != null)
                {
                    // 후보: 이 도시에서 훈련할 수 있는(남길 별을 지키는) 유닛 + 훈련하지 않음(마지막).
                    var rows = new List<UnitCsvRow>();
                    var features = new List<float[]>();
                    int stars = econ.Resources[team].Stars, rule = -1;
                    foreach (var row in econ.UnitRows)
                    {
                        if (!CitySystem.CanTrain(grid, world, econ, team, i, row, out _) || stars - row.Cost < reserve) continue;
                        if (row == best) rule = rows.Count;
                        rows.Add(row);
                        var f = new float[PolicySystem.CandidateSize];
                        f[1] = row.Cost / 10f; f[2] = (stars - row.Cost) / 20f; f[4] = row == best ? 1f : 0f;
                        f[5] = row.AttackAttack / 5f; f[6] = row.Defense / 5f; f[7] = row.MaxHp / 20f; f[8] = row.MoveRange / 3f;
                        f[9] = row.AttackRange / 3f; f[10] = row.Domain == TerrainType.Water ? 1f : 0f; f[11] = row.HealAmount > 0 ? 1f : 0f;
                        f[12] = econ.Cities[i].Level / 5f; f[13] = population / (float)Mathf.Max(1, capacity);
                        features.Add(f);
                    }
                    var skip = new float[PolicySystem.CandidateSize];
                    skip[0] = 1f; skip[2] = stars / 20f; skip[12] = econ.Cities[i].Level / 5f; skip[13] = population / (float)Mathf.Max(1, capacity);
                    features.Add(skip);
                    int pick = PolicySystem.Choose(econ, team, PolicyDecisionKind.Train, global, features, rule);
                    best = pick >= 0 && pick < rows.Count ? rows[pick] : null;
                }
                if (best == null || !CitySystem.PayForTraining(grid, world, econ, team, i, best)) continue;

                pending++;
                log.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Train, Subject = best.Name, SpawnUnitId = best.Id, Position = econ.Cities[i].Position, CityIndex = i });
            }
        }

        /// <summary>정책이 고르는 건설 후보 수(점수 상위).</summary>
        private const int ImproveCandidates = 8;

        private static void Improve(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int reserve, List<EconomyLogEntry> log, float[] global = null)
        {
            PlanConnections(grid, econ, team, reserve, log);

            for (int n = 0; n < GameRules.AI.MaxImprovementsPerTurn; n++)
            {
                (Vector2Int Pos, TileOption Option)? best = null;
                float bestScore = 0f;
                int stars = econ.Resources[team].Stars;
                bool hasPort = CountBuildings(grid, team, BuildingDefinition.Port) > 0;
                var scored = global != null ? new List<((Vector2Int Pos, TileOption Option) C, float Score)>() : null;
                foreach (var candidate in TileImprovementSystem.GetAllOptions(grid, econ, team))
                {
                    if (!candidate.Option.Enabled) continue;
                    if (candidate.Option.Cost > 0 && stars - candidate.Option.Cost < reserve) continue;
                    float score = Score(grid, team, candidate.Pos, candidate.Option, hasPort);
                    if (score > bestScore) { bestScore = score; best = candidate; }
                    if (scored != null && score > 0f) scored.Add((candidate, score));
                }
                if (best == null) return;
                if (scored != null)
                {
                    // 후보: 점수 상위 ImproveCandidates개(같은 점수는 먼저 나온 것 — 규칙 AI의 선택이 0번) + 이번 턴 건설 끝(마지막).
                    var top = scored.Select((c, i) => (c.C, c.Score, i)).OrderByDescending(c => c.Score).ThenBy(c => c.i).Take(ImproveCandidates).ToList();
                    var features = new List<float[]>();
                    foreach (var c in top)
                    {
                        var f = new float[PolicySystem.CandidateSize];
                        int cost = c.C.Option.Cost;
                        f[1] = cost / 10f; f[2] = (stars - cost) / 20f; f[3] = c.Score / bestScore; f[4] = features.Count == 0 ? 1f : 0f;
                        f[5] = Mathf.Min(c.Score, 10f) / 10f; f[6] = c.C.Option.IsBuilding ? 1f : 0f; f[7] = c.Score >= 1000f ? 1f : 0f;
                        f[8] = c.Score >= 100f && c.Score < 1000f ? 1f : 0f; f[9] = n / (float)GameRules.AI.MaxImprovementsPerTurn;
                        features.Add(f);
                    }
                    var stop = new float[PolicySystem.CandidateSize];
                    stop[0] = 1f; stop[2] = stars / 20f; stop[9] = n / (float)GameRules.AI.MaxImprovementsPerTurn;
                    features.Add(stop);
                    int pick = PolicySystem.Choose(econ, team, PolicyDecisionKind.Improve, global, features, 0);
                    if (pick < 0 || pick >= top.Count) return;
                    best = top[pick].C;
                }
                if (!TileImprovementSystem.Execute(grid, econ, team, best.Value.Pos, best.Value.Option.Id, log)) return;
            }
        }

        private static int CountBuildings(GridWorld grid, Team team, string buildingId)
        {
            int n = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var t = grid.GetTile(new Vector2Int(x, y));
                if (t.OwnerTeam == (int)team && t.BuildingId == buildingId) n++;
            }
            return n;
        }

        /// <summary>
        /// 건설/채집 한 건의 "별 1당 가치". 인구 1 = 가치 1, 별 수입 1/턴 = 가치 1로 본다.
        /// 기념물(무료)은 가장 먼저, 도로/다리는 연결 계획(PlanConnections)이 따로 짓고, 벌목/화전/숲 조성/파괴는 쓰지 않는다.
        /// </summary>
        private static float Score(GridWorld grid, Team team, Vector2Int pos, TileOption option, bool hasPort)
        {
            float value;
            if (option.IsBuilding)
            {
                var info = TileImprovementSystem.FindBuilding(option.Id).Value;
                if (!string.IsNullOrEmpty(info.TaskId)) return 1000f;
                if (info.IsRoad || info.ActsAsRoad) return 0f;
                if (info.ProducesStarsFromAdjacent)
                    value = Mathf.Min(GameRules.City.MarketStarsCap, MarketPotential(grid, pos, info, team));
                else
                    value = info.Population + TileImprovementSystem.ProcessorPopulation(grid, pos, info, team) + NeighborProcessorGain(grid, pos, info.Id, team);
                if (info.Id == BuildingDefinition.Port && !hasPort) value += 3f;
                if (info.IsTemple) value *= 0.5f;
            }
            else
            {
                var info = TileImprovementSystem.FindAction(option.Id).Value;
                if (info.Kind != TileActionKind.Harvest) return 0f;
                if (info.StarsGain > 0) return 100f;
                value = info.Population;
            }
            return value <= 0 ? 0f : value / Mathf.Max(1, option.Cost);
        }

        /// <summary>baseBuildingId를 pos에 지으면 옆의 가공 건물(같은 팀 영토)이 추가로 만드는 인구.</summary>
        private static int NeighborProcessorGain(GridWorld grid, Vector2Int pos, string baseBuildingId, Team team)
        {
            int gain = 0;
            foreach (var n in grid.GetNeighbors(pos, true))
            {
                var t = grid.GetTile(n);
                if (t.OwnerTeam != (int)team) continue;
                var info = TileImprovementSystem.FindBuilding(t.BuildingId);
                if (info == null || info.Value.PopulationPerAdjacent == 0) continue;
                if (System.Array.IndexOf(info.Value.AdjacentBuildings, baseBuildingId) >= 0) gain += info.Value.PopulationPerAdjacent;
            }
            return gain;
        }

        /// <summary>시장 후보 칸 옆 가공 건물들이 지금 만드는 인구 합(= 시장 별 수입).</summary>
        private static int MarketPotential(GridWorld grid, Vector2Int pos, BuildingInfo market, Team team)
        {
            int stars = 0;
            foreach (var n in grid.GetNeighbors(pos, true))
            {
                var t = grid.GetTile(n);
                if (t.OwnerTeam != (int)team || System.Array.IndexOf(market.AdjacentBuildings, t.BuildingId) < 0) continue;
                stars += TileImprovementSystem.BuildingPopulation(grid, n);
            }
            return stars;
        }

        // ---------- 연결 계획(도로/다리) ----------

        /// <summary>
        /// 수도와 연결되지 않은 자기 도시마다, 이미 연결된 망(수도/연결 도시/쓸 수 있는 도로·다리)까지 가장 싼 도로·다리 경로를
        /// 찾아(도로 3, 다리 5, 이미 있는 도로/다리/도시 0) 예산(MaxConnectionBudget) 안이면 별이 허락하는 만큼 경로를 따라
        /// 짓는다. 연결되면 도시/수도 인구 +1(위키 City Connections)이라 가치가 크다.
        /// </summary>
        private static void PlanConnections(GridWorld grid, EconomyWorld econ, Team team, int reserve, List<EconomyLogEntry> log)
        {
            var road = System.Array.Find(BuildingDefinition.All, b => b.Id == BuildingDefinition.Road);
            if (!string.IsNullOrEmpty(road.UnlockKey) && !TechSystem.HasUnlock(econ.TechNodes, econ.Tech[team], road.UnlockKey)) return;
            int capital = CitySystem.FindCapital(econ, team);
            if (capital < 0) return;

            for (int i = 0; i < econ.Cities.Count; i++)
            {
                var city = econ.Cities[i];
                if (city.Owner != team || city.IsCapital || city.ConnectedToCapital) continue;
                var network = ConnectedNetwork(econ, team, capital);
                var path = CheapestConnectionPath(grid, econ, team, city.Position, network, out int cost);
                if (path == null || cost > GameRules.AI.MaxConnectionBudget) continue;

                foreach (var p in path)
                {
                    var t = grid.GetTile(p);
                    string build = null;
                    if (t.Terrain == TerrainType.Land && !t.HasRoad && !CitySystem.IsSettlementTile(grid, p)) build = BuildingDefinition.Road;
                    else if (t.Terrain == TerrainType.Water && t.BuildingId != BuildingDefinition.Bridge) build = BuildingDefinition.Bridge;
                    if (build == null) continue;
                    int price = TileImprovementSystem.FindBuilding(build).Value.Cost;
                    if (econ.Resources[team].Stars - price < reserve) return;
                    if (!TileImprovementSystem.Execute(grid, econ, team, p, build, log)) break;
                }
            }
        }

        /// <summary>수도와 이미 연결된 도시 칸들(연결 계획의 도착점).</summary>
        private static HashSet<Vector2Int> ConnectedNetwork(EconomyWorld econ, Team team, int capital)
        {
            var set = new HashSet<Vector2Int> { econ.Cities[capital].Position };
            foreach (var c in econ.Cities)
                if (c.Owner == team && c.ConnectedToCapital) set.Add(c.Position);
            return set;
        }

        /// <summary>from에서 network 칸까지(8방향 — 연결 판정과 같은 이웃) 도로/다리로 잇는 가장 싼 경로. 경로에는 from과 도착
        /// 칸을 뺀 중간 칸만 담긴다. 없으면 null.</summary>
        private static List<Vector2Int> CheapestConnectionPath(GridWorld grid, EconomyWorld econ, Team team, Vector2Int from,
            HashSet<Vector2Int> network, out int totalCost)
        {
            totalCost = 0;
            var road = TileImprovementSystem.FindBuilding(BuildingDefinition.Road).Value;
            var bridge = TileImprovementSystem.FindBuilding(BuildingDefinition.Bridge).Value;
            var cost = new Dictionary<Vector2Int, int> { [from] = 0 };
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var open = new List<Vector2Int> { from };
            while (open.Count > 0)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++) if (cost[open[k]] < cost[open[bi]]) bi = k;
                var cur = open[bi];
                open.RemoveAt(bi);
                if (cost[cur] > GameRules.AI.MaxConnectionBudget) break;
                if (cur != from && network.Contains(cur))
                {
                    totalCost = cost[cur];
                    var path = new List<Vector2Int>();
                    for (var p = prev[cur]; p != from; p = prev[p]) path.Add(p);
                    path.Reverse();
                    return path;
                }
                foreach (var n in grid.GetNeighbors(cur, true))
                {
                    int step = StepCost(grid, econ, team, n, network, road, bridge);
                    if (step < 0) continue;
                    int nc = cost[cur] + step;
                    if (cost.TryGetValue(n, out var old) && old <= nc) continue;
                    cost[n] = nc;
                    prev[n] = cur;
                    open.Add(n);
                }
            }
            return null;
        }

        /// <summary>연결 경로에서 칸 하나의 비용: 망/자기 도시/쓸 수 있는 도로·다리 0, 지을 수 있는 도로 3·다리 5, 불가 -1.</summary>
        private static int StepCost(GridWorld grid, EconomyWorld econ, Team team, Vector2Int p, HashSet<Vector2Int> network, BuildingInfo road, BuildingInfo bridge)
        {
            if (network.Contains(p)) return 0;
            if (!VisionSystem.IsExplored(grid, team, p)) return -1;
            var t = grid.GetTile(p);
            if (t.OwnerTeam != TileData.NoOwner && t.OwnerTeam != (int)team) return -1;
            int city = CitySystem.FindCityAt(econ, p);
            if (city >= 0) return econ.Cities[city].Owner == team ? 0 : -1;
            if (t.HasRoad || t.BuildingId == BuildingDefinition.Bridge) return 0;
            var cls = TileImprovementSystem.Classify(grid, p);
            string s = t.StructureId;
            bool blocked = s == StructureGenerationSystem.VillageStructureId || s == StructureGenerationSystem.CapitalStructureId ||
                           s == StructureGenerationSystem.RuinStructureId || s == StructureGenerationSystem.LighthouseStructureId;
            if (blocked) return -1;
            if ((road.Terrain & cls) != 0) return road.Cost;
            if ((bridge.Terrain & cls) != 0 && string.IsNullOrEmpty(t.BuildingId) && TileImprovementSystem.HasOppositeLand(grid, p)) return bridge.Cost;
            return -1;
        }

        // ---------- 배 ----------

        /// <summary>자기 영토 안의 뗏목을 살 수 있는 가장 비싼 배로 업그레이드한다(위키 Raft: 자기 영토 안에서만).</summary>
        private static void UpgradeRafts(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id) || world.Get<Team>(id) != team) continue;
                if (EmbarkSystem.NavalUnitId(world, id) != NavalUnitDefinition.RaftId) continue;
                string best = null;
                int bestCost = -1;
                foreach (var u in NavalUnitDefinition.Upgrades)
                    if (u.Row.Cost > bestCost && EmbarkSystem.CanUpgrade(grid, world, econ, id, u.Row.Id, out _)) { best = u.Row.Id; bestCost = u.Row.Cost; }
                if (best != null) EmbarkSystem.Upgrade(grid, world, econ, id, best, log);
            }
        }
    }
}
