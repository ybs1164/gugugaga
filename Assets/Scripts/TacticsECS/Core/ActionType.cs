using System;

namespace TacticsECS
{
    /// <summary>
    /// 유닛이 쓸 수 있는 행동의 종류를 나타내는 태그. 비트 플래그라 한 유닛이 여러 행동을 동시에
    /// 가질 수 있다.
    /// 예전에는 "이 유닛이 이 행동을 쓸 수 있는가"를 판단하는 단일 기준점(AvailableActions 비트마스크 +
    /// HasFlag)이었지만, 지금은 그 판단을 각 IUnitAction(Assets/Scripts/TacticsECS/Actions)이 스스로
    /// 담당한다(CanExecute/Execute — Systems/UnitActionQueries.Find&lt;T&gt;로 찾아 위임). 이 enum은 더
    /// 이상 실행 가능 여부 판정에 관여하지 않는 순수 플레이스홀더로만 남아있다 — IUnitAction.GetActionType()이
    /// 돌려주는 식별 태그로서 CSV 등 외부 데이터 연동, View(BattleHud 행동 버튼·패시브 배지 아이콘 매칭)의
    /// 표시 용도로만 쓰인다.
    /// Counter(반격)/Charge(돌격)/Retreat(대피)는 예외적으로 "행동 버튼"이 아니라 항상 자동으로
    /// 적용되는 패시브다 — 플레이어가 직접 고르는 게 아니라, Counter는 공격을 받았을 때
    /// CombatSystem.TryAttack이, Charge/Retreat는 MoveAction/AttackAction 자신의 CanExecute가
    /// (UnitActionQueries.Find로 보유 여부만 확인해) 알아서 반영한다. BattleHud에서도 클릭 버튼이 아니라
    /// 정보용 배지(동그라미 배경)로만 표시된다.
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
        Counter = 1 << 5,
        /// <summary>돌격: 이번 턴 이미 이동했어도 공격할 수 있게 해주는 패시브(AttackAction.CanExecute가
        /// 참조). 값을 갖지 않는 순수 마커 — Actions/ChargeAction.cs 참고.</summary>
        Charge = 1 << 6,
        /// <summary>대피: 이번 턴 이미 공격했어도 이동할 수 있게 해주는 패시브(MoveAction.CanExecute가
        /// 참조). 값을 갖지 않는 순수 마커 — Actions/RetreatAction.cs 참고. Charge와 함께 있어도 공격은
        /// 여전히 턴당 1회로 제한되므로("대피로 이동 후 다시 공격"은 발동하지 않음) 별도 처리가 필요 없다.
        /// </summary>
        Retreat = 1 << 7,
        /// <summary>기습: 공격 시 대상의 반격(CounterAction)을 발동시키지 않는 패시브(CombatSystem.TryAttack이
        /// 참조). 값을 갖지 않는 순수 마커 — Actions/AmbushAction.cs 참고.</summary>
        Ambush = 1 << 8,
        /// <summary>잠입: 적 유닛에 의한 이동 방해만 무시하는 패시브(PathfindingSystem.GetReachable/
        /// MoveAction.CanEnter가 참조). 아군에 의한 차단은 그대로 적용된다는 점에서 모든 유닛을 무시하는
        /// MoveAction.IgnoreUnitBlocking과 다르다. 값을 갖지 않는 순수 마커 — Actions/InfiltrateAction.cs 참고.</summary>
        Infiltrate = 1 << 9,
        /// <summary>무리: 주변 1블록 내 아군에게 가속(Accelerated) 상태를 부여하는 패시브
        /// (PassiveAuraSystem.RefreshHerdAura가 참조). 값을 갖지 않는 순수 마커 — Actions/HerdAction.cs 참고.</summary>
        Herd = 1 << 10,
        /// <summary>전향: 공격이 성사되고 대상이 살아남으면 그 대상을 아군으로 전환하는 패시브
        /// (CombatSystem.TryAttack이 참조). 값을 갖지 않는 순수 마커 — Actions/ConvertAction.cs 참고.</summary>
        Convert = 1 << 11,
        /// <summary>연타: 공격으로 대상을 처치하면 같은 턴에 추가로 공격할 수 있게 해주는 패시브
        /// (AttackAction.Execute가 참조). 값을 갖지 않는 순수 마커 — Actions/ComboAction.cs 참고.</summary>
        Combo = 1 << 12,
        /// <summary>정찰: 시야 +1. 아직 시야/포그오브워 시스템 자체가 없어 지금은 실제 게임플레이 효과가
        /// 없는 플레이스홀더 마커다(VisionRange 컴포넌트만 준비) — Actions/ScoutAction.cs 참고.</summary>
        Scout = 1 << 13,
    }
}
