namespace TacticsECS
{
    /// <summary>
    /// 비행 패시브(위키 Unit Skills "Air"): 지형과 관계없이 이동하고 도로 보너스를 받지 않는다. 지형·도시 방어 보너스도 받지 않고 배가 되지 않는다. PathfindingSystem/TechEffectSystem/EmbarkSystem이 보유 여부만 확인한다. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class FlyAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Fly;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
