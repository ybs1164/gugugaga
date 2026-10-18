# 신앙 효과 CSV

규칙: [faith](../game/faith.md). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseFaithEffects` / `ParseTemplePassives` → `GameTables.FaithEffects` / `GameTables.TemplePassives`. 적용: `FaithEffectSystem`.
신앙 능력은 효과 행을 조합해 만든다 — 신전 패시브는 [`TemplePassives.csv`](#templepassivescsv--신전-패시브)가, 신앙 펀치·신화 유닛 액티브는 [`FaithPunches.csv`](faith-punches.md#faithpunchescsv--신앙-펀치)가 효과 Index를 나열한다. 표 사이 참조와 유형이 그 자리에 맞는지는 `ArrayTableValidationSystem.ValidateFaith`가 불러올 때 경고한다.

## FaithEffects.csv — 신앙 효과
파일: `Assets/Resources/Tables/FaithEffects.csv`
한 행 = 효과 하나. 효과 크기 = `Amount` + `PerPrayer` × (그 신전 곁에서 기도 중인 유닛 수), 0 미만이면 0. 펀치에서는 기도 유닛 수가 0이다. 정수가 필요한 곳(피해·회복·구조물 수·별·신앙 최대치)은 반올림한다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `TemplePassives`·`FaithPunches`의 `Effect{n}`이 가리킨다 |
| `Id` | 문자열 | O | | 이름표(중복 불가). 설명 문구의 자리표시자 `{FaithEffect.<Id>.Amount}`·`.PerPrayer`·`.MinHp`가 가리킨다 |
| `Effect` | 열거 | O | | 효과 유형 — [아래 표](#effect) |
| `Side` | `Enemy`·`Ally` | | `Enemy` | 유닛 효과가 닿는 편(효과를 쓰는 팀 기준) |
| `Stat` | `Attack`·`Defense` | | `Attack` | `Stat`이 올리는 능력치 |
| `Amount` | 실수 | | 0 | 기본 크기 |
| `PerPrayer` | 실수 | | 0 | 신전 패시브에서 기도 유닛 하나당 더해지는 크기(음수면 약해진다) |
| `MinHp` | 정수 | | 0 | `Damage`가 남기는 최소 체력. 0이면 처치할 수 있다 |
| `TargetId` | 구조물 Id | | 없음 | `Structure`가 만들 구조물 |
| `Territory` | `Own`·`OwnOrNeutral`·`Any` | | `Own` | `Structure`가 쓸 수 있는 칸의 영토 |
| `FromTerrain`, `ToTerrain` | `Land`·`Water`·`Mountain` | | `Land` | `Terrain`: 대상 칸이 `FromTerrain`이면 `ToTerrain`으로 바꾼다 |
| `SkipHidden` | 0/1 | | 0 | 숨은 적 유닛은 대상에서 뺀다 |
| `SkipPeace` | 0/1 | | 0 | 평화 조약 상대의 유닛은 대상에서 뺀다 |
| `CountsAsAttack` | 0/1 | | 0 | `Damage`를 공격으로 친다: 전쟁 상태가 되고, 은신이 풀리고, 공격·처치 과업에 센다 |
| `Note` | 메모 | | | 로더 무시 |

### Effect
| 값 | 효과 | 패시브 | 펀치 |
|---|---|---|---|
| `Damage` | 범위 안 `Side` 유닛에게 피해(체력은 `MinHp` 아래로 내려가지 않는다). 기도가 풀린다 | 턴 시작 | O |
| `Heal` | 범위 안 다친 `Side` 유닛의 체력 회복(최대 체력까지) | 턴 시작 | O |
| `Stat` | 범위 안 `Side` 유닛의 `Stat` +크기 | 늘 | |
| `GuardStat` | 범위 안 `Side` 유닛이 방어 태세(`Defend`)일 때 방어력 +크기 | 늘 | |
| `Structure` | 범위 안 `Territory`의 빈 육지 칸(탐험한 칸)에 `TargetId` 구조물을 크기만큼 생성. 가까운 칸부터 | 턴 시작 | O |
| `Stars` | 신전마다 별 +크기/턴 | 늘 | |
| `FaithCap` | 신전마다 신앙 최대 보유량 +크기 | 늘 | |
| `Terrain` | 대상 칸 지형 변화 — 유닛이 있는 칸·도시 칸은 안 된다. 바뀐 칸의 건물·도로·구조물은 사라진다 | | O |
| `Descend` | 대상 아군을 신의 [신화 유닛](../game/faith.md#신화-유닛)으로 바꾼다(강림) | | O |

"패시브" 열의 "늘"은 신전이 있는 동안 계속, "턴 시작"은 그 팀 턴 시작에 패시브 `Period` 턴마다 한 번이다. 빈 칸은 그 자리에 쓸 수 없다(경고).

## TemplePassives.csv — 신전 패시브
파일: `Assets/Resources/Tables/TemplePassives.csv`
한 행 = 신전 패시브 하나. 신([`Gods.csv`](gods.md) `Passive{n}`)을 고른 팀의 신전(`FaithCap`이 있는 자기 영토 건물)마다 신전 칸에서 체비쇼프 `Radius` 안에 효과들을 준다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치. `Gods.csv` `Passive{n}`이 가리킨다 |
| `Id` | 문자열 | O | | 이름표(중복 불가). 자리표시자 `{TemplePassive.<Id>.Radius}`·`.Period` |
| `Radius` | 정수 | | 1 | 신전 칸 중심 체비쇼프 반경 |
| `Period` | 정수 | | 1 | 턴 시작 효과가 이 턴 수마다 한 번 발동(턴 번호가 나누어떨어질 때) |
| `Effect{n}` | [FaithEffects](#faitheffectscsv--신앙-효과) Index | | 없음 | 이 패시브의 효과들 |
| `Note` | 메모 | | | 로더 무시 |
