# 유닛 CSV

규칙: [unit](../game/unit.md). 기본 유닛·배는 배열형 표 2개(`Assets/Resources/Tables/`) — 로더 `ArrayTableCsvSerializer.ParseUnits`/`ParseBoats` → `GameTables.Units`/`GameTables.Boats`(배는 `NavalUnitDefinition`에도 종류별로). 칸 읽기는 둘 다 `UnitCsvSerializer.Parse` → `UnitCsvRow`, 행동 객체는 `UnitCsvActionFactory.BuildActions`.
표 사이 관계(해금 내역 쪽은 [tech](tech.md)):

```
Boats ◄── Units.BoatIndex, TechUnlocks.BoatIndex
Units ◄── TechUnlocks.UnitIndex, Tribes.StartUnit{n}
```

## Units.csv — 육지 유닛
파일: `Assets/Resources/Tables/Units.csv`
한 행 = 유닛 종류 하나. 샌드박스에서 유닛 CSV를 불러오지 않으면 이 표가 전투에 쓰인다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 유닛 종류 키. 해금 키 `Unit.<Id>`, `GameRules.csv`의 `*UnitId`, 스프라이트 `Unit.<Id>`가 가리킨다 |
| `Name` | 문자열 | | `Id` | 표시 이름 |
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
| `BoatIndex` | [Boats](#boatscsv--배) Index | | -1 | 자기 항구에 들어가면 바뀌는 배. -1 = 뗏목 |
| `Note` | 메모 | | | 로더 무시 |

이 유닛을 여는 기술은 [TechUnlocks](tech.md#techunlockscsv--해금-내역)의 `UnitIndex`가 정한다. 가리키는 해금 내역이 없으면 기술 없이 훈련할 수 있다.

## Boats.csv — 배
파일: `Assets/Resources/Tables/Boats.csv`
배는 도시에서 훈련하지 않는다 — [unit](../game/unit.md#배). 스탯 컬럼은 [Units.csv](#unitscsv--육지-유닛)와 같은 뜻이고(`Name`, `Defense`, `Action{n}`, `Move.Range`, `Attack.*`, `Heal.*`, `Cost`), `Domain`은 `Water`로 강제, `MaxHp`는 무시(태운 유닛의 체력).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치 |
| `Id` | 문자열 | O | | 배 키. 해금 키 `Unit.<Id>`, 스프라이트 `Boat.<Id>`가 가리킨다 |
| `Kind` | `Raft`/`Upgrade`/`Special` | O | `Special` | 아래 표 |
| `Name`, `Defense`, `Action{n}`, `Move.Range`, `Attack.Attack`, `Attack.Range`, `Heal.Amount`, `Heal.Range`, `Cost` | | | | Units.csv와 같음. `Cost` = 뗏목에서 업그레이드하는 별 |
| `Note` | 메모 | | | 로더 무시 |

| `Kind` | 뜻 |
|---|---|
| `Raft` | 뗏목 — 항구에 들어간 육지 유닛이 기본으로 바뀌는 배. 정확히 한 행, `Id`는 `raft`. 없으면 코드 기본 뗏목 + 경고 |
| `Upgrade` | 뗏목에서 업그레이드하는 배. 여는 기술은 TechUnlocks의 `BoatIndex`(없으면 기술 없이 가능) |
| `Special` | 유닛 `BoatIndex`가 가리키는 전용 배. 업그레이드 불가 |

## 샌드박스 유닛 CSV
파일: `docs/sample_units.csv`
샌드박스 "불러오기/내보내기"용 한 파일 형식(예시가 위 파일). 키형(`Id`) — `Index`가 없고, 배는 `BoatIndex` 대신 배 `Id`를 직접 적는다.
컬럼은 [Units.csv](#unitscsv--육지-유닛)의 `Index`·`BoatIndex`를 뺀 나머지 + 아래 컬럼. 이 형식은 모르는 컬럼을 경고하지 않는다(오타 주의). 옛 한 칸 목록 `Actions`(`Move;Attack`)도 읽는다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Id`, `Name`, `MaxHp`, `Defense`, `BaseVisual`, `Action{n}`, `Domain`, `Move.Range`, `Attack.Attack`, `Attack.Range`, `Heal.Amount`, `Heal.Range`, `Cost`, `Trainable` | | | | Units.csv와 같음 |
| `Boat` | 배 Id | | 뗏목 | 자기 항구에 들어가면 바뀌는 배(`Boats.csv`의 `Special` 행 Id) |

불러온 표에서도 종족 시작 유닛은 `Units.csv`의 그 Index 행과 같은 `Id`로 찾는다(없으면 빠진다).
