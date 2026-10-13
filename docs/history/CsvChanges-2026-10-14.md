# CSV 변경 정리 (2026-10-12 빌드 → 2026-10-14 빌드)

CSV를 고치는 사람 기준으로 지난 빌드(`64849e1`) 이후 바뀐 점만 모았다. 수치는 CSV에 있으므로 여기에는 키·컬럼 이름만 적는다.
현재 규칙의 정본은 [`docs/spec`](../spec/README.md)이고, 이 문서는 이력이다.

## 한눈에

| 파일 | 바뀐 점 | 옛 파일을 그대로 쓰면 |
|---|---|---|
| `Tables/Biomes.csv` | **삭제.** `Tables/TribeTerrain.csv`로 대체 | 읽지 않는다(표 폴더에 남아 있어도 무시) |
| `Tables/TribeTerrain.csv` | **새 파일.** 종족마다 지형 묶음 하나 + `Default` 묶음 | 없으면 지형 생성이 안 된다(`UI.Sandbox.NoBiomes`) |
| `Tables/Tribes.csv` | `BiomeIndex` 열 삭제. 지형 관련 열 없음 | `BiomeIndex`는 경고 후 무시. 그 밖엔 그대로 읽힌다 |
| `Pixel2D/SpriteCatalog.csv` | `Ground.Field_<종족>` 12행 추가(종족별 평지 색) | 해당 행이 없으면 평지가 `Ground.Grass`로 보인다 |
| `GameRules.csv` | `Map.SuburbMinDistance` 추가 | 키가 없으면 코드 기본값 |
| `Strings.csv` | `Biome.*.Name` 삭제, 샌드박스 바이옴 선택 UI 문구 삭제, 탭 키 이름 변경 | 남은 옛 키는 쓰이지 않을 뿐 오류는 없다 |

