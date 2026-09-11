using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 이 행동을 쓸 수 있고, 실행 가능 여부
    /// 판정과 실제 이동(그리드 occupant 갱신 포함)을 전부 이 클래스가 직접 담당한다 — 예전
    /// MovementSystem.TryMove에 있던 로직이 그대로 이 안으로 옮겨왔다. MovementSystem은 이제
    /// UnitActionQueries로 이 행동을 찾아 Execute를 호출해주는 얇은 진입점일 뿐이다.
    /// </summary>
    [System.Serializable]
    public class MoveAction : IMoveAction
    {
        [SerializeField] private int moveRange;
        [Tooltip("true면 지형(Walkable=false인 타일, 예: 벽/장애물)을 무시하고 이동할 수 있다. 비행 유닛 등에 사용.")]
        [SerializeField] private bool ignoreTerrain;
        [Tooltip("true면 다른 유닛이 있는 타일도 지나가거나 멈출 수 있다. 유령/투명체 등에 사용.")]
        [SerializeField] private bool ignoreUnitBlocking;
        [Tooltip("대각선 방향(8방향) 이동을 허용할지 여부. false면 상하좌우 4방향만 이동 가능.")]
        [SerializeField] private bool allowDiagonal;

        public ActionType GetActionType() => ActionType.Move;

        public int MoveRange => moveRange;
        public bool IgnoreTerrain => ignoreTerrain;
        public bool IgnoreUnitBlocking => ignoreUnitBlocking;
        public bool AllowDiagonal => allowDiagonal;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasMoved>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            if (!CanExecute(world, unitId)) return false;
            if (!grid.InBounds(destination)) return false;
            if (!world.Get<IgnoreTerrain>(unitId).Value && !grid.IsWalkable(destination)) return false;

            int occ = grid.GetOccupant(destination);
            if (!world.Get<IgnoreUnitBlocking>(unitId).Value && occ != TileData.NoOccupant && occ != unitId) return false;

            grid.RemoveOccupant(world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new GridPosition { Value = destination });
            world.Set(unitId, new HasMoved { Value = true });
            grid.PlaceOccupant(destination, unitId);

            return true;
        }
    }
}
