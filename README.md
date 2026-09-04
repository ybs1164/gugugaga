# gugugaga

Unity 6000.3.20f1 기반 턴제 전술 전투 프로토타입.

## 구조 (야매 ECS)

- `Assets/Scripts/TacticsECS/Core`, `Data`: 순수 데이터 (struct/필드만). 로직 없음. [`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs)가 범용 엔티티-컴포넌트 저장소, [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)가 그 안에 저장되는 컴포넌트 타입들(아래 참고).
- `Assets/Scripts/TacticsECS/Systems`: 정적 클래스. `Data`를 읽고 써서 판정/이동/전투/AI를 처리. 자체 상태 없음.
- `Assets/Scripts/TacticsECS/View`: 화면 표시 전용 MonoBehaviour. 로직 없음.
- `Assets/Scripts/TacticsECS/BattleController.cs`: 입력을 받아 System을 호출하고 View에 반영하는 조율자. `Assets/Scenes/SampleScene.unity`에 배치되어 있음.
- `Assets/Prefabs/Units`: 유닛 타입별 프리팹(`Unit_Melee`/`Unit_Ranged`/`Unit_Guard`). 각 프리팹은 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs) + [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트를 갖는다.

8x8 그리드에 플레이어/적 각 4유닛(Melee/Ranged/Guard)이 배치된 데모. 클릭으로 유닛 선택 → 이동/공격, 턴 종료 버튼으로 턴 전환.

## 유닛 종류

유닛 타입은 코드의 enum 분기가 아니라 **프리팹**으로 관리한다. `Assets/Prefabs/Units`의 각 프리팹은
[`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트에 스탯과 팀별 색상을 인스펙터 값으로
들고 있고, `BattleController`는 이 프리팹 3개를 인스펙터에서 참조해 스폰한다.
`UnitSpawner.Spawn`/`UnitView`/`UnitDefinition` 어디에도 타입별 `switch`는 없으며, 전부 프리팹에 붙은 값을 그대로 읽어 쓴다.
새 타입을 추가하려면 코드를 고칠 필요 없이 프리팹을 하나 더 만들고 `UnitDefinition` 값만 채우면 된다. 사거리는 맨해튼 거리 기준.

### 데이터 저장 방식 (Entity-Component)

유닛 전용 저장소를 따로 두지 않고, **"유닛"이라는 개념을 전혀 모르는 범용 엔티티-컴포넌트 저장소**
[`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs) 위에 유닛을 구현한다.

- **엔티티**는 데이터를 하나도 갖지 않는 정수 id일 뿐이다 (`world.CreateEntity()`로 발급).
- **컴포넌트**는 [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)에 정의된, 값 하나만 담는
  독립된 타입들이다 — `Team`(팀), `GridPosition`(위치), `Hp`/`MaxHp`, `Attack`/`Defense`/`AttackRange`/`CanGuard`(전투),
  `MoveRange`/`IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal`(이동), `HasMoved`/`HasActed`/`IsGuarding`(턴 상태).
- `EntityWorld`는 `Set<T>(id, value)` / `Get<T>(id)`만 제공하고, 각 컴포넌트 타입마다 내부적으로 완전히 분리된
  리스트에 저장한다(Structure of Arrays) — 새 컴포넌트 타입을 추가해도 `EntityWorld` 자체는 손댈 필요가 없다.
- "살아있는지"(`Hp > 0`) 같은 유닛 전용 해석은 `EntityWorld`가 아니라
  [`UnitQueries`](Assets/Scripts/TacticsECS/Systems/UnitQueries.cs)(System)가 담당한다 — 저장소는 의미를 모르고,
  의미를 아는 건 그 값을 읽는 System 쪽이라는 원칙.
- 지금은 모든 엔티티가 유닛이지만, 이후 유닛이 아닌 다른 엔티티(장애물/투사체/아이템 등)가 필요해져도
  이 저장소를 그대로 재사용하고 새 컴포넌트 타입만 추가하면 된다.

### 이동 방식

이동은 더 이상 "이동 범위 몇 칸" 하나로만 정해지지 않고, 프리팹의 `UnitDefinition` 값으로 세부 지정한다(각각 독립된 속성):

| 속성 | 의미 |
| --- | --- |
| `MoveRange` | 이동 가능 칸 수 |
| `IgnoreTerrain` | true면 `Walkable = false`인 지형(벽/장애물)을 무시하고 이동 (비행 유닛 등) |
| `IgnoreUnitBlocking` | true면 다른 유닛이 있는 타일도 지나가거나 멈출 수 있음 (유령/투명체 등) |
| `AllowDiagonal` | true면 대각선을 포함한 8방향 이동, false면 상하좌우 4방향만 |

기본 3종 유닛은 모두 `IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal`이 꺼진 평범한 지상 4방향 이동이며,
[`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)과
[`MovementSystem.TryMove`](Assets/Scripts/TacticsECS/Systems/MovementSystem.cs)가 `EntityWorld`에서 이 컴포넌트들을
각각 조회해 판정한다.

| 타입 | HP | 공격력 | 방어력 | 이동 범위 | 사거리 | 특징 |
| --- | --- | --- | --- | --- | --- | --- |
| **Melee** (근접, `Unit_Melee.prefab`) | 12 | 5 | 1 | 3칸 | 1칸 | 이동력이 좋고 공격력이 높지만 방어가 약함. 적에게 바로 붙어 때리는 딜러. |
| **Ranged** (원거리, `Unit_Ranged.prefab`) | 8 | 4 | 0 | 2칸 | 3칸 | HP/방어력이 가장 낮은 대신 멀리서 공격 가능. 근접에게 붙잡히면 위험. |
| **Guard** (방어, `Unit_Guard.prefab`) | 18 | 3 | 3 | 2칸 | 1칸 | HP/방어력이 가장 높은 탱커. `UnitDefinition.CanGuard = true`인 유닛만 공격 대신 **방어 태세**를 선택할 수 있고, 이번 턴 방어력이 +2 추가되어 총 5가 된다 (`CombatSystem.TryDefend`). |

플레이어 유닛은 파란/청록 계열, 적 유닛은 빨강/주황 계열 색으로 구분되며, 방어 태세 중인 유닛은 팀에 관계없이 노란색으로 표시된다. 이 색상은 각 프리팹의 `UnitDefinition.playerColor`/`enemyColor` 값이며 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)가 `GetComponent<UnitDefinition>()`으로 읽어서 적용한다. 유닛 머리 위 텍스트는 `현재HP/최대HP`.

