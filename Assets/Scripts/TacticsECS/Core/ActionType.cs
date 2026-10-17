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
    public enum ActionType : long
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
        /// <summary>침투(위키 Infiltrate): 공격 대신 인접한 적 도시에 들어가 반란을 일으킨다(자신은 소모, 도시 레벨만큼 Dagger 소환,
        /// 그 도시 수입을 빼앗음). 이 행동을 가진 유닛은 유닛을 공격할 수 없다. 값을 갖지 않는 순수 마커 — Systems/InfiltrationSystem.cs 참고.
        /// (2026-09-29 전에는 "적 유닛 통과" 패시브였다 — 그 효과는 위키대로 Hide로 옮겼다.)</summary>
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
        /// <summary>정찰: 시야 반경 2(5x5). VisionSystem이 참조하는 순수 마커 — Actions/ScoutAction.cs 참고.</summary>
        Scout = 1 << 13,
        /// <summary>스플래시: 공격이 성사되면 대상 주변 1블록 내 적 유닛(공격자 기준)에게도 광역 피해를
        /// 입히는 패시브(AttackAction.Execute가 참조). 값을 갖지 않는 순수 마커 — Actions/SplashAction.cs 참고.</summary>
        Splash = 1 << 14,
        /// <summary>뻣뻣함: 이 유닛이 공격받았을 때, 반격(CounterAction)을 갖고 있어도 발동시키지 않는
        /// 패시브(CombatSystem.TryAttack이 대상 쪽에서 참조). 값을 갖지 않는 순수 마커 — Actions/StiffAction.cs 참고.</summary>
        Stiff = 1 << 15,
        /// <summary>빙결: 공격이 성사되고 대상이 살아남으면, 대상을 다음 자기 턴 하나를 통째로 행동불능으로
        /// 만드는 패시브(AttackAction.Execute가 Frozen 컴포넌트를 세팅, TurnSystem.ResetUnitStates가 그 턴에
        /// HasMoved/HasActed를 강제로 true로 만들고 소모). 값을 갖지 않는 순수 마커 — Actions/FreezeAction.cs 참고.</summary>
        Freeze = 1 << 16,
        /// <summary>요새화(위키 Fortify): 자기 도시 칸에서 방어 보너스(x1.5, 성벽 x4 — TechEffectSystem.RefreshUnits).
        /// 값을 갖지 않는 순수 마커 — Actions/FortifyAction.cs 참고.</summary>
        Fortify = 1 << 17,
        /// <summary>은신: 한 바퀴 동안 피해를 받지 않으면 적에게 보이지 않는다 — Actions/HideAction.cs, Systems/StealthSystem.cs. (옛 이름 Stealth — CSV에서도 읽힌다.)</summary>
        Hide = 1 << 18,
        // 1 << 19 ~ 1 << 21: 예전 약탈(Pillage)/고정(Anchored)/수송(Transport) 플레이스홀더 자리. 효과가 정해지지 않은 채
        // 쓰이지 않아 2026-09-29에 뺐다 — 약탈·고정은 위키에 없는 스킬이고, 수송(위키 Carry)은 항구 승선(EmbarkSystem)이 맡는다.
        /// <summary>대기: 이번 턴 행동을 종료하고 체력을 회복하는 액티브 행동(회복 2, 자기 영토 내 4 —
        /// 영토는 현재 플레이스홀더). 턴 종료 시 미행동 유닛은 자동으로 대기 처리된다.
        /// Actions/WaitAction.cs 참고.</summary>
        Wait = 1 << 22,
        /// <summary>장애물 통과: Walkable=false인 지형(벽/장애물)을 무시하고 이동한다(비행 유닛 등). 예전엔
        /// MoveAction 자신의 값이었지만, 대부분의 유닛에는 해당 없는 드문 케이스라 Charge/Retreat 같은
        /// 값 없는 순수 마커 패시브로 옮겼다 — `MoveAction.Execute`/`PathfindingSystem.GetReachable`이
        /// `UnitActionQueries.Find`로 보유 여부만 확인한다. Actions/IgnoreTerrainAction.cs 참고.</summary>
        IgnoreTerrain = 1 << 23,
        /// <summary>장애물(유닛) 통과: 다른 유닛(아군/적군 모두)이 있는 타일도 지나가거나 멈출 수 있다
        /// (유령/투명체 등). 적 유닛에 의한 차단만 무시하고 싶다면 이 대신 잠입(Infiltrate)을 쓴다. 값을
        /// 갖지 않는 순수 마커 — Actions/IgnoreUnitBlockingAction.cs 참고.</summary>
        IgnoreUnitBlocking = 1 << 24,
        /// <summary>대각선 이동: 8방향 이동을 허용한다(없으면 상하좌우 4방향만). 값을 갖지 않는 순수 마커 —
        /// Actions/AllowDiagonalAction.cs 참고.</summary>
        AllowDiagonal = 1 << 25,
        /// <summary>독립(위키 Unit Skills "Independent"): 어느 도시에도 속하지 않아 도시 유닛 수용량을 차지하지 않는다
        /// (Dagger 등). 값을 갖지 않는 순수 마커 — Actions/IndependentAction.cs 참고.</summary>
        Independent = 1 << 26,
        /// <summary>고정(위키 Unit Skills "Static"): 베테랑으로 승급할 수 없다(Cloak/Dagger 등). 값을 갖지 않는 순수 마커 —
        /// Actions/StaticAction.cs 참고.</summary>
        Static = 1 << 27,
        /// <summary>지형무시(위키 Unit Skills "Creep"): 숲·산에서 멈추지 않고 도로 보너스도 받지 않는다 — Actions/CreepAction.cs.</summary>
        Creep = 1 << 28,
        Stomp = 1 << 29,
        /// <summary>기도: 신전 근처 유닛이 고르는 액티브 행동(PrayerSystem). 유닛 CSV의 행동이 아니다 — 선택 시 BattleController가 조건을 보고
        /// 버튼 표시용으로만 붙인다. 규칙: docs/spec/game/faith.md</summary>
        Pray = 1 << 30,
        // ---- 1 << 31부터는 long 비트(규칙: docs/spec/game/unit.md#행동과-패시브) ----
        /// <summary>잠입: 적 유닛 영향권(ZoC)에 멈추지 않는다. 적 유닛 칸은 지나가지 못한다(위키 Sneak). 순수 마커.</summary>
        Sneak = 1L << 31,
        /// <summary>비행: 지형과 관계없이 이동, 도로 보너스 없음(위키 Air). 순수 마커.</summary>
        Fly = 1L << 32,
        /// <summary>약탈: 적 도시를 직접 공격해 그 도시의 별 수입만큼 빼앗는다 — Systems/PlunderSystem.cs.</summary>
        Plunder = 1L << 33,
        /// <summary>독 공격: 살아남은 피격 대상(스플래시 포함)을 중독시킨다(Poisoned) — CombatSystem.</summary>
        Poison = 1L << 34,
        /// <summary>헤엄: 배 없이 얕은 물을 다니고, 항구에서 배가 되지 않는다. 순수 마커.</summary>
        Swim = 1L << 35,
        /// <summary>항해: 깊은 바다 이동 기술 없이도 깊은 바다에 들어간다(위키 Navigate). 순수 마커.</summary>
        Navigate = 1L << 36,
        /// <summary>성장: Grow.Turns 턴 뒤 Grow.Into 유닛으로 바뀐다(위키 Grow) — Systems/GrowSystem.cs.</summary>
        Grow = 1L << 37,
        /// <summary>파괴: 지나간 칸의 건물·유적을 없앤다 — MovementSystem.</summary>
        Raze = 1L << 38,
        /// <summary>독립: 누구의 지휘도 받지 않는다(플레이어가 고를 수 없고 AI도 움직이지 않는다). 순수 마커.</summary>
        Wild = 1L << 39,
        /// <summary>스케이트: 얼음 칸에서 이동을 시작하면 이동력 +1 — MovementSystem.EffectiveMoveRange.</summary>
        Skate = 1L << 40,
        /// <summary>침수: 서 있는 평지·숲 칸을 침수 지형으로 바꾸는 행동을 쓴다 — Systems/TerrainChangeSystem.cs.</summary>
        Flood = 1L << 41,
        /// <summary>폭우: 빈 칸도 공격할 수 있고, 공격한 칸을 침수시킨다(위키 Drench) — Systems/TerrainChangeSystem.cs.</summary>
        Drench = 1L << 42,
        /// <summary>자동 빙결: 이동할 때마다 주변 8칸의 물을 얼리고 적 유닛을 빙결시킨다(위키 Auto Freeze) — Systems/TerrainChangeSystem.cs.</summary>
        AutoFreeze = 1L << 43,
    }
}
