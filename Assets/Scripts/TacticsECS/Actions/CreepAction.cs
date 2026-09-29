namespace TacticsECS
{
    /// <summary>
    /// 잠행 패시브(위키 Unit Skills "Creep" — "ignore movement barriers imposed by terrain except mountains", Cloak 문서: "ignores any
    /// movement penalties as well as road bonuses"). 순수 마커 — PathfindingSystem이 숲 정지와 도로 보너스를 이 유닛에게 적용하지 않는다.
    /// 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class CreepAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Creep;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
