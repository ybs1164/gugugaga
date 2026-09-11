using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 치유 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.Heal을 쓸 수 있고,
    /// 회복량/사거리를 이 항목이 직접 들고 있다.
    /// </summary>
    [System.Serializable]
    public class HealAction : IUnitAction
    {
        [Tooltip("치유 행동을 가진 유닛이 회복시키는 체력량.")]
        [SerializeField] private int healAmount;
        [Tooltip("치유가 닿는 사거리(맨해튼 거리). 사거리 내의 모든 아군(자신 제외)이 대상이 된다.")]
        [SerializeField] private int healRange;

        public ActionType Type => ActionType.Heal;

        public int HealAmount => healAmount;
        public int HealRange => healRange;
    }
}
