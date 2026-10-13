# 종족 지형 CSV

파일: `Assets/Resources/Tables/TribeTerrain.csv`
Sandbox의 `Tables/TribeTerrain.csv` 항목에서 다른 파일을 고르면(또는 표 폴더에 있으면) 그 파일이 대신 쓰인다. 실제 지형 반영은 지형 생성 버튼으로 수행한다. 로더: `BiomeCsvSerializer.Parse` → `BiomeCsvRow`. 규칙: [map](../game/map.md), [tribe](../game/tribe.md#영역-지형).

롱 포맷: 한 행이 종족 묶음 하나(`Tribe` 행), 또는 그 묶음의 타일 엔트리 하나, 또는 구조물 엔트리 하나다. 행 종류마다 안 쓰는 칸은 비운다.
묶음 Id = [tribes](tribes.md)의 `Id`. 종족을 고르지 않은 팀과 묶음이 없는 종족은 `Default` 묶음(없으면 첫 묶음)을 쓴다.
첫 `Land` 타일 행이 그 묶음의 **평지**다 — 종족마다 다른 평지 Id를 쓰면 색만 다른 맨땅이 된다([sprites](sprites.md)의 `Ground.<Id>`).

| 컬럼 | 타입 | 기본 | 쓰는 행 | 뜻 |
|---|---|---|---|---|
| `Kind` | `Tribe`/`Tile`/`Structure` | `Tribe` | 전부 | 행 종류 |
| `Tribe` | 종족 Id 또는 `Default` | (필수) | 전부 | 소속 묶음. `Tribe` 행이 없으면 기본값 묶음이 만들어진다 |
| `Name` | 문자열 | `Tribe` | Tribe | 표시 이름. 기본 표는 비우고 [번역 표](strings.md#키-규칙) `Tribe.<Tribe>.Name`을 쓴다(번역이 있으면 이 칸보다 앞선다) |
| `NoiseType` | 문자열 | `Perlin` | Tribe | 노이즈 종류. `Perlin`만 지원 |
| `Frequency` | 실수 | 0 | Tribe | 노이즈 주파수 |
| `Octaves` | 정수 | 0 | Tribe | 노이즈 옥타브 |
| `SeedOffset` | 정수 | 0 | Tribe | 이 묶음 노이즈장의 시드 오프셋 |
| `InnerRadius` | 정수 | 0 | Tribe | 가장 가까운 도시에서 이 거리 이내 = Inner(타일 가중치용) |
| `MountainRate` | 실수 | 0 | Tribe | 산 레이어 배수(1 = 기준 비율). 0이면 산 없음 |
| `ForestRate` | 실수 | 0 | Tribe | 숲 레이어 배수(1 = 기준 비율). 0이면 숲 레이어 없음 |
| `Entry` | 문자열 | | Tile, Structure | Tile: TileTypeId / Structure: StructureId |
| `TerrainType` | `TerrainType` | `Land` | Tile | 이 타일의 이동 지형 |
| `InnerWeight` | 실수 | 0 | Tile | Inner 영역 가중치(노이즈로 보정) |
| `OuterWeight` | 실수 | 0 | Tile | Outer 영역 가중치 |
| `Weight` | 실수 | 0 | Structure | 개수 기반 구조물끼리 같은 칸을 다툴 때 가중치 |
| `MinCount` | 정수 | 0 | Tile, Structure | 최소 개수 |
| `CountPerTiles` | 실수 | 0 | Tile, Structure | >0이면 (해당 칸 수 ÷ 이 값)개까지 최소 개수를 늘린다. `MinCount`와 큰 쪽 |
| `MinDistance` | 정수 | 0 | Tile, Structure | 같은 Id끼리 최소 체비쇼프 거리. 0 = 제약 없음 |
| `EdgeMargin` | 정수 | 0 | Tile, Structure | 맵 가장자리에서 최소 거리 |
| `MaxDistanceFromCity` | 정수 | 0 | Structure | >0이면 가장 가까운 도시에서 이 거리 이내만 |
| `MaxWaterFractionOnLakes` | 실수 | 없음 | Structure | Lakes 맵에서 물 위에 놓일 수 있는 비율 상한(0~1). 비우면 제약 없음 |
| `FillRemaining` | 불리언 | false | Structure | 개수 무시하고 자리가 없을 때까지 놓는다 |
| `InnerRate` | 실수 | 0 | Structure | >0이면 자원 — 도시 거리 1 칸 중 이 비율을 채운다. 같은 타일 묶음 안 비율 합이 1을 넘으면 앞쪽 행부터 칸을 가져간다 |
| `OuterRate` | 실수 | 0 | Structure | 자원 — 도시 거리 2 칸 중 이 비율 |
| `AllowedTile{n}` | TileTypeId | 없음 | Structure | 놓일 수 있는 타일. **비우면 어디에도 안 놓인다**. 깊은 바다는 `Ocean` |
| `Exclude{n}` | Id | 없음 | Tile, Structure | Tile: 상하좌우로 붙을 수 없는 TileTypeId / Structure: 8방향으로 붙을 수 없는 StructureId |

입력은 위 롱 포맷만 사용한다.

## CSV에 적지 않는 것
자동으로 만들어진다: 수도(`Capital`), 등대(`Lighthouse`), 숲·산 타일(`Forest`/`Mountain` — `MountainRate`/`ForestRate`로 조절), 깊은 바다(`Ocean`), 맵 타입 전용 마을. [map](../game/map.md) 참고.

## 화면에 보이는 Id
`Entry`에 아무 Id나 적을 수 있지만, 화면에 보이려면 [sprites](sprites.md)에 `Ground.<TileTypeId>` / `Structure.<StructureId>`가 있어야 한다. 없으면 오류 없이 기본 표시로 대체된다.
