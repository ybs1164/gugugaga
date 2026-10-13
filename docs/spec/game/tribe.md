# 종족

값: [종족 CSV](../csv/tribes.md). 코드: `TribeSystem`, `StartConditionSystem`. 위키: [Tribes](https://polytopia.fandom.com/wiki/Tribes).

## 적용
샌드박스에서 팀(아군/적)마다 종족을 고르면 전투 시작 때 적용된다:
- 시작 별 `StartStars`
- 시작 수도 레벨 `StartCapitalLevel` — 작업장·탐험가·성벽·미선택 보상 없이 시작
- 연구 가능한 기술 = 기술 그룹 `TechGroupIndex` — [tech](tech.md#트리)
- 처음부터 해금된 기술 `StartTech{n}`
- 수도의 시작 유닛 `StartUnit{n}` — [city](city.md#유닛-수용량과-훈련)
- 그 팀 수도 영역의 지형(아래 영역 지형), 수도 주변 시작 조건 `StartConditionIndex` — [map](map.md#단계)

종족을 고르지 않은 팀은 기본값(전체 기술, `Economy.StartingStars`, 기본 시작 유닛, 첫 바이옴 그대로)을 쓴다.

## 영역 지형
맵의 수도 영역은 팀마다 하나다(영역 수 = 팀 수). 바이옴을 따로 고르지 않고, 영역 바이옴은 그 팀 종족이 정한다. 코드: `TribeSystem.RegionBiome`.
1. 기본 바이옴 = `Biome`.
2. 평지: 기본 바이옴의 평지 타일(첫 `Land` 타일)을 `GroundTile`로 바꾼다 — 타일 행과 구조물·자원의 `AllowedTile`, 타일 `Exclude`까지. 종족마다 색만 다른 맨땅(`Ground.<GroundTile>`)이 된다. 평지 Id가 달라도 규칙상 같은 `Field`다([tile](tile.md)).
3. 배수: 숲·산 레이어 = 바이옴 배수 × `MountainRate`/`ForestRate`. 자원 = 바이옴 `InnerRate`/`OuterRate` × 자원별 배수(`FruitRate` 등). 같은 타일 묶음 안 비율 합이 1을 넘으면 CSV 앞쪽 자원부터 칸을 가져간다([map](map.md#단계) 8-5).

## 원문과 다른 점
- 종족 지형 배수는 위키 Tribes의 산·숲·자원 배수를 그대로 쓰되, 물 배수는 넣지 않는다(원문도 실제 생성에 반영되지 않는다고 적음 — [reference](../../reference/PolytopiaMapGeneration.md) 4절).
- 기본 바이옴은 셋(평원·사막·고지대)뿐이라 여러 종족이 같은 바이옴을 쓴다. 종족마다 다른 것은 평지 색과 배수다.
- 원문은 종족이 없는 플레이어가 없다. 종족을 고르지 않은 팀은 첫 바이옴을 배수 없이 쓴다.
