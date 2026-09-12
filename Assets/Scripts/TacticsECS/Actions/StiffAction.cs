namespace TacticsECS
{
    /// <summary>
    /// 뻣뻣함 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, CombatSystem.TryAttack이 반격
    /// (CounterAction)을 발동시키기 전에 UnitActionQueries.Find&lt;StiffAction&gt;로 대상(피격자)의 보유
    /// 여부를 확인해, 대상이 CounterAction을 갖고 있어도 발동시키지 않는다. 공격자 쪽의 기습(AmbushAction)과
    /// 대칭이되 반대쪽(피격자)에서 판단한다는 점이 다르다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class StiffAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Stiff;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
