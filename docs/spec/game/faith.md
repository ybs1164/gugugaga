# 신앙

값: [`GameRules.csv`](../csv/game-rules.md) `Faith.*`. 코드: `FaithSystem`, `FaithHud`. 위키에 없는 확장이다 — 기획 계획은 [`FaithPlan`](../../history/FaithPlan.md).

## 신앙 포인트
- 팀 공용 재화 하나. 별([turn](turn.md#별))과 따로 센다.
- 팀의 최대 보유량은 `Faith.BaseCap` + 도시 레벨 합 × `Faith.CityLevelCap` + 자기 영토 신전들의 [`FaithCap`](../csv/buildings.md). 도시·건물 변화로 상한이 다시 계산되고, 현재량이 새 상한을 넘으면 상한까지 줄어든다.
- 최대 보유량을 넘는 획득분은 버려진다. 현재량은 0 아래로 내려가지 않는다.

## 턴 시작 획득
- 별 수입과 같은 시점(2턴부터 자기 턴 시작 때)에 자동으로 받는다.
- 살아있는 유닛 수가 많은 팀이 높은 순위다. 같은 수면 공동 순위이고 같은 양을 받는다. 공동 1위 다음은 3위다.
- 획득량 = `Faith.BaseGain` + 순위 보너스(`Faith.RankBonus1`~`3`, 4위 이하는 없음) + `Faith.TurnsPerGrowth` 턴마다 +1.
- 기도 중인 유닛 하나당 `Faith.PrayGain`을 더 받는다.

## 기도
- 신전(`FaithCap`이 있는 건물) 주변 `Faith.PrayRange`칸(체비쇼프) 안의 자기 유닛이 고른다. 신전은 자기 팀 영토에 있어야 한다. 유닛 메뉴의 기도 버튼(`ActionType.Pray`)이며 유닛 CSV의 행동이 아니다.
- 고르면 그 유닛의 이번 턴 행동이 끝난다. 이미 행동한 유닛은 못 한다.
- 기도 중에는 방어력이 0이다.
- 기도는 피격(공격·반격·자폭·스플래시·짓밟기), 이동, 공격으로 풀린다. 신전이 사라지거나 멀어져도 턴 시작에 풀린다.

## 화면
- `FaithHud`: 현재/최대와 게이지, 다음 턴 예상 획득량과 내 순위. 별 표시줄 아래에 놓인다. 문구는 [`Strings.csv`](../csv/strings.md) `UI.Faith.*`.

## 아직 없는 것
신·신앙 펀치·신화 유닛·신앙 승리 — [`FaithPlan`](../../history/FaithPlan.md) 단계 3 이후.
