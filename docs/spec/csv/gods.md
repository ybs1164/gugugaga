# 신 CSV

규칙: [faith](../game/faith.md#신과-신전-패시브). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseGods` → `GameTables.Gods`.

## Gods.csv — 신
파일: `Assets/Resources/Tables/Gods.csv`
한 행 = 신 하나, 신전 패시브 하나. 신전 하나의 효과 크기 = `Value` + `PerPrayer` × (그 신전 곁에서 기도 중인 유닛 수), 0 미만이면 0.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. 샌드박스 신 드롭다운이 가리킨다 |
| `Id` | 문자열 | O | | 이름표. 이름·설명은 [번역 표](strings.md#키-규칙) `God.<Id>.Name`/`.Desc` |
| `Passive` | 열거 | | `None` | 패시브 유형 — 아래 표 |
| `Target` | 문자열 | | 없음 | 유형별 대상 — 아래 표 |
| `Value` | 실수 | | 0 | 신전 하나의 기본 효과 크기 |
| `PerPrayer` | 실수 | | 0 | 기도 유닛 하나당 더해지는 크기(음수면 약해진다) |
| `Period` | 정수 | | 1 | `Resource`: 이 턴 수마다 한 번 발동 |
| `MythicIndex` | [MythicUnits](faith-punches.md#mythicunitscsv--신화-유닛) Index | | -1 | 강림하는 신화 유닛. -1 = 없음 |
| `FaithPunch{n}` | [FaithPunches](faith-punches.md#faithpunchescsv--신앙-펀치) Index | | 없음 | 이 신이 쓸 수 있는 신앙 펀치 |
| `Note` | 메모 | | | 로더 무시 |

### Passive
| 값 | 효과 | `Target` |
|---|---|---|
| `None` | 없음 | |
| `Aura` | 신전에 인접(8칸)한 아군 유닛의 능력치 +크기 | `Attack` 또는 `Defense` |
| `Buff` | 신전에 인접한 아군이 방어 태세(`Defend`)일 때 방어력 +크기 | 쓰지 않음 |
| `Damage` | 그 팀 턴 시작에 신전에 인접한 적 유닛에게 피해(체력 1 아래로는 깎지 않음) | 쓰지 않음 |
| `Resource` | `Period` 턴마다 신전 인접 자기 영토 빈 육지 칸에 구조물을 크기(반올림)개 생성 | 구조물 Id |
| `Gold` | 신전마다 별 +크기/턴 | 쓰지 않음 |
| `FaithCap` | 신전마다 신앙 최대 보유량 +크기 | 쓰지 않음 |
