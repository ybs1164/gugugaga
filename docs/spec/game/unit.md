# 유닛

값: [유닛 CSV](../csv/units.md), [`GameRules.csv`](../csv/game-rules.md) `Heal.*`, `Unit.*`, `Veteran.*`. 코드: `UnitFactorySystem`, `Actions/*`, `EmbarkSystem`, `VeteranSystem`, `RuinSystem`(해산). 위키: [Units](https://polytopia.fandom.com/wiki/Units), [Unit Skills](https://polytopia.fandom.com/wiki/Unit_Skills).

## 구성
- 유닛 = 스탯(체력·공격·방어·사거리·이동력) + 행동 목록. 행동이 실행 가능 여부와 효과를 스스로 판단한다(`IUnitAction`).
- 모든 유닛은 `Wait`(대기)를 기본으로 갖는다.
- 유닛은 소속 도시를 갖는다 — [city](city.md#유닛-수용량과-훈련).

## 행동과 패시브
`Action{n}` 칸에 적는 이름. **액티브**는 플레이어가 고르고, **패시브**는 조건이 맞으면 자동 적용된다.

| 이름 | 종류 | 효과 |
|---|---|---|
| `Move` | 액티브 | 이동 — [movement](movement.md) |
| `Attack` | 액티브 | 공격 — [combat](combat.md) |
| `Defend` | 액티브 | 다음 자기 턴까지 방어 +`Combat.GuardDefenseBonus` |
| `Heal` | 액티브 | 맨해튼 `Heal.Range` 이내 다른 아군 전부 +`Heal.Amount`(최대 체력까지) |
| `SelfDestruct` | 액티브 | 맨해튼 1 이내 적 전부에게 자기 남은 체력만큼 피해, 자신은 사망 |
| `Wait` | 액티브 | 대기·회복 — [회복](#회복) |
| `Counter` | 패시브 | 반격 |
| `Charge` | 패시브 | 이동 후에도 공격 가능 |
| `Retreat` | 패시브 | 공격 후에도 이동 가능 |
| `Ambush` | 패시브 | 내 공격에 대상이 반격하지 않음 |
| `Stiff` | 패시브 | 나는 반격하지 않음 |
| `Splash` | 패시브 | 대상 주변 적에게 나눈 피해 |
| `Combo` | 패시브 | 처치하면 한 번 더 공격 |
| `Convert` | 패시브 | 살아남은 대상을 내 팀으로 |
| `Freeze` | 패시브 | 살아남은 대상은 다음 자기 턴 행동 불가 |
| `Herd` | 패시브 | 맨해튼 1 이내 다른 아군에게 가속(이동력 +1). 가속은 피격될 때까지 유지 |
| `Fortify` | 패시브 | 자기 도시 칸 방어 보너스 — [combat](combat.md#방어-보너스) |
| `Scout` | 패시브 | 넓은 시야 — [vision](vision.md#구름) |
| `Hide` | 패시브 | 은신 — [vision](vision.md#은신) |
| `Infiltrate` | 패시브 | 유닛 공격 불가, 대신 적 도시 침투 — [vision](vision.md#침투) |
| `Creep` | 패시브 | 숲에서 멈추지 않음, 도로 보너스 없음 |
| `IgnoreTerrain` | 패시브 | 지형·장애물 무시 이동 |
| `IgnoreUnitBlocking` | 패시브 | 모든 유닛을 지나가고 그 칸에 멈출 수 있음 |
| `AllowDiagonal` | 패시브 | 8방향 이동 |
| `Independent` | 패시브 | 도시 수용량을 차지하지 않음 |
| `Static` | 패시브 | 베테랑 승급 불가 |

새 행동 추가: `Core/ActionType.cs`에 플래그, `Actions/`에 클래스, `UnitCsvActionFactory.BuildActions`에 한 줄, 이 표에 한 행.

## 회복
- `Wait`: 이번 턴 이동도 행동도 하지 않은 유닛만. 자기 영토에서 `Heal.OwnTerritory`, 그 밖에서 `Heal.Other` 회복.
- 턴 종료 시 그런 유닛은 자동으로 `Wait`한다(이동한 유닛은 회복하지 않는다).

## 베테랑
- 공격·반격·스플래시로 처치할 때마다 처치 수 +1.
- 처치 수 `Veteran.KillsRequired` 이상이면 승급 가능: 최대 체력 +`Veteran.MaxHpBonus` 후 완전 회복, 한 번만. 시점은 플레이어가 고르고 행동을 쓰지 않는다.
- 승급 불가: 배(승선 중·물 유닛), 슈퍼 유닛, `Static`.

## 해산
해금 키 `Ability.Disband`. 이번 턴 이동도 행동도 하지 않은 자기 유닛을 없애고 훈련 비용 ÷ `Unit.DisbandRefundDivisor`(내림)를 돌려받는다. 배는 태운 육지 유닛의 비용 기준(업그레이드 비용은 환급 없음).

## 배
값: [`Boats.csv`](../csv/units.md#boatscsv--배). 위키: [Port](https://polytopia.fandom.com/wiki/Port), [Raft](https://polytopia.fandom.com/wiki/Raft).
- 배는 훈련하지 않는다. 육지 유닛이 **자기 팀 항구** 칸에 들어가면 배가 되고 그 턴이 끝난다. 배 종류는 유닛의 `BoatIndex`(-1이면 뗏목).
- 배의 체력은 태운 유닛 체력 그대로. 나머지 스탯·행동은 배 행의 값.
- 배가 육지 칸에 들어가면 원래 유닛으로 돌아오고 그 턴이 끝난다. 업그레이드는 사라진다.
- 뗏목은 자기 영토 안에서 별(`Cost`)과 해금 키(`Unit.<배 Id>` — TechUnlocks `BoatIndex`)로 업그레이드 배가 된다. 행동을 쓰지 않는다.
