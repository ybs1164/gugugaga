# 배열형 테이블 (Assets/Resources/Tables)

2026-09-30부터 새로 만드는 게임 데이터 표는 **배열형**으로 만든다. 기존 표(`Buildings.csv`/`TechTree.csv`/바이옴 CSV 등)는 그대로 둔다.

## 공통 규칙

| 규칙 | 내용 |
| --- | --- |
| `Index` 컬럼 | 모든 행의 첫 칸. 0부터 1씩 빈틈없이 — 행 순서 = 배열 위치. 다르면 불러올 때 오류(`ArrayTableCsvSerializer`). |
| 참조 = Index | 다른 표를 가리킬 때는 문자열 Id가 아니라 그 표의 `Index`(정수). 컬럼 이름은 `…Index`(예: `BiomeIndex`). `-1`이나 빈 칸 = 없음. |
| 여러 값 = 반복 컬럼 | 한 칸에 한 값(CLAUDE.md 규칙 6). `Unlock1, Unlock2 …`처럼 번호를 붙여 늘어놓고, 빈 칸은 건너뛴다. 컬럼 수는 자유. |
| 문자열 | `Id`(사람이 읽는 이름표, 코드는 Index로 찾음), `Name`, `Description`. 쉼표가 든 값은 큰따옴표로 감싼다. |
| 주석 | 첫 칸이 `#`인 행은 무시. 모르는 컬럼은 경고만 남기고 무시(`Note`/`Wiki` 같은 메모 컬럼은 허용). |
| 참조 검사 | `ArrayTableValidationSystem` — 범위 밖 Index, 기술 그룹 밖 시작 기술, 규칙 거리 등. 게임 시작 시(`GameDataLoader.LoadAll`) 콘솔 경고로 나온다. |

읽은 값은 `GameTables`(Data 계층 정적 배열)에 들어가고 System들은 `GameTables.Tribes[i]`처럼 Index로 바로 꺼낸다.

## 표 목록

```
TechUnlocks ◄── Techs.Unlock*            (기술 → 해금 내역)
Techs       ◄── Techs.ParentIndex        (선행 기술)
            ◄── TechGroups.Tech*         (종족별 기술 묶음)
            ◄── Tribes.StartTech*
TechGroups  ◄── Tribes.TechGroupIndex
StartConditions ◄── Tribes.StartConditionIndex
StartConditionRules ◄── StartConditions.Rule*
바이옴 CSV(Biome 행 순서) ◄── Tribes.BiomeIndex     (기본 Tables/Biomes.csv, 샌드박스에서 불러오면 그 파일)
유닛 CSV(행 순서)         ◄── Tribes.StartUnit*     (샌드박스에서 불러온 유닛 CSV, 없으면 SandboxUnits.csv)
```

### TechUnlocks.csv — 기술 연구 시 해금되는 내역

| 컬럼 | 뜻 |
| --- | --- |
| `Category` | 해금 종류: `Build`(건물) `Unit`(유닛 훈련/배 업그레이드) `Move` `Defense` `Reveal`(숨은 자원) `Harvest` `Ability` `Task`(과업) `Vision` `Connect` `Literacy` |
| `Target` | 대상(건물 Id, 유닛 CSV Id, 구조물 Id …). 비우면 `Category` 자체가 키 |
| `Name`, `Description` | 표시용 |
| `Icon` | 기술트리 상세 패널의 "해금되는 것" 아이콘 줄에 쓰는 아이콘 이름(`Resources/Icons`). 효과 문장 대신 이 아이콘을 보여 준다 |

게임 코드가 조회하는 해금 키는 `Category.Target`(예: `Build` + `Farm` → `Build.Farm`, `Literacy`는 그대로). 키 의미는 [TechTreeCsv.md](TechTreeCsv.md).

### Techs.csv — 기술

`Id, Name, ParentIndex(-1 = 1티어 루트), Tier, Slot, Icon, CostBase, CostPerCity, Unlock1..N(TechUnlocks Index), Description`.
기존 `TechTree.csv` 25개 기술을 그대로 옮겼다(검증: `ArrayTableVerification.VerifyTechsMirrorTechTree`). 종족을 고른 전투는 이 표로 기술트리를
만들고(`TechGroupSystem.BuildTechNodes`), 종족이 없으면 예전처럼 `TechTree.csv`(또는 샌드박스 "기술 불러오기" 파일)를 쓴다.

### TechGroups.csv — 종족별 기술 그룹

