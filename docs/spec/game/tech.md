# 기술

값: [기술 CSV](../csv/tech.md), [`GameRules.csv`](../csv/game-rules.md) `Tech.*`. 코드: `TechSystem`, `TechGroupSystem`, `TechEffectSystem`. 위키: [Technology](https://polytopia.fandom.com/wiki/Technology).

## 트리
- 고정 슬롯 트리에 기술을 배치한다. 선행 기술 = 선행 슬롯에 놓인 기술. 루트 슬롯 기술은 처음부터 연구 가능.
- 팀은 자기 종족 기술 그룹에 든 기술만 보고 연구할 수 있다. 종족이 없으면 전부.
- 종족의 시작 기술은 처음부터 해금되어 있다.

## 연구
- 비용 = `CostBase` + `CostPerCity` × 보유 도시 수. 도시가 0이어도 `CostBase`는 든다.
- 해금 키 `Literacy`를 가진 팀은 비용에서 1/`Tech.LiteracyDivisor`를 깎는다(올림).
- 조건: 선행 기술 해금 + 별 충분. 별을 내고 즉시 해금된다.

## 효과
기술 이름이 아니라 **해금 키**로만 효과가 연결된다 — 키 목록은 [해금 키](../csv/tech.md#해금-키).
- 트리에 어떤 키가 아예 없으면 처음부터 열린 것으로 본다.
- `Reveal.<StructureId>`가 트리에 있으면 해금 전까지 그 구조물이 지도·정보 패널에 보이지 않는다.
- `Move.*`, `Defense.*`는 유닛 컴포넌트로 옮겨 적힌다(이동·턴 시작·연구·점령·훈련 뒤 갱신) — [movement](movement.md), [combat](combat.md#방어-보너스).

## 원문과 다른 점
- 외교 기술의 대사관·평화 조약, 전략 기술의 평화 조약: 외교 시스템이 없어 제외.
