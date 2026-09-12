namespace TacticsECS
{
    /// <summary>
    /// 연타 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, AttackAction.Execute가 공격으로
    /// 대상을 처치했을 때 UnitActionQueries.Find&lt;ComboAction&gt;로 보유 여부만 확인해 방금 세운
    /// HasActed를 다시 되돌려 "같은 턴에 추가로 공격 가능"으로 판단을 바꿔준다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class ComboAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Combo;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
