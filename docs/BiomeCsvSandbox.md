# CSV 바이옴(지형 생성) 제작 가이드

CSV 파일만 편집해서 맵의 바이옴(지형 타입) 구성과 타일/구조물 생성 규칙을 정의하고, 인게임 샌드박스에서
실제로 지형을 생성해 검증하는 테스트 도구입니다. [`UnitCsvSandbox.md`](UnitCsvSandbox.md)의 유닛 CSV와 같은
철학(코드를 건드리지 않고 CSV만으로 콘텐츠를 늘림)을 지형 쪽에 적용한 것입니다.

생성 규칙의 근거는 Polytopia 위키 요약인 [`PolytopiaMapGeneration.md`](PolytopiaMapGeneration.md)이고,
전체 파이프라인 한눈에 보기는 [`TerrainGenerationSummary.md`](TerrainGenerationSummary.md)에 있습니다.

---

## 1. 샌드박스 툴 사용법

빌드된 실행 파일(`gugugaga.exe`)을 실행하거나, 유니티 에디터에서 `Assets/Scenes/Sandbox.unity` 씬을 열고
**Play**를 누르면 바로 테스트할 수 있습니다.

1. **바이옴 불러오기**: 좌측 상단 **바이옴 불러오기** 버튼으로 바이옴 CSV를 고릅니다(예시: `docs/sample_biomes.csv`).
   불러오기만으로는 지형이 바뀌지 않고, 목록만 메모리에 올라갑니다.
2. **맵 크기 / 습도(맵 타입) 선택**
   - 맵 크기: Tiny 11x11 / Small 14x14 / Normal 16x16 / Large 18x18 / Huge 20x20 / Massive 30x30.
   - 맵 타입: Drylands / Lakes / Continents / Pangea / Archipelago / Waterworld(각각 물 비율과 대륙 모양이 다름).
   - 맵 크기를 바꾸려면 먼저 배치된 유닛을 모두 치워야 합니다(그리드를 통째로 다시 만들기 때문).
3. **지형 생성**: 수도 → 마을 → 지형 → 등대 → 자원 → 유적/불가사리 순으로 채워집니다. 누를 때마다 새 시드를
   뽑으므로 같은 CSV로도 결과가 매번 다릅니다.
4. 구조물 위에 마우스를 올리면(샌드박스) 또는 클릭하면(전투) 이름/설명 패널이 뜹니다.

---

## 2. 반드시 알아야 할 규칙

- **한 칸에 한 값(롱 포맷)**: 바이옴 하나, 타일 엔트리 하나, 구조물 엔트리 하나가 각각 **한 행**입니다. `Kind` 컬럼이
  행 종류(`Biome`/`Tile`/`Structure`), `Biome` 컬럼이 소속 바이옴 Id입니다. 이름 여러 개(인접 배제, 허용 타일)는
  `Exclude1`, `Exclude2` …처럼 번호 붙은 컬럼에 하나씩 적습니다(CLAUDE.md 규칙 6). 행 종류마다 안 쓰는 칸은 비웁니다.
  - 예전 형식(한 행 = 한 바이옴, `Tiles`/`Structures` 칸에 `;`/`:`/`|`로 묶음)도 헤더에 `Tiles`가 있으면 그대로 읽힙니다.
    저장(내보내기)은 항상 새 형식입니다.
- **컬럼은 이름으로 찾습니다**: 순서를 바꾸거나 메모 컬럼을 끼워도 됩니다. `#`으로 시작하는 행은 주석입니다.
- **값이 비거나 잘못돼도 안전**: 숫자 칸이 비었거나 형식이 틀리면 `0`(TerrainType은 `Land`)으로 채워집니다.
  손으로 편집하다 실수해도 불러오기는 항상 성공합니다. 안 쓰는 컬럼은 헤더에서 통째로 빼도 됩니다.
