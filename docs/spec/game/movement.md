# 이동

값: [유닛 CSV](../csv/units.md) `Move.Range`, `Domain`. 코드: `PathfindingSystem`, `Actions/MoveAction`. 위키: [Movement](https://polytopia.fandom.com/wiki/Movement).

## 기본
- 턴당 한 번. 이번 턴 공격했으면 `Retreat`가 있어야 이동할 수 있다.
- 기본 4방향, `AllowDiagonal`이면 8방향.
- 실제 이동력 = `Move.Range` + 가속(`Accelerated`)이면 1.
- 칸 비용 1. 도로 칸끼리(도시·마을·다리 포함) 이동은 0.5. 적 영토의 도로는 쓰지 못한다.

## 들어갈 수 있는 칸
- 구름 칸(미탐험)에는 들어갈 수 없다. `IgnoreTerrain`은 이것만 지키고 아래 지형 조건과 험지 정지를 모두 무시한다.
- 유닛 `Domain`과 칸 `TerrainType`이 같아야 하고, 칸이 `Walkable`이어야 한다.
- 산은 `Move.Mountain`, 깊은 바다는 `Move.Ocean` 해금 키가 트리에 있으면 해금 후에만.
- 육지 유닛은 적 영토가 아닌 다리 칸을 육지처럼 지나간다.
- 다른 유닛이 있는 칸은 지나가지도 멈추지도 못한다. 예외: `IgnoreUnitBlocking`(누구든), `Hide`(적만 지나감, 멈출 수 없음).

## 멈추는 칸 (들어가면 그 턴 이동 끝)
- 험지: 숲(도로 없음)·산. `Creep`은 숲에서 멈추지 않는다(산은 멈춤, 도로 보너스 없음).
- 적 유닛과 인접한 칸(영향권). 인접 판정은 이동 방향 기준(4방향, `AllowDiagonal`이면 8방향). `Hide`, `IgnoreUnitBlocking`은 무시.
- 자기 항구(승선), 배에서 육지로 내림(하선) — [unit](unit.md#배).

## 숨은 적
숨은 적은 모르는 것으로 친다: 길을 막지도, 영향권을 만들지도 않는다. 그 칸으로 들어가려 하면 이동이 취소되고 적이 드러난다(행동 소모 없음).

## 원문과 다른 점
- 기본 이동이 4방향이라 영향권의 "인접"도 4방향으로 본다(위키는 8방향).
