# 유닛 CSV

규칙: [unit](../game/unit.md). 로더: `UnitCsvSerializer.Parse` → `UnitCsvRow`, 행동 객체는 `UnitCsvActionFactory.BuildActions`.
이 표는 모르는 컬럼을 경고하지 않는다(오타 주의).

## 육지 유닛

파일: `SandboxUnits.csv` (프로젝트 루트 — `Assets/Resources`가 아님)
한 행 = 유닛 종류 하나. 키형(`Id`). 샌드박스 "불러오기"로 다른 파일을 고르면 그 파일이 대신 쓰인다.
행 순서는 [tribes](tribes.md)의 `StartUnit{n}`이 Index로 가리킨다 — 행 순서를 바꾸면 종족 표도 고친다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Id` | 문자열 | O | | 유닛 종류 키. 해금 키 `Unit.<Id>`, `GameRules.csv`의 `*UnitId`, 스프라이트 `Unit.<Id>`가 가리킨다 |
| `Name` | 문자열 | O | | 표시 이름 |
| `MaxHp` | 정수 | O | 0 | 최대 체력 |
| `Defense` | 실수 | O | 0 | 방어력(소수 허용 — 위키 원값 그대로) |
| `BaseVisual` | 문자열 | O | | 스폰 프리팹 `Assets/Prefabs/Units/Unit_<BaseVisual>`. 스프라이트에 `Unit.<Id>`가 없으면 이 외형을 빌린다 |
| `Action{n}` | 행동 이름 | O | | 행동·패시브 하나씩 — 목록은 [unit](../game/unit.md#행동과-패시브). 옛 이름 `Stealth`는 `Hide`로 읽는다 |
| `Domain` | `TerrainType` | | `Land` | 들어갈 수 있는 지형 |
| `Move.Range` | 정수 | | 0 | 이동력(`Move`가 있을 때) |
| `Attack.Attack` | 실수 | | 0 | 공격력(`Attack`이 있을 때, 소수 허용) |
| `Attack.Range` | 정수 | | 0 | 공격 사거리. 1 = 근접 |
| `Heal.Amount` | 정수 | | 0 | 치유량(`Heal`이 있을 때) |
| `Heal.Range` | 정수 | | 0 | 치유 사거리 |
| `Cost` | 정수 | | 2 | 훈련 비용(별) |
| `Trainable` | 불리언 | | true | false면 도시에서 훈련 불가(보상·침투로만 등장) |
| `Boat` | 배 Id | | 뗏목 | 자기 항구에 들어가면 바뀌는 배([배 표](#배)의 특수 배 Id) |

옛 한 칸 목록 `Actions`(`Move;Attack`)도 읽는다.

## 배

파일: `Assets/Resources/NavalUnits.csv`
컬럼 상속: `SandboxUnits.csv`
[육지 유닛](#육지-유닛) 표의 컬럼 + 아래 컬럼. 모든 행의 `Domain`은 `Water`로 강제된다. 체력(`MaxHp`)은 무시 — 태운 유닛의 체력을 쓴다.
로더: `GameTableCsvSerializer.ParseNavalUnits` → `NavalUnitDefinition`.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Unlock` | 해금 키 | | 없음 | 뗏목에서 이 배로 업그레이드하는 데 필요한 키. 비우면 업그레이드 불가 특수 배(유닛 `Boat`가 가리킴) |
| `Note` | 메모 | | | 로더 무시 |

행 종류는 `Id`와 `Unlock`으로 정해진다:
| 조건 | 종류 |
|---|---|
| `Id` = `raft` | 뗏목 — 항구에 들어간 육지 유닛이 기본으로 바뀌는 배. 없으면 코드 기본 뗏목 + 경고 |
| `Unlock` 있음 | 뗏목 업그레이드 대상 |
| `Unlock` 없음 | 특수 배 |