- **NoiseType**: 현재는 `Perlin`만 지원합니다. 비우면 자동으로 `Perlin`.
- **미등록 Id는 조용히 무시**: 아무 `TileId`/`StructureId`나 적을 수 있지만, 색이나 모델로 보이려면 6·7번의 등록된
  값이어야 합니다. 미등록 값은 에러 없이 표시만 안 됩니다(타일은 육지/물 기본색, 구조물은 안 보임).
- **자동 생성되는 것들 — CSV에 적지 않음**: 수도(`Capital`), 등대(`Lighthouse`), 숲/산 타일(`Forest`/`Mountain`,
  `MountainRate`/`ForestRate`로 조절), 깊은 바다 타일(`Ocean`). 맵 타입 전용 마을(Suburb, Pre-terrain 마을,
  Pangea/Continents 본토 마을, 외딴 섬 마을)도 자동입니다.

---

## 3. CSV 컬럼 명세 — `Kind = Biome` 행

`Kind`가 `Biome`인 행이 바이옴 하나입니다. 바이옴은 Polytopia의 "종족"에 해당하며, 바이옴 수 = 수도 수 = 플레이어 수입니다.
그 바이옴의 타일/구조물 규칙은 같은 `Biome` 값을 가진 `Tile`/`Structure` 행들입니다(4·5번).

```
Kind,Biome,Name,NoiseType,Frequency,Octaves,SeedOffset,InnerRadius,MountainRate,ForestRate,Entry,TerrainType,...
Biome,Grassland,평원,Perlin,0.15,3,101,1,1,1,,,...
Tile,Grassland,,,,,,,,,Grass,Land,0.7,0.5,...
Structure,Grassland,,,,,,,,,Ruin,,,,1,0,0,2,0,0,0.34,0,0,0,Grass,Forest,Mountain,Ocean,,Capital,Village,
```

| 컬럼 | 필수 | 의미 | 값 예시 |
|---|---|---|---|
| `Kind` | O | 행 종류 | `Biome` |
| `Biome` | O | 바이옴 고유 식별자(`Biome` 행끼리 중복 불가) | `Grassland` |
| `Name` | O | 표시용 이름 | `평원` |
| `NoiseType` | 선택 | 노이즈 종류 | `Perlin`(비우면 자동) |
| `Frequency` | O | 노이즈 주파수(클수록 잘게 쪼개짐) | `0.15` |
| `Octaves` | O | 노이즈 옥타브 수 | `3` |
| `SeedOffset` | O | 이 바이옴만의 노이즈 시드 오프셋 | `101` |
| `InnerRadius` | O | 가장 가까운 도시(수도 + 지형 전에 정해진 마을)로부터 이 거리 이내를 "Inner"로 봄. 타일 확률(`InnerWeight`/`OuterWeight`)에만 쓰임 | `1`(원문의 "도시에 인접" = 1) |
| `MountainRate` | 선택 | 산 배수. `1`이면 육지의 14%가 `Mountain` | `1`, `1.5`. 비우면 `0` = 산 없음 |
| `ForestRate` | 선택 | 숲 배수. `1`이면 육지의 38%가 `Forest`(산 배수 반영 후 비례 보정) | `1`, `0.5`. 비우면 `0` = 숲 없음 |

> **숲/산 레이어**: `Tile` 행들로 육지를 채운 뒤, 바이옴마다 수도/마을 칸을 뺀 육지 중 정확히
> `산 = 14% × MountainRate`, `숲 = 38% × (100%−산%)/86% × ForestRate`만큼을 `Mountain`/`Forest`로 바꿉니다(확률이
> 아니라 개수 쿼터, 노이즈 순위로 골라 덩어리로 뭉침). 나머지 육지 타일(`Grass`/`Sand`/…)이 "평지"입니다. 그래서
> `Tile` 행에는 `Forest` 엔트리를 따로 넣지 않는 것을 권장합니다.

---

## 4. `Kind = Tile` 행 — 타일 엔트리

