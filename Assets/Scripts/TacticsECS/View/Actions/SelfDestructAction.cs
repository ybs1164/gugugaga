namespace TacticsECS
{
    /// <summary>
    /// 자폭 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.SelfDestruct를 쓸 수 있다.
    /// 별도 값은 없다 — 피해량은 발동 시점의 남은 체력(Hp)을 그대로 쓴다(AbilitySystem.TrySelfDestruct).
    /// </summary>
    [System.Serializable]
    public class SelfDestructAction : IUnitAction
    {
        public ActionType Type => ActionType.SelfDestruct;
    }
}
