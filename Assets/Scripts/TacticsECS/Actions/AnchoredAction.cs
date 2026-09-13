namespace TacticsECS
{
    /// <summary>
    /// 고정 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없다. ScoutAction과 마찬가지로 아직
    /// 구체적인 게임플레이 효과가 정해지지 않은 플레이스홀더다(SandboxUnits.csv 예시의 사제/함선류에 쓰임).
    /// 나중에 효과가 정해지면 UnitActionQueries.Find&lt;AnchoredAction&gt;로 보유 여부를 확인해 쓰면 된다.
    /// 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class AnchoredAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Anchored;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
