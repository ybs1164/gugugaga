# 요새화 (Fortify)

**상태**: 플레이스홀더 — CSV/코드에 태그만 있고 전투 효과는 아직 없다.

| | |
|---|---|
| 사용 예시 유닛 | 보병 / 방패병 / 궁병 (`SandboxUnits.csv`) |
| 예상 효과 | 제자리(이동 안 한 턴)에 있으면 방어 보너스 |
| CSV 표기 | `Actions` 컬럼에 `Fortify` 추가 (다른 값 없음) |
| 코드 | [`FortifyAction`](../../Assets/Scripts/TacticsECS/Actions/FortifyAction.cs) |

## 지금 되는 것

- CSV에 `Fortify`를 적으면 파싱/내보내기/유닛 스폰까지 정상 동작한다.
- 순수 마커라 자체 동작(`Execute`)이 없다 — 그냥 "이 유닛은 요새화를 갖고 있다"는 표식만 붙는다.
- `BattleHud`의 패시브 배지에는 아직 연결하지 않았다(효과 없는 배지만 노출하면 혼란을 줄 수 있어 보류).

## 효과를 구현하려면

1. `FortifyAction`을 보유했는지 `UnitActionQueries.Find<FortifyAction>(world, unitId)`로 확인하는 지점을
   원하는 시스템(예: 방어력 계산부인 `CombatSystem.CalculateDamage`)에 추가한다.
2. 배지로 보여주려면 `BattleHud`의 `PassiveDefs`에 아이콘/설명을 등록한다.
