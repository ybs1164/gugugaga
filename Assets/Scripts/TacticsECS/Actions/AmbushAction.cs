namespace TacticsECS
{
    /// <summary>
    /// 기습 패시브. Charge/Retreat와 마찬가지로 플레이어가 고르는 "행동 버튼"이 아니라 항상 자동으로
    /// 적용되는 태그다 — 자기 자신을 Execute하는 동작이 없고(그래서 IUnitAction만 구현하고 ISelfAction/
    /// ITargetedAction 등 하위 인터페이스는 구현하지 않는다), CombatSystem.TryAttack이
    /// UnitActionQueries.Find&lt;AmbushAction&gt;로 보유 여부만 확인해 "공격이 성사돼도 대상의 반격
    /// (CounterAction)을 발동시키지 않는다"로 판단을 바꿔준다. 값(수치)은 없다 — 순수 마커.
    /// </summary>
    [System.Serializable]
    public class AmbushAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Ambush;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
