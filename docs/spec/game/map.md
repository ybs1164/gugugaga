# 맵 생성

값: [바이옴 CSV](../csv/biomes.md), [시작 조건 CSV](../csv/tribes.md). 코드: `TerrainGenerationSystem`, `StructureGenerationSystem`, `StartConditionSystem`.
위키 조사 요약: [reference/PolytopiaMapGeneration](../../reference/PolytopiaMapGeneration.md) — 코드 주석의 "원문 N절"은 이 문서의 절 번호다.

## 입력
- 바이옴 CSV, 맵 크기(정사각형 변 길이 — `BattleController.MapSizePresets`), 맵 타입, 시드.
- 같은 입력이면 항상 같은 맵(시드 하나로 지형·구조물 전체 결정).

## 맵 타입
습도 프리셋 이름. 이름과 목표 물 비율은 코드 `BattleController.WetnessPresets`. [시작 조건](../csv/tribes.md) `MapType`도 이 이름을 쓴다.
| 맵 타입 | 땅 모양 |
|---|---|
| `Drylands` | 마스크 없이 타일 확률로 채움 |
| `Lakes` | 노이즈 마스크, 테두리는 육지 |
| `Continents` | 서로 떨어진 대륙을 키움(대륙 사이 물 1칸 이상) |
| `Pangea` | 중앙 대륙 |
| `Archipelago` | 바이옴 수만큼 섬 중심 |
| `Waterworld` | 거의 물, 도시 자리만 육지 |

마스크를 쓰는 타입은 목표 물 비율을 정확히 맞춘다. 수도 주변과 미리 정한 마을 칸은 비율보다 우선해 육지가 된다.

## 단계
1. **수도**: 쿼드런트 맵은 구역마다 하나. `Pangea`/`Continents`는 땅을 만든 뒤 서로 가장 멀게.
2. **지형 전 마을**: 맵 타입에 따라 Suburb·Pre-terrain 마을 위치를 먼저 정한다.
3. **땅 모양**: 위 표.
4. **바이옴 배정**: 칸마다 가장 가까운 수도의 바이옴(Voronoi). 종족이 있으면 그 종족의 `BiomeIndex`.
5. **타일 채우기**: 바이옴 `MinCount`/`CountPerTiles` 먼저, 나머지는 Inner/Outer 가중치 × 노이즈. Inner = 가장 가까운 도시에서 `InnerRadius` 이내.
6. **숲·산 레이어**: 바이옴마다 도시 칸을 뺀 육지에 기준 비율 × `MountainRate`/`ForestRate`만큼 쿼터로.
7. **물 깊이**: 상하좌우에 육지가 있는 물 = 바이옴의 물 타일(얕은 물), 없으면 `Ocean`.
8. **구조물**(`StructureGenerationSystem`, 순서 고정):
   1. 수도 + 지형 전 마을
   2. Post-terrain 마을 — 자리가 없을 때까지. 외딴 섬 마을(`Pangea`/`Continents`/`Waterworld`). `Lakes`는 수도마다 육로로 닿는 마을이 2개 이상이 되도록 육지 다리
   3. 물 깊이 재분류
   4. 등대 — 맵 네 모서리
   5. 자원 — 모든 도시에서 거리 1(Inner)/2(Outer)인 칸을 `InnerRate`/`OuterRate` 쿼터로. 같은 타일을 쓰는 자원은 CSV 순서대로 쿼터를 떼어 간다
   6. 개수 기반(유적·불가사리 등) — CSV 첫 등장 순서대로. 유적 개수는 맵 크기별 고정표(`StructureGenerationSystem.RuinCounts`)
9. **시작 조건**: 종족의 [시작 조건](../csv/tribes.md#startconditionrulescsv--시작-조건-규칙)을 수도마다 적용 — 고리 안 `Target`이 `Count`개 미만이면 빈 칸을 골라 채운다. 수도·마을 칸과 유닛 칸은 건드리지 않는다.

## 간격 규칙
- 모든 도시(수도·마을)는 서로 대각선 포함 바로 옆 금지. 지형이 다 생긴 뒤 놓이는 마을은 더 멀리(코드 상수).
- 구조물 인접 배제(`Exclude{n}`)는 8방향, 타일 인접 배제는 상하좌우.

## 원문과 다른 점
원문 규칙별 구현 대응표(11절)와 이 프로젝트가 정한 보강 규칙(12절): [reference/PolytopiaMapGeneration](../../reference/PolytopiaMapGeneration.md). 여기에 다시 적지 않는다.

## 샌드박스 2단계 생성
습도 탭: 맵 타입·물 비율로 1차 지형(육지/물 + 수도·마을 자리)을 만든 뒤, 같은 모양에 바이옴만 바꿔 채울 수 있다. 조작은 [README](../../../README.md).
