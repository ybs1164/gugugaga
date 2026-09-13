namespace TacticsECS
{
    /// <summary>
    /// 은신 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없다. ScoutAction과 마찬가지로 아직
    /// 구체적인 게임플레이 효과(예: 적에게 발견되지 않음)가 정해지지 않은 플레이스홀더다. 나중에 효과가
    /// 정해지면 UnitActionQueries.Find&lt;StealthAction&gt;로 보유 여부를 확인해 쓰면 된다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class StealthAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Stealth;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
