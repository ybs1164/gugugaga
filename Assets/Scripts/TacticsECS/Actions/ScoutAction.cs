namespace TacticsECS
{
    /// <summary>
    /// 정찰 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없다. 시야 +1을 나타내지만, 아직 시야/
    /// 포그오브워 시스템 자체가 없어 지금은 실제 게임플레이 효과가 없는 플레이스홀더다. 나중에 시야
    /// 시스템이 생기면 UnitActionQueries.Find&lt;ScoutAction&gt;로 보유 여부를 확인해 VisionRange
    /// (Core/UnitComponents.cs)에 +1을 더해 쓰면 된다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class ScoutAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Scout;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
