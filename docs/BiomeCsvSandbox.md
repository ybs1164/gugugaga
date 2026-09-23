# CSV 바이옴(지형 생성) 제작 가이드

CSV 파일 편집만으로 맵의 바이옴(지형 타입) 구성과 타일/구조물 생성 규칙을 자유롭게 정의하고,
인게임 샌드박스에서 실제로 지형을 생성해 검증할 수 있는 테스트 도구입니다. [`UnitCsvSandbox.md`](UnitCsvSandbox.md)의
유닛 CSV와 같은 철학(코드를 건드리지 않고 CSV만으로 콘텐츠를 늘림)을 지형 쪽에 적용한 것입니다.

---

## 1. 샌드박스 툴 사용법 (기획자가 할 수 있는 것)

빌드된 실행 파일(`gugugaga.exe`)을 실행하거나, 유니티 에디터에서 `Assets/Scenes/Sandbox.unity` 씬을 열고 **Play**를 누르면 바로 테스트할 수 있습니다.

1. **바이옴 불러오기**: 좌측 상단 **바이옴 불러오기** 버튼을 눌러 테스트할 바이옴 CSV 파일을 선택합니다. (예시 파일: `docs/sample_biomes.csv`)
   - 불러오기만으로는 지형이 바뀌지 않습니다. 목록만 메모리에 올라갑니다.
2. **맵 크기 / 습도 프리셋 선택**:
   - 상단 툴바에서 맵 크기(예: 8x8, 16x16 등)와 습도 프리셋(Drylands/Pangea/Lakes/Continents/Archipelago/Waterworld 등)을 고릅니다.
   - 맵 크기를 바꾸려면 먼저 배치된 유닛을 모두 치워야 합니다(그리드를 통째로 다시 만들기 때문).
3. **지형 생성**:
   - **지형 생성** 버튼을 누르면 불러온 바이옴 CSV 규칙에 따라 타일과 구조물(수도/유적/자원/불가사리/마을)이 절차적으로 채워집니다.
   - 매번 새 시드를 뽑으므로, 같은 CSV로도 누를 때마다 다른 결과가 나옵니다.
   - 같은 바이옴 목록을 유지한 채 여러 번 눌러 결과를 비교해볼 수 있습니다(불러오기와 생성이 분리되어 있음).

---

## 2. 기획자가 알아야 할 필수 규칙

- **구분자 3단계**: CSV 컬럼 구분자는 쉼표(`,`)이므로, 그 안에 여러 값을 더 담아야 하는 `Tiles`/`Structures` 컬럼은 자체 구분자 체계를 씁니다.
  - 세미콜론(`;`) = 엔트리(타일 하나/구조물 하나) 구분
  - 콜론(`:`) = 한 엔트리 안의 필드 구분
  - 파이프(`|`) = 필드 안에서 여러 이름을 나열할 때(예: 인접 배제 목록) 구분
  - 콤마는 `Tiles`/`Structures` 값 안에 절대 쓰지 않습니다(CSV 컬럼이 깨짐).
- **값이 비거나 잘못돼도 안전**: 숫자 컬럼이 비어있거나 형식이 틀리면 예외 없이 `0`(정수/실수) 또는 `Land`(TerrainType)로 채워집니다. 손으로 편집하다 실수해도 불러오기 자체는 항상 성공합니다.
- **NoiseType**: 현재는 `Perlin`만 실제로 지원됩니다. 값을 비워두면 자동으로 `Perlin`이 채워집니다.
- **미등록 Id는 조용히 무시됨**: `TileTypeId`/`StructureId`에 뭘 적어도 파싱은 항상 성공하지만, 화면에 색이나 모델로 보이려면 아래 4번/5번의 **등록된 값**이어야 합니다. 등록되지 않은 값은 에러 없이 그냥 표시만 안 됩니다(타일은 육지/물 기본색으로 폴백, 구조물은 아예 안 보임).

---

## 3. CSV 컬럼 명세서 (최상위)

한 행(Row)이 하나의 바이옴을 나타냅니다. 엑셀이나 텍스트 에디터로 열어 편집할 수 있습니다.