데미지 공식은 `max(1, 공격자 공격력 - (대상 방어력 + 방어 태세 보너스))`.

## 조작법

- **유닛 선택**: 자기 팀(플레이어) 유닛을 좌클릭. 이미 이동+행동을 모두 마친 유닛이나 적/빈 타일을 클릭하면 선택이 풀린다.
- **이동**: 유닛 선택 시 파란색으로 하이라이트된 타일이 이동 가능 범위. 그 타일을 클릭하면 이동한다 (턴당 1회).
- **공격**: 하이라이트된 빨간 타일 위의 적 유닛을 클릭하면 공격한다 (턴당 1회, 이동 여부와 무관하게 가능).
- **방어 태세** 버튼(화면 좌상단): Guard 타입 유닛을 선택하고 아직 행동하지 않았을 때만 나타난다. 공격 대신 방어력을 올리고 턴을 소모한다.
- **선택 해제** 버튼: 현재 선택을 취소한다.
- **턴 종료** 버튼: 플레이어 턴을 마치고 적 턴으로 넘긴다. 적 턴은 [`EnemyAI`](Assets/Scripts/TacticsECS/Systems/EnemyAI.cs)가 자동으로 진행하며 별도 입력이 필요 없다.
- 한쪽 팀 유닛이 전멸하면 자동으로 전투가 종료되고 좌상단에 "승리!"/"패배..." 메시지가 표시된다.
- **카메라 이동**: `W`/`A`/`S`/`D` 또는 방향키로 화면을 팬(pan)한다. isometric 시점 기준 화면상의 상/하/좌/우로 움직인다.
- **카메라 줌**: 마우스 휠로 확대/축소한다. 턴/전투 상태와 무관하게 항상 조작 가능.

## 카메라

정통 isometric 구도(Y축 45도 + 피치 35.264도 = `atan(1/√2)`)로 그리드를 대각선 위에서 내려다보며, 원근 대신 orthographic 투영을 사용해 거리에 따른 크기 왜곡이 없다. `BattleController` 인스펙터의 `Camera (Isometric)` 항목(`isoYawDegrees`/`isoPitchDegrees`/`isoZoom`)에서 초기 각도와 확대율을 조정할 수 있다.

