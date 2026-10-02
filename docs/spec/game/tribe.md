# 종족

값: [종족 CSV](../csv/tribes.md). 코드: `TribeSystem`, `StartConditionSystem`. 위키: [Tribes](https://polytopia.fandom.com/wiki/Tribes).

## 적용
샌드박스에서 팀(아군/적)마다 종족을 고르면 전투 시작 때 적용된다:
- 시작 별 `StartStars`
- 연구 가능한 기술 = 기술 그룹 `TechGroupIndex` — [tech](tech.md#트리)
- 처음부터 해금된 기술 `StartTech{n}`
- 수도의 시작 유닛 `StartUnit{n}` — [city](city.md#유닛-수용량과-훈련)
- 그 팀 수도 영역의 바이옴 `BiomeIndex`, 수도 주변 시작 조건 `StartConditionIndex` — [map](map.md#단계)

종족을 고르지 않은 팀은 기본값(전체 기술, `Economy.StartingStars`, 기본 시작 유닛)을 쓴다.

## 원문과 다른 점
- Luxidoor의 "레벨 3 수도로 시작"은 도시 시작 레벨을 표에 두지 않아 아직 없다.
- 종족 고유 지형 배수는 바이옴으로 근사한다 — 기본 바이옴 표가 셋뿐이라 여러 종족이 같은 바이옴을 쓴다.
