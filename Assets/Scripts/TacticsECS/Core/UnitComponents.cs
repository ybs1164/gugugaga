using UnityEngine;

namespace TacticsECS
{
    // 유닛으로 쓰이는 엔티티가 가질 수 있는 컴포넌트들.
    // 각각 값 하나만 담는 독립된 타입이며, EntityWorld는 이 타입들이 "유닛"과 관련 있다는 것조차 모른다 —
    // EntityWorld 입장에서는 그냥 "Set<T>/Get<T>로 오가는 임의의 컴포넌트 타입"일 뿐이다.
    // 여러 개를 한 파일에 모아뒀을 뿐 저장은 타입별로 완전히 분리되어 있다 (컴포넌트 타입 = 저장 단위).

    // ---- 위치 ----
    [System.Serializable] public struct GridPosition { public Vector2Int Value; }

    // ---- 체력 ----
    [System.Serializable] public struct Hp { public int Value; }
    [System.Serializable] public struct MaxHp { public int Value; }

    // ---- 전투 (스폰 후 불변) ----
    [System.Serializable] public struct Attack { public int Value; }
    [System.Serializable] public struct Defense { public int Value; }
    [System.Serializable] public struct AttackRange { public int Value; }

    // ---- 치유 (스폰 후 불변) ----
    [System.Serializable] public struct HealAmount { public int Value; }
    [System.Serializable] public struct HealRange { public int Value; }

    // ---- 사용 가능 행동 (스폰 후 불변) ----
    // 이동/공격/방어/치유/자폭 중 이 유닛이 실제로 쓸 수 있는 것이 무엇인지는 UnitActions.Value(아래) —
    // UnitDefinition.actions(행동별 개별 스크립트의 집합)가 스폰 시 그대로 복사된 목록 — 가 정한다.
    // 각 IUnitAction이 스스로 CanExecute/Execute로 판단하며, Systems는 UnitActionQueries.Find<T>로 이
    // 목록에서 필요한 행동을 찾아 위임한다(Assets/Scripts/TacticsECS/Actions 참고).
    [System.Serializable] public struct UnitActions { public System.Collections.Generic.IReadOnlyList<IUnitAction> Value; }

    // AvailableActions는 위 UnitActions를 ActionType 비트마스크로 합친 값이다. 지금은 실행 판정에
    // 쓰이지 않는 플레이스홀더 — CSV 내보내기/불러오기 같은 외부 데이터 연동과 View(BattleHud 아이콘
    // 매칭) 표시 용도로만 쓰인다(Core/ActionType.cs 참고).
    [System.Serializable] public struct AvailableActions { public ActionType Value; }

    // ---- 이동 방식 (스폰 후 불변) ----
    [System.Serializable] public struct MoveRange { public int Value; }
    [System.Serializable] public struct IgnoreTerrain { public bool Value; }
    [System.Serializable] public struct IgnoreUnitBlocking { public bool Value; }
    [System.Serializable] public struct AllowDiagonal { public bool Value; }

    // 이 유닛이 들어갈 수 있는 지형(TileData.Terrain, Core/TerrainType.cs와 공유). 육지 유닛은 물 타일에,
    // 물 유닛은 육지 타일에 (IgnoreTerrain이 없는 한) 들어갈 수 없다 — PathfindingSystem.GetReachable과
    // MoveAction.Execute가 함께 판정한다.
    [System.Serializable] public struct MoveDomain { public TerrainType Value; }

    // ---- 수송 플레이스홀더 (스폰 후 불변) ----
    // Transport(ActionType) 보유 유닛이 태울 수 있는 유닛 수. VisionRange(정찰 플레이스홀더)와 같은 성격 —
    // 아직 유닛을 태우고 내리는 시스템 자체가 없어 지금은 값만 들고 있을 뿐 실제 게임플레이 효과는 없다.
    // 나중에 수송 시스템이 생기면 이 값을 정원으로 쓰면 된다 — Actions/TransportAction.cs 참고.
    [System.Serializable] public struct CargoCapacity { public int Value; }

    // ---- 턴 상태 (매 턴 리셋) ----
    [System.Serializable] public struct HasMoved { public bool Value; }
    [System.Serializable] public struct HasActed { public bool Value; }
    [System.Serializable] public struct IsGuarding { public bool Value; }

    // ---- 지속 상태 (턴이 아니라 특정 조건으로 갱신/해제) ----
    // 가속: 무리(HerdAction) 보유 유닛 주변 1블록 내 아군에게 매 이동/턴 시작마다 다시 부여되고
    // (PassiveAuraSystem.RefreshHerdAura), 피격 시(AttackAction/CounterAction/SelfDestructAction의 데미지
    // 적용 지점에서) 해제된다. 이동 거리 +1 효과는 MovementSystem.EffectiveMoveRange로 계산한다.
    [System.Serializable] public struct Accelerated { public bool Value; }

    // 빙결: 빙결(FreezeAction) 보유 유닛의 공격을 맞으면 대상에게 세워진다(AttackAction.Execute). 그 유닛의
    // 다음 자기 팀 턴이 시작될 때 TurnSystem.ResetUnitStates가 그 턴의 HasMoved/HasActed를 강제로 true로
    // 만들어 소모시키고 즉시 해제한다 — "1턴간 행동불능".
    [System.Serializable] public struct Frozen { public bool Value; }

    // ---- 정찰 플레이스홀더 (스폰 후 불변) ----
    // 아직 시야/포그오브워 시스템이 없어 지금은 값만 들고 있을 뿐 실제 게임플레이 효과는 없다.
    // Scout(ActionType)을 가진 유닛의 실효 시야는 나중에 시야 시스템이 생기면 이 값 + 1로 계산하면 된다.
    [System.Serializable] public struct VisionRange { public int Value; }

    // Team(Core/Team.cs)은 이미 다른 목적으로 쓰이지 않는 고유한 타입이라 별도 래퍼 없이
    // 그 자체로 컴포넌트 타입("팀 소속")으로 재사용한다.
}