카메라는 고정 시점이 아니라 이동/줌이 가능하다 ([`BattleController.HandleCameraControl`](Assets/Scripts/TacticsECS/BattleController.cs)):
- **이동**: `WASD`/방향키 입력을 카메라의 isometric 회전 기준 수평 방향(좌/우/앞/뒤)으로 투영해 focus 지점을 옮기고, 그 지점을 기준으로 매 프레임 카메라 위치를 재계산한다. focus는 그리드 영역 주변(여유 마진 포함)으로 제한되어 배틀필드를 완전히 벗어나지 않는다. 속도는 `cameraPanSpeed`로 조정.
- **줌**: 마우스 휠로 `orthographicSize`를 직접 조절한다. `zoomSensitivity`로 민감도를, `minOrthoSize`/`maxOrthoSize`로 확대/축소 한계를 조정한다.

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
- 2026-09-04: 상대 유닛이 앞쪽 칸에 있을 때 클릭이 안 되던 버그 수정, 카메라 이동/줌 기능 추가.
  - **버그 수정**: `BattleController.HandleClick`이 `Physics.Raycast`로 콜라이더를 맞혀 클릭 대상을 판정했는데, 유닛(Capsule)이 타일 위로 솟아 있어서 isometric 각도에서는 카메라에 더 가까운(앞쪽) 칸의 유닛 콜라이더가 그 뒤 칸으로 가는 레이를 가로막는 문제가 있었다. 3D 콜라이더 레이캐스트 대신 그리드 바닥 평면(`Plane`)과의 교차점을 구해 그리드 좌표로 역산한 뒤 `GridWorld`에서 직접 occupant를 조회하는 방식으로 교체 — 화면에 보이는 칸과 항상 일치하며 유닛 높이로 인한 가림 문제가 원천적으로 사라짐.
  - **카메라 이동/줌 추가**: `WASD`/방향키로 카메라 focus를 팬하고(그리드 영역 주변으로 클램프), 마우스 휠로 `orthographicSize`를 조절해 줌 인/아웃 가능. 기존 `PositionCamera`가 카메라를 한 번만 배치하던 구조에서, focus/회전/거리를 필드로 유지하고 매 프레임 `ApplyCameraTransform`으로 위치를 재계산하는 구조로 변경.
