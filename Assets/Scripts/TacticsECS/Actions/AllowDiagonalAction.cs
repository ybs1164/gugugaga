namespace TacticsECS
{
    /// <summary>
    /// 대각선 이동 패시브. Charge/Retreat/Infiltrate와 같은 성격 — 자기 자신을 Execute하는 동작이 없고,
    /// PathfindingSystem.GetReachable이 이웃 타일을 탐색할 때 UnitActionQueries.Find&lt;AllowDiagonalAction&gt;로
    /// 보유 여부만 확인해 "8방향(대각선 포함) 이동 가능"으로 판단을 바꿔준다 — 없으면 상하좌우 4방향만.
    /// 값(수치)은 없다. 대부분의 유닛에는 해당 없는 드문 케이스라 예전 MoveAction 자신의 bool 필드
    /// (CSV의 Move.AllowDiagonal 컬럼)에서 이 패시브로 옮겨왔다.
    /// </summary>
    [System.Serializable]
    public class AllowDiagonalAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.AllowDiagonal;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
