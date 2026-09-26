# 요새화 (Fortify)

**상태**: 구현됨(2026-09-26) — 폴리토피아 Fortify. 자기 팀 도시 칸에 서 있으면 방어력 +1, 그 도시에 성벽(도시 레벨 3 보상)이
있으면 +3. `TechEffectSystem.RefreshUnits`가 `UnitActionQueries.Find<FortifyAction>`로 보유 여부를 보고
`PositionalDefenseBonus` 컴포넌트에 넣고, `CombatSystem.EffectiveDefense`가 더한다. 경제(도시)가 없는 씬에서는 효과가 없다.
아래 "지금 되는 것/효과를 구현하려면"은 구현 전 기록이다.

| | |
|---|---|
| 사용 예시 유닛 | 보병 / 방패병 / 궁병 (`SandboxUnits.csv`) |
| 효과 | 자기 도시 칸에서 방어 +1 (성벽 +3) |
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
