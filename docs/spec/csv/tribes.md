# 종족·시작 조건 CSV

규칙: [tribe](../game/tribe.md). 배열형 표 3개(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer` → `GameTables`.

```
StartConditionRules ◄── StartConditions.Rule{n}
StartConditions     ◄── Tribes.StartConditionIndex
```

## Tribes.csv — 종족
파일: `Assets/Resources/Tables/Tribes.csv`

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 종족 이름표. 이름·설명은 [번역 표](strings.md#키-규칙) `Tribe.<Id>.Name`/`.Desc` |
| `BiomeIndex` | 바이옴 Index | | -1 | 종족 영역을 채울 바이옴([biomes](biomes.md)의 `Biome` 행 순서). 범위 밖이면 0번 |
| `TechGroupIndex` | [TechGroups](tech.md#techgroupscsv--종족별-기술-묶음) Index | | -1 | 연구 가능한 기술 묶음 |
| `StartTech{n}` | [Techs](tech.md#techscsv--기술) Index | | 없음 | 처음부터 해금된 기술 |
| `StartStars` | 정수 | | `GameRules.csv` `Economy.StartingStars` | 시작 별 |
| `StartUnit{n}` | [Units](units.md#unitscsv--육지-유닛) Index | | 없음 | 수도에 받는 시작 유닛 |
| `StartConditionIndex` | StartConditions Index | | -1 | 수도 주변 시작 조건. -1 = 없음 |
| `Wiki`, `Note` | 메모 | | | 로더 무시 |

## StartConditions.csv — 시작 조건 묶음
파일: `Assets/Resources/Tables/StartConditions.csv`

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 이름표. 이름·설명은 [번역 표](strings.md#키-규칙) `StartCondition.<Id>.Name`/`.Desc` |
| `Rule{n}` | StartConditionRules Index | | | 적힌 순서대로 적용할 규칙 |
| `Note` | 메모 | | | 로더 무시 |

## StartConditionRules.csv — 시작 조건 규칙
파일: `Assets/Resources/Tables/StartConditionRules.csv`
한 행 = "수도에서 거리 `MinDistance`~`MaxDistance` 고리 안에 `Target`이 최소 `Count`개".

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | | | 이름표 |
| `Kind` | `Tile`/`Structure` | O | `Tile` | Tile = 칸의 TileTypeId를 바꿈 / Structure = 칸 위에 구조물 |
| `Target` | TileTypeId 또는 StructureId | O | | 보장할 대상 |
| `TerrainType` | `TerrainType` | | `Land` | Tile: 바뀐 칸의 이동 지형 / Structure: `AllowedTile`이 비었을 때 놓일 수 있는 지형 |
| `Count` | 정수 | | 1 | 최소 개수 |
| `MinDistance` | 정수 | | 1 | 수도에서 체비쇼프 거리 하한(1 = 바로 옆 8칸) |
| `MaxDistance` | 정수 | | 1 | 거리 상한 |
| `MapType` | 맵 타입 이름 | | 항상 | 이 맵 타입에서만 적용 — [map](../game/map.md#맵-타입) |
| `AllowedTile{n}` | TileTypeId | | | Tile: 바꿔도 되는 원래 타일(비우면 도시가 아닌 아무 육지) / Structure: 놓일 수 있는 타일 |
| `Description` | 메모 | | | 설계용 설명(화면에 나오지 않는다) |
| `Note` | 메모 | | | 로더 무시 |
