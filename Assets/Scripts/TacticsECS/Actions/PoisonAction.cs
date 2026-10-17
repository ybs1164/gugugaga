namespace TacticsECS
{
    /// <summary>
    /// 독 공격 패시브(위키 Unit Skills "Poison"): 공격(스플래시 포함)을 맞고 살아남은 적을 중독(Poisoned)시킨다 — CombatSystem. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class PoisonAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Poison;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
