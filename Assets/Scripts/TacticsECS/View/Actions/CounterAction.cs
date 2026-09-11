namespace TacticsECS
{
    /// <summary>
    /// 반격 패시브. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.Counter를 갖는다.
    /// 다른 행동과 달리 플레이어가 직접 고르는 "행동 버튼"이 아니라, 공격을 받았을 때
    /// CombatSystem.TryAttack이 자동으로 확인해 발동시키는 패시브다(BattleHud에서도 버튼이 아니라
    /// 정보용 배지로만 표시). 별도 값은 없다 — 반격 피해량은 AttackAction의 Attack/AttackRange를 그대로 쓴다.
    /// </summary>
    [System.Serializable]
    public class CounterAction : IUnitAction
    {
        public ActionType Type => ActionType.Counter;
    }
}