| 컬럼 | 필수 | 의미 | 설정 가능한 값 예시 |
|---|---|---|---|
| `Id` | O | 바이옴 고유 식별자 (다른 행과 중복 불가) | 영문 (예: `Grassland`, `Desert`) |
| `Name` | O | 바이옴 이름 (표시용) | 자유 텍스트 (예: `평원`, `사막`) |
| `NoiseType` | 선택 | 지형 분포에 쓸 노이즈 종류 | 현재는 `Perlin`만 지원(비우면 자동으로 `Perlin`) |
| `Frequency` | O | 노이즈 주파수 (클수록 지형이 잘게 쪼개짐) | 0보다 큰 실수 (예: `0.15`) |
| `Octaves` | O | 노이즈 옥타브 수 (디테일 레이어 수) | 자연수 (예: `2`, `3`) |
| `SeedOffset` | O | 이 바이옴만의 노이즈 시드 오프셋 (다른 바이옴과 겹치지 않게) | 정수 (예: `101`) |
| `InnerRadius` | O | 바이옴 앵커(수도 자리) 기준 "Inner" 반경(체비쇼프 거리). 이 안쪽/바깥쪽에 따라 타일 확률이 달라짐 | 자연수 (예: `3`) |
| `Tiles` | O | 이 바이옴에 속한 타일 타입들의 생성 규칙 목록 | `;`로 구분된 타일 엔트리 (아래 4번 참고) |
| `Structures` | 선택 | 이 바이옴에 얹히는 구조물 생성 규칙 목록 (수도 제외) | `;`로 구분된 구조물 엔트리 (아래 5번 참고) |
| `MountainRate` | 선택 | 산 스폰 배수(Polytopia 종족 배수와 같은 의미). `1`이면 육지의 14%가 `Mountain` 타일 | 0 이상 실수 (예: `1`, `1.5`). 비우면 `0` = 산 없음 |
| `ForestRate` | 선택 | 숲 스폰 배수. `1`이면 육지의 38%가 `Forest` 타일(산 배수를 먼저 반영해 비례 보정) | 0 이상 실수 (예: `1`, `0.5`). 비우면 `0` = 숲 없음 |

> **숲/산 레이어**: `Tiles`로 육지를 채운 뒤, 바이옴 영역마다 수도/마을 칸을 뺀 육지 중 정확히
> `산 = 14% × MountainRate`, `숲 = 38% × (100%−산%)/86% × ForestRate` 만큼을 `Mountain`/`Forest` 타일로 바꿉니다
> (확률이 아니라 개수 쿼터). 노이즈 순위로 골라서 산맥/숲 덩어리로 뭉쳐 나옵니다. 나머지 육지 타일(`Grass`/`Sand`/…)이
> "평지" 역할입니다. 따라서 `Tiles`에는 `Forest` 엔트리를 따로 넣지 않는 것을 권장합니다. 두 컬럼이 없는 예전 CSV도
> 그대로 불러와지며(레이어 꺼짐), 산/숲 칸은 `Structures`의 `AllowedTileTypes`에 `Mountain`/`Forest`로 지정할 수 있습니다
> (예: 광물은 `Mountain`, 식량은 `Grass|Forest`).

---

## 4. `Tiles` 컬럼 — 타일 엔트리 명세

`Tiles` 컬럼 하나에 이 바이옴의 타일 타입 전부를 세미콜론(`;`)으로 나열합니다. 엔트리 하나의 필드 순서는 콜론(`:`)으로 구분되며 다음과 같습니다.

```
TileId:TerrainType:InnerWeight:OuterWeight:MinCount:CountPerTiles:MinDistance:EdgeMargin:ExcludeAdjacent
```

| 순서 | 필드 | 필수 | 의미 | 값 예시 |
|---|---|---|---|---|
| 1 | `TileId` | O | 타일 타입 키 (표시 색/식별용) | 자유 텍스트 (예: `Grass`, `Forest`, `Water`) — 색상 등록 목록은 6번 참고 |
| 2 | `TerrainType` | O | 이동 판정상 육지/물 | `Land` 또는 `Water` (대소문자 무관, 잘못 적으면 `Land`로 폴백) |
| 3 | `InnerWeight` | O | Inner 영역(바이옴 앵커 `InnerRadius` 이내)에서의 기본 배치 확률 계수 | 0 이상 실수 (예: `0.7`) |
| 4 | `OuterWeight` | O | Outer 영역(그 밖)에서의 기본 배치 확률 계수 | 0 이상 실수 (예: `0.5`) |
| 5 | `MinCount` | 선택 | 이 바이옴 영역 안에 최소 보장할 개수 | 0 이상 정수 (예: `0`, `15`) |
| 6 | `CountPerTiles` | 선택 | 0보다 크면 "바이옴 영역 칸 수 / 이 값" 만큼(반올림) 최소 개수를 자동으로 늘림. `MinCount`와 비교해 더 큰 쪽을 씀 | 0 이상 실수, `0`이면 비활성 |
| 7 | `MinDistance` | 선택 | 같은 `TileId`끼리 유지해야 하는 최소 거리(체비쇼프) | 0 이상 정수, `0`이면 제약 없음 |
| 8 | `EdgeMargin` | 선택 | 맵 가장자리로부터 최소 이 거리 이상 떨어진 칸에만 배치 가능 | 0 이상 정수, `0`이면 제약 없음 |
| 9 | `ExcludeAdjacent` | 선택 | 상하좌우로 인접할 수 없는 다른 `TileId` 목록 | 파이프(`\|`)로 구분 (예: `Water`), 비우면 제약 없음 |

