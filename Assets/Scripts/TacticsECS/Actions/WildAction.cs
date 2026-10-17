namespace TacticsECS
{
    /// <summary>
    /// 독립 패시브: 어떤 플레이어·AI의 지휘도 받지 않는다 — BattleController(선택 불가)/EnemyAI·EconomyAI(건너뜀). 턴 끝에는 다른 유닛처럼 자동 대기한다. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class WildAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Wild;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
