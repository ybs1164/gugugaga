> **이력 문서** — 작성 당시 기록이며 갱신하지 않는다. 현재 동작·형식은 [`docs/spec`](../spec/README.md).

# 건물 기능 구현 계획 + 적 시뮬레이션

작성: 2026-09-27. 기준: [Polytopia Wiki — Buildings](https://polytopia.fandom.com/wiki/Buildings),
[Roads](https://polytopia.fandom.com/wiki/Roads_(Building)), [Bridge](https://polytopia.fandom.com/wiki/Bridge),
[Port](https://polytopia.fandom.com/wiki/Port), [Temples](https://polytopia.fandom.com/wiki/Temple),
[Embassy](https://polytopia.fandom.com/wiki/Embassy), [Movement](https://polytopia.fandom.com/wiki/Movement)
(MediaWiki API로 받은 원문 위키텍스트와 대조).

> **구현 결과 (2026-09-27)** — 아래 계획을 실행했다. 결정: 시야 시스템 포함, 신전은 점수만, 위키에 있는데 없던 규칙도 가능한 한
> 추가. 그래서 7단계(약탈)는 위키 규칙이 아니라 하지 않았고, 6단계(대사관)는 외교가 없어 계속 보류. 계획과 달라진 점:
> 신전은 신앙과 엮지 않고 점수만(4단계 제안 폐기), 신의 눈은 "등대 도달" 대체 대신 실제 시야 시스템으로 판정, 위키 Movement의
> 험지 정지/Zone of Control/구름 진입 불가와 City Connections의 해로 5칸 제한(Port 문서의 4칸 대신)·깊은 바다 조건, Ruins의
> 탐험 조건/탐험가 보상, Lighthouse의 인구 +1을 추가로 넣었다. 코드와 검증 목록은 README의
> "건물 기능 · 시야 · 적 시뮬레이션" 절.
>
> | 단계 | 상태 |
> |---|---|
> | 1 도로 이동 | ✅ `PathfindingSystem` (다익스트라, 험지/ZoC/구름 포함) |
> | 2 다리 | ✅ `BuildingDefinition.Bridge`, `PathfindingSystem.CanEnter`, `CitySystem.RefreshConnections` |
> | 3 항구 보강 | ✅ `EmbarkSystem`, `NavalUnitDefinition`, 해로 5칸 |
> | 4 점수/신전 | ✅ `ScoreSystem`, `TileData.BuildingTurn` (신앙 연동 없음) |
> | 5 기념물 | ✅ `TaskSystem`, `TaskDefinition` (7종, 신의 눈은 시야 기반) |
> | 6 대사관 | ⏸ 보류 (외교 없음) |
> | 7 약탈 | ✖ 위키 규칙이 아니라 제외 |
> | 2-1/2-2 AI | ✅ `EconomyAI`, `EnemyAI` (팀 인자화) |
> | 2-3 하네스 | ✅ `UnitFactorySystem`, `Assets/Editor/EconomySimulation.cs` |

---

## 0. 현황 — 위키 대비 무엇이 되어 있고 무엇이 비었나 (계획 당시)

| 건물 | 위키 효과 | 현재 구현 | 빈 부분 |
|---|---|---|---|
| 벌목장/농장/광산 | 인구 +1/+2/+2 | ✅ | — |
| 제재소/풍차/대장간 | 인접 기반 건물당 인구 | ✅ | — |
| 시장 | 인접 가공 건물 레벨당 별 1 (최대 8) | ✅ | — |
| 항구 | 인구 +1, **육지 유닛 → 뗏목(Raft)**, 물 4칸 이내 연결 | 인구 +1, 연결(거리 무제한) | 뗏목 승선, 연결 거리 4칸 제한 |
| 도로 | 수도 연결 + **도로 사이 이동 비용 0.5** | 연결만 | 이동 보너스 |
| 다리 | 물 1칸 위 도로 역할, 육지 유닛 통과, 파괴 가능 | ❌ | 전부 |
| 신전 4종 | 인구 +1, 점수 100 → 2턴마다 +50 (5레벨) | 인구 +1, 신앙 최대치 +5 | 신전 레벨, 점수 |
| 기념물 7종 | 과업 달성 시 무료, 인구 +3, 점수 400 | ❌ | 과업 추적 + 건설 |
| 대사관 | 평화 상태 상대 수도에 건설, 양쪽 수입 +2 | ❌ | 외교(평화) 시스템 필요 |
| 도시 보상(공방/성벽/공원) | — | ✅ | — |

부수 사항: 점수(Score) 시스템 없음, 적 AI는 도로/시장/다리/항구 승선을 쓰지 않음, 유닛 스폰이 View(`UnitSpawner`)를
거쳐야 해서 헤드리스로 여러 턴을 돌릴 수 없음, `EnemyAI`가 `Team.Enemy`/`Team.Player`로 하드코딩되어 있음.

---

## 1. 건물 기능 구현 (단계별)

각 단계는 독립적으로 커밋 가능하게 나눴다. 모든 단계는 CLAUDE.md 규칙대로 **값은 Core/Data, 로직은 Systems**에 둔다.

### 1단계 — 도로 이동 보너스 (작음)
- 위키: 도로가 있는 칸 사이의 이동 비용 0.5. 도시 칸도 도로로 친다. 적 영토의 도로는 쓸 수 없고 중립 영토는 쓸 수 있다.
- `PathfindingSystem.GetReachable`을 BFS → **정수 비용 다익스트라**로 바꾼다(반 칸을 표현하려고 비용을 2배로: 일반 2, 도로↔도로 1,
  이동력도 ×2). 반환 형식(cameFrom/reachableSet)은 그대로 유지해 호출자 변경 없음.
- 판정 함수 `IsRoadFor(grid, team, pos)`(도로 또는 도시 칸 && 적 영토 아님)를 `PathfindingSystem`에 둔다.
- `MoveAction.Execute`의 거리 검증이 `GetReachable` 결과를 쓰는지 확인하고, 직접 거리 계산이 있으면 같이 교체.
- 검증: 직선 도로 4칸 위에서 이동력 1 유닛이 2칸 이동, 적 영토 도로에서는 보너스 없음.

### 2단계 — 다리 (중간)
- 위키: 비용 5, 도로(Roads) 기술, 얕은 물 1칸에 건설, **상하 또는 좌우 양쪽이 육지**여야 함(대각선 불가), 중립 물에도 건설 가능,
  파괴 가능, 육지 유닛이 건널 수 있고 도로처럼 이동 비용 0.5 + 수도 연결. 물 유닛도 통과 가능.
- 데이터: `BuildingInfo`에 `RequiresOppositeLand`(bool), `AllowNeutral`(bool — 지금 `IsRoad`에 묶인 "중립 가능"을 분리) 추가.
  `BuildingDefinition`에 `Bridge` 행 추가(`Build.Road` 키 공유 → `TechTree.csv` 수정 불필요).
- 로직: `TileImprovementSystem` 배치 판정에 반대편 육지 검사, `PathfindingSystem`에서 "다리가 있는 물 칸 = 육지 유닛 진입 가능",
  `CitySystem.IsConnectionNode`에서 다리를 도로와 같게 취급, 1단계 `IsRoadFor`에 다리 포함.
- View: 다리 모델(Kenney 키트에 없으면 `ProceduralPropMeshes`로 판자+난간 조립).
- 검증: 대각선/한쪽만 육지 거부, 육지 유닛 도하, 다리 파괴 후 연결 해제(인구 -1).

### 3단계 — 항구 보강: 연결 거리 + 뗏목 승선 (큼)
- **연결 거리**: `RefreshConnections`의 바다 구간을 "항구에서 출발해 물 칸 4개 이내"로 제한(BFS에 물 구간 길이 카운트).
- **뗏목**: 육지 유닛이 자기(또는 중립) 항구 칸에 들어가면 뗏목으로 변하고 그 턴의 남은 행동이 끝난다. 물에서 육지에 내리면
  원래 유닛으로 돌아온다(위키: 체력은 유지).
  - 데이터(`Core/UnitComponents.cs`): `Embarked { bool Value; string LandUnitId; int LandAttack/Defense/MoveRange/...; MoveDomain LandDomain }` —
    원래 스탯을 보관하고, 승선 중엔 뗏목 고정 스탯(공격 0, 방어 1, 이동 2)을 `Data/NavalUnitDefinition.cs` 표에서 가져온다.
  - 로직: `Systems/EmbarkSystem.cs` — `TryEmbark`/`TryDisembark`를 `MovementSystem.TryMove` 직후 호출.
    `PathfindingSystem`은 "항구 칸은 육지 유닛도 진입 가능, 승선 유닛은 물 도메인"으로 판정.
  - 후속(선택): 뗏목 → 정찰선/충각선/폭격선 업그레이드(기술 `Unit.scout` 등 해금 키 이미 존재, 비용 별). 기존 `TransportAction`
    플레이스홀더는 폴리토피아 규칙상 필요 없으므로 그대로 둔다.
- View: 승선 시 유닛 모델 위에 뗏목 메시를 덧씌우는 방식(프리팹 교체 없이).
- 검증: 승선/하선 스탯 복원, 체력 유지, 적 영토 항구 사용 불가, 연결 5칸 거리 항구는 연결 안 됨.

### 4단계 — 점수 + 신전 레벨 (중간)
- 데이터: `TileData.BuildingTurn`(건설된 턴, 신전 레벨 계산용), `CityResourceData.Score`, `EconomyWorld.Turn`(현재 턴 — 지금은
  `BattleController`의 `TurnState`에만 있음).
- 로직: `Systems/ScoreSystem.cs` — 위키 Score 문서 기준(기술/도시 레벨/영토/유닛/신전/기념물) 매 턴 재계산.
  신전 레벨 = `min(5, 1 + (현재턴 - 건설턴) / 3)`, 점수 100 + 50×(레벨-1).
- 원문과 다른 점(결정 필요): 이 프로젝트의 **신앙** 자원과 엮을지. 제안 — 신앙 최대치 +5는 유지하고, 신전 레벨당 신앙 +1/턴을
  추가(레벨이 오를수록 가치가 커지는 위키 의도를 신앙으로 옮김).
- HUD: 자원 바에 점수 칸 추가, 신전 모델은 레벨별 장식(층수) 추가.

### 5단계 — 기념물 (중간)
- 위키 과업 7종 중 이 프로젝트에 맞는 6종 (Cymanti/얼음 부족 전용 제외):

| 기념물 | 과업 | 필요 추적 값 |
|---|---|---|
| 평화의 제단 | 5턴 연속 공격 안 함 (명상 필요) | `TurnsWithoutAttack` |
| 황제의 무덤 | 별 100 보유 (교역 필요) | 현재 별 |
| 신의 눈 | 등대 전부 발견 | `LighthousesFound` — 시야가 없어 "유닛이 등대 칸/인접에 도달"로 대체 |
| 힘의 문 | 적 유닛 10기 처치 | `Kills` |
| 대시장 | 수도에 도시 5개 연결 | 연결 도시 수 |
| 행운의 공원 | 5레벨 이상 도시 | 최대 도시 레벨 |
| 지혜의 탑 | 기술 전부 연구 (철학 필요) | 해금 수 |

- 데이터: `Core/TaskProgressData.cs`(팀별 카운터 + 달성/건설 여부 플래그), `EconomyWorld.Tasks`. `BuildingInfo`에 `TaskId` 추가,
  기념물 행은 비용 0, 인구 3, 지형 Field|ShallowWater, 팀당 1회.
- 로직: `Systems/TaskSystem.cs` — 공격/처치(`CombatSystem` 결과 로그), 턴 종료, 연결 갱신 시점에 카운터 갱신. 배치 판정은
  `TileImprovementSystem`에서 "과업 달성 && 아직 안 지음"을 추가 조건으로.
- 검증: 각 과업을 인위적으로 만족시켜 옵션 등장/1회 제한/인구 +3.

### 6단계 — 대사관 (보류 권장)
- 평화 조약/전쟁 상태가 있어야 의미가 있다. 지금은 팀이 Player/Enemy 둘뿐이고 항상 전쟁이라 **구현하지 않고 보류**한다.
  3팀 이상 + 외교 시스템을 만들 때 같이 한다.

### 7단계 — 약탈 패시브 연결 (선택, 프로젝트 확장)
- 폴리토피아엔 약탈이 없지만 `PillageAction` 플레이스홀더가 있다. 제안: "적 영토 건물 칸에서 턴을 시작하면 그 건물을 파괴하고
  건설비의 절반을 별로 얻는다". 적 AI의 공격 목표 후보로도 쓸 수 있다. 원하면 진행.

---

## 2. 적 시뮬레이션

"적이 새 건물 기능을 실제로 쓰는가"와 "여러 턴을 돌렸을 때 경제가 굴러가는가"를 둘 다 확인할 수 있게 세 층으로 나눈다.

### 2-1. 경제 AI(`EconomyAI`) 확장 — 건물을 전부 쓰게
현재는 "별당 인구"만 보고 도로/시장/파괴/지형 변경은 0점이다. 다음 순서로 확장한다.
1. **예산 분배**: 매 턴 별을 "훈련 몫 / 건설 몫"으로 나눈다. 위협도(플레이어 유닛이 자기 영토 3칸 이내에 있는 수)가 높을수록
   훈련 몫을 키운다. 계산에 필요한 값은 인자로만 받는다(시스템 무상태 규칙).
2. **미래 가치 점수**: 농장 점수 = 즉시 인구 + (인접에 풍차를 지을 수 있는 빈 평지 수 × 0.5) 식으로 가공 건물 연쇄를 반영.
   시장은 인접 가공 건물 인구(최대 8)를 별로 환산해 "몇 턴에 회수되는지"로 점수화.
3. **연결 계획**: 수도와 미연결인 도시마다 도로/다리/항구로 잇는 최단 경로(비용 = 칸당 도로 3, 다리 5, 항구 7)를 구해
   이득(도시 +1, 수도 +1 인구)이 비용 대비 좋으면 경로 첫 칸부터 짓는다.
4. **신전/기념물**: 기념물은 비용 0이라 달성 즉시 가장 좋은 칸에 짓는다. 신전은 더 좋은 선택지가 없고 별이 남을 때만.
5. **지형 변경**: 화전(숲 → 작물)은 "그 칸에 농장을 지을 수 있고 인접에 풍차 후보가 있을 때"만 점수를 준다.

### 2-2. 군사 AI(`EnemyAI`) 확장
1. `RunTurn(grid, world, econ, Team team, ...)`로 팀을 인자화(지금은 Enemy/Player 하드코딩) — 시뮬레이션에서 양쪽을 AI로 돌리기 위함.
2. 도로/다리는 1·2단계에서 `GetReachable`에 들어가므로 자동 반영.
3. **도해 상륙**: 육지로 닿지 않는 목표(섬 마을/도시)면 가장 가까운 자기 항구로 가서 승선 → 목표 해안에 하선(3단계 이후).
4. **도시 방어**: 적(플레이어) 유닛이 자기 도시 2칸 안에 있으면 가장 가까운 유닛 하나는 도시 칸으로 복귀해 요새화.
5. (7단계를 하면) 약탈 가능 건물을 공격 목표 후보에 추가.

### 2-3. 헤드리스 시뮬레이션 하네스 (`Assets/Editor/EconomySimulation.cs`)
- View 없이 여러 턴을 돌리기 위해 **데이터 전용 유닛 스폰**을 분리한다: `UnitSpawner.SpawnFromCsv`의 엔티티 생성 부분을
  `Systems/UnitFactorySystem.cs`(`CreateFromCsv(grid, world, team, row, pos) → id`)로 빼고, `UnitSpawner`는 그 위에 View만 붙인다.
- 턴 루프(에디터 메서드, `-executeMethod TacticsECS.EditorTools.EconomySimulation.Run`):
  지형 생성(시드 N개) → 수도 창설 → [팀별: `TurnSystem.StartTurn` → `CityResourceSystem.ApplyTurnStart` → `EconomyAI.RunTurn`
  (Train 로그는 `UnitFactorySystem`으로 스폰) → `EnemyAI.RunTurn(team)` → 연결/점수 갱신] × 최대 T턴, 도시 전멸 시 종료.
- 출력: `Logs/Simulation/seed_<n>.csv`(턴, 팀, 별, 수입, 도시 수/레벨, 건물 종류별 개수, 기술 수, 유닛 수, 점수).
- 자동 판정(ALL PASS 형식, 기존 Verification과 동일):
  - 예외 없이 T턴 완주, 별/인구 음수 폭주 없음.
  - 시드 전체에서 건물 표의 모든 종류가 적어도 한 번 지어짐(다리/항구는 물이 있는 맵 시드 한정).
  - 20턴 시점 AI 평균 도시 레벨/연결 도시 수가 기준 이상(기준값은 첫 실행 결과를 보고 정한다).
  - 같은 시드 두 번 실행 결과가 동일(재현성 — `EconomyWorld.RandomCounter` 기반).
- (선택) 샌드박스 씬 "관전 모드": 양 팀 AI + 턴 간 딜레이로 화면에서 시뮬레이션을 지켜보는 토글.

---

## 3. 진행 순서 제안

1. 2-3의 `UnitFactorySystem` 분리 + 시뮬레이션 하네스(현재 기능 기준선 수치 확보)
2. 1단계 도로 이동 → 2단계 다리 → 3단계 항구/뗏목 (이동 계열, 매 단계 하네스로 회귀 확인)
3. 2-1 경제 AI 확장 + 2-2 군사 AI(팀 인자화, 도해 상륙, 방어)
4. 4단계 점수/신전 → 5단계 기념물
5. (선택) 7단계 약탈, 관전 모드. 6단계 대사관은 외교 시스템과 함께.

## 4. 결정 (2026-09-27 확정)
- 신전: 점수만 준다(신앙 연동 없음, 기존 신앙 최대치 +5도 제거).
- 약탈(7단계): 위키 규칙이 아니므로 넣지 않는다.
- 신의 눈: 시야 시스템을 구현해 위키대로 "등대를 밝힘"으로 판정한다.
