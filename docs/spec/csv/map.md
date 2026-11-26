# 맵 크기·맵 타입 CSV

규칙: [map](../game/map.md). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseMapSizes`/`ParseMapTypes` → `GameTables.MapSizes`/`MapTypes`.

## MapSizes.csv — 맵 크기
파일: `Assets/Resources/Tables/MapSizes.csv`
한 행 = 고를 수 있는 맵 크기 하나. 행 순서가 시작 화면 **맵** 줄과 샌드박스 **맵 크기** 버튼의 순서다(작은 것부터).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치(시작 화면 선택·`-recordMapSize`가 가리킨다) |
| `Id` | 문자열 | O | | 이름표(중복 불가). 표시 이름은 [번역 표](strings.md#키-규칙) `UI.MapSize.<Id>` |
| `Size` | 정수 ≥ 1 | O | | 정사각형 맵의 한 변(칸) |
| `TinyIslands` | 정수 | | 0 | 한 변이 `Size`인 맵에 놓는 외딴 섬 마을 수 |
| `Ruins` | 정수 | | 0 | 한 변이 `Size`인 맵에 놓는 유적 수 |
| `Note` | 메모 | | | 로더 무시. 원문과 다르면 이유를 적는다 |

한 변이 어느 행의 `Size`와도 같지 않은 맵(씬에 직접 넣은 크기)은 [GameRules](game-rules.md) `Map.TinyIslandTilesPerVillage`·`Map.RuinTilesPerRuin`으로 센다.

## MapTypes.csv — 맵 타입
파일: `Assets/Resources/Tables/MapTypes.csv`
한 행 = 맵 타입 하나. 행 순서가 샌드박스 습도 탭 목록의 순서다. 처음 고른 값은 [GameRules](game-rules.md) `Map.DefaultType`.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치(`Map.DefaultType`이 가리킨다) |
| `Id` | 문자열 | O | | 이름표(중복 불가). [시작 조건](tribes.md#startconditionrulescsv--시작-조건-규칙) `MapType`이 이 값과 비교한다. 표시 이름은 [번역 표](strings.md#키-규칙) `UI.MapType.<Id>` |
| `Shape` | 열거 | | `Freeform` | 땅 모양 — 아래 |
| `Wetness` | 0..1 | | 0 | 이 타입을 골랐을 때의 목표 물 비율. 샌드박스 습도 탭 슬라이더의 처음 값이고, 빠른 한 판은 이 값을 그대로 쓴다 |
| `Note` | 메모 | | | 로더 무시. 원문과 다르면 이유를 적는다 |

### Shape
| 값 | 땅 모양 |
|---|---|
| `Freeform` | 마스크 없이 타일 확률대로 채운다 |
| `Pangea`, `Lakes`, `Continents`, `Archipelago`, `Waterworld` | 그 이름의 땅 모양 마스크 — [map](../game/map.md#맵-타입) |
