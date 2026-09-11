namespace TacticsECS
{
    /// <summary>
    /// 방어 태세 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.Defend를 쓸 수 있다.
    /// 별도 값은 없다 — 방어 태세 보너스(CombatSystem.GuardDefenseBonus)와 기본 방어력
    /// (UnitDefinition.Defense)은 이 행동의 보유 여부와 무관하게 항상 적용되는 값이라 여기 두지 않는다.
    /// </summary>
    [System.Serializable]
    public class DefendAction : IUnitAction
    {
        public ActionType Type => ActionType.Defend;
    }
}
