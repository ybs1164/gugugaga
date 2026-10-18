# 신 CSV

규칙: [faith](../game/faith.md#신과-신전-패시브). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseGods` → `GameTables.Gods`.

## Gods.csv — 신
파일: `Assets/Resources/Tables/Gods.csv`
한 행 = 신 하나. 신은 능력을 직접 정하지 않고 다른 표의 행을 묶는다 — 신전 패시브와 효과는 [`faith-effects`](faith-effects.md), 펀치·신화 유닛은 [`faith-punches`](faith-punches.md).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. 샌드박스 신 드롭다운이 가리킨다 |
| `Id` | 문자열 | O | | 이름표(중복 불가). 이름·설명은 [번역 표](strings.md#키-규칙) `God.<Id>.Name`/`.Desc` |
| `Passive{n}` | [TemplePassives](faith-effects.md#templepassivescsv--신전-패시브) Index | | 없음 | 신전 패시브들. 여럿이면 모두 작용한다 |
| `FaithPunch{n}` | [FaithPunches](faith-punches.md#faithpunchescsv--신앙-펀치) Index | | 없음 | 이 신이 쓸 수 있는 신앙 펀치 |
| `MythicIndex` | [MythicUnits](faith-punches.md#mythicunitscsv--신화-유닛) Index | | -1 | 강림하는 신화 유닛. -1 = 없음 |
| `Note` | 메모 | | | 로더 무시 |
