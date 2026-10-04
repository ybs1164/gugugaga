# 건물·타일 행동

값: [`Buildings.csv`](../csv/buildings.md), [`TechUnlocks.csv`](../csv/tech.md#techunlockscsv--해금-내역) `BuildingIndex`, [`TileActions.csv`](../csv/tile-actions.md), [`GameRules.csv`](../csv/game-rules.md) `City.Market*`. 코드: `TileImprovementSystem`. 위키: [Buildings](https://polytopia.fandom.com/wiki/Buildings).

## 공통 배치 조건
- 자기 영토 안에서만. 예외: `Neutral` 태그(도로·다리)는 중립 땅·물도 가능. 적 영토는 불가.
- 칸 하나에 건물 하나. 도로는 건물이 아니라 칸 개량이라 건물과 공존한다.
- 도시·마을·유적·등대 칸에는 짓지 않는다. 자원 칸은 그 자원이 필요한 건물·행동만.
- 구름 칸에서는 아무것도 못 한다.
- 지형(`Terrain{n}`), 필요 구조물(`RequiredStructure{n}`, 지으면 소모), 해금 키(TechUnlocks `BuildingIndex`가 가리키면 `Build.<Id>`), 별(`Cost`)을 모두 만족해야 한다.
- 메뉴: 기술·지형·구조물이 안 맞는 항목은 숨기고, 별·인접 조건만 모자라면 비활성 + 이유를 보여준다.

## 효과
- `Population`은 그 칸을 영토로 가진 도시에 들어간다.
- 인접 조건과 인접 인구는 8방향, **같은 팀 영토** 칸만 센다.
- 가공 건물(`PopulationPerAdjacent`): 옆에 기반 건물이 생기거나 없어지면 가공 건물이 주는 인구도 같이 바뀐다.
- `OnePerCity`: 도시 영토당 하나.
- 시장(`StarsFromAdjacent`): 인접 가공 건물들의 인구 합 × `City.MarketStarsPerLevel`만큼 별/턴, 상한 `City.MarketStarsCap`.
- 신전(`Temple`): 지은 턴부터 레벨이 오른다 — 점수는 [task-score](task-score.md).
- 기념물(`Task`): 그 과업을 달성하면 한 번 무료로 지을 수 있다.
- 다리(`OppositeLand`): 상하 또는 좌우 양쪽 이웃이 육지인 물 칸. 육지 유닛은 다리 칸을 육지처럼 지나간다.
- 항구: 육지 유닛이 들어가면 배가 된다 — [unit](unit.md#배).
- 도로·항구처럼 수도 연결이 바뀌는 건설 뒤에는 연결을 다시 계산한다([city](city.md#수도-연결)).
- 건물 레벨(표시용): 신전 = 지난 턴 기반, 가공 건물 = 인접 기반 건물 수, 시장 = 인접 가공 인구 합, 나머지 = 1.

## 타일 행동
- 배치 조건은 건물과 같다(영토, 지형, 해금 키, 별, 필요 구조물).
- 효과 종류는 [`TileActions.csv` Kind](../csv/tile-actions.md#kind).
- 숲을 없앤 칸은 주변 8칸에서 가장 흔한 평지 타일이 된다(없으면 `Grass`).

## 원문과 다른 점
- 대장간 지형: 위키 표는 "벌목장 인접"이지만 효과(인접 광산당 인구)와 맞지 않는 오기라 광산 인접으로 했다.
- 얼음 신전과 Cymanti·Aquarion 전용 건물은 해당 지형·부족이 없어 제외.
- 화전 비용: 위키 문서끼리 값이 달라 Burn Forest 정보상자·Population 표 값을 따른다.
- 항구 해금: 위키 Technology 표(낚시)와 Buildings 표(배 타기)가 달라 Technology 표를 따른다.

## 대사관
- `Embassy` 플래그 건물은 외교 기술을 연구한 팀이 전쟁 중이 아닌 상대의 원래 수도에서 타일 메뉴로 건설한다. 부족마다 하나만 가능하며 수도 건물과 공존한다.
- 비용은 Buildings.csv, 양쪽 별 수입·평화 조약 배수·주변 공개 반경은 GameRules.csv `Diplomacy.*`.
- 건설 시 수도 주변을 탐험한다. 양쪽 대사관을 동시에 둘 수 있다.
- 유닛 공격·도시 점령·짓밟기 공격은 전쟁 상태로 바꾸고 양쪽 대사관을 없앤다. 수도가 점령되면 그 수도의 대사관을 모두 없앤다. 실패한 공격은 상태를 바꾸지 않는다.
- 초기 관계는 전쟁 전 상태다. 평화 조약 상태의 수입과 공격 금지는 `DiplomacySystem.SetPeaceTreaty`로 적용하며, 조약 제안·수락 UI는 없다.
