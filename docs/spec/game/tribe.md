# 종족

값: [종족 CSV](../csv/tribes.md), [종족 지형 CSV](../csv/tribe-terrain.md). 코드: `TribeSystem`, `StartConditionSystem`. 위키: [Tribes](https://polytopia.fandom.com/wiki/Tribes).

## 적용
샌드박스에서 팀(아군/적)마다 종족을 고르면 전투 시작 때 적용된다:
- 시작 별 `StartStars`
- 시작 수도 레벨 `StartCapitalLevel` — 작업장·탐험가·성벽·미선택 보상 없이 시작
- 연구 가능한 기술 = 기술 그룹 `TechGroupIndex` — [tech](tech.md#트리)
- 처음부터 해금된 기술 `StartTech{n}`
- 수도의 시작 유닛 `StartUnit{n}` — [city](city.md#유닛-수용량과-훈련)
- 그 팀 수도 영역의 지형(아래 영역 지형), 수도 주변 시작 조건 `StartConditionIndex` — [map](map.md#단계)

종족을 고르지 않은 팀은 기본값(전체 기술, `Economy.StartingStars`, 기본 시작 유닛, `Default` 지형 묶음)을 쓴다.

## 영역 지형
맵의 수도 영역은 팀마다 하나다(영역 수 = 팀 수). 영역 지형은 그 팀 종족의 [`TribeTerrain.csv`](../csv/tribe-terrain.md) 묶음(묶음 Id = 종족 `Id`)이 정한다 — 노이즈, 타일 비율, 숲·산 배수, 마을·자원·유적 배치까지 종족마다 따로 적는다. 종족이 없거나 묶음이 없으면 `Default` 묶음. 코드: `TribeSystem.RegionBiomes`.
- 평지: 묶음의 첫 `Land` 타일. 종족마다 다른 평지 Id(`Field_<종족>`)를 써서 색만 다른 맨땅(`Ground.<Id>`)이 되고, 그 위에 생긴 숲·산의 바닥도 이 맨땅이다([tile](tile.md#타일-값) `GroundTileId`). 평지 Id가 달라도 규칙상 같은 `Field`다([tile](tile.md)).
- 같은 타일 묶음 안 자원 비율 합이 1을 넘으면 CSV 앞쪽 자원부터 칸을 가져간다([map](map.md#단계) 8-5).

## 원문과 다른 점
- 묶음의 숲·산 배수와 자원 비율은 위키 Tribes의 산·숲·자원 배수를 바탕 지형(평원·사막·고지대 셋)에 곱해 미리 적은 값이다. 물 배수는 넣지 않는다(원문도 실제 생성에 반영되지 않는다고 적음 — [reference](../../reference/PolytopiaMapGeneration.md) 4절).
- 원문은 종족이 없는 플레이어가 없다. 종족을 고르지 않은 팀은 `Default` 묶음(평원)을 쓴다.
