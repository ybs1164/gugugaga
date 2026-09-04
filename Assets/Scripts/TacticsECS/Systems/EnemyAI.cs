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
        public static void RunTurn(GridWorld grid, UnitWorld units)
        {
            var enemyIds = new List<int>();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units.Get(i);
                if (u.IsAlive && u.Team == Team.Enemy) enemyIds.Add(i);
            }

            foreach (var id in enemyIds)
            {
                var self = units.Get(id);
                if (!self.IsAlive) continue;

                var targets = units.All.Where(t => t.IsAlive && t.Team == Team.Player).ToList();
                if (targets.Count == 0) return;

                var nearest = targets.OrderBy(t => PathfindingSystem.Distance(self.GridPos, t.GridPos)).First();

                if (CombatSystem.IsInAttackRange(units, id, nearest.Id))
                {
                    CombatSystem.TryAttack(grid, units, id, nearest.Id, out _);
                    continue;
                }

                var movement = units.GetStats(id).Movement;
                PathfindingSystem.GetReachable(grid, self.GridPos, movement, id, out var reachable);

                Vector2Int? best = null;
                int bestDist = int.MaxValue;
                foreach (var tile in reachable)
                {
                    int d = PathfindingSystem.Distance(tile, nearest.GridPos);
                    if (d < bestDist && d >= 1)
                    {
                        bestDist = d;
                        best = tile;
                    }
                }

                if (best.HasValue)
                {
                    MovementSystem.TryMove(grid, units, id, best.Value);
                    if (CombatSystem.IsInAttackRange(units, id, nearest.Id))
                        CombatSystem.TryAttack(grid, units, id, nearest.Id, out _);
                }
            }
        }
    }
}
