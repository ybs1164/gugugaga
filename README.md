# gugugaga

Unity 6000.3.20f1 기반 턴제 전술 전투 프로토타입.

## 구조 (야매 ECS)

- `Assets/Scripts/TacticsECS/Core`, `Data`: 순수 데이터 (struct/필드만). 로직 없음.
- `Assets/Scripts/TacticsECS/Systems`: 정적 클래스. `Data`를 읽고 써서 판정/이동/전투/AI를 처리. 자체 상태 없음.
- `Assets/Scripts/TacticsECS/View`: 화면 표시 전용 MonoBehaviour. 로직 없음.
- `Assets/Scripts/TacticsECS/BattleController.cs`: 입력을 받아 System을 호출하고 View에 반영하는 조율자. `Assets/Scenes/SampleScene.unity`에 배치되어 있음.

8x8 그리드에 플레이어/적 각 4유닛(Melee/Ranged/Guard)이 배치된 데모. 클릭으로 유닛 선택 → 이동/공격, 턴 종료 버튼으로 턴 전환.

## 유닛 종류

유닛 스탯은 [`UnitData.Create`](Assets/Scripts/TacticsECS/Core/UnitData.cs)에서 타입별로 정의된다. 사거리는 맨해튼 거리 기준.

| 타입 | HP | 공격력 | 방어력 | 이동 범위 | 사거리 | 특징 |
| --- | --- | --- | --- | --- | --- | --- |
| **Melee** (근접) | 12 | 5 | 1 | 3칸 | 1칸 | 이동력이 좋고 공격력이 높지만 방어가 약함. 적에게 바로 붙어 때리는 딜러. |
| **Ranged** (원거리) | 8 | 4 | 0 | 2칸 | 3칸 | HP/방어력이 가장 낮은 대신 멀리서 공격 가능. 근접에게 붙잡히면 위험. |
| **Guard** (방어) | 18 | 3 | 3 | 2칸 | 1칸 | HP/방어력이 가장 높은 탱커. 공격 대신 **방어 태세**를 선택하면 이번 턴 방어력이 +2 추가되어 총 5가 된다 (`CombatSystem.TryDefend`). |

플레이어 유닛은 파란/청록 계열, 적 유닛은 빨강/주황 계열 색으로 구분되며, 방어 태세 중인 유닛은 팀에 관계없이 노란색으로 표시된다 ([`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)). 유닛 머리 위 텍스트는 `현재HP/최대HP`.

데미지 공식은 `max(1, 공격자 공격력 - (대상 방어력 + 방어 태세 보너스))`.

## 조작법

- **유닛 선택**: 자기 팀(플레이어) 유닛을 좌클릭. 이미 이동+행동을 모두 마친 유닛이나 적/빈 타일을 클릭하면 선택이 풀린다.
- **이동**: 유닛 선택 시 파란색으로 하이라이트된 타일이 이동 가능 범위. 그 타일을 클릭하면 이동한다 (턴당 1회).
- **공격**: 하이라이트된 빨간 타일 위의 적 유닛을 클릭하면 공격한다 (턴당 1회, 이동 여부와 무관하게 가능).
- **방어 태세** 버튼(화면 좌상단): Guard 타입 유닛을 선택하고 아직 행동하지 않았을 때만 나타난다. 공격 대신 방어력을 올리고 턴을 소모한다.
- **선택 해제** 버튼: 현재 선택을 취소한다.
- **턴 종료** 버튼: 플레이어 턴을 마치고 적 턴으로 넘긴다. 적 턴은 [`EnemyAI`](Assets/Scripts/TacticsECS/Systems/EnemyAI.cs)가 자동으로 진행하며 별도 입력이 필요 없다.
- 한쪽 팀 유닛이 전멸하면 자동으로 전투가 종료되고 좌상단에 "승리!"/"패배..." 메시지가 표시된다.
- 카메라는 조작 대상이 아니라 [`BattleController.PositionCamera`](Assets/Scripts/TacticsECS/BattleController.cs)가 그리드 중앙을 기준으로 자동 배치하는 고정 isometric 시점이다 (아래 참고).

## 카메라

정통 isometric 구도(Y축 45도 + 피치 35.264도 = `atan(1/√2)`)로 그리드를 대각선 위에서 내려다보며, 원근 대신 orthographic 투영을 사용해 거리에 따른 크기 왜곡이 없다. `BattleController` 인스펙터의 `Camera (Isometric)` 항목(`isoYawDegrees`/`isoPitchDegrees`/`isoZoom`)에서 각도와 확대율을 조정할 수 있다.

## 작업 로그

- 2026-09-04: 프로젝트 동작 검증.
  - Unity CLI 헤드리스 빌드(`unity run . -- -nographics`)로 컴파일 에러 없음 확인.
  - `Data`/`Systems`/`View` 계층 전체 코드 리뷰 — CLAUDE.md 규칙(Data는 값만, Systems는 무상태) 준수 확인, 로직 버그 없음.
  - **버그 발견 및 수정**: `SampleScene`에 `BattleController`가 배치되어 있지 않아 Play를 눌러도 아무것도 스폰되지 않던 문제. 씬에 `BattleController` GameObject를 추가해 해결.
  - 저장소에 `.gitignore` 추가(Unity 표준) 후 첫 커밋.
- 2026-09-04: 유닛 설명/조작법을 README에 정리, 카메라를 정통 isometric 구도(45도/35.264도 + orthographic)로 변경.
  - `BattleController.PositionCamera`가 기존의 임의 각도 원근 카메라 대신 대각선 45도 + 피치 35.264도, orthographic 투영을 사용하도록 수정. 인스펙터에서 각도/줌 조정 가능하도록 `SerializeField` 추가.
  - Unity Editor가 이미 열려 있어 CLI 헤드리스 빌드로는 검증하지 못함(같은 프로젝트 중복 실행 불가) — 표준 Unity API만 사용한 코드 리뷰로 확인.
- 2026-09-04: 콘솔 경고 "Missing types referenced from component UniversalRenderPipelineGlobalSettings ... UnityEngine.PathTracing.Core.WorldRenderPipelineResources" 해결.
  - 원인: `UniversalRenderPipelineGlobalSettings.asset`이 GPU Path Tracing 리소스(`WorldRenderPipelineResources`, `Unity.PathTracing.Runtime` 어셈블리)를 참조하는데, 해당 타입을 제공하는 에디터 내장 패키지 `com.unity.path-tracing`이 `Packages/manifest.json`에 누락되어 있었음.
  - 조치: Unity CLI 배치모드(`-executeMethod`로 `UnityEditor.PackageManager.Client.Add("com.unity.path-tracing")` 실행)로 패키지 추가 → 리졸브 확인 후 재검증. 에디터 GUI(Package Manager 창)는 사용하지 않음.
  - 결과: `Packages/manifest.json`/`packages-lock.json`에 `com.unity.path-tracing@1.0.0` 등록, 콘솔 경고 소멸(재검증 배치모드 실행 로그에서 확인). `UniversalRenderPipelineGlobalSettings.asset`은 패키지 리졸브에 따라 일부 구버전 필드가 최신 스키마로 정리됨(값 손실 없음).