| 컬럼 | 필드 | 필수 | 의미 | 값 예시 |
|---|---|---|---|---|
| `Entry` | `TileId` | O | 타일 타입 키 | `Grass`, `Water` — 색 목록은 6번 |
| `TerrainType` | `TerrainType` | O | 이동 판정상 육지/물 | `Land` / `Water`(대소문자 무관, 잘못 적으면 `Land`) |
| `InnerWeight` | `InnerWeight` | O | Inner 영역에서의 배치 확률 계수 | `0.7` |
| `OuterWeight` | `OuterWeight` | O | Outer 영역에서의 배치 확률 계수 | `0.5` |
| `MinCount` | `MinCount` | 선택 | 이 바이옴 영역 안 최소 보장 개수 | `0` |
| `CountPerTiles` | `CountPerTiles` | 선택 | "바이옴 영역 칸 수 / 이 값"(반올림)만큼 최소 개수를 늘림. `MinCount`와 큰 쪽 사용 | `25`, `0`=끔 |
| `MinDistance` | `MinDistance` | 선택 | 같은 `TileId`끼리 최소 거리(체비쇼프) | `2`, `0`=제약 없음 |
| `EdgeMargin` | `EdgeMargin` | 선택 | 맵 가장자리로부터 최소 거리 | `1`, `0`=제약 없음 |
| `Exclude1`..`ExcludeN` | `ExcludeAdjacent` | 선택 | 상하좌우로 인접할 수 없는 `TileId` — 칸마다 하나 | `Water`, 비우면 제약 없음 |

- 가중치는 노이즈로 한 번 더 보정되어 최종 확률이 됩니다(`TerrainGenerationSystem.ComputeWeight`).
- **물 타일 규칙은 Drylands에서만 그대로 쓰입니다.** 나머지 맵 타입은 대륙 모양(랜드마스 마스크)이 육지/바다를 먼저
  정하고, 바다 칸은 그 바이옴의 첫 번째 `Water` 타일로 채웁니다.
- **얕은 물 / 깊은 바다**: 생성이 끝나면, 상하좌우 4방향으로 육지와 맞닿은 물 칸은 바이옴의 물 타일(예: `Water`, 얕은 물)로
  남고, 육지와 닿지 않은 물 칸은 자동으로 `Ocean`(깊은 바다)이 됩니다. 물고기는 얕은 물, 바다 유적은 깊은 바다에
  놓으려면 이 두 Id를 `AllowedTile1..N`에서 구분해 쓰면 됩니다.

---

## 5. `Kind = Structure` 행 — 구조물 엔트리

| 컬럼 | 필드 | 필수 | 의미 | 값 예시 |
|---|---|---|---|---|
| `Entry` | `StructureId` | O | 구조물 키 | 7번 목록(예: `Village`, `Resource_Fruit`, `Ruin`) |
| `AllowedTile1`..`AllowedTileN` | `AllowedTileTypes` | O | 놓일 수 있는 `TileId` — 칸마다 하나. **다 비우면 어디에도 안 놓임**(오타 방지) | `Grass`, `Forest` / `Water`, `Ocean` |
| `Weight` | `Weight` | 선택 | 같은 칸에 같은 Id 엔트리가 여럿일 때의 가중치(자원에는 안 쓰임) | `1` |
| `MinCount` | `MinCount` | 선택 | 최소 개수(바이옴별, 맵 전체로 합산) | `0` |
| `CountPerTiles` | `CountPerTiles` | 선택 | "허용 타일 칸 수 / 이 값"(반올림)만큼 목표를 늘림. 바이옴별로 계산해 맵 전체로 합산 | `25`(물 25칸당 1개) |
| `MinDistance` | `MinDistance` | 선택 | 같은 `StructureId`끼리 최소 거리(체비쇼프, 맵 전체) | `2` = 바로 옆 금지 |
| `EdgeMargin` | `EdgeMargin` | 선택 | 맵 가장자리로부터 최소 거리 | `2` |
| `MaxDistanceFromCity` | `MaxDistanceFromCity` | 선택 | 0보다 크면 가장 가까운 도시(수도/마을)로부터 이 거리 이내에만. 자원은 이 값과 무관하게 항상 2칸 이내 | `0`=제약 없음 |
| `MaxWaterFractionOnLakes` | `MaxWaterFractionOnLakes` | 선택 | **Lakes 맵에서만** 물 위에 놓이는 비율 상한(0~1) | `0.34`, **빈 값** = 제약 없음(`0`은 "물 위 0%"라서 주의) |
| `FillRemaining` | `FillRemaining` | 선택 | `1`이면 목표 개수 없이 자리가 없을 때까지 채움 | `0` / `1` |
| `Exclude1`..`ExcludeN` | `ExcludeAdjacentStructures` | 선택 | **8방향(대각선 포함)**으로 인접할 수 없는 `StructureId` — 칸마다 하나 | `Capital`, `Village` |
| `InnerRate` | `InnerRate` | 선택 | **자원 전용.** 도시 바로 옆(거리 1) 허용 타일 칸 중 이 자원이 되는 비율(0~1) | `0.375` |
| `OuterRate` | `OuterRate` | 선택 | **자원 전용.** 도시에서 거리 2인 허용 타일 칸 중 비율(0~1) | `0.125` |

