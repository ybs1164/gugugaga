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
        public static void RunTurn(GridWorld grid, EntityWorld world)
        {
            var enemyIds = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Enemy) enemyIds.Add(i);

            foreach (var id in enemyIds)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;

                var targetIds = new List<int>();
                for (int i = 0; i < world.EntityCount; i++)
                    if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Player) targetIds.Add(i);
                if (targetIds.Count == 0) return;

                int nearestId = targetIds
                    .OrderBy(t => PathfindingSystem.Distance(world.Get<GridPosition>(id).Value, world.Get<GridPosition>(t).Value))
                    .First();

                if (CombatSystem.IsInAttackRange(world, id, nearestId))
                {
                    CombatSystem.TryAttack(grid, world, id, nearestId, out _);
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
                        CombatSystem.TryAttack(grid, world, id, nearestId, out _);
                }
            }
        }
    }
}
