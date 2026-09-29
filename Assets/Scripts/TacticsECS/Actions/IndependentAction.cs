namespace TacticsECS
{
    /// <summary>
    /// 독립 패시브(위키 Unit Skills "Independent" — "do not take up a population slot in or belong to any city"). 순수 마커 —
    /// 이 행동을 가진 유닛은 소속 도시(HomeCity)를 받지 않아 도시 유닛 수용량을 차지하지 않는다(CitySystem.AssignHome).
    /// 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class IndependentAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Independent;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