`InnerRate`/`OuterRate` 컬럼이 없으면 0(= 자원 아님)입니다.

### 5.1 엔트리가 처리되는 단계

엔트리는 성격에 따라 서로 다른 단계에서 배치됩니다. 적는 순서는 상관없고, 단계 순서는 Polytopia 원문을 따릅니다.

| 단계 | 대상 엔트리 | 동작 |
|---|---|---|
| ① 마을 | `StructureId = Village` | 맵 전체를 훑으며 **자리가 없을 때까지** 놓음(Post-terrain 마을). 다른 모든 도시와 **거리 3 이상**(CSV `MinDistance`가 더 크면 그 값) |
| ② 등대 | 자동 | 맵 네 모서리(자원보다 먼저라 항상 4개) |
| ③ 자원 | `InnerRate` 또는 `OuterRate` > 0 | **모든 도시(수도 + 모든 마을)**로부터 거리 1(Inner)/2(Outer)인 칸에서, 칸 수 × 비율만큼 쿼터로 채움 |
| ④ 개수 기반 | 그 외(유적/불가사리 등) | 맵 전체 목표 개수만큼. **`Ruin`은 맵 크기별 고정 개수**(11→4, 14→5, 16→7, 18→9, 20→11, 30→23). CSV에 처음 나온 Id 순서대로 처리 |

- **도시 간격**: 수도/마을은 종류와 상관없이 서로 **대각선 포함 바로 옆 금지(거리 2 이상)**가 최소 규칙이고,
  지형이 다 만들어진 뒤 놓이는 마을(①, 외딴 섬 마을, Pangea/Continents 본토 마을)은 **거리 3 이상**입니다.
- **자원은 도시를 기준으로만 생깁니다.** 수도 옆도 Inner 칸이라 자원이 가장 많이 생기는 곳입니다. 자원 엔트리에는
  `Capital` 인접 배제를 넣지 마세요.

### 5.2 자원 비율(InnerRate/OuterRate) 정하는 법

Polytopia 원문 표(육지 전체 대비 %)를 **그 지형 칸 중 몇 %**로 바꾼 값입니다. 기준값(Luxidoor):

| 자원 | 허용 타일 | InnerRate | OuterRate | 계산 |
|---|---|---|---|---|
| 과일 `Resource_Fruit` | 평지 | 0.375 | 0.125 | 18%/48%, 6%/48% |
| 작물 `Resource_Crop` | 평지 | 0.375 | 0.125 | 18%/48%, 6%/48% |
| 사냥감 `Resource_Animal` | `Forest` | 0.5 | 0.158 | 19%/38%, 6%/38% |
| 광물 `Resource_Metal` | `Mountain` | 0.786 | 0.214 | 11%/14%, 3%/14% |
| 물고기 `Resource_Fish` | 얕은 물(`Water`) | 0.5 | 0.5 | 얕은 물의 50% |