> *참고: 노이즈로 한 번 더 보정된 값이 최종 확률에 반영됩니다(`InnerWeight`/`OuterWeight`가 곧 최종 확률은 아님) — 자세한 계산식은 `TerrainGenerationSystem.ComputeWeight`(코드) 참고.*

---

## 5. `Structures` 컬럼 — 구조물 엔트리 명세

`Structures` 컬럼도 같은 구분자 체계(`;`/`:`/`\|`)를 재사용합니다. **수도(`Capital`)는 이 목록에 넣지 않습니다** —
바이옴 앵커 위치에 자동으로 배치됩니다. 엔트리 하나의 필드 순서는 다음과 같습니다.

```
StructureId:AllowedTileTypes:Weight:MinCount:CountPerTiles:MinDistance:EdgeMargin:MaxDistanceFromAnchor:MaxWaterFraction:FillRemaining:ExcludeAdjacentStructures
```

| 순서 | 필드 | 필수 | 의미 | 값 예시 |
|---|---|---|---|---|
| 1 | `StructureId` | O | 구조물 키 (표시용 프리팹 매칭) | 등록 목록은 7번 참고 (예: `Ruin`, `Resource_Food`) |
| 2 | `AllowedTileTypes` | O | 이 구조물이 놓일 수 있는 `TileId` 목록. **비우면 어떤 타일에도 놓이지 않음**(오타 방지용 — "전체 허용"을 원하면 그 바이옴의 모든 `TileId`를 직접 나열해야 함) | 파이프(`\|`)로 구분 (예: `Grass\|Forest`) |
| 3 | `Weight` | O | 배치 확률 가중치 | 0 이상 실수 (예: `0.1`) |
| 4 | `MinCount` | 선택 | 최소 보장 개수 | 0 이상 정수 |
| 5 | `CountPerTiles` | 선택 | 0보다 크면 "허용 타일 칸 수 / 이 값" 만큼(반올림) 목표 개수를 늘림. `MinCount`와 비교해 더 큰 쪽을 씀 | 0 이상 실수, `0`이면 비활성 |
| 6 | `MinDistance` | 선택 | 같은 `StructureId`끼리 유지해야 하는 최소 거리(체비쇼프) | 0 이상 정수, `0`이면 제약 없음 |
| 7 | `EdgeMargin` | 선택 | 맵 가장자리로부터 최소 이 거리 | 0 이상 정수, `0`이면 제약 없음 |
| 8 | `MaxDistanceFromAnchor` | 선택 | 0보다 크면 바이옴 앵커(수도 자리)로부터 이 거리(체비쇼프) 이내에만 배치 가능 | 0 이상 정수, `0`이면 제약 없음 |
| 9 | `MaxWaterFraction` | 선택 | 이 구조물 중 물(`Water`) 타일에 배치되는 비율의 상한(0~1) | 실수 또는 **빈 값**(= 제약 없음). `0`을 넣으면 "무조건 0%만 허용"이 되므로 주의 |
| 10 | `FillRemaining` | 선택 | `1`이면 `MinCount`/`CountPerTiles` 목표를 무시하고, 제약을 만족하는 칸이 남지 않을 때까지 계속 배치 | `0` 또는 `1` |
| 11 | `ExcludeAdjacentStructures` | 선택 | 상하좌우로 인접할 수 없는 다른 `StructureId` 목록 (예: 유적/자원이 수도 바로 옆에 못 놓이게) | 파이프(`\|`)로 구분 (예: `Capital`) |

> **도시 간격 고정 규칙**: `Village`는 CSV의 `MinDistance`와 상관없이 **모든 수도/마을(다른 바이옴 영역 포함)과
> 체비쇼프 거리 2 이상**(대각선 포함 인접 금지)을 항상 지킵니다. `MinDistance`가 더 크면 그 값을 씁니다.
> `MinDistance` 검사는 이제 바이옴 영역 안이 아니라 맵 전체 기준입니다.

---

## 6. 화면에 색으로 표시되는 `TileId` 목록

`Tiles` 엔트리의 `TileId`에 아무 문자열이나 적을 수 있지만, 아래 7개 키만 고유 색으로 표시됩니다.
등록되지 않은 `TileId`는 에러 없이 그냥 `TerrainType` 기본색(육지=회색, 물=파랑)으로 폴백합니다.

