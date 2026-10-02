# 과업·점수

값: [과업 CSV](../csv/tasks.md), [`GameRules.csv`](../csv/game-rules.md) `Score.*`. 코드: `TaskSystem`, `ScoreSystem`. 위키: [Score](https://polytopia.fandom.com/wiki/Score), [Buildings](https://polytopia.fandom.com/wiki/Buildings)(Monuments).

## 과업
- 팀별 진행 값(연속 비공격 턴, 처치 수 등)을 기록하고, 상태가 바뀔 때마다(턴 시작·끝, 경제 행동, 전투) 달성을 확인한다.
- 과업의 `Unlock` 키가 있으면 그 기술을 연구해야 과업이 열린다.
- 달성한 과업마다 그 과업을 `Task`로 가진 기념물을 한 번 무료로 지을 수 있다 — [building](building.md#효과).
- 반격은 공격이 아니다. 침투도 공격이 아니다.

## 점수
저장하지 않고 지금 상태에서 매번 계산한다(유닛이 죽거나 건물·도시를 잃으면 줄어든다). 항목별 값은 `Score.*` 키의 `Description`.
- 유닛(비용 비례, 슈퍼 유닛은 고정), 영토 칸, 탐험한 칸, 도시(기본 + 레벨 + 인구), 공원, 기념물, 신전(레벨), 해금한 기술(티어 비례).
- 신전 레벨: 지은 턴부터 `Score.TempleTurnsPerLevel` 턴마다 1, 최대 `Score.TempleMaxLevel`.

## 원문과 다른 점
- 과업 개방 조건 중 "등대를 하나 찾으면", "적을 처치하면", "도시 레벨 업 시"는 그 조건 없이 달성 자체가 불가능하므로 생략했다(`Tasks.csv` `Note`).
- Cymanti 전용 과업은 해당 부족이 없어 제외.
