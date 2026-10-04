# 명세 (spec)

이 프로젝트의 **현재 동작**과 **CSV 형식**을 정의하는 문서. 작업 전에 이 파일을 먼저 읽는다.

## 정보는 한 곳에만 (단일 출처)

새로 적기 전에 아래 표에서 그 정보가 들어갈 자리를 먼저 찾는다. 이미 다른 곳에 있는 정보는 **다시 적지 않고 링크**한다.

| 정보 | 유일한 위치 | 다른 문서에서는 |
|---|---|---|
| 수치(비용·스탯·상수·확률) | CSV 셀 (`Assets/Resources/**.csv`) | 키·컬럼 이름만 적는다. 숫자를 옮겨 적지 않는다 |
| 화면에 보이는 문자열(이름·설명·버튼·안내) | [`Strings.csv`](csv/strings.md) | 키만 적는다. 코드·데이터 표에 문구를 두지 않는다 |
| 컬럼의 뜻·타입·기본값·허용값 | [`csv/<표>.md`](csv/) | 링크 |
| CSV 공통 형식(읽기 규칙, 반복 컬럼, Index) | [`csv-common.md`](csv-common.md) | 링크 |
| 게임 규칙(플레이어가 겪는 동작) | [`game/<요소>.md`](game/) | 링크 |
| 원문(위키)과 다른 점 | 수치는 그 CSV 행의 `Note`(`GameRules.csv`는 `Wiki`·`Note`), 동작은 해당 `game/*.md`의 "원문과 다른 점" | 링크 |
| 알고리즘·구현 세부(탐색 순서, 가중치 계산 등) | 코드 주석(해당 System) | `세부: TypeName.Method` 한 줄 |
| 위키 조사 요약 | [`../reference/`](../reference/) | 링크 |
| 작업 이력·설계 비교 | git 커밋 메시지 (과거 기록은 [`../history/`](../history/)) | 적지 않는다 |
| 실행·조작·빌드 방법 | [`/README.md`](../../README.md) | 링크 |

작성 규칙:
1. spec은 **현재 상태만** 적는다. 날짜, "N차 재정비", "예전에는" 같은 이력은 커밋 메시지에 남긴다.
2. 열거값 목록(`Kind`, `Flag` 등)은 그 열거를 쓰는 CSV의 `csv/*.md`에 한 번만 적는다.
3. 코드·CSV를 바꾸면 **같은 커밋에서** 해당 spec을 고친다. 고칠 spec이 없으면 위 표의 자리에 새로 만든다.
4. 코드 주석에는 규칙을 다시 풀어 쓰지 않고 `// 규칙: docs/spec/game/<요소>.md`로 가리킨다(기존 주석은 고칠 때 정리).
5. 문서를 다 쓰면 `python scripts/check_spec.py`로 CSV 헤더와 컬럼 표, 링크가 맞는지 확인한다.

## 요소 지도

| 요소 | 규칙 | CSV | 주요 System |
|---|---|---|---|
| 맵 생성 | [map](game/map.md) | [biomes](csv/biomes.md), [tribes](csv/tribes.md), [game-rules](csv/game-rules.md) `Map.*` | `TerrainGenerationSystem`, `StructureGenerationSystem`, `StartConditionSystem` |
| 타일·구조물 | [tile](game/tile.md) | [biomes](csv/biomes.md) | `TileImprovementSystem`, `RuinSystem` |
| 도시 | [city](game/city.md) | [city-rewards](csv/city-rewards.md), [game-rules](csv/game-rules.md) | `CitySystem`, `CityResourceSystem` |
| 건물·타일 행동 | [building](game/building.md) | [buildings](csv/buildings.md), [tile-actions](csv/tile-actions.md) | `TileImprovementSystem` |
| 기술 | [tech](game/tech.md) | [tech](csv/tech.md) | `TechSystem`, `TechGroupSystem`, `TechEffectSystem` |
| 유닛·배 | [unit](game/unit.md) | [units](csv/units.md) | `UnitFactorySystem`, `EmbarkSystem`, `VeteranSystem`, `Actions/*` |
| 전투 | [combat](game/combat.md) | [game-rules](csv/game-rules.md) `Combat.*` | `CombatSystem` |
| 이동 | [movement](game/movement.md) | [units](csv/units.md) | `PathfindingSystem`, `MovementSystem` |
| 시야·은신·침투 | [vision](game/vision.md) | [game-rules](csv/game-rules.md) `Vision.*` | `VisionSystem`, `StealthSystem`, `InfiltrationSystem` |
| 턴·별 | [turn](game/turn.md) | [game-rules](csv/game-rules.md) | `TurnSystem`, `CityResourceSystem` |
| 종족 | [tribe](game/tribe.md) | [tribes](csv/tribes.md) | `TribeSystem`, `StartConditionSystem` |
| 과업·점수 | [task-score](game/task-score.md) | [tasks](csv/tasks.md), [game-rules](csv/game-rules.md) `Score.*` | `TaskSystem`, `ScoreSystem` |
| AI | [ai](game/ai.md) | [game-rules](csv/game-rules.md) `AI.*` | `EnemyAI`, `EconomyAI` |
| 화면 표시 | — | [sprites](csv/sprites.md) | `View/PixelSpriteCatalog` |
| 화면 문구·언어 | — | [strings](csv/strings.md) | `LocalizationSystem`, `View/LanguagePreference` |

## 검증

Sandbox 입력용 CSV 명세 스냅샷: [형식 목록](sandbox-input-csv/formats.csv), [컬럼 명세](sandbox-input-csv/columns.csv), [허용값](sandbox-input-csv/values.csv), [공통·입력 규칙](sandbox-input-csv/rules.csv), [게임 규칙 키](sandbox-input-csv/game-rule-keys.csv). 각 파일의 독립 명세는 같은 폴더의 `<파일명>.spec.csv`다. 출처 컬럼과 Structures 관련 설명은 생략하며, 실제 입력 컬럼·조건은 유지한다. 기존 명세·코드에서 생성한 조회용 자료이며, `python scripts/export_sandbox_csv_spec.py`로 갱신한다.

Sandbox CSV 예시 파일: [`../sandbox-csv-examples/`](../sandbox-csv-examples/) — Sandbox가 다루는 표 전부(`GameDataLoader.SandboxCsvPaths`)를 `Assets/Resources`와 같은 구조로 복사한 스냅샷이다. 원본은 `Assets/Resources`이고 이 폴더는 값의 출처가 아니다. 폴더 그대로 "표 불러오기"로 읽을 수 있다.

CSV를 고친 뒤에는 전체 검증을 돌린다 — 명령은 [README](../../README.md#실행).
