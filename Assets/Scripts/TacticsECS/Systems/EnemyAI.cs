using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 규칙 기반 군사 AI(한 팀의 유닛 이동/공격). 어느 팀이든 돌릴 수 있다(헤드리스 시뮬레이션은 양쪽 모두 이 AI).
    /// 자체 상태 없음 — 판단에 필요한 값은 전부 GridWorld/EntityWorld/EconomyWorld에서 읽는다.
    ///
    /// 유닛마다(id 순서):
    ///   1. 지금 사거리 안에 보이는 적이 있으면 가장 이득인 대상(처치 가능 > 피해량)을 공격하고 끝.
    ///   2. 목표를 고른다: (a) 점령 목표 칸 위에 서 있으면 다음 턴 점령을 위해 대기, (b) 보이는 적이 자기 도시 2칸 안에
    ///      있고 도시 칸이 비어 있으면 도시로 복귀(도시마다 한 유닛), (c) 가장 가까운(이동 거리 기준) 보이는 적/점령 목표
    ///      (점령 목표는 유닛마다 하나씩 나눠 가진다), (c') 배는 보이는 불가사리(항해 연구 시 — 다음 턴 인양), (d) 없으면 가장 가까운
    ///      구름 가장자리로 탐험. 불가사리 위의 배는 인양을 위해 그 자리에서 기다린다(인양은 EconomyAI가 턴 시작에 한다).
    ///   3. 이동 가능한 칸 중 목표까지의 "여행 거리"(지형을 고려한 BFS — 바다 건너 목표면 배로 가는 거리)가 가장 짧은 칸으로 간다.
    ///      육지로 닿지 않는 목표면 자기 항구로 가서 뗏목을 타고(EmbarkSystem), 배는 목표 쪽 해안에 내린다.
    ///   4. 이동 후 사거리 안에 적이 있으면(돌격 등으로 가능하면) 공격.
    /// 시야: 구름(아직 탐험하지 않은 칸)의 적/정착지는 모른다 — VisionSystem.IsExplored로 거른다.
    /// </summary>
    public static class EnemyAI
    {
        private const int Unreachable = int.MaxValue;

        /// <summary>배(승선 유닛/물 유닛)이고 팀이 불가사리 인양(항해)을 연구했는지.</summary>
        private static bool CanSeekStarfish(GridWorld grid, EntityWorld world, EconomyWorld econ, int id, Team team) =>
            econ != null && (EmbarkSystem.IsEmbarked(world, id) || world.Get<MoveDomain>(id).Value == TerrainType.Water) &&
            TechSystem.HasUnlock(econ.TechNodes, econ.Tech[team], RuinSystem.StarfishKey);

        /// <summary>예전 호출(적 팀 고정) 호환용.</summary>
        public static List<BattleLogEntry> RunTurn(GridWorld grid, EntityWorld world, IReadOnlyCollection<Vector2Int> captureTargets = null) =>
            RunTurn(grid, world, null, Team.Enemy, captureTargets);

        /// <summary>
        /// team의 이번 턴 행동들을 순서대로 BattleLogEntry 목록으로 돌려준다. EnemyAI 자신은 View를 전혀 모르지만,
        /// 이 값을 받은 BattleController가 공격자 View를 대상 쪽으로 돌리거나 행동 로그/데미지 라벨을 띄운다.
        /// captureTargets는 점령 가능한 칸(중립 마을 + 다른 팀 도시), econ은 과업(처치/공격) 기록용(null 가능).
        /// </summary>
        public static List<BattleLogEntry> RunTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, IReadOnlyCollection<Vector2Int> captureTargets = null)
        {
            var entries = new List<BattleLogEntry>();

            var ownIds = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == team) ownIds.Add(i);

            var claimedTargets = new HashSet<Vector2Int>();
            var defendedCities = new HashSet<Vector2Int>();

            foreach (var id in ownIds)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;
                // 베테랑 승급은 행동을 쓰지 않는다 — AI는 되는 즉시 승급한다(체력이 깎였을수록 이득이지만 단순하게).
                if (VeteranSystem.Promote(world, id)) entries.Add(new BattleLogEntry { ActorId = id, Verb = BattleLogVerb.Promote, TargetId = BattleLogEntry.NoTarget });
                if (world.Get<HasMoved>(id).Value && world.Get<HasActed>(id).Value) continue;
                var selfPos = world.Get<GridPosition>(id).Value;

                var enemies = VisibleEnemies(grid, world, team);

                if (TryBestAttack(grid, world, econ, id, enemies, entries)) continue;

                // (a) 점령 대기
                if (captureTargets != null && captureTargets.Contains(selfPos)) { claimedTargets.Add(selfPos); continue; }
                bool starfishBoat = CanSeekStarfish(grid, world, econ, id, team);
                if (starfishBoat && grid.GetStructure(selfPos) == RuinSystem.StarfishStructureId) { claimedTargets.Add(selfPos); continue; }

                Vector2Int? goal = null;
                bool goalIsEnemy = false;

                // (b) 도시 방어
                if (econ != null)
                {
                    foreach (var city in econ.Cities)
                    {
                        if (city.Owner != team || defendedCities.Contains(city.Position)) continue;
                        if (!enemies.Any(e => PathfindingSystem.Distance(world.Get<GridPosition>(e).Value, city.Position) <= GameRules.AI.DefendRadius)) continue;
                        int occ = grid.GetOccupant(city.Position);
                        if (occ != TileData.NoOccupant && occ != id) continue;
                        if (PathfindingSystem.Distance(selfPos, city.Position) > 4) continue;
                        goal = city.Position;
                        defendedCities.Add(city.Position);
                        break;
                    }
                }

                // (c) 가장 가까운 적/점령 목표(여행 거리)
                if (goal == null)
                {
                    var candidates = new List<(Vector2Int Pos, bool IsEnemy)>();
                    foreach (var e in enemies) candidates.Add((world.Get<GridPosition>(e).Value, true));
                    if (captureTargets != null)
                        foreach (var c in captureTargets)
                        {
                            if (claimedTargets.Contains(c) || !VisionSystem.IsExplored(grid, team, c)) continue;
                            int occ = grid.GetOccupant(c);
                            if (occ != TileData.NoOccupant && occ != id && world.Get<Team>(occ) == team) continue;
                            candidates.Add((c, false));
                        }

                    var fromSelf = TravelMap(grid, world, id, selfPos, unitIsOrigin: true);
                    int best = Unreachable;
                    foreach (var c in candidates)
                    {
                        if (!fromSelf.TryGetValue(c.Pos, out int d)) continue;
                        if (!c.IsEnemy) d -= 1; // 같은 거리면 점령 목표 우선
                        if (d < best) { best = d; goal = c.Pos; goalIsEnemy = c.IsEnemy; }
                    }

                    // 여행 거리로 닿지 않으면(바다 건너) 가장 가까운 목표(직선 거리)를 배로 노린다.
                    if (goal == null && candidates.Count > 0 && (EmbarkSystem.IsEmbarked(world, id) || HasOwnPort(grid, team)))
                    {
                        var pick = candidates.OrderBy(c => PathfindingSystem.Distance(selfPos, c.Pos)).First();
                        goal = pick.Pos; goalIsEnemy = pick.IsEnemy;
                    }
                    if (goal.HasValue && !goalIsEnemy) claimedTargets.Add(goal.Value);
                }

                // (c') 배: 가장 가까운 보이는 불가사리
                if (goal == null && starfishBoat)
                {
                    var fromSelf = TravelMap(grid, world, id, selfPos, unitIsOrigin: true);
                    int best = Unreachable;
                    foreach (var kv in fromSelf)
                    {
                        if (kv.Value >= best || claimedTargets.Contains(kv.Key) || grid.GetStructure(kv.Key) != RuinSystem.StarfishStructureId) continue;
                        if (!VisionSystem.IsExplored(grid, team, kv.Key) || (grid.GetOccupant(kv.Key) != TileData.NoOccupant && grid.GetOccupant(kv.Key) != id)) continue;
                        best = kv.Value; goal = kv.Key;
                    }
                    if (goal.HasValue) claimedTargets.Add(goal.Value);
                }

                // (d) 탐험
                bool exploring = false;
                if (goal == null) { goal = NearestFrontier(grid, world, id, team); exploring = true; }
                if (goal == null) continue;

                if (MoveToward(grid, world, id, goal.Value, goalIsEnemy, allowSail: !exploring || !CanReachOnFoot(grid, world, id, goal.Value)))
                {
                    entries.Add(new BattleLogEntry { ActorId = id, Verb = BattleLogVerb.Move, TargetId = BattleLogEntry.NoTarget });
                    VisionSystem.Reveal(grid, team, world.Get<GridPosition>(id).Value, VisionSystem.SightRadius(grid, world, id));
                }

                TryBestAttack(grid, world, econ, id, VisibleEnemies(grid, world, team), entries);
            }

            return entries;
        }

        private static bool HasOwnPort(GridWorld grid, Team team)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var t = grid.GetTile(new Vector2Int(x, y));
                if (t.BuildingId == BuildingDefinition.Port && t.OwnerTeam == (int)team) return true;
            }
            return false;
        }

        private static List<int> VisibleEnemies(GridWorld grid, EntityWorld world, Team team)
        {
            var list = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) != team && VisionSystem.IsExplored(grid, team, world.Get<GridPosition>(i).Value))
                    list.Add(i);
            return list;
        }

        /// <summary>사거리 안의 적 중 처치할 수 있는 대상 우선, 아니면 (준 피해 − 받을 반격의 절반)이 큰 대상을 공격한다. 처치하지
        /// 못하는데 반격에 죽는 공격은 하지 않는다(위키 전투 공식에서는 방어가 높은 대상에게 붙으면 반격이 공격보다 크다).</summary>
        private static bool TryBestAttack(GridWorld grid, EntityWorld world, EconomyWorld econ, int id, List<int> enemies, List<BattleLogEntry> entries)
        {
            var attack = UnitActionQueries.Find<AttackAction>(world, id);
            if (attack == null || !attack.CanExecute(world, id)) return false;
            int bestTarget = -1;
            float bestScore = float.MinValue;
            foreach (var e in enemies)
            {
                if (!UnitQueries.IsAlive(world, e) || !CombatSystem.IsInAttackRange(world, id, e)) continue;
                int dmg = CombatSystem.CalculateDamage(world, id, e);
                bool kill = dmg >= world.Get<Hp>(e).Value;
                int retaliation = CombatSystem.PreviewRetaliation(world, id, e);
                if (!kill && retaliation >= world.Get<Hp>(id).Value) continue;
                float score = dmg - retaliation * 0.5f + (kill ? 100f : 0f);
                if (score > bestScore) { bestScore = score; bestTarget = e; }
            }
            if (bestTarget < 0) return false;
            TryAttackAndLog(grid, world, econ, id, bestTarget, entries);
            return true;
        }

        /// <summary>이번 턴 도달 가능한 칸 중 goal까지 여행 거리가 가장 짧은 칸으로 이동한다(같으면 직선 거리). 적이 목표면
        /// 그 칸 자체에는 설 수 없으므로 거리 1 이상인 칸만 고른다. 실제로 움직였으면 true.</summary>
        private static bool MoveToward(GridWorld grid, EntityWorld world, int id, Vector2Int goal, bool goalIsEnemy, bool allowSail = true)
        {
            var move = UnitActionQueries.Find<MoveAction>(world, id);
            if (move == null || !move.CanExecute(world, id)) return false;

            var selfPos = world.Get<GridPosition>(id).Value;
            var toGoal = TravelMap(grid, world, id, goal, unitIsOrigin: false, allowSail);
            PathfindingSystem.GetReachable(grid, world, selfPos, id, out var reachable);

            int Score(Vector2Int tile)
            {
                if (toGoal.TryGetValue(tile, out int d)) return d;
                return 10000 + PathfindingSystem.Distance(tile, goal);
            }

            Vector2Int? best = null;
            int bestScore = Score(selfPos);
            int bestLine = PathfindingSystem.Distance(selfPos, goal);
            foreach (var tile in reachable)
            {
                if (tile == selfPos || grid.IsOccupied(tile)) continue;
                if (goalIsEnemy && tile == goal) continue;
                int s = Score(tile);
                int line = PathfindingSystem.Distance(tile, goal);
                if (s < bestScore || (s == bestScore && line < bestLine))
                {
                    bestScore = s; bestLine = line; best = tile;
                }
            }
            return best.HasValue && MovementSystem.TryMove(grid, world, id, best.Value);
        }

        /// <summary>
        /// 여행 거리 지도. unitIsOrigin=false면 "각 칸에서 origin(목표)까지", true면 "origin(유닛 위치)에서 각 칸까지"의
        /// 걸음 수(지형만 보고 유닛/도로/험지는 무시). 탐험한 칸만 지난다. 경계를 넘는 방향(승선/하선)은 실제 이동 방향 기준으로
        /// 판정한다 — BFS가 목표에서 거꾸로 퍼질 때도.
        ///   - 육지 유닛: 육지 + 다리. 자기 항구가 있으면 항구를 거쳐 물로 나가 다른 해안에 내리는 경로도 센다(+2 가산 — 승선/하선 턴).
        ///   - 배(승선 중): 물 + 해안 육지(내림).
        ///   - 물 유닛: 물만.
        /// </summary>
        private static Dictionary<Vector2Int, int> TravelMap(GridWorld grid, EntityWorld world, int id, Vector2Int origin, bool unitIsOrigin, bool allowSail = true)
        {
            var team = world.Get<Team>(id);
            bool embarked = EmbarkSystem.IsEmbarked(world, id);
            bool waterUnit = !embarked && world.Get<MoveDomain>(id).Value == TerrainType.Water;
            bool ignoreTerrain = UnitActionQueries.Find<IgnoreTerrainAction>(world, id) != null;
            bool canSail = embarked || (allowSail && !waterUnit && HasOwnPort(grid, team));

            // 상태: (칸, 물 위인가). 육지 유닛은 항구에서 물로 나가고, 물에서 육지로 내린다.
            var dist = new Dictionary<(Vector2Int, bool), int>();
            var queue = new Queue<(Vector2Int, bool)>();
            var start = (origin, grid.InBounds(origin) && grid.GetTerrain(origin) == TerrainType.Water && !IsBridge(grid, origin));
            dist[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var (cur, onWater) = queue.Dequeue();
                int d = dist[(cur, onWater)];
                foreach (var n in grid.GetNeighbors(cur, true))
                {
                    if (!VisionSystem.IsExplored(grid, team, n) || !grid.IsWalkable(n)) continue;
                    if (!ignoreTerrain && TechEffectSystem.IsTerrainLocked(grid, world, id, n)) continue;
                    var t = grid.GetTile(n);
                    bool nWater = t.Terrain == TerrainType.Water && !IsBridge(grid, n);
                    int step = 1;
                    // 실제 이동 방향(from → to).
                    bool fromWater = unitIsOrigin ? onWater : nWater;
                    bool toWater = unitIsOrigin ? nWater : onWater;
                    var to = unitIsOrigin ? n : cur;
                    if (ignoreTerrain) { }
                    else if (waterUnit) { if (!nWater) continue; }
                    else if (!fromWater && toWater)
                    {
                        // 승선: 자기 항구 칸으로 들어갈 때만.
                        if (!canSail || !IsOwnPort(grid, team, to)) continue;
                        step = 2;
                    }
                    else if (fromWater && !toWater)
                    {
                        // 하선: 배는 아무 해안에나 내린다.
                        if (!canSail) continue;
                        step = 2;
                    }
                    var key = (n, nWater);
                    if (dist.ContainsKey(key)) continue;
                    dist[key] = d + step;
                    queue.Enqueue(key);
                }
            }

            var result = new Dictionary<Vector2Int, int>();
            foreach (var kv in dist)
                if (!result.TryGetValue(kv.Key.Item1, out var old) || kv.Value < old) result[kv.Key.Item1] = kv.Value;
            return result;
        }

        /// <summary>배를 타지 않고 goal까지 갈 수 있는지(승선 중이면 false).</summary>
        private static bool CanReachOnFoot(GridWorld grid, EntityWorld world, int id, Vector2Int goal)
        {
            if (EmbarkSystem.IsEmbarked(world, id)) return false;
            return TravelMap(grid, world, id, world.Get<GridPosition>(id).Value, unitIsOrigin: true, allowSail: false).ContainsKey(goal);
        }

        private static bool IsBridge(GridWorld grid, Vector2Int p) => grid.GetTile(p).BuildingId == BuildingDefinition.Bridge;

        private static bool IsOwnPort(GridWorld grid, Team team, Vector2Int p)
        {
            var t = grid.GetTile(p);
            return t.BuildingId == BuildingDefinition.Port && t.OwnerTeam == (int)team;
        }

        /// <summary>유닛에게서 여행 거리가 가장 가까운 "구름 가장자리"(탐험했고 이웃에 구름이 있는 칸). 없으면 null.
        /// 육지 유닛은 걸어서 닿는 가장자리를 먼저 찾고, 없을 때만 배를 타는 경로를 본다(뭍과 바다를 오가며 헤매지 않게).</summary>
        private static Vector2Int? NearestFrontier(GridWorld grid, EntityWorld world, int id, Team team)
        {
            if (!grid.FogEnabled) return null;
            if (!EmbarkSystem.IsEmbarked(world, id))
            {
                var onLand = NearestFrontier(grid, world, id, team, allowSail: false);
                if (onLand.HasValue) return onLand;
            }
            return NearestFrontier(grid, world, id, team, allowSail: true);
        }

        private static Vector2Int? NearestFrontier(GridWorld grid, EntityWorld world, int id, Team team, bool allowSail)
        {
            var selfPos = world.Get<GridPosition>(id).Value;
            var fromSelf = TravelMap(grid, world, id, selfPos, unitIsOrigin: true, allowSail);
            // 가장 가까운 칸, 같은 거리면 주변 구름이 많은 칸(더 많이 걷힌다), 그래도 같으면 좌표 순(결정적).
            Vector2Int? best = null;
            int bestD = Unreachable, bestFog = 0;
            foreach (var kv in fromSelf)
            {
                if (kv.Value == 0 || kv.Value > bestD) continue;
                int fog = 0;
                foreach (var n in grid.GetNeighbors(kv.Key, true))
                    if (!VisionSystem.IsExplored(grid, team, n)) fog++;
                if (fog == 0) continue;
                bool better = kv.Value < bestD || fog > bestFog ||
                              (fog == bestFog && (kv.Key.y < best.Value.y || (kv.Key.y == best.Value.y && kv.Key.x < best.Value.x)));
                if (!better) continue;
                best = kv.Key; bestD = kv.Value; bestFog = fog;
            }
            return best;
        }

        /// <summary>공격 한 번(+반격/사망)을 실행하고 결과를 entries에 그대로 옮겨 담는다. 플레이어 쪽
        /// BattleController.TryAttack과 같은 판단 순서(공격 -> 대상 사망 확인 -> 반격 -> 반격자 사망 확인)를
        /// 따른다. econ이 있으면 과업(공격/처치)도 기록한다.</summary>
        private static void TryAttackAndLog(GridWorld grid, EntityWorld world, EconomyWorld econ, int attackerId, int targetId, List<BattleLogEntry> entries)
        {
            var aliveBefore = econ != null ? TaskSystem.SnapshotAlive(world) : null;
            if (!CombatSystem.TryAttack(grid, world, attackerId, targetId, out int damage, out int counterDamage)) return;
            if (econ != null)
            {
                TaskSystem.RecordAttack(econ, world.Get<Team>(attackerId));
                TaskSystem.RecordDeaths(econ, world, aliveBefore);
            }

            entries.Add(new BattleLogEntry { ActorId = attackerId, Verb = BattleLogVerb.Attack, TargetId = targetId, Amount = damage });
            if (!UnitQueries.IsAlive(world, targetId))
                entries.Add(new BattleLogEntry { ActorId = targetId, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget });

            if (counterDamage > 0)
            {
                entries.Add(new BattleLogEntry { ActorId = targetId, Verb = BattleLogVerb.Counter, TargetId = attackerId, Amount = counterDamage });
                if (!UnitQueries.IsAlive(world, attackerId))
                    entries.Add(new BattleLogEntry { ActorId = attackerId, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget });
            }
        }
    }
}
