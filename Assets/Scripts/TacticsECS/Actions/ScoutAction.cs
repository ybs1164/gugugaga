namespace TacticsECS
{
    /// <summary>
    /// 정찰 패시브(위키 Unit Skills "Scout"). 순수 마커 — 자기 자신을 Execute하는 동작이 없다. 보유한 유닛은 스폰/승선 시
    /// VisionRange가 VisionDefinition.ExtendedSightRadius(5x5)로 정해지고, VisionSystem이 그 반경만큼 구름을 걷는다.
    /// 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class ScoutAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Scout;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
