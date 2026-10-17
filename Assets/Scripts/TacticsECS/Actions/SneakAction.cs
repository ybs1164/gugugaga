namespace TacticsECS
{
    /// <summary>
    /// 잠입 패시브(위키 Unit Skills "Sneak"): 적 유닛 영향권(ZoC)에 들어가도 멈추지 않는다. 적 유닛 칸을 지나가지는 못한다. PathfindingSystem이 보유 여부만 확인한다. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class SneakAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Sneak;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
