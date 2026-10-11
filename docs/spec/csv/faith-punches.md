# 신앙 펀치 CSV

규칙: [faith](../game/faith.md#신앙-펀치). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseFaithPunches` → `GameTables.FaithPunches`. 어느 신이 어느 펀치를 쓰는지는 [`Gods.csv`](gods.md) `FaithPunch{n}`.

## FaithPunches.csv — 신앙 펀치
파일: `Assets/Resources/Tables/FaithPunches.csv`
한 행 = 펀치 하나. 대상 칸을 중심으로 체비쇼프 `Range` 안에 효과를 준다(0이면 그 칸만).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `Gods.csv` `FaithPunch{n}`이 가리킨다 |
| `Id` | 문자열 | O | | 이름표. 이름·설명은 [번역 표](strings.md#키-규칙) `FaithPunch.<Id>.Name`/`.Desc` |
| `Kind` | 열거 | O | | 효과 유형 — 아래 표 |
| `Cost` | 정수 | | 0 | 소모하는 신앙 |
| `Range` | 정수 | | 0 | 대상 칸 중심 체비쇼프 반경 |
| `Value` | 정수 | | 0 | 유형별 크기 |
| `Target` | 문자열 | | 없음 | `Resource`의 구조물 Id |
| `Note` | 메모 | | | 로더 무시 |

### Kind
| 값 | 효과 | `Value` |
|---|---|---|
| `Strike` | 범위 안 적 유닛에게 피해(숨은 유닛·평화 조약 상대 제외) | 피해량 |
| `Boost` | 범위 안 다친 아군 체력 회복 | 회복량 |
| `Resource` | 범위 안 자기 영토·중립의 빈 육지 칸에 `Target` 구조물 생성 | 최대 개수 |
| `Terrain` | 대상 칸 지형을 땅→물→산→땅으로 바꿈 | 쓰지 않음 |
| `Descend` | 대상 아군을 이 신의 [신화 유닛](#mythicunitscsv--신화-유닛)으로 바꿈([강림](../game/faith.md#신화-유닛)) | 쓰지 않음 |

## MythicUnits.csv — 신화 유닛
파일: `Assets/Resources/Tables/MythicUnits.csv`
한 행 = 신화 유닛 하나. 스탯·패시브는 [`Units.csv`](units.md)의 같은 `Id` 행(훈련 불가, `Independent` 권장)이 정하고, 이 표는 유지 턴과 액티브 능력을 정한다. 액티브 능력은 신앙 펀치를 약하게 쓰는 것이다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `Gods.csv` `MythicIndex`가 가리킨다 |
| `Id` | 문자열 | O | | `Units.csv`의 `Id`. 이름은 `Unit.<Id>.Name` |
| `Upkeep` | 정수 | | 1 | 강림 때 받는 유지 턴 |
| `ActivePunch` | FaithPunches Index | | -1 | 액티브 능력으로 쓰는 펀치. -1 = 없음 |
| `ActiveFaithCost` | 정수 | | 0 | 액티브 능력의 신앙 비용(펀치의 `Cost` 대신) |
| `ActiveValue` | 정수 | | 0 | 액티브 능력의 크기(펀치의 `Value` 대신) |
| `ActiveReach` | 정수 | | 1 | 신화 유닛에서 대상 칸까지 체비쇼프 사거리 |
| `ActiveUpkeepCost` | 정수 | | 0 | 쓸 때 추가로 줄어드는 유지 턴 |
| `Note` | 메모 | | | 로더 무시 |
