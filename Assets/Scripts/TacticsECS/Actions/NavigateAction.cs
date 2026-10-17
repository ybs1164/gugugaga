namespace TacticsECS
{
    /// <summary>
    /// 항해 패시브(위키 Unit Skills "Navigate"): 깊은 바다 이동 해금(Move.Ocean) 없이도 깊은 바다에 들어간다 — TechEffectSystem. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class NavigateAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Navigate;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
