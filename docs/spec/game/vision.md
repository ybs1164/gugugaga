# 시야·은신·침투

수치: [`GameRules.csv`](../csv/game-rules.md) `Vision.*`, `Infiltration.*`. 코드: `VisionSystem`, `StealthSystem`, `InfiltrationSystem`. 위키: [Terrain](https://polytopia.fandom.com/wiki/Terrain)(Cloud), [Explorer](https://polytopia.fandom.com/wiki/Explorer), [Cloak](https://polytopia.fandom.com/wiki/Cloak).

## 구름
- 팀마다 탐험한 칸을 기록한다. 한 번 밝힌 칸은 계속 보인다.
- 유닛은 반경 `Vision.BaseSightRadius`를 밝힌다. 산 위 또는 `Scout`면 `Vision.ExtendedSightRadius`(이 값이 상한).
- 자기 영토는 항상 밝다. 전투 시작 시 수도 주변 `Vision.StartRevealRadius`가 보인다.
- 해금 키 `Vision.Capital`을 가진 팀은 다른 팀 수도 칸이 보인다.
- 구름 칸에는 들어가거나 건설·행동할 수 없고, AI도 그 안의 적·정착지를 모른다.
- 경제가 없는 씬에서는 구름이 없다.

## 등대
처음 밝힌 등대마다 수도 인구 +`Vision.LighthousePopulation`(수도가 없으면 가장 오래된 도시). 전부 밝히면 탐험가 과업.

## 탐험가
도시 보상·유적 보상. `Vision.ExplorerMoves`번 움직이며 가까운 구름 쪽으로 가고, 걸음마다 주변 3×3을 밝힌다. 산·얕은 물·깊은 바다는 각각 `Move.Mountain`·`Move.Ocean`·`Connect.Ocean` 해금 후에만.
세부: `VisionSystem.RunExplorer`.

## 은신
`Hide` 유닛.
- 이동하면 숨는다. 공격·점령·유적 탐험·훈련 시, 또는 적이 그 칸에 들어가려 할 때 드러난다.
- 숨은 유닛은 다른 팀의 공격 대상·AI 인식·영향권·경로 차단에서 빠진다([movement](movement.md#숨은-적)).
- 적 유닛 8방향 옆에 숨은 유닛이 있으면, 그 적은 "근처에 숨은 적"만 안다(위치는 모름).

## 침투
`Infiltrate` 유닛.
- 조건: 이번 턴 행동 전. 이동했다면 `Charge`가 있고 턴을 숨은 채 시작했어야 한다. 대상은 상하좌우로 인접한 다른 팀 도시 — 포위됐거나 이미 침투당한 도시는 제외.
- 효과: 침투 유닛은 소모된다. 도시 칸의 적 유닛은 침투 유닛 공격력만큼 피해. 도시 레벨만큼(최대 `Infiltration.MaxDaggers`) `Infiltration.DaggerUnitId` 유닛이 침투 팀으로 그 영토에 나타나 다음 턴까지 행동하지 못한다(물 칸이면 그 유닛의 `BoatIndex` 배). 침투 팀이 그 도시의 별 수입만큼 즉시 받고, 도시는 주인의 다음 턴 수입이 0.
- 침투는 공격이 아니다(평화주의 과업에 영향 없음).
- 소환 칸 우선순위 세부: `InfiltrationSystem`.

## 원문과 다른 점
- 수도 시야: 위키는 "발견한 부족"의 수도만 보이지만 팀이 둘뿐이라 발견 조건을 생략했다.
