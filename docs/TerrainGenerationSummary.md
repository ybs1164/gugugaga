# 절차적 지형 생성 로직 요약

`TerrainGenerationSystem`(타일) + `StructureGenerationSystem`(타일 위 구조물)이 바이옴 CSV 규칙에
따라 `GridWorld`를 채우는 파이프라인 전체를 요약한다. Polytopia 맵 생성 규칙([docs/PolytopiaMapGeneration.md](PolytopiaMapGeneration.md))을
이 프로젝트에 맞게 채택한 것이며, `Sandbox` 씬의 "바이옴 불러오기 → 맵 크기/습도 선택 → 지형 생성"
버튼(`BattleController.HandleGenerateTerrain`)이 진입점이다.

## 1. 데이터: 바이옴 CSV

한 행(`BiomeCsvRow`)이 바이옴 하나. 노이즈 설정(`NoiseType`/`Frequency`/`Octaves`/`SeedOffset`) +
`InnerRadius`(앵커 기준 Inner/Outer 경계) + 타일 규칙 목록(`Tiles`) + 구조물 규칙 목록(`Structures`)로
구성된다. `BiomeCsvSerializer`가 세미콜론(엔트리 구분)/콜론(필드 구분)/파이프(목록 구분) 3단계
구분자로 CSV 컬럼 하나에 여러 규칙을 욱여넣는다(예시: [docs/sample_biomes.csv](sample_biomes.csv)).

**타일 규칙(`BiomeTileEntry`)**: `TileId` + `TerrainType`(Land/Water, 이동 판정에 쓰이는 유일한 값) +
`InnerWeight`/`OuterWeight`(앵커 기준 확률 계수) + `MinCount`/`CountPerTiles`(최소 개수) +
`MinDistance`/`EdgeMargin`(거리 제약) + `ExcludeAdjacent`(인접 배제 타입 목록).

**구조물 규칙(`BiomeStructureEntry`)**: 타일 규칙과 같은 제약 어휘를 재사용하되, `TerrainType` 대신
`AllowedTileTypes`(어떤 `TileTypeId` 위에만 놓일 수 있는지)를 갖는다. 추가로 `MaxDistanceFromAnchor`
(수도로부터 최대 거리), `MaxWaterFraction`(물 위 배치 비율 상한, `float?` — 비우면 제약 없음),
`FillRemaining`(개수 제한 없이 자리가 남는 한 채움), `ExcludeAdjacentStructures`(다른 구조물과 인접
배제)를 갖는다.

## 2. 타일 생성 파이프라인 (`TerrainGenerationSystem.Generate`)

```
Generate(grid, biomes, seed, wetnessMultiplier, shapeMode, targetWaterFraction)
```

`shapeMode`가 `Freeform`(기본값)이면 **자유 배치 경로**, 그 외(`Pangea`/`Lakes`/`Continents`/
`Archipelago`/`Waterworld`)면 **랜드마스 마스크 경로**를 탄다. 두 경로 모두 바이옴별 앵커
(`Vector2Int[]`)를 반환해 `StructureGenerationSystem`이 재사용한다.

### 2-1. 자유 배치 경로 (Freeform, "Drylands" 포함)

1. **쿼드런트 앵커 배치** (`GenerateQuadrantAnchors`) — 바이옴 수만큼 `ceil(sqrt(N))`×`ceil(sqrt(N))`
   구역으로 맵을 나누고, 구역을 셔플해 바이옴마다 서로 다른 구역 안에서 랜덤 앵커를 하나씩 뽑는다
   (완전 랜덤 시드 대신 맵 전체에 고르게 퍼지도록 하는 공정성 장치 — Polytopia의 수도 배치와 같은 원리).
2. **Voronoi 배정** (`ComputeBiomeIndexPerCell`) — 모든 칸을 유클리드 거리로 가장 가까운 앵커의
   바이옴에 배정한다.
3. **습도 배율 적용** (`ApplyWetness`, `wetnessMultiplier ≠ 1`일 때만) — Water 타입 엔트리의
   `InnerWeight`/`OuterWeight`에는 배율을 곱하고 `CountPerTiles`는 나눈 임시 복제본을 만든다(원본 CSV
   불변). Drylands(0.05)/Continents(0.55, 기준 1.0배) 같은 낮은~중간 습도만 이 경로로 충분하다.
4. **최소 개수 쿼터 우선 배치** (`PlaceMinCountQuota`) — 엔트리별 목표 개수(`EffectiveMinCount` =
   `max(MinCount, round(바이옴 영역 칸 수 / CountPerTiles))`)만큼, 그 바이옴 영역의 빈 칸을 셔플한
   순서로 훑어 제약(`EdgeMargin`/`MinDistance`/`ExcludeAdjacent`)을 만족하는 칸에 채운다.
