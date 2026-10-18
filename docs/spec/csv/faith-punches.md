# 신앙 펀치 CSV

규칙: [faith](../game/faith.md#신앙-펀치). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseFaithPunches` → `GameTables.FaithPunches`. 어느 신이 어느 펀치를 쓰는지는 [`Gods.csv`](gods.md) `FaithPunch{n}`.

## FaithPunches.csv — 신앙 펀치
파일: `Assets/Resources/Tables/FaithPunches.csv`
한 행 = 펀치 하나. 대상 칸을 중심으로 체비쇼프 `Range` 안에 효과들을 준다(0이면 그 칸만). 효과의 뜻과 펀치에 쓸 수 있는 유형은 [`FaithEffects.csv`](faith-effects.md#effect).
- 효과 중 하나라도 받을 대상이 있으면 쓸 수 있다. 하나도 없으면 첫 효과의 이유가 보인다.
- 모든 효과의 대상은 발동 전 상태로 정한다 — 예: `Terrain` 효과 여럿을 나열하면 발동 전 지형에 맞는 하나만 닿는다.
- 미리보기의 "영향" 수는 효과를 받는 유닛·칸을 한 번씩 센다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `Gods.csv` `FaithPunch{n}`, `MythicUnits.csv` `ActivePunch`가 가리킨다 |
| `Id` | 문자열 | O | | 이름표(중복 불가). 이름·설명은 [번역 표](strings.md#키-규칙) `FaithPunch.<Id>.Name`/`.Desc` |
| `Cost` | 정수 | | 0 | 소모하는 신앙 |
| `Range` | 정수 | | 0 | 대상 칸 중심 체비쇼프 반경 |
| `Effect{n}` | [FaithEffects](faith-effects.md#faitheffectscsv--신앙-효과) Index | O | | 효과들(나열한 순서로 적용) |
| `Note` | 메모 | | | 로더 무시 |

## MythicUnits.csv — 신화 유닛
파일: `Assets/Resources/Tables/MythicUnits.csv`
한 행 = 신화 유닛 하나. 스탯·패시브는 [`Units.csv`](units.md)의 같은 `Id` 행(훈련 불가, `Independent` 권장)이 정하고, 이 표는 유지 턴과 액티브 능력을 정한다. 액티브 능력은 펀치 행 하나를 쓴다 — 신의 펀치를 약하게 쓰려면 약한 판 행을 `FaithPunches.csv`에 따로 둔다(신의 `FaithPunch{n}`에 넣지 않으면 신앙 펀치 목록에는 나오지 않는다).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `Gods.csv` `MythicIndex`가 가리킨다 |
| `Id` | 문자열 | O | | `Units.csv`의 `Id`. 이름은 `Unit.<Id>.Name` |
| `Upkeep` | 정수 | | 1 | 강림 때 받는 유지 턴 |
| `ActivePunch` | FaithPunches Index | | -1 | 액티브 능력으로 쓰는 펀치(신앙 비용은 그 행의 `Cost`). -1 = 없음. `Descend` 효과가 있는 펀치는 쓸 수 없다 |
| `ActiveReach` | 정수 | | 1 | 신화 유닛에서 대상 칸까지 체비쇼프 사거리 |
| `ActiveUpkeepCost` | 정수 | | 0 | 쓸 때 추가로 줄어드는 유지 턴 |
| `Note` | 메모 | | | 로더 무시 |
