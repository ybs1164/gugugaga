namespace TacticsECS
{
    /// <summary>
    /// 침투 행동(위키 Unit Skills "Infiltrate" — "incite a revolt and spawn units by entering an enemy city", Cloak 문서). 순수 마커 —
    /// 실제 판정/효과는 InfiltrationSystem.CanInfiltrate/Infiltrate(인접 적 도시 대상, 자신 소모, Dagger 소환, 수입 탈취). 이 행동을 가진
    /// 유닛은 유닛을 공격할 수 없다(AttackAction.Execute가 확인 — 위키: "it can only target adjacent enemy cities with its attack").
    /// 값(수치)은 없다. 2026-09-29 전에는 "적 유닛 통과" 패시브였다 — 그 효과는 HideAction으로 옮겼다.
    /// </summary>
    [System.Serializable]
    public class InfiltrateAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Infiltrate;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
