namespace TacticsECS
{
    /// <summary>
    /// 헤엄 패시브: 배 없이 얕은 물 칸을 다니고, 항구에 들어가도 배가 되지 않는다 — PathfindingSystem/EmbarkSystem. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class SwimAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Swim;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
