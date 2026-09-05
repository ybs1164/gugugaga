using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 아주 단순한 규칙 기반 AI.
    /// 각 적 유닛: 사거리 안에 플레이어 유닛이 있으면 공격, 없으면 가장 가까운 적 쪽으로 이동 후 가능하면 공격.
    /// </summary>
    public static class EnemyAI
    {
        /// <summary>
        /// 이번 턴 실제로 성사된 공격들을 (공격자 id, 대상 id) 목록으로 돌려준다.
        /// EnemyAI 자신은 View를 전혀 모르지만(Systems는 View/MonoBehaviour를 몰라야 함),
        /// 이 값을 받은 BattleController가 공격자 View를 대상 쪽으로 바라보게 하는 데 쓴다.
        /// </summary>
        public static List<(int AttackerId, int TargetId)> RunTurn(GridWorld grid, EntityWorld world)
        {
            var attacks = new List<(int, int)>();

            var enemyIds = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Enemy) enemyIds.Add(i);

            foreach (var id in enemyIds)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;

                var targetIds = new List<int>();
                for (int i = 0; i < world.EntityCount; i++)
                    if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Player) targetIds.Add(i);
                if (targetIds.Count == 0) return attacks;

                int nearestId = targetIds
                    .OrderBy(t => PathfindingSystem.Distance(world.Get<GridPosition>(id).Value, world.Get<GridPosition>(t).Value))
                    .First();

                if (CombatSystem.IsInAttackRange(world, id, nearestId))
                {
                    if (CombatSystem.TryAttack(grid, world, id, nearestId, out _))
                        attacks.Add((id, nearestId));
                    continue;
                }

                PathfindingSystem.GetReachable(grid, world, world.Get<GridPosition>(id).Value, id, out var reachable);

                Vector2Int? best = null;
                int bestDist = int.MaxValue;
                foreach (var tile in reachable)
                {
                    int d = PathfindingSystem.Distance(tile, world.Get<GridPosition>(nearestId).Value);
                    if (d < bestDist && d >= 1)
                    {
                        bestDist = d;
                        best = tile;
                    }
                }

                if (best.HasValue)
                {
                    MovementSystem.TryMove(grid, world, id, best.Value);
                    if (CombatSystem.IsInAttackRange(world, id, nearestId))
                    {
                        if (CombatSystem.TryAttack(grid, world, id, nearestId, out _))
                            attacks.Add((id, nearestId));
                    }
                }
            }

            return attacks;
        }
    }
}
