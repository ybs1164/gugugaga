# CSV 공통 형식

모든 게임 데이터 CSV에 적용된다. 표마다 다른 내용은 [`csv/`](csv/)에 있다.
읽기: [`CsvTableReader`](../../Assets/Scripts/TacticsECS/Systems/Csv/CsvTableReader.cs). 진입점: [`GameDataLoader.LoadAll`](../../Assets/Scripts/TacticsECS/Systems/Csv/GameDataLoader.cs)(전투 시작, 에디터 검증·시뮬레이션 시작 시).

## 파일
- 위치는 `Assets/Resources/`. 예외 위치는 각 `csv/*.md`의 머리에 적는다.
- UTF-8(BOM 있어도 됨). 스프레드시트에서는 "CSV UTF-8"로 저장한다.
- 값은 CSV에만 둔다. 코드에는 기본 표를 두지 않는다(로드 실패 시 빈 표).

## 읽기 규칙
| 항목 | 규칙 |
|---|---|
| 헤더 | 첫 줄. 컬럼은 **이름으로** 찾는다(순서 무관, 대소문자 무시) |
| 빈 칸 | 그 컬럼의 기본값 |
| 주석 행 | 첫 칸이 `#`인 행, 빈 행 무시. 첫 칸이 빈 행도 무시(모델 CSV는 예외 — [sprites](csv/sprites.md#레거시-3d-모델)) |
| 인용 | `"쉼표, 포함"`, `""` = 따옴표 한 개 |
| 불리언 | `1`/`0`/`true`/`false`/`yes`/`no` |
| 메모 컬럼 | `Note`, `Wiki`는 로더가 읽지 않는다(허용된 표에서만 — 각 `csv/*.md`의 컬럼 표에 적힘) |
| 모르는 컬럼 | 경고 후 무시 |
| 헤더보다 많은 칸 | 경고 후 무시 — 대개 인용하지 않은 쉼표가 값을 끊은 것 |
| 잘못된 값 | `[GameData] 파일:줄 컬럼: 내용` 경고 후 그 칸만 기본값. 게임은 멈추지 않는다 |

## 한 칸에 한 값 (반복 컬럼)
여러 값은 번호 붙은 컬럼에 하나씩 나열한다(`CLAUDE.md` 규칙 6). 컬럼 표에는 `Name{n}`으로 적는다.
- `Terrain1`, `Terrain2` … 처럼 필요한 만큼 늘리고, 빈 칸은 건너뛴다.
- 일부 표는 옛 한 칸 목록 컬럼(`a;b`)도 읽기만 호환한다. 새로 쓰지 않는다.

## 표 형식 두 가지
| 형식 | 행을 가리키는 방법 | 사용 표 |
|---|---|---|
| 키형 | 문자열 `Id`(중복 금지) | 타일 행동, 과업, 바이옴, 규칙(`Key`), 샌드박스 유닛·기술 파일 |
| 배열형 | 정수 `Index` | `Assets/Resources/Tables/` 중 바이옴 표를 뺀 전부 |

배열형 규칙:
- 첫 컬럼 `Index`는 0부터 1씩 빈틈없이 — 행 순서와 같아야 한다(다르면 오류).
- 다른 표 참조는 그 표의 `Index`(컬럼 이름 `…Index`), `-1` 또는 빈 칸 = 없음.
- 참조 범위는 [`ArrayTableValidationSystem`](../../Assets/Scripts/TacticsECS/Systems/ArrayTableValidationSystem.cs)이 로드 시 검사한다.
- 행을 지우거나 끼우면 뒤 Index가 모두 바뀐다 — 참조하는 표도 같이 고친다. 보통은 끝에 추가한다.

## 공통 값
| 이름 | 값 | 뜻 |
|---|---|---|
| `TerrainType` | `Land`, `Water` | 이동 판정용 지형. 유닛 `Domain`과 같아야 들어간다 |
| `TileClass` | `Field`, `Forest`, `Mountain`, `ShallowWater`, `Ocean` | 건설·행동 배치 조건용 지형. 타일에서 계산된다(저장 안 함) — [tile](game/tile.md#지형) |
| 해금 키 | `Category.Target` 문자열 | 기술이 여는 것. 형식과 Category 목록은 [tech](csv/tech.md#해금-키) |
| StructureId | `Capital`, `Village`, `Ruin`, `Resource_*`, `Lighthouse`, `Starfish` | 타일 위 구조물 — [tile](game/tile.md#구조물) |
| TileTypeId | 바이옴 CSV의 `Entry` + `Ocean` | 세부 타일 종류 — [biomes](csv/biomes.md) |
