# 기술 CSV

규칙: [tech](../game/tech.md). 기본 트리는 배열형 표 5개(`Assets/Resources/Tables/`)를 조합한다 — 로더 `GameDataLoader.LoadTechNodes` → `TechGroupSystem.BuildTechNodes`.
표 사이 관계:

```
TechSlots   ◄── TechSlots.ParentIndex, TechTreeLayout.SlotIndex
Techs       ◄── TechTreeLayout.TechIndex, TechGroups.Tech{n}, Tribes.StartTech{n}
TechUnlocks ◄── Techs.Unlock{n}
TechGroups  ◄── Tribes.TechGroupIndex
Buildings / Units / Boats ◄── TechUnlocks.BuildingIndex / UnitIndex / BoatIndex
```
건물·유닛·배 표는 [buildings](buildings.md), [units](units.md).

## 해금 키
게임 코드는 기술이 아니라 **해금 키**(`Category.Target`)로만 효과를 조회한다. 여러 기술이 같은 키를 가져도 된다(하나만 연구하면 열림).
트리 어디에도 없는 키는 처음부터 열린 것으로 본다(예: `Unit.<Id>`가 없는 유닛은 기술 없이 훈련 가능).

| Category | Target | 여는 것 | 키를 소비하는 곳 |
|---|---|---|---|
| `Build` | (`BuildingIndex`) | 그 건물 건설 | [buildings](buildings.md) — 키는 `Build.<건물 Id>` |
| `Harvest` | 행동 이름 | 그 타일 행동 | [tile-actions](tile-actions.md) `Unlock`. `Harvest.Starfish`는 배의 불가사리 인양 |
| `Ability` | 행동 이름 | 타일 행동, 또는 `Ability.Disband`(유닛 해산) | [tile-actions](tile-actions.md) `Unlock`, 코드 |
| `Unit` | (`UnitIndex` / `BoatIndex`) | 그 유닛 훈련 / 뗏목 업그레이드 | [units](units.md) — 키는 `Unit.<유닛·배 Id>` |
| `Task` | 과업 Id | 그 과업 | [tasks](tasks.md) `Unlock` |
| `Reveal` | StructureId | 연구 전에는 그 구조물이 보이지 않음 | 코드 |
| `Move` | `Mountain` / `Ocean` | 산 / 깊은 바다 진입 | 코드 |
| `Defense` | `Mountain` / `Forest` / `Water` | 그 지형에서 방어 보너스 | 코드 |
| `Connect` | `Ocean` | 깊은 바다를 건너는 항구 연결 | 코드 |
| `Vision` | `Capital` | 다른 팀 수도 칸이 항상 보임 | 코드 |
| `Literacy` | (비움) | 연구 비용 할인 | 코드 |

"코드"인 키는 이름이 코드에 고정되어 있다 — 바꾸려면 코드도 고친다.

