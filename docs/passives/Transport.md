# 수송 (Transport)

**상태**: 플레이스홀더 — 정원 값은 CSV로 받지만, 태우고 내리는 시스템 자체가 아직 없어 전투 효과는 없다.

| | |
|---|---|
| 사용 예시 유닛 | 뗏목 / 정찰선 / 충각선 / 범선 등 함선류 (`SandboxUnits.csv`) |
| 예상 효과 | 육지 유닛을 태우고 물을 건너게 해줌 |
| CSV 표기 | `Actions` 컬럼에 `Transport` 추가 + `Transport.Capacity` 컬럼에 정원(정수) 입력 |
| 코드 | [`TransportAction`](../../Assets/Scripts/TacticsECS/Actions/TransportAction.cs), [`CargoCapacity`](../../Assets/Scripts/TacticsECS/Core/UnitComponents.cs) |

## 지금 되는 것

- CSV의 `Transport.Capacity` 값(예: 뗏목=1, 정찰선/충각선=2, 범선=4)이 파싱 → `TransportAction.Capacity` →
  스폰 시 `CargoCapacity` 컴포넌트까지 그대로 보존된다. 다른 플레이스홀더와 달리 **값 자체는 이미 살아있다.**
- 다만 실제로 유닛을 "태우고/내리는" 동작, 태운 유닛이 화면에서 사라지거나 함께 이동하는 처리는 없다.
- [지형(육지/물)](../UnitCsvSandbox.md#지형-육지물) 이동 제한(`MoveDomain`)은 이 패시브와 **무관하게** 이미
  별도로 동작한다 — 물 유닛은 지금도 물만, 육지 유닛은 지금도 육지만 다닐 수 있다. 수송 기능이 하는 일은
  "육지 유닛이 물 유닛에 올라타 있는 동안만 예외적으로 물을 건너게 해주는 것"이라 이 제한 위에 얹히는
  추가 규칙이다.
- `BattleHud`의 패시브 배지에는 아직 연결하지 않았다.

## 효과를 구현하려면

1. "탑승/하차" 상태를 나타낼 데이터가 필요하다(예: 이 유닛이 어느 함선에 타 있는지, 함선이 몇 명을 태우고
   있는지 — `CargoCapacity`는 정원 상한만 있고 현재 탑승 인원/명단은 아직 없음).
2. 이동 판정(`PathfindingSystem`/`MoveAction`)에서 "탑승 중인 육지 유닛은 함선의 `MoveDomain`을 따른다"는
   예외를 추가한다.
3. 배지로 보여주려면 `BattleHud`의 `PassiveDefs`에 아이콘/설명을 등록한다.
