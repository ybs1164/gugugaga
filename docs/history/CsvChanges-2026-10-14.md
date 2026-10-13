# CSV 변경 정리 (2026-10-12 빌드 → 2026-10-14 빌드)

CSV를 고치는 사람 기준으로 지난 빌드(`64849e1`) 이후 바뀐 점만 모았다. 수치는 CSV에 있으므로 여기에는 키·컬럼 이름만 적는다.
현재 규칙의 정본은 [`docs/spec`](../spec/README.md)이고, 이 문서는 이력이다.

## 한눈에

| 파일 | 바뀐 점 | 옛 파일을 그대로 쓰면 |
|---|---|---|
| `Tables/Tribes.csv` | `BiomeIndex` 삭제 → `Biome`(Id) + `GroundTile` + 배수 7열 추가 | `BiomeIndex`는 경고 후 무시 → **모든 종족이 첫 바이옴**, 배수 1 |
| `Tables/Biomes.csv` | 바이옴 간 차이를 종족 배수로 옮김(`Biome` 행 `MountainRate`/`ForestRate`, 일부 자원 `InnerRate`/`OuterRate`를 공통값으로) | 그대로 읽힌다. 다만 종족 배수와 곱해져 차이가 두 번 들어간다 |
| `Pixel2D/SpriteCatalog.csv` | `Ground.Field_<종족>` 12행 추가(종족별 평지 색) | 해당 행이 없으면 평지가 `Ground.Grass`로 보인다 |
| `GameRules.csv` | `Map.SuburbMinDistance` 추가 | 키가 없으면 코드 기본값 |
| `Strings.csv` | 샌드박스 바이옴 선택 UI 문구 삭제, 탭 키 이름 변경 | 남은 옛 키는 쓰이지 않을 뿐 오류는 없다 |