| `TileId` | 표시 색 |
|---|---|
| `Grass` | 연두색 |
| `Forest` | 녹색 + 나무 3그루 모델 (숲/산 레이어가 자동 생성) |
| `Mountain` | 갈회색 + 설산 봉우리 모델 (숲/산 레이어가 자동 생성) |
| `Sand` | 황토색 |
| `Rock` | 회색 |
| `Snow` | 흰색 |
| `Water` | 파란색 |

---

## 7. 화면에 모델로 표시되는 `StructureId` 목록

`Structures` 엔트리의 `StructureId`도 아무 문자열이나 적을 수 있지만, 아래 목록만 실제 3D 모델(프리팹)이 인스턴스화됩니다.
등록되지 않은 `StructureId`는 에러 없이 그냥 안 보입니다(그리드 데이터에는 남아있음).

| `StructureId` | 비고 |
|---|---|
| `Capital` | **CSV에 직접 적지 않음** — 각 바이옴 앵커에 자동 배치 |
| `Ruin` | 유적 |
| `Resource_Food` | 식량 자원 |
| `Resource_Ore` | 광물 자원 |
| `Starfish` | 불가사리 (주로 물 위) |
| `Village` | 마을 |

---

## 8. 예시 (`docs/sample_biomes.csv`의 `Grassland` 행 풀어보기)

```
Grassland,평원,Perlin,0.15,3,101,3,Grass:Land:0.7:0.5:0:0:0:0:;Water:Water:0.2:0.15:0:25:2:1:,Ruin:Grass|Forest|Mountain:0.1:1:40:5:2:0:1:0:Capital;Resource_Food:Grass|Forest:0.3:1:10:2:0:2:1:0:Capital;Resource_Ore:Mountain:0.3:0:10:2:0:2:1:0:Capital;Starfish:Water:0.2:1:25:2:0:0:1:0:Capital;Village:Grass:0.15:0:0:3:2:0:1:1:Capital|Ruin,1,1
```

- **바이옴**: `Id=Grassland`, `Name=평원`, `Perlin` 노이즈, `Frequency=0.15`, `Octaves=3`, `SeedOffset=101`, `InnerRadius=3`,
  `MountainRate=1`, `ForestRate=1`(Polytopia 기준값 — 육지의 14%가 산, 38%가 숲).
- **Tiles** (2개 타일 엔트리 — 숲/산은 레이어가 만들므로 넣지 않음):
  - `Grass:Land:0.7:0.5:0:0:0:0:` — 육지(평지), Inner 확률 0.7 / Outer 확률 0.5, 최소 개수 제약 없음, 인접 배제 없음.
  - `Water:Water:0.2:0.15:0:25:2:1:` — 물, 25칸당 1개 이상, 물끼리 최소 거리 2, 가장자리에서 최소 1칸 이상 떨어짐.
- **Structures** (5개 구조물 엔트리, 전부 `Capital`과는 인접 불가):
  - `Ruin`: 평지/숲/산 위, 가중치 0.1, 최소 40칸당 1개, 유적끼리 최소 거리 5, 가장자리 2칸 이상.
  - `Resource_Food`: 평지/숲 위, 가중치 0.3, 10칸당 1개, 최소 거리 2, 앵커(수도)로부터 최대 2칸 이내.
  - `Resource_Ore`: 산 위에만, 가중치 0.3, 산 10칸당 1개, 앵커로부터 최대 2칸 이내.
  - `Starfish`: 물 위에만, 가중치 0.2, 25칸당 1개.
  - `Village`: 평지(`Grass`) 위에만, 가중치 0.15, 다른 모든 도시와 최소 거리 3, 가장자리 2칸 이상, `Ruin`과도 인접 불가, `FillRemaining=1`이라 자리가 남는 한 계속 채움.

---

## 9. 참고 문서

- 규칙의 원본 설계 근거: [`docs/PolytopiaMapGeneration.md`](PolytopiaMapGeneration.md)
- 구현 코드: [`BiomeCsvRow.cs`](../Assets/Scripts/TacticsECS/Data/Csv/BiomeCsvRow.cs), [`BiomeCsvSerializer.cs`](../Assets/Scripts/TacticsECS/Systems/Csv/BiomeCsvSerializer.cs), [`TerrainGenerationSystem.cs`](../Assets/Scripts/TacticsECS/Systems/TerrainGenerationSystem.cs), [`StructureGenerationSystem.cs`](../Assets/Scripts/TacticsECS/Systems/StructureGenerationSystem.cs)
- 왕복 파싱/생성 검증: [`TerrainGenerationVerification.cs`](../Assets/Editor/TerrainGenerationVerification.cs), [`StructureGenerationVerification.cs`](../Assets/Editor/StructureGenerationVerification.cs)
