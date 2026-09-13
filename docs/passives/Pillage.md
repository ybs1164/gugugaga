# 약탈 (Pillage)

**상태**: 플레이스홀더 — CSV/코드에 태그만 있고 전투 효과는 아직 없다.

| | |
|---|---|
| 사용 예시 유닛 | 스파이 (`SandboxUnits.csv`) |
| 예상 효과 | 자원 획득(자원 시스템 필요) |
| CSV 표기 | `Actions` 컬럼에 `Pillage` 추가 (다른 값 없음) |
| 코드 | [`PillageAction`](../../Assets/Scripts/TacticsECS/Actions/PillageAction.cs) |

## 지금 되는 것

- CSV에 `Pillage`를 적으면 파싱/내보내기/유닛 스폰까지 정상 동작한다.
- 순수 마커라 자체 동작(`Execute`)이 없다.
- 이 효과를 구현하려면 애초에 자원(골드/재화 등) 시스템 자체가 프로젝트에 없어야 하는데, 지금은 없다 —
  다른 플레이스홀더보다 선행 작업(자원 시스템)이 하나 더 필요하다.
- `BattleHud`의 패시브 배지에는 아직 연결하지 않았다.

## 효과를 구현하려면

1. 먼저 자원 시스템이 있어야 한다(없으면 "획득"할 대상이 없음).
2. 자원 시스템이 생기면, 원하는 트리거(예: 적 처치 시, 특정 타일 점령 시)에서
   `UnitActionQueries.Find<PillageAction>(world, unitId)`로 보유 여부를 확인해 자원을 지급하면 된다.
3. 배지로 보여주려면 `BattleHud`의 `PassiveDefs`에 아이콘/설명을 등록한다.
