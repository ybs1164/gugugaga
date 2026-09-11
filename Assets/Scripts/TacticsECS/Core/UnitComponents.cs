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

    // ---- 턴 상태 (매 턴 리셋) ----
    [System.Serializable] public struct HasMoved { public bool Value; }
    [System.Serializable] public struct HasActed { public bool Value; }
    [System.Serializable] public struct IsGuarding { public bool Value; }

    // Team(Core/Team.cs)은 이미 다른 목적으로 쓰이지 않는 고유한 타입이라 별도 래퍼 없이
    // 그 자체로 컴포넌트 타입("팀 소속")으로 재사용한다.
}
