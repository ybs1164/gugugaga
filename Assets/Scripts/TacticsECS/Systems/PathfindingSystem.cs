using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 순수 함수 형태의 시스템(정적 클래스). GridWorld/EntityWorld 데이터만 읽어서 결과를 계산하고,
    /// 상태는 전혀 들고 있지 않는다. BFS 기반이며 이동하는 엔티티의 컴포넌트(MoveRange/Accelerated/
    /// MoveDomain)와 보유 패시브(IgnoreTerrainAction/IgnoreUnitBlockingAction/AllowDiagonalAction/
    /// InfiltrateAction)에 따라 실효 이동 거리/대각선 이동/지형 무시/유닛 무시 여부가 달라진다.
    /// </summary>
    public static class PathfindingSystem
    {
        /// <summary>
        /// start에서 selfUnitId의 실효 이동 거리(MovementSystem.EffectiveMoveRange — 가속 보너스 포함)
        /// 이내로 이동 가능한 모든 타일을 계산한다.
        /// IgnoreTerrainAction이 없으면 Walkable=false인 타일이나 MoveDomain과 지형(TileData.Terrain)이 다른
        /// 타일(예: 육지 유닛의 물 타일)을 지나갈 수 없고,
        /// IgnoreUnitBlockingAction이 없으면 다른 유닛이 서 있는 타일은 통과/정지 모두 불가(자기 자신은 예외),
        /// AllowDiagonalAction이 있으면 8방향, 없으면 4방향으로 탐색한다.
        /// 반환값은 각 타일의 직전 타일(경로 역추적용), reachableSet은 도달 가능한 타일 전체.
        /// </summary>
        public static Dictionary<Vector2Int, Vector2Int> GetReachable(
            GridWorld grid, EntityWorld world, Vector2Int start, int selfUnitId,
            out HashSet<Vector2Int> reachableSet)
        {
            int moveRange = MovementSystem.EffectiveMoveRange(world, selfUnitId);
            bool ignoreTerrain = UnitActionQueries.Find<IgnoreTerrainAction>(world, selfUnitId) != null;
            bool ignoreUnitBlocking = UnitActionQueries.Find<IgnoreUnitBlockingAction>(world, selfUnitId) != null;
            bool allowDiagonal = UnitActionQueries.Find<AllowDiagonalAction>(world, selfUnitId) != null;
            var moveDomain = world.Get<MoveDomain>(selfUnitId).Value;

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var cur = frontier.Dequeue();
                int curDist = dist[cur];
                if (curDist >= moveRange) continue;

                foreach (var next in grid.GetNeighbors(cur, allowDiagonal))
                {
                    if (dist.ContainsKey(next)) continue;
                    if (!ignoreTerrain && (!grid.IsWalkable(next) || grid.GetTerrain(next) != moveDomain ||
                        TechEffectSystem.IsTerrainLocked(grid, world, selfUnitId, next))) continue;
                    if (IsBlockedByOccupant(grid, world, selfUnitId, next, ignoreUnitBlocking)) continue;

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

        /// <summary>tile의 점유자가 selfUnitId의 이동을 막는지 판정한다(빈 타일/자기 자신은 막지 않음).
        /// ignoreUnitBlocking이 true면 점유자가 누구든 무시하고, 그렇지 않아도 selfUnitId가 잠입
        /// (InfiltrateAction)을 가졌으면 적 팀 점유자에 의한 차단만 추가로 무시한다 — 아군에 의한
        /// 차단은 잠입으로도 무시되지 않는다. GetReachable(경로 탐색)과 MoveAction.Execute(실제 이동)가
        /// 이 판정을 공유한다.</summary>
        public static bool IsBlockedByOccupant(GridWorld grid, EntityWorld world, int selfUnitId, Vector2Int tile, bool ignoreUnitBlocking)
        {
            int occ = grid.GetOccupant(tile);
            if (occ == TileData.NoOccupant || occ == selfUnitId) return false;
            if (ignoreUnitBlocking) return false;
            if (world.Get<Team>(occ) != world.Get<Team>(selfUnitId) &&
                UnitActionQueries.Find<InfiltrateAction>(world, selfUnitId) != null) return false;
            return true;
        }
    }
}