5. **나머지 채우기** (`FillRemaining`) — 아직 빈 칸을 셔플한 순서로 훑어, 그 칸에서 제약을 만족하는
   엔트리들 중 **Inner/Outer 가중치 × 노이즈**(`ComputeWeight`)로 가중 랜덤 선택해 채운다. 노이즈는
   엔트리마다 `SeedOffset + entryIndex*997`로 독립된 필드를 써서 타입별로 자연스러운 패치가 생기게
   한다. 후보가 하나도 안 남으면(제약이 전부 막혔으면) 그 바이옴 첫 엔트리로 폴백한다.

### 2-2. 랜드마스 마스크 경로 (`GenerateWithShape`, Pangea/Lakes/Continents/Archipelago/Waterworld)

개별 타일 확률 + `MinDistance`만으로는 "중앙 대륙", "여러 대륙", "호수", "흩어진 섬", "거의 전부 물"
같은 **공간적 모양**을 만들 수 없다(특히 Water 타일의 `MinDistance`가 물끼리 인접을 막아버려서 바다가
절대 하나로 이어질 수 없었다 — 5차 재정비에서 발견한 근본 원인). 그래서 이 5종은 모양 자체를 먼저
"마스크"로 확정한 뒤 그 위에 기존 알고리즘을 얹는다.

1. **랜드마스 마스크 생성** (`GenerateMapShapeLandMask`) — 칸마다 "육지 점수"를 매긴다:
   - 중심점(들)로부터의 거리 기반 방사형 감쇠 값 + Perlin 노이즈를, 프리셋별 `RadialWeight`로 혼합.
   - 점수 내림차순 정렬 후 상위 `(1 - targetWaterFraction)` 비율만큼을 육지로 확정(임계값을 눈대중
     으로 맞추는 대신 순위 컷으로 목표 물 비율에 정확히 맞춘다).
   - `ForceBorderLand`(Lakes)면 맨 바깥 테두리 칸에, `ProtectAnchors`(Waterworld)면 앵커 주변 칸에
     점수 보너스를 더해 순위 컷에서도 거의 항상 살아남게(육지로) 만든다.
2. **앵커를 육지로 스냅** (`SnapAnchorsToLand`) — 쿼드런트로 뽑은 원래 앵커가 물 칸에 떨어지면 가장
   가까운 육지 칸으로 옮긴다(완전히 바다인 구역에 수도가 놓이는 것을 방지).
3. **물 칸 직접 채움** (`ApplyLandmassMask`) — 마스크가 물인 칸은 그 칸이 속한 바이옴의 물 타일로
   `MinDistance` 등 개별 제약을 건너뛰고 바로 채운다 — 이래야 바다가 실제로 하나로 이어진다.
4. **육지 칸은 기존 알고리즘 재사용** — 물 타일 엔트리를 뺀 바이옴 목록(`StripWaterTiles`)으로 위
   2-1의 3~5단계(`PlaceMinCountQuota`/`FillRemaining`)를 그대로 실행한다(마스크가 이미 육지/바다를
   정했으므로 이중으로 물이 섞이지 않도록).

**프리셋별 마스크 파라미터(`MapShapeParams`)**:

| 프리셋 | 중심점 개수 | 방사형 비중 | 가장자리 강제 육지 | 앵커 보호 | 모양 특징 |
|---|---|---|---|---|---|
| Pangea | 1(맵 정중앙) | 0.65 | - | - | 중앙 대륙 + 외곽 바다 |
| Lakes | 0(순수 노이즈) | - | O | - | 흩어진 호수 + 테두리는 항상 육지 |
| Continents | 바이옴 수만큼(쿼드런트) | 0.6 | - | - | 바이옴별로 뚜렷이 분리된 대륙 여러 개 |
| Archipelago | 바이옴 수만큼(쿼드런트) | 0.25 | - | - | 노이즈 비중이 높아 조각조각 흩어진 섬 |
| Waterworld | 바이옴 수만큼(쿼드런트) | 0.5 | - | O(반경 1) | 거의 전부 물, 수도 주변만 작은 육지 |

Drylands는 목표 물 비율이 0~10%로 낮아 마스크 없이도 충분해 `Freeform` 그대로 쓴다.

## 3. 구조물 생성 파이프라인 (`StructureGenerationSystem.Generate`)

타일 생성이 끝난 뒤 `TerrainGenerationSystem.Generate`가 반환한 앵커를 그대로 넘겨받아 실행한다.
구조물(`GridWorld.StructureId`)은 순수 시각 요소로, 이동/점유 판정에는 관여하지 않는다.