`Id, Name, Description, Tech1..N(Techs Index)`. 그룹에 없는 기술은 그 팀 기술트리에 나타나지 않고 연구할 수도 없다(`TechTreeData.Allowed`,
`TechSystem.IsAvailable`). 위키(Tribes)대로 일반 부족 12개는 모두 같은 트리라 지금은 12개 그룹이 25개 기술을 전부 담고 있다 — 종족마다 줄을
빼거나 더해서 조정한다.

### Tribes.csv — 종족

| 컬럼 | 뜻 |
| --- | --- |
| `BiomeIndex` | 종족 영역을 채울 바이옴(바이옴 CSV의 Biome 행 순서, 0부터) |
| `TechGroupIndex` | 연구 가능한 기술 그룹 |
| `StartTech1..N` | 처음부터 해금된 기술(Techs Index). Luxidoor처럼 없으면 비움 |
| `StartGold` | 시작 골드 |
| `StartUnit1..N` | 수도에 받는 시작 유닛(유닛 CSV 행 Index — SandboxUnits.csv: 0 보병, 1 방패병, 2 검투사, 3 기병, 5 궁수, 7 사제 …) |
| `StartConditionIndex` | 수도 주변 시작 조건(-1 = 없음) |
| `Wiki` | 메모: 위키 원문의 시작 기술/유닛/골드 |

값은 [Polytopia Wiki — Tribes](https://polytopia.fandom.com/wiki/Tribes)의 일반 부족 12개. 원문과 다른 점: Luxidoor의 "레벨 3 수도로 시작"은
아직 없음, 종족 고유 지형은 기본 바이옴 3개(평원/사막/고지대)로 근사.

### StartConditions.csv / StartConditionRules.csv — 수도 주변 시작 조건

바이옴 표가 "영역 전체의 비율"이라면, 이 두 표는 "수도 몇 칸 안에 무엇이 최소 몇 개"라는 개별 보장이다(바이옴 표와 분리). 지형/구조물 생성이
끝난 뒤 `StartConditionSystem`이 종족의 수도마다 적용한다.

`StartConditions.csv`: `Id, Name, Description, Rule1..N`(규칙 Index, 적힌 순서대로 적용).

`StartConditionRules.csv`:

| 컬럼 | 뜻 |
| --- | --- |
| `Kind` | `Tile`(칸의 TileTypeId를 바꿈) / `Structure`(칸 위에 구조물) |
| `Target` | TileTypeId(예: `Water`) 또는 StructureId(예: `Resource_Fish`) |
| `TerrainType` | Tile: 바뀐 칸의 이동 지형(`Land`/`Water`). Structure: `AllowedTile`이 비었을 때 놓일 수 있는 지형 |
| `Count` | 최소 개수(이미 그만큼 있으면 아무것도 안 함) |
| `MinDistance`, `MaxDistance` | 수도로부터 체비쇼프 거리 고리(1 = 바로 옆 8칸) |
| `MapType` | 이 맵 타입(습도 프리셋 이름)에서만. 비우면 항상 |
| `AllowedTile1..N` | Tile: 바꿔도 되는 원래 타일(비우면 도시가 아닌 아무 육지). Structure: 놓일 수 있는 타일 |

수도/마을 칸과 유닛이 있는 칸은 건드리지 않는다. 지금 들어 있는 규칙은 위키 Map Generation의 "Drylands에서 Kickoo/Luxidoor 수도는 물 2칸 +
각 칸에 물고기" 하나(`DrylandsCoast`).

## 샌드박스에서 쓰기 (습도 탭)

툴바의 "습도 탭" 버튼으로 연다.

1. **맵 타입 / 물 비율** — 맵 타입을 고르면 대표 물 비율로 슬라이더가 맞춰지고, 슬라이더로 비율을 직접 조정한다.
2. **1차 지형 생성** — 선택한 맵 타입의 절차(수도 → 사전 마을 → 랜드마스 마스크)대로 육지/물 아웃라인과 수도·마을 자리만 만든다(회색 육지/파란 물).
3. **바이옴** — "자동"(불러온 바이옴 전부, 종족이 있으면 종족 바이옴) 또는 바이옴 하나. 바이옴 CSV를 불러오면 목록이 그 파일로 바뀐다.
4. **종족(아군/적)** — 고르면 영역이 팀마다 하나(수도 2개)가 되고, 전투 시작 때 시작 골드/기술/기술 그룹/시작 유닛과 그 영역 수도가 적용된다.
5. **지형 생성**(툴바) — 1차 지형이 지금 설정과 맞으면 그 모양 그대로 바이옴만 채운다. 바이옴을 바꿔 가며 눌러 같은 땅 모양에서 규칙을 비교할 수 있다.