컬럼 정의: [tribes](../spec/csv/tribes.md), [biomes](../spec/csv/biomes.md), [sprites](../spec/csv/sprites.md), [game-rules](../spec/csv/game-rules.md). 규칙: [tribe 영역 지형](../spec/game/tribe.md#영역-지형).

## 1. Tribes.csv — 종족이 영역 지형을 정한다 (`6f327f3`)

바이옴은 더 이상 샌드박스에서 따로 고르지 않는다. **팀 하나 = 수도 영역 하나**이고, 그 영역의 지형은 팀 종족 행이 정한다.

새 헤더:
```
Index,Id,Biome,GroundTile,TechGroupIndex,StartTech1,StartStars,StartCapitalLevel,StartUnit1,StartConditionIndex,MountainRate,ForestRate,FruitRate,CropRate,AnimalRate,MetalRate,FishRate,Wiki
```

| 컬럼 | 넣는 값 | 비우면 | 주의 |
|---|---|---|---|
| `Biome` | `Biomes.csv`의 `Biome` Id 문자열(`Grassland` 등) | 첫 바이옴 | 예전 `BiomeIndex`(행 순서 숫자)가 아니다. 오타면 첫 바이옴으로 조용히 대체 |
| `GroundTile` | 평지를 대신할 TileTypeId(`Field_XinXi` 등) | 바이옴 평지 그대로 | 바이옴의 **첫 `Land` 타일 행**을 이 Id로 바꾼다. 그 Id를 쓰는 `AllowedTile{n}`·타일 `Exclude{n}`도 함께 바뀐다 |
| `MountainRate`, `ForestRate` | 실수 ≥ 0 | 1 | 바이옴 같은 컬럼 값과 **곱한다** |
| `FruitRate`, `CropRate`, `AnimalRate`, `MetalRate`, `FishRate` | 실수 ≥ 0 | 1 | 바이옴 `Resource_*` 행의 `InnerRate`/`OuterRate`에 곱한다. 0이면 그 자원 없음 |

편집 팁
- 새 종족을 추가하면서 새 `GroundTile` Id를 만들었다면 `SpriteCatalog.csv`에 `Ground.<GroundTile>` 행도 추가한다(아래 3). 평지 Id가 달라도 규칙상으로는 같은 평지(`Field`)로 취급된다.
- 배수 기준은 위키 Tribes 표의 산·숲·자원 배수다. 물 배수는 넣지 않았다(원문에서도 생성에 반영되지 않음). 각 행 근거는 `Wiki` 열.
- 같은 타일 묶음 안에서 자원 비율 합이 1을 넘으면 CSV 앞쪽 자원부터 칸을 가져간다 — 배수를 크게 올릴 때 뒤쪽 자원이 밀릴 수 있다.
- 종족을 고르지 않은 팀은 첫 바이옴을 배수 없이 쓴다.

## 2. Biomes.csv — 바이옴은 공통 바탕만 (`6f327f3`)

종족 배수가 생기면서 바이옴 쪽에 있던 종족 차이를 덜어 냈다.
- `Biome` 행: `Desert`, `Highland`의 `MountainRate`/`ForestRate`를 기본값으로 되돌렸다(차이는 종족 배수로).
- `Structure` 행: `Desert`의 `Resource_Animal`, `Highland`의 `Resource_Metal` `InnerRate`/`OuterRate`를 다른 바이옴과 같은 값으로 맞췄다.
- 평지 = 각 바이옴의 **첫 `Land` 타일 행**. 행 순서를 바꾸면 종족 `GroundTile`이 갈아 끼우는 타일도 바뀐다.
- Archipelago의 섬 수는 이제 바이옴 수가 아니라 영역(팀) 수다.
- 샌드박스의 "바이옴 불러오기" 버튼은 없어졌다. 다른 바이옴 표는 **CSV 데이터 설정**의 `Tables/Biomes.csv` 항목(또는 표 폴더)으로 바꾼다.

## 3. SpriteCatalog.csv — 종족 평지 색 (`6f327f3`, `a68aacd`)

- `Ground.Field_<종족 Id>` 12행 추가. `Pattern/Solid` 단색이고 색은 `Color` 열.
- 숲·산 칸의 바닥도 그 칸의 맨땅(`Ground.<GroundTile>`)으로 그린다. 숲을 베거나 산이 사라지면 그 맨땅으로 돌아간다.
- 행이 없으면 `Ground.Grass`로 대체된다.

## 4. GameRules.csv — Suburb 간격 (`3849fe3`)

- `Map.SuburbMinDistance` 추가: Suburb 마을과 **모든 수도** 사이 최소 체비쇼프 거리. 다른 도시와의 간격은 계속 `Map.CityMinDistance`.
- 규칙: [map 간격 규칙](../spec/game/map.md#간격-규칙).

## 5. Strings.csv — 샌드박스 탭 정리 (`6f327f3`)

- 삭제: `UI.Sandbox.BiomeOnly`, `BiomeAuto`, `BiomeLoadNoRows`, `BiomesLoaded`, `BiomeTribeWarnings`, `BiomeLoadFailed`, `RegionSingle`, `RegionCount`, `UI.SandboxHud.LoadBiomes`, `Biome`, `BiomeAuto`, `BiomeSelected`, `BiomeClear`, `BiomeOptionOn`, `BiomeOptionOff`, `PickBiomeCsv`.
- 이름 변경: `UI.SandboxHud.BiomeTabButton`/`BiomeTabTitle` → `TribeTabButton`/`TribeTabTitle`("종족 · 신" 탭).
- 문구 변경: `UI.Sandbox.TribeInfoEmpty`, `UI.Sandbox.NoBiomes`.

## CSV 수정이 없는 동작 변경

수치·컬럼은 그대로지만 같은 값이 다르게 작동한다.
- `Splash` 유닛 능력: 대상 상하좌우 → **주변 8칸**. 짓밟기(`Stomp`)와 같은 규칙(평화조약 상대 제외, 맞으면 전쟁, 숨은 유닛 노출, 반격 없음). `Combat.SplashDivisor`는 그대로 — [combat](../spec/game/combat.md#공격-후) (`435da11`).
- 이동: 아군 유닛 칸(도시 위 포함)은 **지나갈 수 있고** 멈출 수는 없다. 아군은 영향권(ZOC)을 만들지 않는다 — [movement](../spec/game/movement.md) (`7660af1`).
- 영토 경계: 같은 팀 영토를 한 외곽선으로 합쳐 팀 색으로 그린다 — [tile](../spec/game/tile.md) (`7ef34b4`, `995028d`).
