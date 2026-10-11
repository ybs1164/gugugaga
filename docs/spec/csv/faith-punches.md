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
| `Descend` | 강림(신화 유닛) — 아직 쓰지 않는다 | |
