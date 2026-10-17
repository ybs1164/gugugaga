namespace TacticsECS
{
    /// <summary>
    /// 스케이트 패시브(위키 Unit Skills "Skate"): 얼음 칸에서 이동을 시작하면 이동력 +1 — MovementSystem.EffectiveMoveRange. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class SkateAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Skate;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
