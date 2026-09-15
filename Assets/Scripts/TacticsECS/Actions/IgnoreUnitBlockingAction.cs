namespace TacticsECS
{
    /// <summary>
    /// 장애물(유닛) 통과 패시브. Charge/Retreat/Infiltrate와 같은 성격 — 자기 자신을 Execute하는 동작이
    /// 없고, PathfindingSystem.GetReachable과 MoveAction.Execute가 이동 경로상의 점유 유닛을 차단할지
    /// 판정할 때 UnitActionQueries.Find&lt;IgnoreUnitBlockingAction&gt;로 보유 여부만 확인해 "다른 유닛
    /// (아군/적군 모두)이 있는 타일도 지나가거나 멈출 수 있음"으로 판단을 바꿔준다(유령/투명체 등). 적
    /// 유닛에 의한 차단만 무시하고 싶다면 이 대신 잠입(InfiltrateAction) 패시브를 쓴다. 값(수치)은 없다.
    /// 대부분의 유닛에는 해당 없는 드문 케이스라 예전 MoveAction 자신의 bool 필드
    /// (CSV의 Move.IgnoreUnitBlocking 컬럼)에서 이 패시브로 옮겨왔다.
    /// </summary>
    [System.Serializable]
    public class IgnoreUnitBlockingAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.IgnoreUnitBlocking;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