컬럼 정의: [tribe-terrain](../spec/csv/tribe-terrain.md), [tribes](../spec/csv/tribes.md), [sprites](../spec/csv/sprites.md), [game-rules](../spec/csv/game-rules.md). 규칙: [tribe 영역 지형](../spec/game/tribe.md#영역-지형).

## 1. TribeTerrain.csv — 종족이 영역 지형을 통째로 갖는다

바이옴은 더 이상 샌드박스에서 고르지 않는다. **팀 하나 = 수도 영역 하나**이고, 그 영역의 지형은 팀 종족의 묶음이 정한다.
`Biomes.csv`(바이옴 3개)를 종족 수만큼 펼친 파일이라, 형식은 옛 `Biomes.csv`와 같고 두 군데만 다르다.

| 옛 `Biomes.csv` | 새 `TribeTerrain.csv` |
|---|---|
| `Kind` = `Biome`/`Tile`/`Structure` | `Kind` = **`Tribe`**/`Tile`/`Structure` |
| `Biome` 열 = 바이옴 Id(`Grassland` 등) | **`Tribe`** 열 = `Tribes.csv`의 `Id`(`XinXi` 등) 또는 `Default` |

나머지 열(`NoiseType` … `Exclude{n}`)은 이름·뜻 그대로다.

묶음 고르는 규칙
- 팀 종족의 `Id`와 같은 `Tribe` 묶음 → 없으면 `Default` 묶음 → 그것도 없으면 첫 묶음.
- 종족을 고르지 않은 팀은 `Default` 묶음.
- 종족에 묶음이 없으면 표를 읽을 때 경고가 난다(그 종족은 `Default`를 쓴다).

지금 값이 만들어진 방식(편집할 때 참고)
- 바탕은 옛 바이옴 셋이다: 평원 = `Default`·Imperius·Bardur·Kickoo·Hoodrick·Luxidoor·Quetzali, 사막 = Oumaji·Zebasi·Yadakk, 고지대 = XinXi·Vengir·AiMo.
- 위키 Tribes의 산·숲·자원 배수를 미리 곱해 넣었다: `Tribe` 행의 `MountainRate`/`ForestRate`, `Resource_*` 행의 `InnerRate`/`OuterRate`. 이제 곱하는 단계가 없으니 **CSV에 적힌 값이 그대로 최종값**이다.
- 각 묶음의 **첫 `Land` 타일 행**이 평지다. 종족마다 `Field_<종족>`을 써서 색만 다르다. 그 Id를 쓰는 `AllowedTile{n}`·`Exclude{n}`도 같은 이름으로 맞춰 둬야 한다(평지 이름만 바꾸고 구조물 행을 안 고치면 마을·자원이 안 놓인다).
- 같은 타일 묶음 안에서 자원 비율 합이 1을 넘으면 CSV 앞쪽 자원부터 칸을 가져간다.

편집 팁
- 공통 규칙(유적·불가사리·마을 등)을 바꿀 때는 13개 묶음을 모두 고쳐야 한다. 엑셀에서 `Entry` 열로 필터를 걸어 한 번에 고치면 편하다.
- 새 종족을 추가하면 `Tribes.csv` 행 + `TribeTerrain.csv` 묶음(`Tribe` 행 1 + `Tile`/`Structure` 행)을 함께 넣는다. 새 평지 Id를 만들었다면 `SpriteCatalog.csv`에 `Ground.<평지 Id>` 행도 추가한다.
- 샌드박스 **CSV 데이터 설정** 목록의 `TribeTerrain.csv` 항목으로 고친 파일을 바로 불러와 볼 수 있다.

## 2. Tribes.csv — 지형 열이 빠졌다

새 헤더:
```
Index,Id,TechGroupIndex,StartTech1,StartStars,StartCapitalLevel,StartUnit1,StartConditionIndex,Wiki
```
- `BiomeIndex` 삭제. 종족과 지형은 `Id` ↔ `TribeTerrain.csv`의 `Tribe`로 이어진다 — **`Id`를 바꾸면 지형 묶음 이름도 같이 바꾼다.**

## 3. SpriteCatalog.csv — 종족 평지 색

- `Ground.Field_<종족 Id>` 12행 추가. `Pattern/Solid` 단색이고 색은 `Color` 열.
- 숲·산 칸의 바닥도 그 칸의 맨땅(`Ground.<평지 Id>`)으로 그린다. 숲을 베거나 산이 사라지면 그 맨땅으로 돌아간다.
- 행이 없으면 `Ground.Grass`로 대체된다.

## 4. GameRules.csv — Suburb 간격 (`3849fe3`)

- `Map.SuburbMinDistance` 추가: Suburb 마을과 **모든 수도** 사이 최소 체비쇼프 거리. 다른 도시와의 간격은 계속 `Map.CityMinDistance`.
- 규칙: [map 간격 규칙](../spec/game/map.md#간격-규칙).

## 5. Strings.csv — 샌드박스 탭 정리

- 삭제: `Biome.Grassland.Name`, `Biome.Desert.Name`, `Biome.Highland.Name`(지형 묶음 이름은 `Tribe.<Id>.Name`을 쓴다).
- 삭제: `UI.Sandbox.BiomeOnly`, `BiomeAuto`, `BiomeLoadNoRows`, `BiomesLoaded`, `BiomeTribeWarnings`, `BiomeLoadFailed`, `RegionSingle`, `RegionCount`, `UI.SandboxHud.LoadBiomes`, `Biome`, `BiomeAuto`, `BiomeSelected`, `BiomeClear`, `BiomeOptionOn`, `BiomeOptionOff`, `PickBiomeCsv`.
- 이름 변경: `UI.SandboxHud.BiomeTabButton`/`BiomeTabTitle` → `TribeTabButton`/`TribeTabTitle`("종족 · 신" 탭).
- 문구 변경: `UI.Sandbox.TribeInfoEmpty`, `UI.Sandbox.NoBiomes`.

## CSV 수정이 없는 동작 변경

수치·컬럼은 그대로지만 같은 값이 다르게 작동한다.
- `Splash` 유닛 능력: 대상 상하좌우 → **주변 8칸**. 짓밟기(`Stomp`)와 같은 규칙(평화조약 상대 제외, 맞으면 전쟁, 숨은 유닛 노출, 반격 없음). `Combat.SplashDivisor`는 그대로 — [combat](../spec/game/combat.md#공격-후) (`435da11`).
- 이동: 아군 유닛 칸(도시 위 포함)은 **지나갈 수 있고** 멈출 수는 없다. 아군은 영향권(ZOC)을 만들지 않는다 — [movement](../spec/game/movement.md) (`7660af1`).
- 영토 경계: 같은 팀 영토를 한 외곽선으로 합쳐 팀 색으로 그린다 — [tile](../spec/game/tile.md) (`7ef34b4`, `995028d`).

## 같은 날 앞선 빌드를 받았다면

같은 날 먼저 올린 빌드(`13f7b98`)에서는 `Biomes.csv`가 남아 있었고 `Tribes.csv`에 `Biome`·`GroundTile`·배수 7열(`MountainRate`…`FishRate`)이 있었다. 이번 빌드에서 그 열들은 모두 없어지고 `TribeTerrain.csv`로 합쳐졌다. 그 형식으로 고친 파일은 `TribeTerrain.csv`의 해당 종족 묶음으로 옮겨 적어야 한다.