## TechUnlocks.csv — 해금 내역
파일: `Assets/Resources/Tables/TechUnlocks.csv`

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Kind` | `Passive`/`Active`/`UnitProduction`/`BuildingConstruction`/`Quest` | O | `Passive` | 분류(표시용) |
| `Category` | 문자열 | O | | 해금 키 앞부분 |
| `Target` | 문자열 | | | 해금 키 뒷부분. 비우면 `Category`가 곧 키. `Build`/`Unit`은 비우고 아래 Index로 가리킨다 |
| `BuildingIndex` | [Buildings](buildings.md) Index | `Build`일 때 O | -1 | 여는 건물. 키 뒷부분 = 그 건물 `Id` |
| `UnitIndex` | [Units](units.md#unitscsv--육지-유닛) Index | `Unit`일 때 이것이나 `BoatIndex` | -1 | 훈련을 여는 육지 유닛 |
| `BoatIndex` | [Boats](units.md#boatscsv--배) Index | `Unit`일 때 이것이나 `UnitIndex` | -1 | 뗏목 업그레이드를 여는 배(`Kind` `Upgrade`) |
| `Name`, `Description` | 메모 | | | 설계용 메모(화면에 나오지 않는다 — 번역하지 않음) |
| `Note` | 메모 | | | 로더 무시 |

## Techs.csv — 기술
파일: `Assets/Resources/Tables/Techs.csv`
위치·선행 관계는 넣지 않는다(슬롯 표가 정한다). 이름·설명은 [번역 표](strings.md#키-규칙) `Tech.<Id>.Name`/`.Desc`(상세 패널 설명). 슬롯 수보다 많이 정의해도 되고, 배치된 기술만 트리에 나온다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 기술 이름표 |
| `Icon` | 문자열 | | | 아이콘 이름 — [sprites](sprites.md)의 `Icon.<Icon>` |
| `CostBase` | 정수 | O | `GameRules.csv` `Tech.DefaultCostBase` | 비용 고정분 |
| `CostPerCity` | 정수 | O | 0 | 보유 도시 하나당 비용 증가분 |
| `Unlock{n}` | TechUnlocks Index | | | 이 기술이 여는 해금 내역 |
| `Note` | 메모 | | | 로더 무시 |

## TechSlots.csv — 고정 슬롯
파일: `Assets/Resources/Tables/TechSlots.csv`
트리 모양. 기술을 바꾸거나 비용을 조정할 때는 고치지 않는다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. 행 순서 = UI 갈래 순서 |
| `Id` | 문자열 | O | | 슬롯 이름표(기술 이름과 무관) |
| `ParentIndex` | TechSlots Index | O | -1 | 선행 슬롯. -1 = 루트 |
| `Tier` | 정수 | O | 1 | 바깥으로 갈수록 큰 원 |
| `Slot` | 정수 | O | 0 | 갈래 안 좌우 위치(0 = 시계방향 쪽, 1 = 반시계방향 쪽) |
| `Note` | 메모 | | | 로더 무시 |

## TechTreeLayout.csv — 슬롯별 기술 배치
파일: `Assets/Resources/Tables/TechTreeLayout.csv`
슬롯 하나에 서로 다른 기술 하나. 선행 기술은 선행 슬롯에 놓인 기술로 자동 연결된다.
슬롯 누락·중복, 기술 중복, 범위 밖 참조, 순환은 경고하고 **빈 트리**를 쓴다(일부만 적용하면 선행 조건이 사라지므로).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치(배치 순서와 무관) |
| `SlotIndex` | TechSlots Index | O | -1 | 슬롯 |
| `TechIndex` | Techs Index | O | -1 | 놓을 기술 |
| `Note` | 메모 | | | 로더 무시 |

## TechGroups.csv — 종족별 기술 묶음
파일: `Assets/Resources/Tables/TechGroups.csv`
그룹에 없는 기술은 그 팀의 트리에 나오지 않고 연구할 수도 없다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 그룹 이름표 |
| `Name`, `Description` | 메모 | | | 설계용 메모(화면에 나오지 않는다) |
| `Tech{n}` | Techs Index | | | 연구 가능한 기술 |
| `Note` | 메모 | | | 로더 무시 |

## 자주 하는 편집
| 하고 싶은 것 | 고칠 곳 |
|---|---|
| 기술 교체 | `TechTreeLayout.TechIndex` (두 행 맞바꾸기 가능) |
| 새 기술 | `Techs` 끝에 행 추가 → `TechTreeLayout`에 배치 → 필요한 `TechGroups.Tech{n}`에 추가 |
| 비용 조정 | `Techs.CostBase`, `Techs.CostPerCity` |
| 건물·유닛을 다른 기술로 옮기기 | 그 건물·유닛을 가리키는 `TechUnlocks` 행 Index를 다른 기술의 `Techs.Unlock{n}`으로 |
| 종족 시작 기술 | [tribes](tribes.md) `StartTech{n}` |

진행 중인 전투에는 반영되지 않는다(다음 전투부터).

## TechTree.csv — 샌드박스 단일 파일 형식 (옛 형식, 읽기만)
파일: `Assets/Resources/TechTree.csv`
샌드박스 "표 불러오기"에서 `Branch`/`Tier` 칸이 있는 파일을 고르면 이 형식으로 읽는 한 파일 형식의 예제(내보내기는 없다 — 표 폴더로 내보낸다). **이 파일을 고쳐도 기본 전투 기술은 바뀌지 않는다.** 로더: `TechCsvSerializer.Parse`.
위 배열형 표와 달리 기술이 자기 위치·선행 관계(`Branch`, `Parent`, `Tier`, `Slot`)를 직접 갖고, `Unlock{n}`에 해금 키 문자열을 직접 적는다.
이름·설명은 [번역 표](strings.md#키-규칙) `Tech.<Id>.Name`/`.Desc`가 있으면 그쪽이 `Name`·`Effect` 칸보다 앞선다 — 이 예제 파일에는 두 칸이 없다.
컬럼 명세(단일 출처): [`docs/tech_tree_csv_spec.csv`](../../tech_tree_csv_spec.csv). 예시: [`docs/sample_tech_tree.csv`](../../sample_tech_tree.csv).
