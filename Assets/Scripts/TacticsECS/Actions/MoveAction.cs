using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 이 행동을 쓸 수 있고, 실행 가능 여부
    /// 판정과 실제 이동(그리드 occupant 갱신 포함)을 전부 이 클래스가 직접 담당한다 — 예전
    /// MovementSystem.TryMove에 있던 로직이 그대로 이 안으로 옮겨왔다. MovementSystem은 이제
    /// UnitActionQueries로 이 행동을 찾아 Execute를 호출해주는 얇은 진입점일 뿐이다.
    /// 기본적으로 이번 턴 이미 공격한 유닛은 이동할 수 없다(이동 또는 공격 중 하나만) — 대피
    /// (RetreatAction)를 가진 유닛만 그 제약의 예외로 공격 후에도 이동할 수 있다.
    /// </summary>
    [System.Serializable]
    public class MoveAction : IMoveAction
    {
        [SerializeField] private int moveRange;
        [Tooltip("true면 지형(Walkable=false인 타일(예: 벽/장애물) 및 MoveDomain과 다른 지형(예: 육지 유닛의 " +
                 "물 타일))을 무시하고 이동할 수 있다. 비행 유닛 등에 사용.")]
        [SerializeField] private bool ignoreTerrain;
        [Tooltip("true면 다른 유닛(아군/적군 모두)이 있는 타일도 지나가거나 멈출 수 있다. 유령/투명체 등에 " +
                 "사용. 적 유닛에 의한 차단만 무시하고 싶다면 이 필드 대신 잠입(InfiltrateAction) 패시브를 쓴다.")]
        [SerializeField] private bool ignoreUnitBlocking;
        [Tooltip("대각선 방향(8방향) 이동을 허용할지 여부. false면 상하좌우 4방향만 이동 가능.")]
        [SerializeField] private bool allowDiagonal;

        public ActionType GetActionType() => ActionType.Move;

        /// <summary>CSV 행(UnitCsvRow)의 Move 파라미터로 인스턴스를 만든다. 같은 클래스 안이라 private
        /// 필드에 직접 대입할 수 있어, Inspector용 필드를 리플렉션 없이 그대로 재사용한다.</summary>
        public static MoveAction FromCsv(int moveRange, bool ignoreTerrain, bool ignoreUnitBlocking, bool allowDiagonal) =>
            new MoveAction
            {
                moveRange = moveRange,
                ignoreTerrain = ignoreTerrain,
                ignoreUnitBlocking = ignoreUnitBlocking,
                allowDiagonal = allowDiagonal
            };

        public int MoveRange => moveRange;
        public bool IgnoreTerrain => ignoreTerrain;
        public bool IgnoreUnitBlocking => ignoreUnitBlocking;
        public bool AllowDiagonal => allowDiagonal;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasMoved>(unitId).Value &&
            (!world.Get<HasActed>(unitId).Value || UnitActionQueries.Find<RetreatAction>(world, unitId) != null);

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            if (!CanExecute(world, unitId)) return false;
            if (!grid.InBounds(destination)) return false;
            if (!world.Get<IgnoreTerrain>(unitId).Value &&
                (!grid.IsWalkable(destination) || grid.GetTerrain(destination) != world.Get<MoveDomain>(unitId).Value)) return false;
            if (PathfindingSystem.IsBlockedByOccupant(grid, world, unitId, destination, world.Get<IgnoreUnitBlocking>(unitId).Value)) return false;

            grid.RemoveOccupant(world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new GridPosition { Value = destination });
            world.Set(unitId, new HasMoved { Value = true });
            grid.PlaceOccupant(destination, unitId);

            return true;
        }
    }
}