- 2026-09-04: `UnitType` enum + 코드 switch 기반 유닛 타입 관리를 프리팹 기반으로 교체.
  - **동기**: `UnitData.Create`/`UnitView.ColorFor`/`BattleController` 등 여러 곳에 흩어진 `switch(UnitType)` 분기를 없애고, 유닛 타입을 데이터(프리팹)로 관리하도록 요청받음.
  - `Assets/Scripts/TacticsECS/Core/UnitType.cs` 삭제. 대신 순수 데이터 struct [`UnitStats`](Assets/Scripts/TacticsECS/Core/UnitStats.cs)(MaxHp/Attack/Defense/MoveRange/AttackRange/CanGuard)를 추가하고, `UnitData`는 `Type` 필드 대신 `CanGuard` 값만 보유 — `UnitData.Create(id, team, UnitStats, pos)`는 switch 없이 값 복사만 한다.
  - 새 컴포넌트 [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 추가: 유닛 프리팹에 붙어 `UnitStats`와 팀별 색상(`playerColor`/`enemyColor`)을 인스펙터 값으로 보유. `UnitView`는 `GetComponent<UnitDefinition>()`으로 이 값을 읽어 표시만 할 뿐 타입 분기가 없다.
  - `UnitSpawner.Spawn`/`BattleController.SpawnUnit`이 `UnitType` 인자 대신 `UnitView` 프리팹 참조를 받도록 변경. `BattleController`에 `meleePrefab`/`rangedPrefab`/`guardPrefab` 인스펙터 필드 추가.
  - `CombatSystem.TryDefend`의 `unit.Type != UnitType.Guard` 조건을 `!unit.CanGuard`로 교체.
  - `Assets/Editor/UnitPrefabSetup.cs`(Editor 전용, `-executeMethod`로만 실행)를 작성해 `Assets/Prefabs/Units/Unit_Melee.prefab`/`Unit_Ranged.prefab`/`Unit_Guard.prefab`을 기존 하드코딩 값 그대로 생성하고, `SampleScene`의 `BattleController` 3개 프리팹 필드에 자동 연결. GUI 조작 없이 Unity CLI 배치모드(`unity run . -- -executeMethod TacticsECS.EditorTools.UnitPrefabSetup.Generate`)로만 실행.
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 재검증.
  - Unity Editor가 이미 열려 있어 CLI 헤드리스 빌드로 검증하지 못함 — 표준 Unity/Input System API만 사용한 코드 리뷰로 확인.
- 2026-09-04: `UnitData`에 몰려 있던 정보를 "런타임 값"과 "고정 스탯"으로 분리 저장, 이동 방식을 세부 설정 가능하도록 확장.
  - **동기**: `UnitData` struct 하나에 Id/Team/GridPos부터 Hp/Attack/Defense/MoveRange/AttackRange/CanGuard/행동 플래그까지 다 들어있어 정보량이 과도하다는 지적을 받음.
  - [`UnitWorld`](Assets/Scripts/TacticsECS/Data/UnitWorld.cs)가 `List<UnitData>`(런타임, 매 턴 바뀜)와 `List<UnitStats>`(고정 스탯, 스폰 후 불변)를 나란히 보관하도록 변경 — 같은 id는 항상 같은 인덱스. `Spawn(UnitData, UnitStats)` / `GetStats(id)` 추가.
  - [`UnitData`](Assets/Scripts/TacticsECS/Core/UnitData.cs)는 `Id`/`Team`/`GridPos`/`Hp`/[`UnitTurnState`](Assets/Scripts/TacticsECS/Core/UnitTurnState.cs)(`HasMoved`/`HasActed`/`IsGuarding`)만 남기고, `MaxHp`/`Attack`/`Defense`/`MoveRange`/`AttackRange`/`CanGuard`는 [`UnitStats`](Assets/Scripts/TacticsECS/Core/UnitStats.cs) 쪽으로 이동 — `MaxHp`는 그대로, 나머지는 새로 나눈 [`UnitCombatStats`](Assets/Scripts/TacticsECS/Core/UnitCombatStats.cs)(`Attack`/`Defense`/`AttackRange`/`CanGuard`)와 [`UnitMovement`](Assets/Scripts/TacticsECS/Core/UnitMovement.cs)(이동 방식) 두 서브 struct로 묶임.
  - **이동 방식 세부 설정**: `UnitMovement`에 `MoveRange` 외에 `IgnoreTerrain`(지형 무시, 비행 등), `IgnoreUnitBlocking`(유닛 통과), `AllowDiagonal`(대각선 이동) 3개 옵션 추가. [`GridWorld.GetNeighbors`](Assets/Scripts/TacticsECS/Data/GridWorld.cs)에 대각선 지원 추가, [`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)/[`MovementSystem.TryMove`](Assets/Scripts/TacticsECS/Systems/MovementSystem.cs)가 `int moveRange` 대신 `UnitMovement`를 받아 이 값들을 그대로 반영. 기본 3종 유닛은 전부 지상/4방향 값 그대로 유지해 동작은 이전과 동일.
  - `CombatSystem`(`IsInAttackRange`/`CalculateDamage`/`TryDefend`)이 `UnitData` 값 대신 `UnitWorld`+id를 받아 내부에서 `GetStats`로 전투 스탯을 조회하도록 변경. `EnemyAI`/`BattleController`의 호출부도 함께 갱신.
  - `Assets/Editor/UnitPrefabSetup.cs`를 새 `UnitStats` 구조에 맞게 갱신하고 Unity CLI 배치모드로 재실행 — 기존 프리팹 3개를 새 구조로 재생성하고 `SampleScene`의 `BattleController` 참조를 다시 연결(같은 GUID/fileID 유지 확인).
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 검증.
- 2026-09-04: 위 분리가 충분하지 않다는 피드백("각 속성마다 나누라고")을 받고, `UnitTurnState`/`UnitCombatStats`/`UnitMovement`/`UnitStats`/`UnitData` 묶음 struct를 전부 없애고 완전한 속성별(Structure of Arrays) 저장으로 재구성.
  - `Assets/Scripts/TacticsECS/Core/UnitData.cs`/`UnitStats.cs`/`UnitCombatStats.cs`/`UnitMovement.cs`/`UnitTurnState.cs` 전부 삭제. 유닛을 나타내는 struct 자체가 더 이상 없다 — id(리스트 인덱스)만이 유닛의 유일한 식별자.
  - [`UnitWorld`](Assets/Scripts/TacticsECS/Data/UnitWorld.cs)가 `Team`/`GridPos`/`Hp`/`HasMoved`/`HasActed`/`IsGuarding`/`MaxHp`/`Attack`/`Defense`/`AttackRange`/`CanGuard`/`MoveRange`/`IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal` — 14개 속성 각각을 독립된 `List<T>`로 보관. `GetXxx(id)`/`SetXxx(id, value)` 접근자만으로 읽고 쓴다. `Spawn(...)`은 이 14개 값을 각각 인자로 받는다.
  - [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs)도 같은 원칙으로 묶음 없이 필드 하나당 값 하나(`maxHp`/`attack`/`defense`/... 등)만 노출. `UnitSpawner`는 이 값들을 `UnitWorld.Spawn`에 그대로 하나씩 전달.
  - `CombatSystem`/`MovementSystem`/`PathfindingSystem`/`EnemyAI`/`TurnManager`/`BattleController`를 전부 `UnitWorld`의 개별 getter/setter만 사용하도록 다시 작성 (struct를 꺼내 들고 다니는 코드 없음). `GridWorld.GetNeighbors4`(더 이상 안 쓰임)도 함께 정리.
  - `Assets/Editor/UnitPrefabSetup.cs`를 평평해진 `UnitDefinition` 필드에 맞게 갱신하고 Unity CLI 배치모드로 재실행 — 프리팹 3개 재생성, `SampleScene`의 `BattleController` 참조 재연결(같은 GUID/fileID 유지 확인).
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 검증.
- 2026-09-04: "UnitWorld를 Unit에 국한하지 말고 Entity로 생각하라"는 피드백을 받고, 유닛 전용 저장소를 범용 엔티티-컴포넌트 저장소로 재설계.
  - **동기**: `UnitWorld`가 `Team`/`Hp`/`MoveRange`... 같은 속성들을 이름 그대로 하드코딩해서 들고 있어, "유닛"이라는 개념에 완전히 종속된 클래스였다는 지적을 받음.
  - `Assets/Scripts/TacticsECS/Data/UnitWorld.cs` 삭제, 대신 [`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs) 추가 — `CreateEntity()`/`Set<T>(id, value)`/`Get<T>(id)`만 제공하는 완전히 범용적인 저장소. 컴포넌트 타입별로 내부 `Dictionary<Type, List<T>>`에 나눠 저장하며, "유닛"이라는 단어가 이 클래스 안에 전혀 등장하지 않는다 — 새 컴포넌트 타입을 추가해도 이 클래스는 고칠 필요가 없다.
  - [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs) 추가: `GridPosition`/`Hp`/`MaxHp`/`Attack`/`Defense`/`AttackRange`/`CanGuard`/`MoveRange`/`IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal`/`HasMoved`/`HasActed`/`IsGuarding` — 값 하나만 담는 독립된 컴포넌트 타입 13종(기존 `Team` enum은 그대로 컴포넌트 타입으로 재사용).
  - [`UnitQueries`](Assets/Scripts/TacticsECS/Systems/UnitQueries.cs) 추가: `IsAlive`/`AnyAlive`처럼 "유닛"이라는 의미를 해석하는 조회는 `EntityWorld`가 아니라 이 System이 담당 — 저장소는 의미를 모르고, 의미를 아는 건 그 값을 읽는 System 쪽이어야 한다는 원칙.
  - `CombatSystem`/`MovementSystem`/`PathfindingSystem`/`EnemyAI`/`TurnManager`/`UnitSpawner`/`UnitView`/`BattleController`를 전부 `EntityWorld.Get<T>(id)`/`Set<T>(id, value)` 기반으로 다시 작성. `BattleController`의 `_units` 필드도 `_world`(`EntityWorld`)로 이름을 바꿔 "이건 유닛 전용이 아니다"를 코드에서도 드러냄.
  - 기본 3종 유닛의 실제 동작/스탯/이동 방식은 이전과 동일 — 이번 변경은 순수하게 저장소 설계(유닛 전용 → 범용 엔티티-컴포넌트)에 관한 것. `UnitDefinition`/프리팹/씬 연결은 변경 없음.
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 검증.
