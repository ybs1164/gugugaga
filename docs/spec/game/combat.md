# 전투

수치: [`GameRules.csv`](../csv/game-rules.md) `Combat.*`. 코드: `CombatSystem`, `Actions/AttackAction`, `Actions/CounterAction`. 위키: [Combat](https://polytopia.fandom.com/wiki/Combat).

## 공격 조건
- 살아 있는 다른 팀 유닛, 맨해튼 거리 1 ~ `Attack.Range`.
- 턴당 한 번. 이번 턴 이동했으면 `Charge`가 있어야 한다. `Retreat`가 있으면 공격 뒤 이동할 수 있다 — [movement](movement.md#기본).
- 숨은 적은 노릴 수 없다. `Infiltrate` 유닛은 유닛을 공격하지 못한다([vision](vision.md#침투)).

## 피해 공식
공격 전 체력 기준으로 공격·반격을 한 번에 계산한다(`CombatSystem.Resolve`).

```
attackForce  = 공격자.공격 × (공격자 체력 / 최대 체력)
defenseForce = 대상.방어 × (대상 체력 / 최대 체력) × 방어 보너스 배수
공격 피해 = round(attackForce / (attackForce + defenseForce) × 공격자.공격 × Combat.DamageCoefficient)
반격 피해 = round(defenseForce / (attackForce + defenseForce) × 대상.방어 × Combat.DamageCoefficient)
```
- 반올림은 .5 올림.
- 대상.방어에는 방어 태세(`Defend`) 중이면 `Combat.GuardDefenseBonus`를 더한다.

## 반격
- 대상이 살아남고 `Counter`를 가졌고, 공격자가 대상의 사거리 안이면 반격한다.
- 반격 없음: 대상이 `Stiff`, 공격자가 `Ambush`, 또는 공격으로 대상이 전향(`Convert`)됨.

## 방어 보너스
배수 하나만 적용한다(겹치지 않음). 공중 이동 타입·`Fly` 유닛과 중독된 유닛은 항상 1:
| 조건 | 배수 |
|---|---|
| 없음 | 1 |
| 그 지형의 `Defense.*` 해금 키를 가진 팀 유닛이 산·숲·물 칸에 있음, 또는 `Fortify` 유닛이 자기 도시 칸에 있음 | `Combat.DefenseBonusMultiplier` |
| `Fortify` 유닛이 성벽 있는 자기 도시 칸에 있음 | `Combat.WallDefenseMultiplier` |

## 공격 후
- 근접(사거리 1) 유닛이 인접한 적을 처치하면 그 칸으로 전진한다(`Combat.MeleeAdvanceOnKill`).
- `Splash`: 대상 주변 8칸(체비셰프 거리 1)의 적에게 (그 적에게 계산한 공격 피해 ÷ `Combat.SplashDivisor`, 내림). 짓밟기(`Stomp`, [배](unit.md#배))도 같은 규칙이다: 평화조약 상대는 맞지 않고, 맞은 상대와는 전쟁 상태가 되며, 숨은 유닛은 드러나고, 반격은 없다.
- `Combo`: 처치하면 같은 턴에 한 번 더 공격할 수 있다.
- `Freeze`: 살아남은 대상은 다음 자기 턴에 행동하지 못한다.
- `Convert`: 살아남은 대상이 공격자 팀이 된다.
- `Poison`: 살아남은 대상(스플래시로 맞은 적 포함)이 중독된다 — 아래.
- `Drench`: 공격한 칸이 잠긴다 — [tile](tile.md#침수와-얼음).
- 피격된 유닛은 가속(`Accelerated`)을 잃고, 은신이 풀린다([vision](vision.md#은신)).
- 전향된 유닛은 공격한 팀의 수도 소속이 된다(수용량을 넘어도 됨, 수도가 없으면 공격한 유닛의 도시, `Independent`면 소속 없음).
- 처치는 처치한 유닛의 베테랑 카운트와 과업 처치 수에 들어간다(공격·반격·스플래시 모두).

## 중독
- 방어 보너스 배수 1, 방어 스탯 x `Poison.DefenseMultiplier`.
- 대기·치유로 회복하면 독이 풀리고, 그 회복은 체력을 채우지 않는다.

## 원문과 다른 점
- 중독은 기획서 기준이다: 방어 보너스가 사라지고 방어력이 줄어든다. 위키의 이동력 -1·사망 시 포자는 없다.
- 스플래시는 위키에서 나누기만 해서 체력이 .5가 되는 버그가 있어 내림한다.
- 방어 태세(`Defend`)는 위키에 없는 프로젝트 고유 행동이다.
