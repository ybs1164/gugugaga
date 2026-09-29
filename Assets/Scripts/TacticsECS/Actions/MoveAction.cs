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

        public ActionType GetActionType() => ActionType.Move;

        /// <summary>CSV 행(UnitCsvRow)의 Move 파라미터로 인스턴스를 만든다. 같은 클래스 안이라 private
        /// 필드에 직접 대입할 수 있어, Inspector용 필드를 리플렉션 없이 그대로 재사용한다. 장애물 통과/
        /// 지형 무시/대각선 이동은 더 이상 이 행동 자신의 값이 아니라 별도 패시브(IgnoreTerrainAction/
        /// IgnoreUnitBlockingAction/AllowDiagonalAction, Actions에 별도로 담긴다)로 옮겨졌다.</summary>
        public static MoveAction FromCsv(int moveRange) => new MoveAction { moveRange = moveRange };

        public int MoveRange => moveRange;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasMoved>(unitId).Value &&
            (!world.Get<HasActed>(unitId).Value || UnitActionQueries.Find<RetreatAction>(world, unitId) != null);

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            if (!CanExecute(world, unitId)) return false;
            if (!grid.InBounds(destination)) return false;

            // 지형/구름/다리/항구 승선/배 하선 판정은 경로 탐색과 같은 규칙(PathfindingSystem.CanEnter).
            if (!PathfindingSystem.CanEnter(grid, world, unitId, destination, out _)) return false;

            bool ignoreUnitBlocking = UnitActionQueries.Find<IgnoreUnitBlockingAction>(world, unitId) != null;
            int occupant = grid.GetOccupant(destination);
            if (occupant != TileData.NoOccupant && occupant != unitId && StealthSystem.IsHiddenFrom(world, occupant, world.Get<Team>(unitId)))
            {
                // 위키 Cloak: 숨은 유닛 칸으로 들어가려 하면 이동이 취소되고(행동 소모 없음) 그 유닛이 드러난다.
                StealthSystem.Reveal(world, occupant);
                return false;
            }
            if (PathfindingSystem.IsBlockedByOccupant(grid, world, unitId, destination, ignoreUnitBlocking)) return false;
            if (occupant != TileData.NoOccupant && occupant != unitId && !ignoreUnitBlocking) return false; // 지나갈 수는 있어도 멈출 수는 없다

            grid.RemoveOccupant(world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new GridPosition { Value = destination });
            world.Set(unitId, new HasMoved { Value = true });
            grid.PlaceOccupant(destination, unitId);

            return true;
        }
    }
}
