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
                if (units.IsAlive(i) && units.GetTeam(i) == Team.Enemy) enemyIds.Add(i);

            foreach (var id in enemyIds)
            {
                if (!units.IsAlive(id)) continue;

                var targetIds = new List<int>();
                for (int i = 0; i < units.Count; i++)
                    if (units.IsAlive(i) && units.GetTeam(i) == Team.Player) targetIds.Add(i);
                if (targetIds.Count == 0) return;

                int nearestId = targetIds
                    .OrderBy(t => PathfindingSystem.Distance(units.GetGridPos(id), units.GetGridPos(t)))
                    .First();

                if (CombatSystem.IsInAttackRange(units, id, nearestId))
                {
                    CombatSystem.TryAttack(grid, units, id, nearestId, out _);
                    continue;
                }

                PathfindingSystem.GetReachable(grid, units, units.GetGridPos(id), id, out var reachable);

                Vector2Int? best = null;
                int bestDist = int.MaxValue;
                foreach (var tile in reachable)
                {
                    int d = PathfindingSystem.Distance(tile, units.GetGridPos(nearestId));
                    if (d < bestDist && d >= 1)
                    {
                        bestDist = d;
                        best = tile;
                    }
                }

                if (best.HasValue)
                {
                    MovementSystem.TryMove(grid, units, id, best.Value);
                    if (CombatSystem.IsInAttackRange(units, id, nearestId))
                        CombatSystem.TryAttack(grid, units, id, nearestId, out _);
                }
            }
        }
    }
}
