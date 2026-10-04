# 타일 행동 CSV

파일: `Assets/Resources/TileActions.csv`
한 행 = 건물이 아닌 1회성 타일 작업 하나. 키형(`Id`). 로더: `GameTableCsvSerializer.ParseTileActions` → `TileActionDefinition.All`. 규칙: [building](../game/building.md#타일-행동).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Id` | 문자열 | O | | 행동 키 |
| `Kind` | 아래 표 | O | `Harvest` | 효과 종류(효과 자체는 코드) |
| `Unlock` | 해금 키 | | 없음 | [tech](tech.md#해금-키) |
| `Cost` | 정수 | | 0 | 별 |
| `Terrain{n}` | `TileClass` | O | | 할 수 있는 지형 |
| `RequiredStructure{n}` | StructureId | | 없음 | `Harvest`는 이 중 하나가 칸에 있어야 하고 소모한다 |
| `Population` | 정수 | | 0 | 주인 도시 인구 증가 |
| `StarsGain` | 정수 | | 0 | 즉시 얻는 별 |
| `Wiki`, `Note` | 메모 | | | 로더 무시 |


## Kind
| 값 | 효과 |
|---|---|
| `Harvest` | 구조물을 소모하고 인구·별을 얻는다 |
| `ClearForest` | 숲 → 평지, 별 획득 |
| `BurnForest` | 숲 → 작물이 있는 평지 |
| `GrowForest` | 평지 → 숲 |
| `Destroy` | 자기 건물 제거, 그 건물이 준 인구도 되돌린다 |
