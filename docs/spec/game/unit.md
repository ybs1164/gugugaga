# 유닛

값: [유닛 CSV](../csv/units.md), [`GameRules.csv`](../csv/game-rules.md) `Heal.*`, `Unit.*`, `Veteran.*`. 코드: `UnitFactorySystem`, `Actions/*`, `EmbarkSystem`, `VeteranSystem`, `RuinSystem`(해산). 위키: [Units](https://polytopia.fandom.com/wiki/Units), [Unit Skills](https://polytopia.fandom.com/wiki/Unit_Skills).

## 구성
- 유닛 = 스탯(체력·공격·방어·사거리·이동력) + 이동 타입(`Domain` — [movement](movement.md#이동-타입)) + 행동 목록. 행동이 실행 가능 여부와 효과를 스스로 판단한다(`IUnitAction`).
- 등급: 일반 → [베테랑](#베테랑)(처치 누적). 영웅은 조건(도시 레벨 보상)을 채워야 나오는 슈퍼 유닛(`City.SuperUnitId` — [city](city.md))이다. 신화는 신앙으로 바꾼 유닛이고 지속 턴이 끝나면 사라진다 — [faith](faith.md#신화-유닛).
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
| `Flood` | 패시브 | 서 있는 평지·숲 칸을 침수시키는 행동을 쓴다(행동 소모) — [tile](tile.md#침수와-얼음) |
| `Drench` | 패시브 | 폭우: 공격 사거리 안의 빈 칸도 공격할 수 있고, 공격한 칸을 침수시킨다 |
| `AutoFreeze` | 패시브 | 이동할 때마다 주변 8칸의 빈 물을 얼리고 그 범위의 적을 빙결시킨다 |
| `Charge` | 패시브 | 이동 후에도 공격 가능 |
| `Retreat` | 패시브 | 공격 후에도 이동 가능 |
| `Ambush` | 패시브 | 내 공격에 대상이 반격하지 않음 |
| `Stiff` | 패시브 | 나는 반격하지 않음 |
| `Splash` | 패시브 | 대상 주변 적에게 나눈 피해 |
| `Stomp` | 패시브 | 이동·승선·하선 시 주변 적에게 스플래시 피해 — [배](#배) |
| `Combo` | 패시브 | 처치하면 한 번 더 공격 |
| `Convert` | 패시브 | 살아남은 대상을 내 팀으로 |
| `Freeze` | 패시브 | 살아남은 대상은 다음 자기 턴 행동 불가 |
| `Poison` | 패시브 | 살아남은 대상(스플래시 포함)을 중독시킨다 — [combat](combat.md#중독) |
| `Plunder` | 패시브 | 약탈 — [아래](#약탈) |
| `Herd` | 패시브 | 맨해튼 1 이내 다른 아군에게 가속(이동력 +1). 가속은 피격될 때까지 유지 |
| `Fortify` | 패시브 | 자기 도시 칸 방어 보너스 — [combat](combat.md#방어-보너스) |
| `Scout` | 패시브 | 넓은 시야 — [vision](vision.md#구름) |
| `Hide` | 패시브 | 은신 — [vision](vision.md#은신) |
| `Infiltrate` | 패시브 | 유닛 공격 불가, 대신 적 도시 침투 — [vision](vision.md#침투) |
| `Creep` | 패시브 | 지형무시: 숲·산에서 멈추지 않음, 도로 보너스 없음(물↔육지는 못 오감) |
| `Sneak` | 패시브 | 잠입: 적 영향권에 멈추지 않음(적 유닛 칸은 못 지나감) |
| `Fly` | 패시브 | 비행: 지형 무시 이동, 도로·방어 보너스 없음, 승선 불가 — [movement](movement.md#이동-타입) |
| `Swim` | 패시브 | 헤엄: 배 없이 얕은 물 이동, 승선 불가 |
| `Navigate` | 패시브 | 항해: `Move.Ocean` 해금 없이 깊은 바다 진입 |
| `Skate` | 패시브 | 얼음 칸에서 이동을 시작하면 이동력 +1 |
| `Raze` | 패시브 | 파괴: 지나간 칸(도착 칸 포함)의 건물(다리 제외)·유적을 없앤다 |
| `Grow` | 패시브 | 성장 — [아래](#성장) |
| `IgnoreTerrain` | 패시브 | 지형·장애물 무시 이동 |
| `IgnoreUnitBlocking` | 패시브 | 모든 유닛을 지나가고 그 칸에 멈출 수 있음 |
| `AllowDiagonal` | 패시브 | 8방향 이동 |
| `Independent` | 패시브 | 자립: 도시 수용량을 차지하지 않음 |
| `Wild` | 패시브 | 독립: 누구의 지휘도 받지 않음(플레이어가 고를 수 없고 AI도 움직이지 않음, 턴 끝 자동 대기만), 승급 불가 |
| `Static` | 패시브 | 베테랑 승급 불가 |

모든 유닛이 쓰는 칸 행동: 점령([city](city.md#점령)), 유적 탐험·불가사리 인양([tile](tile.md)), 해산(아래), 얼음 깨기(주변 8칸의 빈 얼음을 물로 — 행동 소모, 이동했다면 `Charge` 필요, `AutoFreeze` 유닛은 못 함), 상륙(침수 칸의 배 — [배](#배)).

새 행동 추가: `Core/ActionType.cs`에 플래그, `Actions/`에 클래스, `UnitCsvActionFactory.BuildActions`에 한 줄, 이 표에 한 행.

## 회복
- `Wait`: 이번 턴 이동도 행동도 하지 않은 유닛만. 자기 영토에서 `Heal.OwnTerritory`, 그 밖에서 `Heal.Other` 회복. 중독된 유닛은 독만 풀린다([combat](combat.md#중독)).
- `Heal`도 중독된 아군은 독만 푼다.
- 턴 종료 시 그런 유닛은 자동으로 `Wait`한다(이동한 유닛은 회복하지 않는다).

## 베테랑
- 공격·반격·스플래시로 처치할 때마다 처치 수 +1.
- 처치 수 `Veteran.KillsRequired` 이상이면 승급 가능: 최대 체력 +`Veteran.MaxHpBonus` 후 완전 회복, 한 번만. 시점은 플레이어가 고르고 행동을 쓰지 않는다.
- 승급 불가: 배(승선 중·물 유닛), 슈퍼 유닛, `Static`, `Wild`.

## 해산
해금 키 `Ability.Disband`. 이번 턴 이동도 행동도 하지 않은 자기 유닛을 없애고 훈련 비용 ÷ `Unit.DisbandRefundDivisor`(내림)를 돌려받는다. 배는 태운 육지 유닛의 비용 기준(업그레이드 비용은 환급 없음).

## 배
값: [`Boats.csv`](../csv/units.md#boatscsv--배). 위키: [Port](https://polytopia.fandom.com/wiki/Port), [Raft](https://polytopia.fandom.com/wiki/Raft).
- 배는 훈련하지 않는다. 육지 유닛이 **자기 팀 항구** 칸에 들어가면 배가 되고 그 턴이 끝난다. 배 종류는 유닛의 `BoatIndex`(-1이면 뗏목).
- 배의 체력은 태운 유닛 체력 그대로. 나머지 스탯·행동은 배 행의 값.
- 배가 육지 칸에 들어가면 원래 유닛으로 돌아오고 그 턴이 끝난다. 업그레이드는 사라진다. 침수 칸에서는 배 그대로 멈추고, 상륙 행동(행동 전)으로 내린다.
- 수륙양용·공중 이동 타입, `Swim`·`Fly` 유닛은 항구에서 배가 되지 않는다.
- 뗏목은 자기 영토 안에서 별(`Cost`)과 해금 키(`Unit.<배 Id>` — TechUnlocks `BoatIndex`)로 업그레이드 배가 된다. 행동을 쓰지 않는다.

- 거인의 `BoatIndex`는 전용 `Juggernaut`를 가리킨다. 짓밟기(`Stomp`)는 이동·승선·하선 시 자기 주변 8칸에 [스플래시](combat.md#공격-후)를 일으킨다. 하선 시 피해는 배 형태의 스탯으로 계산한다.

## 약탈
`Plunder` 유닛. 이번 턴 행동 전(이동했다면 `Charge` 필요)에 공격 사거리(맨해튼, 최소 1) 안의 다른 팀 도시를 고른다. 평화조약 상대, 이번 바퀴에 이미 침투·약탈당한 도시는 안 된다.
- 그 도시의 별 수입만큼 즉시 받고, 도시는 주인의 다음 턴에 별을 만들지 않는다. 유닛은 남고 행동을 쓴다.
- 공격으로 친다: 전쟁 상태, 은신 해제, 평화주의 과업 실패.

## 성장
`Grow` 유닛은 유닛 CSV `Grow.Into`·`Grow.Turns`를 쓴다. 자기 턴 시작마다 남은 턴이 1 줄고, 0이 되면 같은 칸에서 `Grow.Into` 유닛이 된다. 받은 피해·소속 도시·처치 수는 이어진다. 배에 탄 동안에는 자라지 않는다.

## 원문과 다른 점
기본 유닛의 행동 목록은 유닛 기획서를 따른다(위키와 다른 행은 `Units.csv`·`Boats.csv`의 `Note`). 동작 차이:
- 은신은 기획서대로 한 바퀴 동안 맞지 않으면 숨는다(위키는 이동하면 숨음). 영향권 무시·적 유닛 통과는 은신이 아니라 `Sneak`(영향권만)가 맡는다.
- `Creep`(지형무시)은 산에서도 멈추지 않는다(위키는 산 제외).
- 신화 유닛은 지속 턴이 끝나면 원본과 함께 사라진다(기획서).
- 전향된 유닛은 수도 소속이 된다(위키 Convert와 같음 — 예전 구현은 전향시킨 유닛의 도시).
- 파괴(`Raze`)는 기획서의 도시 파괴를 아직 하지 않는다 — 도시 목록 인덱스가 영토·유닛 소속에 묶여 있어 도시를 없애는 경로가 없다. 다리는 그 위 유닛이 물에 빠지므로 남긴다.
- 독립(`Wild`)과 자립(`Independent`)은 기획서 이름을 따른다. 위키 `Independent`가 이 프로젝트의 자립이다.
