namespace TacticsECS
{
    /// <summary>
    /// 대피 패시브. ChargeAction과 대칭인 순수 마커 — 자기 자신을 Execute하는 동작이 없고,
    /// MoveAction.CanExecute가 UnitActionQueries.Find&lt;RetreatAction&gt;로 보유 여부만 확인해
    /// "이번 턴 이미 공격했어도 이동 가능"으로 판단을 바꿔준다. 값(수치)은 없다.
    /// 돌격과 함께 있어도 공격은 여전히 AttackAction의 HasActed 체크로 턴당 1회로 제한되므로,
    /// "대피로 이동한 뒤 다시 공격"이 발동하는 일은 없다 — 별도 상태 없이 기존 HasMoved/HasActed
    /// 조합만으로 이 제약이 자연히 성립한다.
    /// </summary>
    [System.Serializable]
    public class RetreatAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Retreat;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