1. **수도 자동 배치** — 바이옴 앵커 칸마다 `Capital`을 배치(CSV 엔트리가 아니라 앵커 자체가 곧
   그 바이옴의 중심점이라는 의미를 이미 갖고 있어서 규칙 없이 그대로 재사용).
2. **바이옴별 구조물 배치** (`PlaceBiomeStructures`) — CSV `Structures` 엔트리마다:
   - 목표 개수를 `AllowedTileTypes`에 해당하는 칸 수 기준으로 계산(`EffectiveMinCount` 재사용 —
     예: Starfish는 Water 타일 수 기준 "N칸당 1개").
   - 그 바이옴 영역의 빈 칸(구조물 없음/미점유/지형 채워짐)을 셔플한 순서로 훑으며, 각 칸에서
     `AllowedTileTypes`/`EdgeMargin`/`MinDistance`/`MaxDistanceFromAnchor`/`MaxWaterFraction`/
     `ExcludeAdjacentStructures`를 만족하고 아직 목표를 채우지 못한(또는 `FillRemaining`인) 엔트리들
     중 `Weight` 비례로 하나를 뽑아 배치 — 여러 엔트리가 같은 타일 타입을 두고 경쟁할 수 있어서
     (예: Ruin과 Resource_Food가 둘 다 Grass에 놓일 수 있음) 이 구조로 `Weight`가 실제 승률에 반영되게
     했다.
3. **외딴 섬 마을** (`PlaceTinyIslandVillages`) — 4방향 이웃이 전부 물인 고립된 물 칸을 찾아 그 칸을
   해당 바이옴의 첫 육지 타일로 바꾸고 `Village`를 배치한다. 개수는 맵 크기(한 변 길이)별 고정
   표(`TinyIslandCounts`: 11→0, 14→1, 16→2, 18→3, 20→4, 30→9)를 따른다.

## 4. 공용 헬퍼 (`ProceduralGenerationUtil`)

두 시스템이 공유하는 순수 함수 모음: `Shuffle`(Fisher–Yates), `ChebyshevDistance`(모든 거리 제약의
기준), `DistanceToEdge`(가장자리 여백 판정), `NearestAnchorIndex`(Voronoi 배정), `WeightedPick`
(가중치 비례 랜덤 선택, 가중치 합 0이면 균등 랜덤 폴백).

노이즈는 `NoiseSystem.Sample(x, y, frequency, octaves, seedOffset)` 하나 — 옥타브를 누적한 프랙탈
Perlin 노이즈를 0~1로 정규화해 반환하는 결정론적 순수 함수다. 같은 인자면 항상 같은 값을 내므로,
전체 파이프라인은 같은 `seed`에 대해 완전히 재현 가능하다(시드 결정론은
`TerrainGenerationVerification`의 여러 테스트가 검증).

## 5. Sandbox UI 연동 (`BattleController`)

- **맵 크기**(`MapSizePresets`): Tiny(11)/Small(14)/Normal(16)/Large(18)/Huge(20)/Massive(30) 중 순환
  선택. 유닛이 이미 배치된 상태면 그리드 재생성을 막는다(`RebuildGridForSize`).
- **습도**(`WetnessPresets`): Drylands(0.05)/Lakes(0.275)/Continents(0.55, 기준)/Pangea(0.50)/
  Archipelago(0.70)/Waterworld(0.95). `ResolveShapeMode`가 프리셋 이름을 `MapShapeMode`로 매핑하고
  (Drylands만 `Freeform`), `wetnessMultiplier`는 선택값을 Continents 기준값으로 나눠서 구한다.
- `HandleGenerateTerrain`이 `TerrainGenerationSystem.Generate` → `_gridView.RefreshTerrain` →
  `StructureGenerationSystem.Generate` → `_gridView.RefreshStructures` 순으로 호출한다.

## 6. 검증

`Assets/Editor/TerrainGenerationVerification.cs` / `StructureGenerationVerification.cs`가 Unity CLI
배치모드(`-executeMethod ....Run`)로 실행되는 자동 테스트다 — CSV 왕복, 제약(EdgeMargin/MinDistance/
ExcludeAdjacent) 준수, `CountPerTiles` 밀도, 쿼드런트 공정 배치, 습도 배율 효과, 시드 결정론,
프리셋별 목표 물 비율·모양 특징(Lakes 테두리 육지, Waterworld 앵커 보호, Continents/Archipelago
파편화 정도 비교)까지 PASS/FAIL로 확인한다.
