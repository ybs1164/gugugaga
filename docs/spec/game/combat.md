# 전투

수치: [`GameRules.csv`](../csv/game-rules.md) `Combat.*`. 코드: `CombatSystem`, `Actions/AttackAction`, `Actions/CounterAction`. 위키: [Combat](https://polytopia.fandom.com/wiki/Combat).

## 공격 조건
- 살아 있는 다른 팀 유닛, 맨해튼 거리 1 ~ `Attack.Range`.
- 턴당 한 번. 이번 턴 이동했으면 `Charge`가 있어야 한다. 이번 턴 공격했으면 `Retreat`가 있어야 이동할 수 있다.
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
배수 하나만 적용한다(겹치지 않음):
| 조건 | 배수 |
|---|---|
| 없음 | 1 |
| 그 지형의 `Defense.*` 해금 키를 가진 팀 유닛이 산·숲·물 칸에 있음, 또는 `Fortify` 유닛이 자기 도시 칸에 있음 | `Combat.DefenseBonusMultiplier` |
| `Fortify` 유닛이 성벽 있는 자기 도시 칸에 있음 | `Combat.WallDefenseMultiplier` |

## 공격 후
- 근접(사거리 1) 유닛이 인접한 적을 처치하면 그 칸으로 전진한다(`Combat.MeleeAdvanceOnKill`).
- `Splash`: 대상과 맨해튼 거리 1인 다른 적에게 (그 적에게 계산한 공격 피해 ÷ `Combat.SplashDivisor`, 내림).
- `Combo`: 처치하면 같은 턴에 한 번 더 공격할 수 있다.
- `Freeze`: 살아남은 대상은 다음 자기 턴에 행동하지 못한다.
- `Convert`: 살아남은 대상이 공격자 팀이 된다.
- 피격된 유닛은 가속(`Accelerated`)을 잃는다.
- 처치는 처치한 유닛의 베테랑 카운트와 과업 처치 수에 들어간다(공격·반격·스플래시 모두).

## 원문과 다른 점
- 스플래시는 위키에서 나누기만 해서 체력이 .5가 되는 버그가 있어 내림한다.
- 방어 태세(`Defend`)는 위키에 없는 프로젝트 고유 행동이다.
