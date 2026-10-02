# 턴·별

수치: [`GameRules.csv`](../csv/game-rules.md) `Economy.*`, `Heal.*`. 코드: `TurnSystem`, `CityResourceSystem`, `BattleController`.

## 턴 순서
- 두 팀(`Player`, `Enemy`)이 번갈아 진행한다. 턴 번호는 `Player` 차례가 올 때 오른다.
- AI 팀은 경제 행동을 먼저 하고 유닛을 움직인다 — [ai](ai.md).

## 턴 시작 (그 팀)
1. 별 수입을 한 번 받는다([city](city.md#별-수입)). 침투당했던 도시는 이 수입이 0이고, 받은 뒤 침투 상태가 풀린다.
2. 유닛의 이동·행동·방어 태세를 초기화한다. 빙결된 유닛은 이번 턴 이동·행동이 막히고 빙결이 풀린다.
3. 은신 유닛의 "턴 시작 시 숨어 있었는지"를 기록한다([vision](vision.md#은신)).
4. 무리 오라를 다시 계산한다([unit](unit.md#행동과-패시브)).

## 턴 종료 (그 팀)
- 이번 턴 이동도 행동도 하지 않은 유닛은 자동으로 대기(회복)한다([unit](unit.md#회복)).
- 공격하지 않은 턴이면 평화주의 과업 카운트가 오른다([task-score](task-score.md)).

## 별
- 팀 공용 재화 하나. 연구·건설·타일 행동·훈련·배 업그레이드에 모두 쓴다.
- 시작 별: 종족이 있으면 [tribes](../csv/tribes.md) `StartStars`, 없으면 `Economy.StartingStars`.

## 승패
- 도시를 한 번이라도 가졌던 팀은 도시를 전부 잃으면 패배한다.
- 도시를 가진 적 없는 팀(경제 없는 데모 씬)은 유닛이 전멸하면 패배한다.
