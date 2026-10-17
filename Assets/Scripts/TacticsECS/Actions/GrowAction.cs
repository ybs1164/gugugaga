namespace TacticsECS
{
    /// <summary>
    /// 성장 패시브(위키 Unit Skills "Grow"): 유닛 CSV Grow.Turns 턴 뒤 Grow.Into 유닛으로 바뀐다 — Systems/GrowSystem.cs. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class GrowAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Grow;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
