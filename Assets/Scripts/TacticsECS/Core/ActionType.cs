using System;

namespace TacticsECS
{
    /// <summary>
    /// 유닛이 쓸 수 있는 행동의 종류. 비트 플래그라 한 유닛이 여러 행동을 동시에 가질 수 있다.
    /// UnitDefinition(프리팹)에서 유닛 타입별로 직접 조합해 지정하고, 그 값이 AvailableActions
    /// 컴포넌트로 EntityWorld에 그대로 옮겨진다 — "이 유닛은 어떤 행동을 쓸 수 있는가"를 판단하는
    /// 단일 기준점이며, Systems(이동/공격/방어/치유/자폭)와 View(BattleHud 행동 버튼 목록) 양쪽이
    /// 모두 이 값 하나만 보고 판단한다.
    /// </summary>
    [Flags]
    public enum ActionType
    {
        None = 0,
        Move = 1 << 0,
        Attack = 1 << 1,
        Defend = 1 << 2,
        Heal = 1 << 3,
        SelfDestruct = 1 << 4,
    }
}
