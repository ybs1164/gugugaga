using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.Attack을 쓸 수 있고,
    /// 공격력/사거리를 이 항목이 직접 들고 있다.
    /// CounterAction(반격)은 별도 값을 갖지 않고 이 값(공격자 자신의 Attack/AttackRange)을 그대로
    /// 재사용한다 — CombatSystem.TryCounter가 EntityWorld의 동일한 Attack/AttackRange 컴포넌트를 읽는다.
    /// </summary>
    [System.Serializable]
    public class AttackAction : IUnitAction
    {
        [SerializeField] private int attack;
        [SerializeField] private int attackRange;

        public ActionType Type => ActionType.Attack;

        public int Attack => attack;
        public int AttackRange => attackRange;
    }
}
