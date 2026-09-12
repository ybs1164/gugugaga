namespace TacticsECS
{
    /// <summary>
    /// 스플래시 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, AttackAction.Execute가 공격을
    /// 성사시켰을 때 UnitActionQueries.Find&lt;SplashAction&gt;로 공격자의 보유 여부만 확인해 대상 주변
    /// 1블록(맨해튼 거리) 내의 다른 적 유닛(공격자 기준, 대상 자신은 제외)에게도 CombatSystem.CalculateDamage로
    /// 계산한 피해를 추가로 입힌다. 값(수치)은 없다 — 광역 피해량도 공격자의 Attack을 그대로 쓴다.
    /// </summary>
    [System.Serializable]
    public class SplashAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Splash;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
