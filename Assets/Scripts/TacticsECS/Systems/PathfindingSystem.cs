using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 순수 함수 형태의 시스템(정적 클래스). GridWorld 데이터만 읽어서 결과를 계산하고,
    /// 상태는 전혀 들고 있지 않는다. 4방향 이동 기준 BFS.
    /// </summary>
    public static class PathfindingSystem
    {
        /// <summary>
        /// start에서 moveRange 이내로 이동 가능한 모든 타일을 계산한다.
        /// 다른 유닛이 서 있는 타일은 통과/정지 모두 불가 (자기 자신은 예외).
        /// 반환값은 각 타일의 직전 타일(경로 역추적용), reachableSet은 도달 가능한 타일 전체.
        /// </summary>
        public static Dictionary<Vector2Int, Vector2Int> GetReachable(
            GridWorld grid, Vector2Int start, int moveRange, int selfUnitId,
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
                if (curDist >= moveRange) continue;

                foreach (var next in grid.GetNeighbors4(cur))
                {
                    if (dist.ContainsKey(next)) continue;
                    if (!grid.IsWalkable(next)) continue;

                    int occ = grid.GetOccupant(next);
                    if (occ != TileData.NoOccupant && occ != selfUnitId) continue;

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
