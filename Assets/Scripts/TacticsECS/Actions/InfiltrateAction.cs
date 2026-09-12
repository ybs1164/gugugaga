namespace TacticsECS
{
    /// <summary>
    /// 잠입 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, PathfindingSystem.GetReachable과
    /// MoveAction.Execute가 이동 경로상의 점유 유닛을 차단할지 판정할 때
    /// UnitActionQueries.Find&lt;InfiltrateAction&gt;로 보유 여부를 확인해 "적 유닛에 의한 차단만 무시"로
    /// 판단을 바꿔준다. 모든 유닛(아군 포함)의 차단을 무시하는 MoveAction.IgnoreUnitBlocking과 달리,
    /// 아군에 의한 차단은 그대로 적용된다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class InfiltrateAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Infiltrate;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
