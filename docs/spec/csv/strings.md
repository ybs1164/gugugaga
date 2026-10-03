# 번역 CSV

파일: `Assets/Resources/Strings.csv`
게임 화면에 보이는 문자열은 전부 이 표에만 있다 — 데이터 표에는 이름·설명 칸이 없고, 코드는 키로만 꺼낸다. 키형(`Key`).
로더: `StringTableCsvSerializer.Parse` → `StringTable`(`GameDataLoader.LoadStrings`, 다른 표보다 먼저). 조회: `LocalizationSystem.T`/`F`.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Key` | 문자열 | O | | 문자열 키. 아래 규칙 |
| `ko` | 문자열 | O | | 원문(한국어). 프리팹에 굽는 글자도 이 열 |
| `en` | 문자열 | | `ko` | 영어. 비면 원문 |
| `Note` | 메모 | | | 로더 무시 |

- `Key`·`Note`가 아닌 열이 전부 언어 열이다. 언어를 더하려면 열(예: `ja`)을 추가하고 `Language.Name` 행에 그 언어 이름을 적는다 — 언어 버튼이 헤더 순서대로 돈다.
- 칸 안의 `\n`은 줄바꿈. `{0}`, `{1}` …은 코드가 채우는 값(`string.Format`) — 번역문에서도 같은 번호를 쓴다.
- 문구에 수치를 옮겨 적지 않는다 — [자리표시자](#자리표시자)로 CSV 값을 가리킨다.
- 없는 키는 화면에 키 그대로 나온다(데이터 표 이름은 `Id`).
- `#`로 시작하는 행은 구역 제목(주석).

## 키 규칙
| 키 | 쓰는 곳 |
|---|---|
| `Language.Name` | 언어 선택 버튼에 보이는 그 언어의 이름 |
| `Unit.<Id>.Name` | [Units·Boats](units.md) 행 이름. 샌드박스 유닛 파일의 `Name`보다 앞선다 |
| `Building.<Id>.Name`, `.Desc` | [Buildings](buildings.md) |
| `TileAction.<Id>.Name`, `.Desc` | [TileActions](tile-actions.md) |
| `Tech.<Id>.Name`, `.Desc` | [Techs](tech.md#techscsv--기술). 샌드박스 기술 파일의 `Name`·`Effect`보다 앞선다 |
| `Task.<Id>.Name`, `.Desc` | [Tasks](tasks.md) |
| `CityReward.<Reward>.Name`, `.Desc` | [CityRewards](city-rewards.md) |
| `Tribe.<Id>.Name`, `.Desc` | [Tribes](tribes.md#tribescsv--종족) |
| `StartCondition.<Id>.Name`, `.Desc` | [StartConditions](tribes.md#startconditionscsv--시작-조건-묶음) |
| `Biome.<Biome>.Name` | [Biomes](biomes.md) `Biome` 행. 불러온 바이옴 파일의 `Name`보다 앞선다 |
| `Structure.<StructureId>.Name`, `.Desc` | 타일 구조물(`StructureDefinition`) |
| `Action.<행동>.Name`, `.Tooltip` | 유닛 행동·패시브 — [unit](../game/unit.md#행동과-패시브) |
| `UI.<화면>.<이름>` | 화면 문구(버튼·안내·로그). `<화면>`은 그 문구를 쓰는 View/System |

## 자리표시자
문구 속 `{이름}`은 표를 읽을 때 CSV 값으로 채워진다(`TextPlaceholderSystem`). 값이 바뀌면 문구도 따라 바뀐다.

| 형식 | 값 | 예 |
|---|---|---|
| `{컬럼}` | 그 문구가 설명하는 행(`<표>.<Id>.Desc`)의 CSV 칸 — 컬럼 이름 그대로 | `Building.Farm.Desc`의 `{Population}` |
| `{표.Id.컬럼}` | 다른 표의 행. 표: `Building`(`Cost` `Population` `PopulationPerAdjacent`), `TileAction`(`Cost` `Population` `StarsGain`), `Task`(`Threshold`), `CityReward`(`Amount`), `Unit` — 유닛·배(`Cost` `MaxHp` `Defense` `Move.Range` `Attack.Attack` `Attack.Range`), `Tech`(`CostBase` `CostPerCity`), `Tribe`(`StartStars`), `StartRule` — 시작 조건 규칙 `Id`(`Count` `MinDistance` `MaxDistance`) | `{Building.Mine.Cost}` |
| `{Rule.키}` | [`GameRules.csv`](game-rules.md)의 `Key` | `{Rule.Score.Monument}` |

- 다른 표 값은 먼저 읽힌 표만 볼 수 있다: 번역 → 규칙 → 타일 행동·도시 보상·과업 → 배·유닛·건물·해금 내역·기술·종족·시작 조건 규칙·시작 조건.
- 못 채운 자리표시자는 화면에 그대로 보인다. `scripts/check_spec.py`가 없는 규칙·Id·컬럼과 언어마다 다른 자리표시자를 잡는다.
- 코드에만 있는 상수(예: `Herd`의 이동력 +1)와 구조적인 수(`1/`, "1차")는 문구에 그대로 둔다.

## 언어 고르기
- 처음 실행: 시스템 언어가 한국어면 `ko`, 아니면 `en`. 언어 버튼(화면 오른쪽 위)을 누르면 다음 언어로 바꾸고 저장한 뒤 씬을 다시 연다(진행 중인 전투는 처음부터).
