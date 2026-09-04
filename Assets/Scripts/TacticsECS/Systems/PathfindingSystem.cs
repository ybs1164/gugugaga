using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 순수 함수 형태의 시스템(정적 클래스). GridWorld 데이터만 읽어서 결과를 계산하고,
    /// 상태는 전혀 들고 있지 않는다. BFS 기반이며 UnitMovement 값에 따라 대각선 이동/지형 무시/
    /// 유닛 무시 여부가 달라진다.
    /// </summary>
    public static class PathfindingSystem
    {
        /// <summary>
        /// start에서 movement.MoveRange 이내로 이동 가능한 모든 타일을 계산한다.
        /// movement.IgnoreTerrain이 false면 Walkable=false인 타일을 지나갈 수 없고,
        /// movement.IgnoreUnitBlocking이 false면 다른 유닛이 서 있는 타일은 통과/정지 모두 불가(자기 자신은 예외),
        /// movement.AllowDiagonal이 true면 8방향, 아니면 4방향으로 탐색한다.
        /// 반환값은 각 타일의 직전 타일(경로 역추적용), reachableSet은 도달 가능한 타일 전체.
        /// </summary>
        public static Dictionary<Vector2Int, Vector2Int> GetReachable(
            GridWorld grid, Vector2Int start, UnitMovement movement, int selfUnitId,
            out HashSet<Vector2Int> reachableSet)
        {
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var cur = frontier.Dequeue();
                int curDist = dist[cur];
                if (curDist >= movement.MoveRange) continue;

                foreach (var next in grid.GetNeighbors(cur, movement.AllowDiagonal))
                {
                    if (dist.ContainsKey(next)) continue;
                    if (!movement.IgnoreTerrain && !grid.IsWalkable(next)) continue;

                    int occ = grid.GetOccupant(next);
                    if (!movement.IgnoreUnitBlocking && occ != TileData.NoOccupant && occ != selfUnitId) continue;

                    dist[next] = curDist + 1;
                    cameFrom[next] = cur;
                    frontier.Enqueue(next);
                }
            }

            reachableSet = new HashSet<Vector2Int>(dist.Keys);
            return cameFrom;
        }

        public static int Distance(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
