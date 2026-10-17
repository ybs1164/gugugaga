using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동의 진입점. 실제 판정(생존/이번 턴 이동 여부/지형/유닛 차단)과 적용(그리드 occupant 갱신
    /// 포함)은 유닛이 가진 MoveAction(Assets/Scripts/TacticsECS/Actions/MoveAction.cs)이 직접 담당하고,
    /// 이 System은 UnitActionQueries로 그 행동을 찾아 위임할 뿐이다.
    /// </summary>
    public static class MovementSystem
    {
        public static bool TryMove(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            var move = UnitActionQueries.Find<MoveAction>(world, unitId);
            bool stomp = UnitActionQueries.Find<StompAction>(world, unitId) != null;
            var aliveBefore = grid.Economy != null ? TaskSystem.SnapshotAlive(world) : null;
            var path = UnitActionQueries.Find<RazeAction>(world, unitId) != null ? PathTo(grid, world, unitId, destination) : null;
            if (move == null || !move.Execute(grid, world, unitId, destination)) return false;

            // 파괴: 지나간 칸(도착 칸 포함)의 건물·유적을 없앤다.
            if (path != null) TerrainChangeSystem.Raze(grid, world, unitId, path);
            // 자동 빙결: 도착한 곳 주변의 물을 얼리고 적을 빙결시킨다.
            if (UnitActionQueries.Find<AutoFreezeAction>(world, unitId) != null) TerrainChangeSystem.FreezeAround(grid, world, unitId);

            // 항구에 들어간 육지 유닛은 뗏목이 되고, 육지에 닿은 배는 원래 유닛으로 내린다(위키 Port/Carry).
            bool changedForm = EmbarkSystem.ApplyAfterMove(grid, world, unitId);
            if (!changedForm && stomp) StompSystem.Apply(grid, world, unitId);
            TaskSystem.RecordDeaths(grid.Economy, world, aliveBefore);

            // 무리: 이동으로 인접 관계가 바뀌었을 수 있으니 가속 부여를 다시 계산한다.
            PassiveAuraSystem.RefreshHerdAura(world);
            return true;
        }

        /// <summary>MoveRange에 가속(Accelerated) +1, 얼음 칸에서 출발하는 스케이트(Skate) +1을 합산한 실제 이동 거리.</summary>
        public static int EffectiveMoveRange(EntityWorld world, int unitId, GridWorld grid = null)
        {
            int bonus = world.Get<Accelerated>(unitId).Value ? 1 : 0;
            if (grid != null && UnitActionQueries.Find<SkateAction>(world, unitId) != null &&
                grid.GetTile(world.Get<GridPosition>(unitId).Value).Iced) bonus++;
            return world.Get<MoveRange>(unitId).Value + bonus;
        }

        /// <summary>지금 자리에서 destination까지 경로 탐색이 고른 칸들(출발 칸 제외, 도착 칸 포함). 갈 수 없으면 도착 칸만.</summary>
        public static System.Collections.Generic.List<Vector2Int> PathTo(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            var start = world.Get<GridPosition>(unitId).Value;
            var cameFrom = PathfindingSystem.GetReachable(grid, world, start, unitId, out _);
            var path = new System.Collections.Generic.List<Vector2Int>();
            var cur = destination;
            while (cur != start && path.Count <= grid.Width * grid.Height)
            {
                path.Add(cur);
                if (!cameFrom.TryGetValue(cur, out cur)) break;
            }
            path.Reverse();
            return path;
        }
    }
}
