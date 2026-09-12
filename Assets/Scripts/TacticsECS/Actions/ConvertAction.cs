namespace TacticsECS
{
    /// <summary>
    /// 전향 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, CombatSystem.TryAttack이 공격을
    /// 성사시키고 대상이 살아남았을 때 UnitActionQueries.Find&lt;ConvertAction&gt;로 공격자의 보유 여부를
    /// 확인해 대상의 Team을 공격자의 Team으로 바꿔준다(반격 판정보다 먼저 처리되어, 전향된 대상은
    /// 더 이상 반격하지 않는다). 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class ConvertAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Convert;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
