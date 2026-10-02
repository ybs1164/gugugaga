# 타일·구조물

수치: [`GameRules.csv`](../csv/game-rules.md) `Ruin.*`, `Starfish.*`. 코드: `TileData`, `TileImprovementSystem.Classify`, `RuinSystem`, `Data/StructureDefinition`.

## 타일 값
| 값 | 뜻 |
|---|---|
| `Terrain` (`TerrainType`) | 이동 판정 지형 `Land`/`Water` |
| `TileTypeId` | 세부 타일(바이옴 `Entry`, `Forest`, `Mountain`, `Ocean`) — 생성·표시용 |
| `StructureId` | 칸 위 구조물(아래) |
| `BuildingId`, `HasRoad` | 건물, 도로 — [building](building.md) |
| `OwnerCity`, `OwnerTeam` | 영토 주인. 없으면 중립 |

## 지형
건설·행동 조건에 쓰는 `TileClass`는 저장하지 않고 계산한다:
| `TileClass` | 조건 |
|---|---|
| `Ocean` | 물 + `TileTypeId` = `Ocean`(깊은 바다) |
| `ShallowWater` | 그 밖의 물 |
| `Forest` | 육지 + `TileTypeId` = `Forest` |
| `Mountain` | 육지 + `TileTypeId` = `Mountain` |
| `Field` | 그 밖의 육지 |

물 깊이: 상하좌우에 육지가 있는 물 = 얕은 물, 없으면 깊은 바다 — [map](map.md).

## 구조물
이름·설명 표는 코드 `Data/StructureDefinition.All`(CSV 아님). 배치는 [map](map.md).
| StructureId | 쓰임 |
|---|---|
| `Capital`, `Village` | 도시 자리 — [city](city.md#점령) |
| `Ruin` | 유적 — 아래 |
| `Resource_Fruit`, `Resource_Crop`, `Resource_Animal`, `Resource_Metal`, `Resource_Fish` | 자원 — 건물·타일 행동의 `RequiredStructure` |
| `Lighthouse` | 등대 — [vision](vision.md#등대) |
| `Starfish` | 불가사리 — 아래 |
| `Resource_Food`, `Resource_Ore` | 옛 CSV 호환 Id(과일·광물과 같이 취급) |

## 유적
- 유적 칸에서 턴을 시작한(이동·행동 전) 유닛이 행동을 써서 탐험한다. 유적은 사라지고 보상 하나를 조건이 맞는 후보 중 균등 무작위로 받는다.
- 후보: 별 `Ruin.Stars` / 무료 기술(연구 가능한 기술이 있을 때) / 수도 인구 `Ruin.Population`(수도가 있을 때) / 탐험가(유적 주변 5×5에 구름이 있을 때) / 유닛.
- 유닛 보상: 육지 유적이면 베테랑 `Ruin.NewFriendsUnitId`, 물 위 유적이면 `Ruin.SeaUnitId`를 태운 베테랑 `Ruin.SeaBoatId`. 그 행이 유닛 CSV에 없으면 가장 싼 유닛(베테랑 아님).

## 불가사리
해금 키 `Harvest.Starfish`를 가진 팀의 배가 불가사리 칸에서 턴을 시작하면(이동·행동 전) 인양할 수 있다 — 별 `Starfish.Stars`, 그 유닛의 턴을 쓴다. 중립·적 영토에서도 된다.