- 같은 타일을 공유하는 자원(과일과 작물은 둘 다 평지)은 **CSV 순서대로** 쿼터를 떼어 갑니다. 두 비율의 합이 1을
  넘으면 뒤쪽 자원이 모자라게 나옵니다.
- **종족 배수 적용**: 자원 배수는 비율에 곱합니다(1 넘으면 1로 자름). 예: Imperius(과일 2배) → 과일 0.75/0.25,
  Bardur(작물 0배) → 작물 엔트리 삭제, Xin-xi(광물 1.5배) → 광물 1.0/0.321. 산/숲 배수는 `MountainRate`/`ForestRate`로.
- 칸 수 × 비율의 소수부는 그 확률로 한 개를 더 놓아서, 작은 묶음에서도 평균 비율이 유지됩니다.

---

## 6. 색으로 표시되는 `TileId`

| `TileId` | 표시 |
|---|---|
| `Grass` | 연두색 |
| `Forest` | 녹색 + 나무 모델(숲/산 레이어가 자동 생성) |
| `Mountain` | 갈회색 + 설산 봉우리 모델(자동 생성) |
| `Sand` | 황토색 |
| `Rock` | 회색 |
| `Snow` | 흰색 |
| `Water` | 파란색(얕은 물) |
| `Ocean` | 짙은 파란색(깊은 바다, 자동 분류) |

등록되지 않은 `TileId`는 `TerrainType` 기본색(육지=회색, 물=파랑)으로 보입니다.

---

## 7. 모델로 표시되는 `StructureId`

| `StructureId` | 모델 |
|---|---|
| `Capital` | 금색 원형 탑 — **CSV에 적지 않음**, 바이옴마다 자동 배치 |
| `Village` | 오두막 두 채 + 좌판 |
| `Resource_Fruit` | 빨간 열매가 달린 초록 덤불 |
| `Resource_Crop` | 흙 두둑 위 황금 밀밭 |
| `Resource_Animal` | 뿔 달린 사슴 |
| `Resource_Metal` | 청록 광맥이 박힌 바위 노두 |
| `Resource_Fish` | 물결 위 금빛 물고기 세 마리 |
| `Lighthouse` | 빨간 띠를 두른 흰 등대 — **CSV에 적지 않음**, 맵 네 모서리에 자동 배치 |
| `Ruin` | 무너진 돌기둥 |
| `Starfish` | 주황 불가사리 |
| `Resource_Food` / `Resource_Ore` | 예전 CSV 호환용 자원 Id — 버섯 군락 / 바위 노두 |

모델은 `StructureAssetSetup`(Editor)이 Unity CLI로 구워 `Assets/Prefabs/Structures`에 저장합니다. 과일·작물·사냥감·
물고기·등대·불가사리는 기성 모델이 없어 저폴리 조각(`ProceduralPropMeshes`)으로 조립한 것입니다.

등록되지 않은 `StructureId`는 그리드 데이터에만 남고 화면에는 안 보입니다.

---

## 8. 예시 — `docs/sample_biomes.csv`의 `Grassland` 행

