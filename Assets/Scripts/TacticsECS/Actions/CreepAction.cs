namespace TacticsECS
{
    /// <summary>
    /// 지형무시 패시브(위키 Unit Skills "Creep"): 숲·산에서 멈추지 않고 도로 보너스도 받지 않는다(기획서 — 위키는 산 제외). 물↔육지는 못 오간다.
    /// 순수 마커 — PathfindingSystem이 보유 여부만 확인한다. 규칙: docs/spec/game/movement.md#멈추는-칸-들어가면-그-턴-이동-끝
    /// </summary>
    [System.Serializable]
    public class CreepAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Creep;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
