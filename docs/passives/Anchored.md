# 고정 (Anchored)

**상태**: 플레이스홀더 — CSV/코드에 태그만 있고 전투 효과는 아직 없다.

| | |
|---|---|
| 사용 예시 유닛 | 사제 / 뗏목·정찰선·충각선·범선 등 함선류 (`SandboxUnits.csv`) |
| 예상 효과 | 미정 |
| CSV 표기 | `Actions` 컬럼에 `Anchored` 추가 (다른 값 없음) |
| 코드 | [`AnchoredAction`](../../Assets/Scripts/TacticsECS/Actions/AnchoredAction.cs) |

## 지금 되는 것

- CSV에 `Anchored`를 적으면 파싱/내보내기/유닛 스폰까지 정상 동작한다.
- 순수 마커라 자체 동작(`Execute`)이 없다.
- 다른 플레이스홀더와 달리 "예상 효과"가 아직 논의되지 않았다 — 이름("고정")만 정해진 상태. 사제에게도
  쓰인 걸 보면 함선 전용 개념(정박)은 아닐 수 있음 — 기획 확정 필요.
- `BattleHud`의 패시브 배지에는 아직 연결하지 않았다.

## 효과를 구현하려면

1. 먼저 "고정"이 구체적으로 무엇을 뜻하는지 기획을 확정해야 한다(예: 이동 불가 대신 방어/사거리 보너스,
   또는 다른 유닛의 이동을 막는 효과 등).
2. 효과가 정해지면 해당 로직이 필요한 시스템에서 `UnitActionQueries.Find<AnchoredAction>(world, unitId)`로
   보유 여부를 확인해 적용한다.
3. 배지로 보여주려면 `BattleHud`의 `PassiveDefs`에 아이콘/설명을 등록한다.
