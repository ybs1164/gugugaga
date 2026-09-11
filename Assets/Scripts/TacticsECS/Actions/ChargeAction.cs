namespace TacticsECS
{
    /// <summary>
    /// 돌격 패시브. Counter와 마찬가지로 플레이어가 고르는 "행동 버튼"이 아니라 항상 자동으로 적용되는
    /// 태그다 — 자기 자신을 Execute하는 동작이 없고(그래서 IUnitAction만 구현하고 ISelfAction/
    /// ITargetedAction 등 하위 인터페이스는 구현하지 않는다), AttackAction.CanExecute가
    /// UnitActionQueries.Find&lt;ChargeAction&gt;로 보유 여부만 확인해 "이번 턴 이미 이동했어도 공격
    /// 가능"으로 판단을 바꿔준다. 값(수치)은 없다 — 순수 마커.
    /// </summary>
    [System.Serializable]
    public class ChargeAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Charge;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
