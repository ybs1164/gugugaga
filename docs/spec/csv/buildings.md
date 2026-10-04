# 건물 CSV

파일: `Assets/Resources/Tables/Buildings.csv`
한 행 = 건물 하나. 배열형(`Index`). 로더: `ArrayTableCsvSerializer.ParseBuildings` → `BuildingDefinition.All`(배열 위치 = `Index`). 규칙: [building](../game/building.md).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. [TechUnlocks](tech.md#techunlockscsv--해금-내역) `BuildingIndex`가 가리킨다 |
| `Id` | 문자열 | O | | 타일에 저장되는 건물 키. 코드가 특별 취급하는 Id는 바꾸지 않는다: `Farm` `Mine` `LumberHut` `Windmill` `Forge` `Sawmill` `Market` `Port` `Road` `Bridge` |
| `Cost` | 정수 | | 0 | 별 |
| `Population` | 정수 | | 0 | 지을 때 주인 도시에 더하는 인구 |
| `Terrain{n}` | `TileClass` | O(1개 이상) | | 지을 수 있는 지형 |
| `RequiredStructure{n}` | StructureId | | 없음 | 이 구조물 중 하나 위에만 지을 수 있고, 지으면 소모 |
| `AdjacentBuildingIndex{n}` | Buildings Index | | 없음 | 8방향 같은 팀 영토에 이 건물 중 하나가 있어야 함 |
| `PopulationPerAdjacent` | 정수 | | 0 | 인접한 `AdjacentBuildingIndex` 건물 하나당 인구 |
| `Flag{n}` | 태그 | | 없음 | 아래 태그 |
| `Task` | 과업 Id | | 없음 | 기념물이면 [과업](tasks.md) Id |
| `Wiki`, `Note` | 메모 | | | 로더 무시 |

이름·설명은 [번역 표](strings.md#키-규칙) `Building.<Id>.Name`/`.Desc`.
여는 기술은 이 표에 적지 않는다 — TechUnlocks에서 `BuildingIndex`로 이 건물을 가리키는 해금 내역의 키(`Build.<Id>`)가 해금 키가 된다. 가리키는 내역이 없으면 기술 없이 지을 수 있다.

## Flag 태그
| 태그 | 뜻 |
|---|---|
| `Road` | 건물이 아니라 타일 개량(도로) — 다른 건물과 한 칸에 공존 |
| `Neutral` | 중립 땅·물에도 지을 수 있음 |
| `OppositeLand` | 상하 또는 좌우 양쪽 이웃이 육지여야 함(다리) |
| `ActsAsRoad` | 수도 연결에서 도로로 취급 |
| `Temple` | 신전 — 지은 턴부터 레벨이 오른다(점수) |
| `StarsFromAdjacent` | 인접 가공 건물의 인구 합만큼 별/턴(시장) |
| `OnePerCity` | 도시 영토당 하나 |
| `Embassy` | 영토·도시 타일 제한 대신 [대사관 규칙](../game/building.md#대사관)을 사용하는 건물 |
