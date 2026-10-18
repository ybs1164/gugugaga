namespace TacticsECS
{
    /// <summary>
    /// 대피 패시브. ChargeAction과 대칭인 순수 마커 — 자기 자신을 Execute하는 동작이 없고,
    /// AttackAction.Execute가 UnitActionQueries.Find&lt;RetreatAction&gt;로 보유 여부를 확인해 공격 직후
    /// HasMoved를 false로 되돌린다. 그래서 이미 이동했더라도 공격 뒤 한 번 더 이동할 수 있다(이동 → 공격 → 이동).
    /// 값(수치)은 없다. 돌격과 함께 있어도 공격은 여전히 AttackAction의 HasActed 체크로 턴당 1회로 제한되므로,
    /// "대피로 이동한 뒤 다시 공격"이 발동하는 일은 없다 — 대피 이동이 HasMoved를 다시 세우면 그 턴은 끝난다.
    /// </summary>
    [System.Serializable]
    public class RetreatAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Retreat;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
