namespace TacticsECS
{
    /// <summary>
    /// 고정 패시브(위키 Unit Skills "Static" — "Prevents a unit from becoming a veteran"). 순수 마커 — VeteranSystem.CanPromote가
    /// 보유 여부만 확인한다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class StaticAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Static;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
