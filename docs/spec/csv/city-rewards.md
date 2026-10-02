# 도시 보상 CSV

파일: `Assets/Resources/CityRewards.csv`
한 행 = 레벨업 보상 선택지 하나. 같은 `Level`의 행들이 그 레벨의 선택지이고, 가장 높은 `Level`의 행들은 그 이상 레벨에서 반복된다.
로더: `GameTableCsvSerializer.ParseCityRewards` → `CityRewardDefinition.All`. 규칙: [city](../game/city.md#레벨업-보상).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Level` | 정수 | O | 2 | 이 레벨에 도달하면 고를 수 있음 |
| `Reward` | 아래 표 | O | `Resources` | 보상 종류(효과는 코드 `CitySystem.ApplyReward`) |
| `Amount` | 정수 | | 0 | 보상 크기 — 뜻은 종류마다 아래 표 |
| `Wiki`, `Note` | 메모 | | | 로더 무시 |

## Reward
| 값 | 효과 | `Amount` |
|---|---|---|
| `Workshop` | 별 수입 증가 | 별/턴 |
| `Explorer` | 탐험가 출발 — [vision](../game/vision.md#탐험가) | 안 씀 |
| `CityWall` | 성벽 — 요새화 방어 배수 강화([combat](../game/combat.md)) | 안 씀 |
| `Resources` | 즉시 별 | 별 |
| `PopulationGrowth` | 즉시 인구 | 인구 |
| `BorderGrowth` | 영토 반경 확장 | 새 반경 |
| `Park` | 별 수입 증가 + 점수 | 별/턴 |
| `SuperUnit` | 슈퍼 유닛 무료 소환(`GameRules.csv` `City.SuperUnitId`) | 안 씀 |
