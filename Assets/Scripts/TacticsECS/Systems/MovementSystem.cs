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
            return move != null && move.Execute(grid, world, unitId, destination);
        }
    }
}
