using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 아주 단순한 규칙 기반 AI.
    /// 각 적 유닛: 사거리 안에 플레이어 유닛이 있으면 공격, 없으면 가장 가까운 적 쪽으로 이동 후 가능하면 공격.
    /// captureTargets(경제가 켜진 전투에서 BattleController가 넘기는 점령 가능한 마을/플레이어 도시 칸)가 주어지면
    /// 플레이어 유닛보다 가까운 목표로 먼저 이동해 그 위에서 턴을 넘긴다 — 다음 적 턴 시작 시 EconomyAI가 점령한다.
    /// </summary>
    public static class EnemyAI
    {
        /// <summary>
        /// 이번 턴 실제로 일어난 행동들을 순서대로 BattleLogEntry 목록으로 돌려준다. EnemyAI 자신은
        /// View를 전혀 모르지만(Systems는 View/MonoBehaviour를 몰라야 함), 이 값을 받은 BattleController가
        /// 공격자 View를 대상 쪽으로 바라보게 하거나(Verb.Attack), 우측 상단 행동 로그에 그대로 옮기거나,
        /// 피해를 입은 유닛 위에 데미지 라벨을 띄우는 데 쓴다.
        /// </summary>
        public static List<BattleLogEntry> RunTurn(GridWorld grid, EntityWorld world, IReadOnlyCollection<Vector2Int> captureTargets = null)
        {
            var entries = new List<BattleLogEntry>();

            var enemyIds = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Enemy) enemyIds.Add(i);

            foreach (var id in enemyIds)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;
                var selfPos = world.Get<GridPosition>(id).Value;

                var targetIds = new List<int>();
                for (int i = 0; i < world.EntityCount; i++)
                    if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Player) targetIds.Add(i);

                int nearestId = targetIds.Count == 0 ? -1 : targetIds
                    .OrderBy(t => PathfindingSystem.Distance(selfPos, world.Get<GridPosition>(t).Value))
                    .First();
                int playerDist = nearestId < 0 ? int.MaxValue : PathfindingSystem.Distance(selfPos, world.Get<GridPosition>(nearestId).Value);

                // 점령 목표(중립 마을/플레이어 도시): 이미 그 위에 서 있으면 다음 턴 점령을 위해 제자리를 지키고,
                // 플레이어 유닛보다 가까운 목표가 있으면 그쪽으로 간다(도착 칸 = 목표 칸 자체).
                Vector2Int? captureGoal = null;
                if (captureTargets != null)
                {
                    int bestCapture = int.MaxValue;
                    foreach (var c in captureTargets)
                    {
                        int occ = grid.GetOccupant(c);
                        if (occ != TileData.NoOccupant && occ != id) continue;
                        int d = PathfindingSystem.Distance(selfPos, c);
                        if (d < bestCapture) { bestCapture = d; captureGoal = c; }
                    }
                    if (captureGoal.HasValue && bestCapture >= playerDist) captureGoal = null;
                }

                if (nearestId >= 0 && CombatSystem.IsInAttackRange(world, id, nearestId))
                {
                    TryAttackAndLog(grid, world, id, nearestId, entries);
                    continue;
                }
                if (captureGoal.HasValue && captureGoal.Value == selfPos) continue;
                if (nearestId < 0 && !captureGoal.HasValue) continue;

                var goal = captureGoal ?? world.Get<GridPosition>(nearestId).Value;
                int minDist = captureGoal.HasValue ? 0 : 1;
                PathfindingSystem.GetReachable(grid, world, selfPos, id, out var reachable);

                Vector2Int? best = null;
                int bestDist = int.MaxValue;
                foreach (var tile in reachable)
                {
                    if (tile != selfPos && grid.IsOccupied(tile)) continue;
                    int d = PathfindingSystem.Distance(tile, goal);
                    if (d < bestDist && d >= minDist)
                    {
                        bestDist = d;
                        best = tile;
                    }
                }

                if (best.HasValue && best.Value != selfPos)
                {
                    if (MovementSystem.TryMove(grid, world, id, best.Value))
                        entries.Add(new BattleLogEntry { ActorId = id, Verb = BattleLogVerb.Move, TargetId = BattleLogEntry.NoTarget });
                }

                if (nearestId >= 0 && CombatSystem.IsInAttackRange(world, id, nearestId))
                    TryAttackAndLog(grid, world, id, nearestId, entries);
            }

            return entries;
        }

        /// <summary>공격 한 번(+반격/사망)을 실행하고 결과를 entries에 그대로 옮겨 담는다. 플레이어 쪽
        /// BattleController.TryAttack과 같은 판단 순서(공격 -> 대상 사망 확인 -> 반격 -> 반격자 사망 확인)를
        /// 따른다.</summary>
        private static void TryAttackAndLog(GridWorld grid, EntityWorld world, int attackerId, int targetId, List<BattleLogEntry> entries)
        {
            if (!CombatSystem.TryAttack(grid, world, attackerId, targetId, out int damage, out int counterDamage)) return;

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
