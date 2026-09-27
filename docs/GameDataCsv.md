# 게임 규칙 CSV 설계 — 위키 대조 + 방식 비교분석 (3회 반복)

작성: 2026-09-27. 기준: [The Battle of Polytopia Wiki](https://polytopia.fandom.com/wiki/The_Battle_of_Polytopia_Wiki)
(MediaWiki API로 받은 원문 위키텍스트 — Technology/Buildings/City/Population/Combat/Score/Stars/Unit Skills/각 유닛·기술 문서).

목표: "기획자가 코드를 건드리지 않고 CSV만 고쳐서 게임 시스템을 만질 수 있게" 하되, 기본값은 위키와 맞춘다.
한 번에 다 바꾸지 않고 **분석 → 방식 3개 비교 → 최적안 구현 → 검증/개선**을 세 번 반복했다.

| 회차 | 대상 | 채택한 방식 | 상태 |
|---|---|---|---|
| 1차 | 기술트리 해금 정합성 + 건물/타일 행동 표 | 넓은 표 + `Flags` 태그 목록(하이브리드), 헤더 이름 기반 읽기 | ✅ |
| 2차 | 흩어진 수치 규칙(도시/점수/시야/전투 상수) | — | 예정 |
| 3차 | 유닛 스탯 + 전투 공식 | — | 예정 |

---

## 공통 — CSV 읽기 규칙 (1차에서 도입, 모든 표에 적용)

[`CsvTableReader`](../Assets/Scripts/TacticsECS/Systems/Csv/CsvTableReader.cs)가 모든 표를 같은 규칙으로 읽는다.

- **컬럼은 이름으로 찾는다** — 순서를 바꾸거나 메모 컬럼(`Note`, `Wiki`)을 끼워 넣어도 된다. 없는 선택 컬럼은 기본값.
- 모르는 컬럼, 잘못된 숫자/지형/태그, 중복 Id, 표에 없는 건물을 가리키는 인접 조건은 **`파일:줄 컬럼: 내용`** 형식으로
  경고한다(`[GameData] Buildings.csv:5 Terrain: 알 수 없는 값 'Feild'`). 게임은 멈추지 않고 그 칸만 기본값으로 읽는다.
- 한 칸에 여러 값은 `;`로 나열(`Field;Forest`, `Resource_Metal;Resource_Ore`).
- 첫 칸이 `#`으로 시작하는 행은 주석. 큰따옴표 인용(`"쉼표, 포함"`)은 스프레드시트 저장 형식 그대로.
- 파일은 `Assets/Resources/`에 두고 [`GameDataLoader.LoadAll`](../Assets/Scripts/TacticsECS/Systems/Csv/GameDataLoader.cs)이
  전투 시작(`BattleController.Awake`)과 에디터 검증/시뮬레이션 시작 시 읽어 Data 표(`BuildingDefinition.All` 등)에 채운다.
  코드에는 기본 표를 두지 않는다 — **CSV가 유일한 원본**(코드 표와 CSV가 어긋나는 일을 막기 위함).

---

## 1차 — 기술트리 정합성 + 건물/타일 행동 표

### 1-1. 위키 대조 결과 (현재 인게임과 다른 점)

| 항목 | 위키(2026) | 인게임(1차 전) | 조치 |
|---|---|---|---|
| 기사도 | 기사 + **파괴**(Destroy) | 기사 + 화전 | ✅ 교정 — 위키 Chivalry 문서 "Path of the Ocean 이전엔 화전"이던 옛 규칙이었음 |
| 건축 | 풍차 + **화전**(Burn Forest) | 풍차 + 파괴 | ✅ 교정 (위와 맞바뀌어 있었음) |
| 방패(Shields) | 2.1 업데이트에서 **전략(Strategy)** 으로 개명, 방어병 + 평화 조약 | 방패 | ✅ Id `Strategy`로 개명. 평화 조약은 외교 시스템이 없어 제외 |
| 도로 | 도로 + 다리 + **교역망 과업**(대시장) | 과업은 조건 없이 개방 | ✅ `Task.Network` 키 추가 |
| 외교 | 망토 + 대사관 + **수도 시야** | 망토만 | ✅ `Vision.Capital` 구현(다른 팀 수도 칸 항상 보임). 대사관은 외교 없어 보류 유지 |
| 화전 비용 | 3 (Burn Forest 정보상자/Population 표) | 5 | ✅ 3으로 |
| 기술 이름 | Climbing/Organization/Smithery/Aquatism | Mountaineering/Gathering/Smithing/Aquatics | ✅ Id를 현재 위키 이름으로 |
| 연구 비용 | 티어 × 도시 수 + 4, Literacy 33%↓(올림) | 같음 | — |
| 건물 비용/인구/지형 | Buildings 표 | 같음(대장간 지형 오기만 원문과 다르게 — 기존 주석 유지) | — |
| 항구 해금 | Technology 표 = 낚시, Buildings 표 = 배 타기 (위키 내부 불일치) | 낚시 | 유지(Technology 표·Raft 문서 기준) |

### 1-2. 건물 표를 CSV로 옮기는 방식 — 3개 비교

같은 건물 21행(자원 건물 8 + 신전 4 + 도로/다리 + 기념물 7)을 세 방식으로 실제로 만들어 재고, 기획자가 자주 하는
편집 4가지를 했을 때 파일에서 바뀌는 줄 수(diff)를 쟀다(`scratchpad` 스크립트로 생성 — 결과만 옮김).

| 방식 | 바이트 | 행 | 컬럼 | 빈칸/0 비율 | S1 비용 수정 | S2 건물 추가 | S3 새 수치 필드 | S4 새 플래그 1개 |
|---|---|---|---|---|---|---|---|---|
| **A** 순수 넓은 표 (불리언도 컬럼) | 2888 | 21 | 17 | 57% | 2 | 1 | 44 | 44 |
| **B** 롱/EAV (`Table,Id,Field,Value`) | 5717 | 129 | 4 | 0% | 2 | 7 | 4 | 2 |
| **C** 효과 DSL 한 칸 (`Pop+2 Needs(Crop) Adj(Farm)x1`) | 2626 | 21 | 7 | 10% | 2 | 1 | 8 | 2 |
| **D** 넓은 표 + `Flags` 태그 목록 (A·C 하이브리드) | 2673 | 21 | 12 | 40% | 2 | 1 | 44* | 2 |

\* S3(모든 건물에 새 수치 컬럼 추가)는 줄 단위 diff로는 전 행이 바뀌지만, 스프레드시트에서는 "열 삽입" 한 번이고,
헤더 기반 읽기 덕분에 **컬럼이 없는 옛 파일도 그대로 읽힌다**(기본값). A도 같은 이점을 갖는다.

정성 비교:

| 기준 | A | B | C | D |
|---|---|---|---|---|
| 스프레드시트에서 한 건물을 한 줄로 보기 | ✅ | ❌ (건물 1개 = 7행) | ✅ | ✅ |
| 비용/인구로 정렬·필터·합계 | ✅ | ❌ (값 컬럼에 종류가 섞임) | ❌ (숫자가 문자열 안) | ✅ |
| 필드 이름 오타 검출 | ✅ 헤더 | ⚠ 행마다(스키마 목록 필요) | ⚠ 파서 오류 | ✅ 헤더 + 태그 목록 |
| 드문 불리언 추가 비용 | ❌ 컬럼 폭증(빈칸 57%) | ✅ | ✅ | ✅ 태그 하나 |
| 학습 비용(기획자) | 낮음 | 중간 | 높음(문법) | 낮음 |
| 파서 복잡도 | 낮음 | 낮음 | 높음 | 낮음 |

**결정: D.** 숫자·문자열 속성은 컬럼(정렬/필터/차트 가능), 대부분의 행에 해당 없는 불리언(도로/중립 건설/양쪽 육지/
도로 역할/신전/인접 골드)은 `Flags` 한 칸의 태그 목록. B는 편집 diff는 작지만 스프레드시트에서 "표"로 볼 수 없어 탈락,
C는 가장 작지만 숫자가 문자열 안에 묻혀 밸런스 작업(정렬/비교)이 불가능해 탈락.

### 1-3. 스키마

**`Assets/Resources/Buildings.csv`** — 한 행 = 건물 하나 ([`BuildingInfo`](../Assets/Scripts/TacticsECS/Core/BuildingInfo.cs))

| 컬럼 | 필수 | 의미 |
|---|---|---|
| `Id` | O | `TileData.BuildingId`에 저장되는 키. 코드가 특별 취급하는 Id(`Port`/`Road`/`Bridge`/`Market`...)는 바꾸지 말 것 |
| `Name` | | 표시 이름 |
| `Unlock` | | 필요한 해금 키(TechTree.csv `Unlocks`와 같은 문자열). 비우면 기술 불필요 |
| `Cost` | | 골드 |
| `Population` | | 지을 때 도시에 더해지는 고정 인구 |
| `Terrain` | O | `Field;Forest;Mountain;ShallowWater;Ocean` 중 지을 수 있는 지형 |
| `RequiredStructures` | | 이 자원(구조물) 중 하나 위에만 — 지으면 소모 (예: `Resource_Crop`) |
| `AdjacentBuildings` | | 인접(8방향, 같은 팀 영토)에 이 건물 중 하나가 있어야 함 — 같은 표의 Id |
| `PopulationPerAdjacent` | | 인접 건물 하나당 인구(풍차/제재소/대장간) |
| `Flags` | | `Road`(타일 개량, 건물과 공존) `Neutral`(중립 땅/물 가능) `OppositeLand`(상하/좌우 양쪽 육지) `ActsAsRoad`(도로 효과) `Temple`(레벨·점수) `GoldFromAdjacent`(인접 인구만큼 골드/턴) |
| `Task` | | 기념물이면 과업 Id(`Pacifist`/`Wealth`/`Explorer`/`Killer`/`Network`/`Metropolis`/`Genius`) — 과업 달성 시 팀당 1회 |
| `Description` | | 메뉴 설명 |

**`Assets/Resources/TileActions.csv`** — 한 행 = 타일 행동 하나 ([`TileActionInfo`](../Assets/Scripts/TacticsECS/Core/TileActionInfo.cs))

| 컬럼 | 필수 | 의미 |
|---|---|---|
| `Id` | O | 행동 키 |
| `Kind` | O | `Harvest`/`ClearForest`/`BurnForest`/`GrowForest`/`Destroy` — 효과 종류(코드) |
| `Unlock`, `Cost`, `Terrain`, `RequiredStructures`, `Population`, `GoldGain`, `Name`, `Description` | | 건물 표와 같은 의미 |

### 1-4. 검증과 개선점

- 신규 [`GameDataCsvVerification`](../Assets/Editor/GameDataCsvVerification.cs): 모든 CSV 무오류 로드, 컬럼 순서 무관/주석 행,
  잘못된 지형·숫자·태그·인접 Id·중복 Id가 `파일:줄`과 함께 보고되는지, 위키 정합(기사도=파괴, 건축=화전, 전략→외교,
  도로=교역망, 외교=수도 시야, 화전 3), 수도 시야가 수도 칸만 밝히는지.
- 기존 검증 5종(Economy/BuildingFeature/UI/UnitCsv) + 헤드리스 시뮬레이션 모두 ALL PASS.
- **2차로 넘긴 개선점**: 이번에 표를 옮기면서 보니 규칙 수치가 아직 코드 곳곳의 `const`에 흩어져 있다 — 시장 골드 상한
  (`CitySystem.MarketGoldCap`), 도시/성벽 방어(`CityDefenseBonus`/`WallDefenseBonus`), 유적 보상, 보상 골드/인구, 점수
  상수 14개, 시야 상수, 탐험가 걸음 수 등 40여 개. 이것들은 "한 행 = 한 개체" 표가 아니라 스칼라라서 1차 방식을 그대로
  쓸 수 없다 → 2차에서 별도로 비교한다. 또 위키와 다르게 둔 값이 어디 있는지 코드 주석을 뒤져야만 알 수 있다는 점도
  함께 풀 문제로 남겼다.