```
Kind,Biome,Name,NoiseType,Frequency,Octaves,SeedOffset,InnerRadius,MountainRate,ForestRate,Entry,TerrainType,InnerWeight,OuterWeight,Weight,MinCount,CountPerTiles,MinDistance,EdgeMargin,MaxDistanceFromCity,MaxWaterFractionOnLakes,FillRemaining,InnerRate,OuterRate,AllowedTile1,AllowedTile2,AllowedTile3,AllowedTile4,AllowedTile5,Exclude1,Exclude2,Exclude3
Biome,Grassland,평원,Perlin,0.15,3,101,1,1,1,,,,,,,,,,,,,,,,,,,,,,
Tile,Grassland,,,,,,,,,Grass,Land,0.7,0.5,,0,0,0,0,,,,,,,,,,,,,
Tile,Grassland,,,,,,,,,Water,Water,0.2,0.15,,0,25,2,1,,,,,,,,,,,,,
Structure,Grassland,,,,,,,,,Village,,,,1,0,0,3,2,0,,1,0,0,Grass,,,,,,,
Structure,Grassland,,,,,,,,,Resource_Fruit,,,,0,0,0,0,0,0,,0,0.375,0.125,Grass,,,,,,,
Structure,Grassland,,,,,,,,,Resource_Crop,,,,0,0,0,0,0,0,,0,0.375,0.125,Grass,,,,,,,
Structure,Grassland,,,,,,,,,Resource_Animal,,,,0,0,0,0,0,0,,0,0.5,0.158,Forest,,,,,,,
Structure,Grassland,,,,,,,,,Resource_Metal,,,,0,0,0,0,0,0,,0,0.786,0.214,Mountain,,,,,,,
Structure,Grassland,,,,,,,,,Resource_Fish,,,,0,0,0,0,0,0,,0,0.5,0.5,Water,,,,,,,
Structure,Grassland,,,,,,,,,Ruin,,,,1,0,0,2,0,0,0.34,0,0,0,Grass,Forest,Mountain,Ocean,,Capital,Village,
Structure,Grassland,,,,,,,,,Starfish,,,,1,0,25,2,0,0,,0,0,0,Water,Ocean,,,,Capital,Village,Lighthouse
```

- **바이옴**: `InnerRadius=1`(도시 바로 옆이 Inner), `MountainRate=1`, `ForestRate=1`(육지의 14%가 산, 38%가 숲).
- **Tile 행**: `Grass`(평지), `Water`(Drylands에서 25칸당 1개 이상, 물끼리 거리 2, 가장자리 1칸 여백).
- **Structure 행**:
  - `Village` — 평지 위, 도시끼리 거리 3, 가장자리 2칸 여백, 자리가 없을 때까지 채움(`FillRemaining=1`).
  - `Resource_Fruit`/`Resource_Crop` — 평지, 도시 옆 37.5% / 거리 2에서 12.5%씩.
  - `Resource_Animal` — 숲, 50% / 15.8%. `Resource_Metal` — 산, 78.6% / 21.4%. `Resource_Fish` — 얕은 물, 50% / 50%.
  - `Ruin` — 평지/숲/산/깊은 바다, 유적끼리와 도시 옆 금지, Lakes에서는 물 위 최대 1/3. 개수는 맵 크기 표.
  - `Starfish` — 얕은 물/깊은 바다, 물 25칸당 1개, 불가사리끼리와 도시·등대 옆 금지.

`Desert`는 사냥감 비율을 0.2배로(Oumaji처럼), `Highland`는 광물 비율을 1.5배로(Xin-xi처럼) 바꾼 예시입니다.

---

## 9. 참고 문서

- 규칙의 근거(원문 요약 + 이 프로젝트의 해석): [`PolytopiaMapGeneration.md`](PolytopiaMapGeneration.md)
- 파이프라인 요약: [`TerrainGenerationSummary.md`](TerrainGenerationSummary.md)
- 구현: [`BiomeCsvRow.cs`](../Assets/Scripts/TacticsECS/Data/Csv/BiomeCsvRow.cs), [`BiomeCsvSerializer.cs`](../Assets/Scripts/TacticsECS/Systems/Csv/BiomeCsvSerializer.cs), [`TerrainGenerationSystem.cs`](../Assets/Scripts/TacticsECS/Systems/TerrainGenerationSystem.cs), [`StructureGenerationSystem.cs`](../Assets/Scripts/TacticsECS/Systems/StructureGenerationSystem.cs)
- 검증: [`TerrainGenerationVerification.cs`](../Assets/Editor/TerrainGenerationVerification.cs), [`StructureGenerationVerification.cs`](../Assets/Editor/StructureGenerationVerification.cs)
