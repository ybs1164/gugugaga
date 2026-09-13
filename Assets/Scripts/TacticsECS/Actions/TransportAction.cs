namespace TacticsECS
{
    /// <summary>
    /// 수송 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없다. ScoutAction과 마찬가지로 아직
    /// 구체적인 게임플레이 효과(예: 육지 유닛을 태우고 물을 건너는 것)가 정해지지 않은 플레이스홀더다
    /// (SandboxUnits.csv 예시의 함선류에 쓰임). 육지/물 이동 제한(MoveDomain, Core/UnitComponents.cs)은
    /// 이 패시브와 무관하게 이미 별도로 동작한다. 나중에 효과가 정해지면 UnitActionQueries.Find&lt;TransportAction&gt;
    /// 로 보유 여부를 확인해 쓰면 된다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class TransportAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Transport;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
