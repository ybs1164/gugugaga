# gugugaga

Unity 6000.3.20f1 기반 턴제 전술 전투 프로토타입.

## 구조 (야매 ECS)

- `Assets/Scripts/TacticsECS/Core`, `Data`: 순수 데이터 (struct/필드만). 로직 없음. [`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs)가 범용 엔티티-컴포넌트 저장소, [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)가 그 안에 저장되는 컴포넌트 타입들(아래 참고).
- `Assets/Scripts/TacticsECS/Systems`: 정적 클래스. `Data`를 읽고 써서 판정/이동/전투/AI를 처리. 자체 상태 없음.
- `Assets/Scripts/TacticsECS/View`: 화면 표시 전용 MonoBehaviour. 로직 없음.
- `Assets/Scripts/TacticsECS/BattleController.cs`: 입력을 받아 System을 호출하고 View에 반영하는 조율자. `Assets/Scenes/SampleScene.unity`에 배치되어 있음.
- `Assets/Prefabs/Units`: 유닛 타입별 프리팹(`Unit_Melee`/`Unit_Ranged`/`Unit_Guard`). 각 프리팹은 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs) + [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트를 갖는다.

8x8 그리드에 플레이어/적 각 4유닛(Melee/Ranged/Guard)이 배치된 데모. 클릭으로 유닛 선택 → 이동/공격, 턴 종료 버튼으로 턴 전환.

## EntityWorld: 범용 엔티티-컴포넌트 저장소

[`GridWorld`](Assets/Scripts/TacticsECS/Data/GridWorld.cs)가 "타일들의 데이터베이스"라면,
[`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs)는 이 게임의 진짜 상태(유닛 HP, 위치, 스탯, 턴 상태 등)가
전부 들어있는 **"엔티티들의 데이터베이스"**다. 유닛은 GameObject/MonoBehaviour가 아니라 이 저장소 위의 값일 뿐이고,
화면에 보이는 로우폴리 캐릭터 모델([`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs))은 그 값을 그려주는 껍데기에 불과하다.

처음에는 이 저장소를 `UnitWorld`라는 이름으로 유닛 전용으로 만들었지만("Id/Team/GridPos/Hp/Attack/..." 같은 필드를
직접 하드코딩), **"Unit에 국한하지 말고 Entity로 생각하라"**는 피드백에 따라 완전히 범용적인 구조로 다시 설계했다.
지금 구조는 다음 두 가지 개념으로만 이루어진다:

- **엔티티(Entity)** — 데이터를 하나도 갖지 않는 정수 id일 뿐이다. `world.CreateEntity()`를 부르면 새 id가 하나
  발급된다. "유닛"이라는 개념은 여기 없다 — 엔티티는 그냥 "무언가가 존재한다"는 표시일 뿐이다.
- **컴포넌트(Component)** — 엔티티에 붙는 값 하나. [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)에
  `Team`(팀), `GridPosition`(위치), `Hp`/`MaxHp`(체력), `Attack`/`Defense`/`AttackRange`(전투), `HealAmount`/`HealRange`(치유),
  `UnitActions`(이 유닛이 실제로 쓸 수 있는 [`IUnitAction`](Assets/Scripts/TacticsECS/Actions/IUnitAction.cs) 목록 —
  실행 가능 여부/효과 판단의 단일 기준점, 자세한 내용은 [사용 가능 행동](#사용-가능-행동-assetsscriptstacticsecsactions) 참고),
  `AvailableActions`([`ActionType`](Assets/Scripts/TacticsECS/Core/ActionType.cs) 플래그 — 실행 판정에는 관여하지 않는
  UI/CSV용 플레이스홀더 태그), `MoveRange`/`IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal`(이동),
  `HasMoved`/`HasActed`/`IsGuarding`(턴 상태)로 정의되어 있다. 각각 값 하나만 담는 아주 작은 타입이고, 서로 아무 관계도 없다.

`EntityWorld` 자체는 오직 `Set<T>(id, value)` / `Get<T>(id)` 두 메서드만 제공한다. 컴포넌트 타입 `T`마다 내부적으로
완전히 분리된 리스트를 하나씩 두고(같은 id는 모든 리스트에서 같은 인덱스를 가리킨다), `Get<Hp>(3)`이라고 부르면
"Hp라는 타입을 저장하는 리스트의 3번 칸"을 돌려준다. 즉 이 클래스의 코드 안에는 `Team`이나 `Hp`, "유닛"이라는
단어가 **전혀 등장하지 않는다** — 새 컴포넌트 타입을 하나 추가해도 `EntityWorld`는 한 줄도 고칠 필요가 없다.

그럼 "이 엔티티가 살아있는가"(`Hp > 0`) 같은 판단은 누가 하는가? `EntityWorld`가 아니라
[`UnitQueries`](Assets/Scripts/TacticsECS/Systems/UnitQueries.cs) 같은 System이 한다. **저장소는 값이 무엇을 의미하는지
모르고, 그 값을 읽어서 의미를 해석하는 건 항상 System 쪽**이라는 원칙이다. `CombatSystem`/`MovementSystem`/
`PathfindingSystem`/`EnemyAI`/`TurnSystem`/`BattleController` 모두 `EntityWorld.Get<T>(id)`/`Set<T>(id, value)`만으로
동작하고, "유닛 데이터"를 통째로 담은 struct를 주고받지 않는다.

지금은 이 저장소에 있는 엔티티가 전부 유닛이지만, 설계 자체는 유닛에 묶여 있지 않다. 나중에 유닛이 아닌 다른
엔티티(장애물, 투사체, 함정, 아이템 등)가 필요해져도 `EntityWorld`는 그대로 두고 필요한 컴포넌트 타입만 추가하면
된다 — 예를 들어 움직이지 않는 장애물이라면 `GridPosition`만 갖고 `MoveRange`/`Attack` 같은 컴포넌트는 아예 없는
엔티티로 만들 수 있다.

## 유닛 종류

유닛 타입은 코드의 enum 분기가 아니라 **프리팹**으로 관리한다. `Assets/Prefabs/Units`의 각 프리팹은
[`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트에 스탯과 팀별 색상·모델 겉감 텍스처를
인스펙터 값으로 들고 있고, `BattleController`는 이 프리팹 3개를 인스펙터에서 참조해 스폰한다.
`UnitSpawner.Spawn`/`UnitView`/`UnitDefinition` 어디에도 타입별 `switch`는 없으며, 전부 프리팹에 붙은 값을 그대로 읽어 쓴다.
새 타입을 추가하려면 코드를 고칠 필요 없이 프리팹을 하나 더 만들고 `UnitDefinition` 값만 채우면 된다. 사거리는 맨해튼 거리 기준.
스폰 시 [`UnitSpawner`](Assets/Scripts/TacticsECS/View/UnitSpawner.cs)가 `UnitDefinition`의 값을 위 컴포넌트들로 하나씩 옮겨 담는다.
`UnitDefinition`이 갖는 "행동의 집합"(`actions`)에 대해서는 바로 아래 [사용 가능 행동](#사용-가능-행동-assetsscriptstacticsecsactions) 참고.

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
[`MoveAction.Execute`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)(`MovementSystem.TryMove`가 찾아 호출)가
`EntityWorld`에서 이 컴포넌트들을 각각 조회해 판정한다.

| 타입 | HP | 공격력 | 방어력 | 이동 범위 | 사거리 | 특징 |
| --- | --- | --- | --- | --- | --- | --- |
| **Melee** (근접, `Unit_Melee.prefab`) | 12 | 5 | 1 | 3칸 | 1칸 | 이동력이 좋고 공격력이 높지만 방어가 약함. 적에게 바로 붙어 때리는 딜러. 이동/공격 외에 **자폭**을 쓸 수 있다. |
| **Ranged** (원거리, `Unit_Ranged.prefab`) | 8 | 4 | 0 | 2칸 | 3칸 | HP/방어력이 가장 낮은 대신 멀리서 공격 가능. 근접에게 붙잡히면 위험. 이동/공격 외에 **치유**(회복량 4, 사거리 2)를 쓸 수 있다. |
| **Guard** (방어, `Unit_Guard.prefab`) | 18 | 3 | 3 | 2칸 | 1칸 | HP/방어력이 가장 높은 탱커. 이동/공격 외에 **방어 태세**를 쓸 수 있고, 이번 턴 방어력이 +2 추가되어 총 5가 된다 (`CombatSystem.TryDefend`). 추가로 **반격** 패시브를 갖고 있어, 자신의 사거리 안에서 공격받으면 자동으로 공격한 대상에게 피해를 되돌려준다. |

### 사용 가능 행동 (Assets/Scripts/TacticsECS/Actions)

어떤 유닛이 이동/공격/방어/치유/자폭/반격/돌격/대피 중 무엇을 쓸 수 있는지, 그리고 그 행동이 실제로 무엇을
하는지는 **값이 아니라 행동 자신**이 정한다. 여덟 행동
([`MoveAction`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)/[`AttackAction`](Assets/Scripts/TacticsECS/Actions/AttackAction.cs)/
[`DefendAction`](Assets/Scripts/TacticsECS/Actions/DefendAction.cs)/[`HealAction`](Assets/Scripts/TacticsECS/Actions/HealAction.cs)/
[`SelfDestructAction`](Assets/Scripts/TacticsECS/Actions/SelfDestructAction.cs)/[`CounterAction`](Assets/Scripts/TacticsECS/Actions/CounterAction.cs)/
[`ChargeAction`](Assets/Scripts/TacticsECS/Actions/ChargeAction.cs)/[`RetreatAction`](Assets/Scripts/TacticsECS/Actions/RetreatAction.cs))
은 [`IUnitAction`](Assets/Scripts/TacticsECS/Actions/IUnitAction.cs)을 구현하는데, 이 인터페이스는 값(프로퍼티) 하나가
아니라 **메서드 두 개**로 정의된다: `CanExecute(world, unitId)`(지금 이 행동을 쓸 수 있는지 — 생존/이번 턴
이동·행동 여부처럼 행동마다 다른 조건을 행동 스스로 판단)와, 매개변수 모양이 다른 세 하위 인터페이스
([`IMoveAction`](Assets/Scripts/TacticsECS/Actions/IMoveAction.cs)(목적지 필요)/
[`ISelfAction`](Assets/Scripts/TacticsECS/Actions/ISelfAction.cs)(대상 없음 — 방어/치유/자폭)/
[`ITargetedAction`](Assets/Scripts/TacticsECS/Actions/ITargetedAction.cs)(대상 유닛 필요 — 공격/반격))가 각각
정의하는 `Execute(...)`(실제 판정 + 효과 적용). 예전에 `MovementSystem`/`CombatSystem`/`AbilitySystem`에 있던
"이동 가능한가/누구를 때릴 수 있는가/치유·자폭 효과" 로직이 전부 해당 행동 클래스 안으로 옮겨갔고, 저
System들은 이제 유닛의 행동 목록에서 필요한 행동을 찾아 위임하는 얇은 진입점(`TryMove`/`TryAttack`/
`TryDefend`/`TryHeal`/`TrySelfDestruct`)일 뿐이다 — 그 조회는
[`UnitActionQueries.Find<T>`](Assets/Scripts/TacticsECS/Systems/UnitActionQueries.cs)가 담당한다.

유닛별로 실제 갖는 행동의 집합은 `UnitDefinition.actions`(`[SerializeReference] List<IUnitAction>`, 프리팹
인스펙터에서 구성)이고, 그 행동에만 필요한 값(이동 사거리, 회복량 등)도 항목 자신이 들고 있다 — 예:
`MoveAction.MoveRange`, `HealAction.HealAmount`. `UnitSpawner`가 스폰 시 이 리스트를 그대로 `EntityWorld`의
[`UnitActions`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs) 컴포넌트로 옮기고, 위 System들과
`BattleController`(이동/공격 가능 범위 하이라이트 계산)가 거기서 필요한 행동을 찾아 쓴다.

[`ActionType`](Assets/Scripts/TacticsECS/Core/ActionType.cs) 플래그(`AvailableActions` 컴포넌트)는 남아있지만
실행 판정에는 더 이상 관여하지 않는 **플레이스홀더**다 — `IUnitAction.GetActionType()`이 돌려주는 식별
태그로서, CSV 같은 외부 데이터 연동과 `BattleHud`의 아이콘 매칭(어떤 행동에 어떤 버튼/배지를 보여줄지)
용도로만 쓰인다.

- **치유**([`HealAction.Execute`](Assets/Scripts/TacticsECS/Actions/HealAction.cs)): 사거리(`HealRange`) 내의 모든 아군(자신 제외)의 체력을 `HealAmount`만큼 회복시킨다. 대상 선택 없이 버튼 클릭 한 번으로 즉시 적용되는 자기 중심 광역 행동.
- **자폭**([`SelfDestructAction.Execute`](Assets/Scripts/TacticsECS/Actions/SelfDestructAction.cs)): 행동 유닛을 즉시 제거하고, 주위 1칸(맨해튼 거리) 내의 모든 적에게 제거되는 시점의 남은 체력만큼 피해를 입힌다.

`Counter`(반격)/`Charge`(돌격)/`Retreat`(대피)는 예외다 — 플레이어가 고르는 "행동"이 아니라 **항상 자동으로
적용되는 패시브**라서 `BattleHud`의 행동 버튼 목록(`OptionalActionDefs`)이 아니라 별도의 패시브 배지 목록
(`PassiveDefs`)에 정의되고, 클릭할 수 없는 정보 표시용 아이콘으로만 나타난다.
- **반격**([`CounterAction.Execute`](Assets/Scripts/TacticsECS/Actions/CounterAction.cs), [`CombatSystem.TryAttack`](Assets/Scripts/TacticsECS/Systems/CombatSystem.cs)이 공격 성사 후 대상 쪽에서 찾아 호출): 공격이 성사되고 대상이 살아남았을 때, 대상이 `CounterAction`을 갖고 있고 공격자가 대상 자신의 사거리 안에 있으면 대상이 자동으로 공격자에게 피해(대상 공격력 − 공격자 방어력, 최소 1)를 되돌려준다. 턴 행동이 아니라 패시브라서 `CanExecute`가 `HasActed`를 보지 않는다 — 이미 이번 턴 행동을 마친 유닛도 반격은 그대로 발동한다.

기본적으로 한 턴에 이동 또는 공격 중 하나만 할 수 있다 — 이동하면 그 턴엔 더 이상 공격할 수 없고
(`AttackAction.CanExecute`가 `HasMoved`를 봄), 공격하면 그 턴엔 더 이상 이동할 수 없다(`MoveAction.CanExecute`가
`HasActed`를 봄). `Charge`/`Retreat`는 값 없는 순수 마커 패시브로, 그 제약을 한쪽 방향으로만 풀어준다:
- **돌격**([`ChargeAction`](Assets/Scripts/TacticsECS/Actions/ChargeAction.cs), `AttackAction.CanExecute`가 `UnitActionQueries.Find`로 보유 여부만 확인): 이번 턴 이미 이동했어도 공격할 수 있다.
- **대피**([`RetreatAction`](Assets/Scripts/TacticsECS/Actions/RetreatAction.cs), `MoveAction.CanExecute`가 `UnitActionQueries.Find`로 보유 여부만 확인): 이번 턴 이미 공격했어도 이동할 수 있다.

둘 다 가진 유닛도 이동/공격은 여전히 턴당 1회씩만 가능하다(각각 `HasMoved`/`HasActed`로 제한) — 예를 들어
공격 → (대피로) 이동까지 마친 뒤에는, 돌격이 있어도 이미 이번 턴 공격을 마쳤으므로("대피로 이동한 뒤 다시
공격"은 발동하지 않음) 다시 공격할 수 없다. 별도 상태 없이 기존 `HasMoved`/`HasActed` 조합만으로 이 제약이
자연히 성립한다.

### 겉모습 (3D 모델)

유닛 모델은 [KayKit - Adventurers Character Pack](https://kaylousberg.itch.io/kaykit-adventurers)(Kay Lousberg 제작, CC0 — 저작자 표시 의무 없음)의 로우폴리 캐릭터를 `Assets/Art/KayKit/Characters`에 받아 사용한다. 라이선스 원문은 [`Assets/Art/KayKit/LICENSE.txt`](Assets/Art/KayKit/LICENSE.txt). 세 유닛에 실루엣이 뚜렷이 구분되도록 매칭했다: **Melee**=Barbarian(양손 도끼), **Ranged**=Rogue(석궁), **Guard**=Knight(한손검 + 사각 방패). 캐릭터 FBX 하나에 무기/방패 변형이 전부 함께 들어있어(예: Knight는 방패 4종 + 검 2종을 전부 포함), [`UnitPrefabSetup`](Assets/Editor/UnitPrefabSetup.cs)이 타입에 맞는 것만 자식 이름으로 찾아 켜고 나머지는 `SetActive(false)`로 꺼서 정리한다. 모델의 기본 정면은 카메라 반대쪽(뒤)을 보고 있어서, 고정 isometric 카메라에 얼굴/무기가 보이도록 프리팹에서 180도 돌려 붙였다. 가만히 서 있을 때뿐 아니라 이동할 때는 이동 방향으로, 공격할 때는 대상 쪽으로 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)가 자동으로 회전시킨다(`FaceTowards`/`MoveRoutine`).

플레이어 유닛은 파란/청록 계열, 적 유닛은 빨강/주황 계열 색으로 구분되며, 방어 태세 중인 유닛은 팀에 관계없이 노란색으로 표시된다. 이 색은 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)가 스폰 시 만드는 유닛별 런타임 머티리얼에서 `UnitDefinition.playerColor`/`enemyColor` 값을 모델의 겉감 텍스처(`UnitDefinition.bodyTexture`) 위에 곱해 틴트하는 방식이라, 모델의 음영/디테일은 남기면서 팀 색을 입힌다(캐릭터의 몸통/팔다리/망토/무기 등 여러 Renderer가 이 머티리얼 하나를 공유). 유닛 머리 위 텍스트는 `현재HP/최대HP`.

데미지 공식은 `max(1, 공격자 공격력 - (대상 방어력 + 방어 태세 보너스))`.

## 조작법

- **유닛 선택**: 자기 팀(플레이어) 유닛을 좌클릭. 이미 이동+행동을 모두 마친 유닛이나 적/빈 타일을 클릭하면 선택이 풀린다.
- **이동**: 유닛 선택 시 파란색으로 하이라이트된 타일이 이동 가능 범위. 그 타일을 클릭하면 이동한다 (턴당 1회). 기본적으로 이번 턴 이미 공격한 유닛은 이동할 수 없다 — 대피(Retreat) 패시브가 있으면 예외.
- **공격**: 하이라이트된 빨간 타일 위의 적 유닛을 클릭하면 공격한다 (턴당 1회). 기본적으로 이번 턴 이미 이동한 유닛은 공격할 수 없다 — 돌격(Charge) 패시브가 있으면 예외.
- **행동 버튼**(화면 우하단): 선택한 유닛의 `AvailableActions`에 있는 행동만, 아직 행동하지 않았을 때만 나타난다 — 방어 태세(Guard), 치유(Ranged), 자폭(Melee)은 모두 대상 선택 없이 버튼 클릭 한 번으로 즉시 적용된다.
- **행동 설명 툴팁**: 행동 버튼(방어/치유/자폭/선택 해제/턴 종료) 위에 마우스를 올리면, 버튼 줄 바로 위 한 구역에 그 행동에 대한 설명이 뜬다.
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

## CSV 유닛 제작·합성 테스트 툴

프리팹을 만들지 않고도 CSV 한 장으로 유닛(스탯 + 기존 6개 행동의 조합)을 정의하고, 인게임에서 그 CSV를
불러와 그리드에 자유 배치한 뒤 실제 턴제 전투로 동작을 확인할 수 있는 샌드박스 모드. 스키마/사용법/검증
방법은 [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md)에 정리했다. `Assets/Scenes/Sandbox.unity`(`SampleScene`을
복제해 `BattleController.sandboxMode`만 켠 씬)에서 Play하면 배치 단계부터 시작한다.

## 작업 로그

- 2026-09-04: 프로젝트 동작 검증.
  - Unity CLI 헤드리스 빌드(`unity run . -- -nographics`)로 컴파일 에러 없음 확인.
  - `Data`/`Systems`/`View` 계층 전체 코드 리뷰 — CLAUDE.md 규칙(Data는 값만, Systems는 무상태) 준수 확인, 로직 버그 없음.
  - **버그 발견 및 수정**: `SampleScene`에 `BattleController`가 배치되어 있지 않아 Play를 눌러도 아무것도 스폰되지 않던 문제. 씬에 `BattleController` GameObject를 추가해 해결.
  - 저장소에 `.gitignore` 추가(Unity 표준) 후 첫 커밋.
- 2026-09-04: 유닛 설명/조작법을 README에 정리, 카메라를 정통 isometric 구도(45도/35.264도 + orthographic)로 변경.
  - `BattleController.PositionCamera`가 기존의 임의 각도 원근 카메라 대신 대각선 45도 + 피치 35.264도, orthographic 투영을 사용하도록 수정. 인스펙터에서 각도/줌 조정 가능하도록 `SerializeField` 추가.
  - Unity Editor가 이미 열려 있어 CLI 헤드리스 빌드로는 검증하지 못함(같은 프로젝트 중복 실행 불가) — 표준 Unity API만 사용한 코드 리뷰로 확인.
- 2026-09-11: 돌격/대피 패시브 추가.
  - 기본 규칙 변경: 유닛은 턴당 이동 또는 공격 중 하나만 할 수 있다(`AttackAction.CanExecute`가 `HasMoved`를, `MoveAction.CanExecute`가 `HasActed`를 보도록 수정). 기존에는 둘 다 순서 상관없이 자유롭게 가능했다.
  - 값 없는 순수 마커 패시브 [`ChargeAction`](Assets/Scripts/TacticsECS/Actions/ChargeAction.cs)(돌격: 이동 후에도 공격 가능)/[`RetreatAction`](Assets/Scripts/TacticsECS/Actions/RetreatAction.cs)(대피: 공격 후에도 이동 가능) 추가 — `Counter`와 같은 패턴으로 자동 적용되는 배지 패시브. "대피로 이동 후 다시 공격 발동 안 함"은 기존 `HasActed`(공격 턴당 1회 제한) 하나로 자연히 성립해 별도 상태가 필요 없다.
  - `ActionType.Charge`/`Retreat` 플래그, `UnitCsvActionFactory`/CSV 연동, `BattleHud.PassiveDefs` 배지(자체 제작 아이콘 `charge.png`/`retreat.png` 추가, `Assets/Art/GameIcons/LICENSE.txt` 갱신) 반영.
  - 검증: Unity CLI 헤드리스 컴파일(`unity run . -- -nographics`) 통과. `MoveAction`/`AttackAction.CanExecute`를 4가지 시나리오(패시브 없음/돌격만/대피만/둘 다)로 직접 호출하는 임시 Edit Mode 스크립트를 `-executeMethod`로 실행해 14개 assertion 전부 통과 확인 후 스크립트 삭제. 기존 CSV 파이프라인 검증 도구(`unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run`)에 `Charge;Retreat` 조합의 `Duelist` 샘플 행([`docs/sample_units.csv`](docs/sample_units.csv))을 추가해 재실행, `ALL PASS` 확인.
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
- 2026-09-05: 유닛 겉모습을 프리미티브 캡슐에서 무료 3D 로우폴리 에셋으로 교체.
  - **동기**: "간단한 3D 로우폴리 무료 에셋 찾아서 현재 유닛들에 알맞게 적용해달라"는 요청을 받음.
  - **에셋 선정**: [KayKit - Adventurers Character Pack](https://kaylousberg.itch.io/kaykit-adventurers)(Kay Lousberg 제작, CC0 — 상업적 이용 자유·저작자 표시 불필요)을 GitHub 미러(`KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0`)에서 받아 사용. Barbarian/Knight/Rogue 세 캐릭터가 각각 근접/방어/원거리 실루엣과 잘 맞아 선택했고, 다운로드 전 사용자에게 에셋 후보·라이선스·용량을 확인받음. `Barbarian.fbx`/`Knight.fbx`/`Rogue.fbx` + 캐릭터별 텍스처(1024px 그라디언트 아틀라스 1장씩)를 `Assets/Art/KayKit/Characters`에 추가(라이선스 원문 [`Assets/Art/KayKit/LICENSE.txt`](Assets/Art/KayKit/LICENSE.txt)). 캐릭터 FBX 하나에 무기/방패 변형 25종 이상이 손 소켓(`handslot.l`/`.r`) 아래 전부 함께 들어있어, 별도 무기 에셋을 따로 받을 필요는 없었음. 캐릭터별 애니메이션 75개가 프레임 단위로 구워져 있어 파일당 용량이 약 20MB(3개 합계 약 60MB)로 예상보다 컸지만, 그대로 사용하기로 확인받음.
  - **프리팹 재구성**: [`UnitPrefabSetup.cs`](Assets/Editor/UnitPrefabSetup.cs)의 `CreatePrefab`을 `GameObject.CreatePrimitive(Capsule)` 대신 해당 캐릭터 모델을 자식으로 인스턴스화하도록 재작성. 루트에는 더 이상 MeshFilter/MeshRenderer/Collider를 두지 않고 `UnitView`/`UnitDefinition`만 남긴다 — 기존 `CapsuleCollider`는 (클릭 판정이 이미 그리드 평면 교차 방식이라) 실제로 쓰인 적이 없었음을 재확인한 뒤 제거. 캐릭터별로 손 소켓 아래 무기/방패 변형 중 하나만 남기고 나머지는 자식 이름으로 찾아 `SetActive(false)`: Melee=Barbarian+`2H_Axe`, Ranged=Rogue+`2H_Crossbow`, Guard=Knight+`1H_Sword`+`Rectangle_Shield`.
  - **팀 색 틴트 유지**: [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs)에 `bodyTexture` 필드를, [`RuntimeMaterial`](Assets/Scripts/TacticsECS/View/RuntimeMaterial.cs)에 텍스처 지정 기능(`SetTexture`)을 추가. [`UnitView.Init`](Assets/Scripts/TacticsECS/View/UnitView.cs)이 `GetComponent<Renderer>()`(단일) 대신 `GetComponentsInChildren<Renderer>()`(복수)로 캐릭터의 몸통/팔다리/망토/무기 Renderer 전부에 같은 런타임 머티리얼 하나를 공유시켜, 기존과 동일한 "팀 색 틴트 + 방어 태세 시 노란색" 로직(`RuntimeMaterial.SetColor`)이 텍스처 있는 모델에도 그대로 적용되게 함(그라디언트 텍스처 위에 팀 색을 곱해 음영/디테일은 유지). 모델 원점이 캡슐과 달리 발밑에 있어 스폰/이동 목표 위치의 Y 오프셋(0.5 → 0.05, 타일 표면 높이)과 HP 텍스트 높이(1.2 → 1.5)도 새 모델 비례에 맞게 조정.
  - **검증**: CLAUDE.md 규칙에 따라 Unity Editor GUI는 쓰지 않고 Unity CLI 배치모드(`unity run . -- -executeMethod ...`)로만 작업. 본 계층·머티리얼·텍스처 연결을 확인하는 1회성 조사 스크립트로 각 캐릭터의 Renderer/본 이름을 덤프해 정확한 손 소켓·무기 이름을 확보했고, 1회성 스크린샷 스크립트로 `UnitView.Init`까지 실제 런타임 경로를 태워 렌더링한 이미지를 직접 눈으로 확인하며 검증(이 과정에서 모델 기본 정면이 고정 isometric 카메라 반대쪽을 보고 있던 문제를 발견해 프리팹에서 모델을 180도 회전시켜 수정). 두 조사/검증용 스크립트와 렌더링 결과물은 확인 후 삭제(최종 산출물 아님). Unity CLI 실행 로그에 컴파일 에러/예외 없음.
- 2026-09-05: 유닛이 이동/공격 시 그 방향을 바라보도록 회전 추가.
  - **동기**: "적어도 이동이나 공격 행동 시 그 방향을 바라봐야 하지 않을까"라는 피드백을 받음 — 새로 붙인 KayKit 모델은 캡슐과 달리 정면이 있는데도, 지금까지는 방향에 관계없이 항상 같은 자세로만 서 있었음.
  - [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs): 이동 중에는 `MoveRoutine`이 위치를 옮기는 것과 동시에 이동 방향으로 부드럽게 회전(`Quaternion.RotateTowards`)하고, 공격처럼 제자리에서 일어나는 행동을 위해 즉시 특정 지점을 바라보게 하는 `FaceTowards(Vector3)`를 새로 추가. 둘 다 같은 내부 헬퍼로 계산: 모델의 정면 보정(180도, `UnitPrefabSetup`이 자식에 붙인 것과 같은 값)을 감안해 `Quaternion.LookRotation(방향) * 180도 보정`으로 실제 원하는 방향을 향하게 한다.
  - **공격 시 바라보기 연결**: `BattleController.TryAttack`(플레이어 조작)은 공격이 성사되면 공격자 View가 대상 위치를 바로 바라보게 한다. 적 턴은 [`EnemyAI.RunTurn`](Assets/Scripts/TacticsECS/Systems/EnemyAI.cs)이 모든 유닛의 행동을 한 번에 처리한 뒤 `BattleController`가 일괄로 `RefreshAllViews`를 부르는 구조라 개별 공격 시점을 알 수 없는데, "`EnemyAI`는 View/MonoBehaviour를 몰라야 한다"는 원칙은 그대로 지키면서 — `RunTurn`이 이번 턴 실제로 성사된 공격들을 `(공격자 id, 대상 id)` 순수 데이터 목록(`List<(int, int)>`)으로 반환하도록 바꾸고, `BattleController`가 이 목록을 받아 각 공격자 View에 `FaceTowards`를 호출하는 방식으로 연결했다.
  - **검증**: Unity CLI 배치모드로 유닛을 스폰해 `FaceTowards`로 동/서/남/북 네 방향을 바라보게 한 뒤, 대상 위치에 마커를 놓고 실제 게임과 같은 isometric 카메라 각도로 렌더링해 확인 — 계산된 회전값이 방향별 예상치(예: 동쪽 목표 → yaw 270도)와 정확히 일치했고, 렌더링 결과에서도 모델 정면/무기가 마커 쪽을 향함을 확인. 이동 중 회전은 같은 계산식을 재사용하므로 별도 로직 없이 함께 검증됨. 검증용 스크립트는 확인 후 삭제. Unity CLI 헤드리스 실행으로 컴파일 에러 없음 확인.
- 2026-09-06: OnGUI 기반 UI(기본 스킨 텍스트 라벨/버튼)를 아이콘 중심 HUD로 전면 교체.
  - **동기**: "UI/UX가 개판이니 무료 에셋을 가져와서 능력치가 직관적으로 보이게, 텍스트는 최대한 줄이고 아이콘으로 표현할 수 있는 건 전부 아이콘으로" 요청을 받음. 기존 UI는 `BattleController.OnGUI`가 `GUI.Label`/`GUI.Button`으로 그린 텍스트 문장(`"N턴 - 플레이어 턴"`, `"턴 종료"`, `"방어 태세"`...)이 전부였고, 선택한 유닛의 공격력/방어력/이동/사거리는 화면 어디에도 표시되지 않았다(머리 위 HP 텍스트가 유일한 수치 표시).
  - **아이콘 에셋**: [game-icons.net](https://game-icons.net)(Lorc/Delapouite/Sbed/Skoll 등, CC BY 3.0)에서 능력치 하나당 아이콘 하나씩 10종 선정 — HP=하트, 공격력=검, 방어력=방패, 이동범위=발자국, 사거리=조준선, 방어 태세(행동)=반짝이는 방패, 선택 해제=X, 턴/턴종료=모래시계, 승리=트로피, 패배=해골. 후보를 사용자에게 미리보기로 보여주고 라이선스·용량(총 10장 약 57KB) 확인받은 뒤 진행. `Assets/Art/GameIcons/Resources/Icons/*.png` + 출처를 명시한 `Assets/Art/GameIcons/LICENSE.txt` 추가. `Resources` 폴더에 넣어 `Resources.Load<Texture2D>`로 불러온 뒤 [`IconLibrary`](Assets/Scripts/TacticsECS/View/IconLibrary.cs)가 `Sprite.Create`로 감싸 캐싱 — 텍스처 임포트 설정(Sprite 타입 지정)을 바꾸는 에디터 작업 없이도 되게 한 선택(CLAUDE.md의 "에디터 직접 조작 금지" 원칙).
  - **[`BattleHud`](Assets/Scripts/TacticsECS/View/BattleHud.cs) 신설**(uGUI Canvas, 코드로만 구성 — 프리팹/씬 편집 없음): 좌상단 턴 배지(모래시계 아이콘 + 턴 수, 배경색으로 플레이어/적 턴 구분 — "N턴 - 플레이어 턴" 문장 제거), 좌하단 선택 유닛 능력치 패널(아이콘+숫자만 있는 5줄: HP는 막대까지, 나머지는 숫자 하나), 우하단 아이콘 전용 행동 버튼 3개(턴 종료/방어 태세/선택 해제 — 텍스트 라벨 없음, 상시 뜨는 턴 종료를 코너에 고정해 선택 여부와 무관하게 위치가 안정적), 중앙 승/패 오버레이(트로피/해골 아이콘 + 최소 텍스트). 방어 태세 중인 능력치는 [`CombatSystem.EffectiveDefense`](Assets/Scripts/TacticsECS/Systems/CombatSystem.cs)를 그대로 호출해 보너스(+2)를 반영하고 노란색으로 강조 — "방어 태세면 얼마나 세지는지"를 UI가 따로 계산하지 않고 실제 데미지 계산과 같은 값을 쓰도록 `CombatSystem`에 `GuardDefenseBonus`/`EffectiveDefense`를 뽑아냄. `BattleController`는 OnGUI를 완전히 제거하고 `BattleHud`의 이벤트(`OnEndTurnClicked`/`OnDefendClicked`/`OnDeselectClicked`)만 구독.
  - **입력 시스템 연동**: 이 프로젝트는 새 Input System 전용(`Project Settings`의 Active Input Handler)이라, uGUI 클릭을 받으려면 레거시 `StandaloneInputModule`이 아니라 `InputSystemUIInputModule`이 필요 — `BattleHud`가 `EventSystem`이 없으면 직접 만들어 붙인다. 또한 `BattleController.Update`의 기존 그리드 클릭 판정 앞에 `EventSystem.current.IsPointerOverGameObject()` 체크를 추가해, HUD 버튼 클릭이 그 뒤 타일/유닛 클릭으로 새지 않게 막았다.
  - **머리 위 체력 표시 개선**: 기존 `"현재/최대"` 텍스트를 숫자(현재 HP만) + 상태를 색으로 보여주는 막대로 교체(초록/노랑/빨강 — [`HpColorScale`](Assets/Scripts/TacticsECS/View/HpColorScale.cs)를 `BattleHud`와 공유해 기준을 하나로 유지). 막대는 배경 Quad 위에 채우기 Quad를 겹쳐 왼쪽 고정으로 줄어들게 구현.
  - **스크린샷 검증 중 발견한 버그 2건과 수정**: `-executeMethod`로 Play Mode에 진입해도 메서드가 리턴하는 즉시 `unity run` 래퍼가 배치를 종료해버려(Play Mode 진입은 비동기라 미처 시작되기도 전에 꺼짐) 애초에 계획한 "실제 플레이 경로" 검증이 불가능했다 — 대신 빈 씬에서 `BattleHud`/`UnitView`를 코드로 직접 생성해 동기적으로 렌더링하는 1회성 스크립트(`Assets/Editor/HudScreenshotVerify.cs`, 확인 후 삭제)로 우회.
    1. 유닛 머리 위 체력 막대/숫자가 유닛 회전(이동·공격 시 몸이 도는 것)에 따라 카메라 반대쪽을 보면 평면(Quad)이라 실처럼 가늘어져 사라지거나(막대), 좌우가 뒤집혀 보이는(TextMesh 숫자) 문제를 스크린샷으로 실제 확인. `UnitView`에 `HpBillboardRotation()`을 추가해 체력 표시 그룹만 몸통과 별개로 항상 카메라를 보게 고정(스폰/이동 중/공격 시점마다 재적용, 매 프레임 갱신은 아니라 유휴 유닛 비용은 그대로 0). 평면 Quad에는 [`RuntimeMaterial.SetDoubleSided`](Assets/Scripts/TacticsECS/View/RuntimeMaterial.cs)로 컬링도 꺼서 이중으로 안전하게 함.
    2. 위 수정이 제대로 동작하려면 스폰 시점에 `Camera.main`이 이미 최종 isometric 각도로 배치돼 있어야 하는데, `BattleController.SetupBattle`이 유닛을 먼저 스폰하고 카메라를 나중에 배치하고 있었다 — 순서를 카메라 배치 → 스폰으로 바꿔 해결.
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 확인.
- 2026-09-06: CLAUDE.md 규칙에 맞춰 프로젝트 전체 재점검, `Systems`가 상태를 갖던 위반 1건 수정.
  - **점검 범위**: `Data`/`Core`/`Systems` 아래 모든 타입을 CLAUDE.md 규칙 2("Data는 값만")/3("Systems는 상태를 갖지 않는다") 기준으로 재검토.
  - **발견한 위반**: [`TurnManager`](Assets/Scripts/TacticsECS/Systems/TurnManager.cs)(옛 파일, 삭제됨)가 `Systems` 폴더에 있으면서도 `MonoBehaviour`로서 `ActiveTeam`/`TurnNumber`/`_world`를 자체 필드로 들고 있었음 — 다른 System(`CombatSystem`/`MovementSystem`/`PathfindingSystem`/`EnemyAI`/`UnitQueries`)은 전부 상태 없는 정적 클래스인데 이 타입만 예외였다.
  - **수정**: 턴 상태를 값 하나로 뽑아 [`TurnState`](Assets/Scripts/TacticsECS/Core/TurnState.cs)(`ActiveTeam`/`TurnNumber`, Core 계층 순수 struct) 신설. `TurnManager`를 삭제하고 [`TurnSystem`](Assets/Scripts/TacticsECS/Systems/TurnSystem.cs)(정적, 무상태)으로 교체 — `StartTurn(world, team, turnNumber)`/`EndTurn(world, current)`이 `TurnState`를 인자로 받아 다음 `TurnState`를 계산해 반환할 뿐, 자기 자신은 아무 값도 보관하지 않는다. 실제 `TurnState` 보관은 오케스트레이터인 `BattleController`의 필드(`_turnState`)로 옮김 — `BattleController`는 System이 아니라 조율자이므로 상태를 가져도 규칙 위반이 아니다. `TurnManager` 전용 `GameObject`도 더 이상 필요 없어져 제거.
  - **그 외 항목**: `GridWorld`/`EntityWorld`(`Data` 계층)는 함수(접근자 메서드)를 갖고 있지만, 둘 다 "유닛" 같은 도메인 개념을 전혀 모르는 범용 저장소(리스트/배열 인덱싱 수준의 Get/Set)이고 게임 규칙(이동 가능 여부, 공격 판정 등)은 여전히 전부 `Systems`에 있어 규칙 2의 취지(도메인 로직을 Data에 두지 않는다)를 벗어나지 않는다고 판단, 손대지 않음. 나머지 `Systems`(정적 클래스들)와 `Core`(struct들)는 규칙 위반 없음.
  - Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러/예외 없음 확인.
- 2026-09-08: 유닛별 "사용 가능 행동"을 직접 지정할 수 있게 하고, 새 행동(치유/자폭)을 추가해 UI에 노출.
  - **동기**: "유닛의 사용 가능 행동을 내가 직접 지정할 수 있어야 한다"는 요청 — 지금까지는 방어 태세만 `UnitDefinition.CanGuard`라는 전용 bool로 개별 관리되고 있어, 행동을 추가할 때마다 비슷한 bool을 하나씩 늘려야 하는 구조였다.
  - **행동 목록의 단일 기준점 도입**: [`ActionType`](Assets/Scripts/TacticsECS/Core/ActionType.cs)(`Move`/`Attack`/`Defend`/`Heal`/`SelfDestruct` 플래그) 신설, `Core/UnitComponents.cs`의 `CanGuard`를 제거하고 `AvailableActions`(`ActionType` 값) 컴포넌트로 교체. `UnitDefinition.availableActions` 인스펙터 필드(다중 선택 드롭다운) 하나로 유닛 타입별 행동 조합을 직접 고를 수 있고, `UnitSpawner`가 그 값을 그대로 `EntityWorld`에 옮긴다. `MovementSystem.TryMove`/`CombatSystem.TryAttack`/`TryDefend`가 전부 이 값 하나로 판정하도록 정리.
  - **새 행동 구현**: [`AbilitySystem`](Assets/Scripts/TacticsECS/Systems/AbilitySystem.cs)(정적, 무상태) 신설. `TryHeal`은 사거리(`HealRange`) 내 모든 아군(자신 제외)을 `HealAmount`만큼 회복, `TrySelfDestruct`는 유닛을 즉시 제거하고 주위 1칸(맨해튼 거리)의 모든 적에게 제거 시점의 남은 체력만큼 피해를 입힌다. 새 컴포넌트 `HealAmount`/`HealRange` 추가. 데모 3종에 배분: Guard=방어, Ranged=치유(회복 4/사거리 2), Melee=자폭.
  - **UI**: [`BattleHud`](Assets/Scripts/TacticsECS/View/BattleHud.cs)의 고정 3버튼(선택해제/방어/턴종료) 구조를 데이터 기반으로 재구성 — 방어/치유/자폭을 `(ActionType, 아이콘, 툴팁)` 표 하나로 정의하고, 선택된 유닛의 `AvailableActions`(그리고 `HasActed`)에 따라 보여줄 버튼만 동적으로 배치(`SetUnitActions`)한다. 행동 버튼(방어/치유/자폭/선택해제/턴종료) 각각에 마우스 진입/이탈 시 설명을 보여주는 툴팁을 붙였고, 풍선말 대신 버튼 줄 바로 위 고정된 한 구역(`BuildTooltip`)에 표시되게 해서 isometric 3D 화면과 겹쳐 가려지는 문제를 피했다.
  - **아이콘**: 기존 GameIcons 세트와 같은 출처인 [game-icons.net](https://game-icons.net)(CC BY 3.0)에서 치유="Health potion"(Delapouite), 자폭="Grenade"(Lorc) 2종을 새로 받아 `Assets/Art/GameIcons/Resources/Icons`에 추가하고 `LICENSE.txt`에 출처를 기록 — 기존 10종과 같은 512x512 검정 실루엣/투명 배경 스타일.
  - **검증**: Unity CLI 배치모드(`-executeMethod UnityEditor.SyncVS.SyncSolution`)로 전체 스크립트 재컴파일 — 에러 없음, 새 아이콘 2장 정상 임포트 확인.
- 2026-09-11: 유닛 행동에 **반격**(패시브) 추가, UI에 패시브 전용 배지(동그라미 배경) 도입.
  - **동기**: "공격을 받았을 때 공격받은 대상에게 데미지를 주는 패시브"를 추가하고, 다른 행동처럼 UI에 아이콘으로 보이되 패시브는 행동 버튼의 네모 배경과 구분되게 동그라미 배경으로 표시해달라는 요청.
  - [`ActionType`](Assets/Scripts/TacticsECS/Core/ActionType.cs)에 `Counter` 플래그 추가. 다른 플래그와 달리 이 값은 플레이어가 누르는 "행동"이 아니라 항상 자동 발동하는 패시브임을 주석으로 명시.
  - [`CombatSystem.TryAttack`](Assets/Scripts/TacticsECS/Systems/CombatSystem.cs)이 공격이 성사되고 대상이 살아남으면 비공개 `TryCounter`를 호출 — 대상이 `Counter`를 갖고 공격자가 대상 사거리 안에 있으면 자동으로 피해를 되돌려주고, 그 반격으로 공격자가 죽으면 그 자리에서 occupant도 제거한다. 반격은 패시브라 `HasActed`를 건드리지 않는다. `TryAttack`이 `counterDamageDealt` out 파라미터를 새로 반환하도록 시그니처가 바뀌어 `BattleController`/`EnemyAI`의 호출부도 함께 갱신.
  - **UI**: [`BattleHud`](Assets/Scripts/TacticsECS/View/BattleHud.cs)의 좌하단 유닛 패널에 패시브 배지 줄을 새로 추가(`PassiveDefs` 표, 선택된 유닛이 실제로 가진 패시브만 표시). 행동 버튼(`CreateIconButton`, 네모 배경 + `Button`)과 구분하기 위해 배지는 클릭 불가능한 표시 전용(`CreatePassiveBadge`)이고, 배경도 네모 `Image` 대신 런타임에 픽셀을 직접 채워 만든 원형 스프라이트(`CircleSprite`, 텍스처 임포트 설정을 건드리지 않기 위해 `IconLibrary`와 같은 방식으로 코드에서 생성)를 쓰며 색도 행동 버튼과 다른 보라색(`PassiveBadgeBg`)으로 한 번 더 구분했다. 마우스 호버 시 기존 툴팁 구역에 설명이 뜨는 것은 행동 버튼과 동일.
  - **아이콘**: game-icons.net에 반격 전용 아이콘이 없어, 기존 10여 종과 같은 512x512 흰색 실루엣/투명 배경 스타일로 `counter.png`(되돌아오는 화살표 모양)를 직접 제작 — Unity 에디터 GUI 없이 PowerShell + System.Drawing(GDI+)으로 그려서 저장하고, 기존 아이콘의 `.meta`를 복사해 새 GUID만 교체(CLAUDE.md의 "에디터 직접 조작 금지" 원칙과 동일한 이유). `Assets/Art/GameIcons/LICENSE.txt`에 자체 제작임을 명시.
  - **데모 배분**: `Unit_Guard.prefab`(탱커)의 `availableActions`에 `Counter`를 추가(7 → 39: Move+Attack+Defend+Counter) — 방어 태세와 궁합이 맞는 역할이라 판단.
  - **검증**: Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 전체 재컴파일 — 컴파일 에러 없음 확인.
- 2026-09-11: `UnitDefinition`의 행동 지정 방식을 `ActionType` 다중 선택 드롭다운에서 행동별 개별 스크립트의 집합으로 교체.
  - **동기**: "`ActionType`에 있는 모든 값을 개별 스크립트로 나누고, `UnitDefinition`의 관련 값들을 그 스크립트로 옮긴 뒤 `UnitDefinition`은 그 집합으로 정의해달라"는 요청. 처음엔 명시적 필드 6개(각자 `enabled` 토글) 구조를 제안했으나, "행동을 정의하는 인터페이스를 만들어서 그 리스트를 집합으로 정의해달라"는 후속 요청을 받아 인터페이스 + 다형 리스트 구조로 다시 잡았다.
  - **범위**: `Systems`/`Data` 계층(`ActionType` 비트플래그, `AvailableActions` 컴포넌트, `CombatSystem`/`MovementSystem`/`AbilitySystem`의 `HasFlag` 판정, `BattleHud`의 행동 버튼·패시브 배지 목록)은 그대로 두기로 사용자와 합의 — `UnitDefinition`(View 계층)의 내부 표현만 바꾸고, 그 바깥으로 보이는 공개 API(`MaxHp`/`Attack`/`AttackRange`/`HealAmount`/`HealRange`/`AvailableActions`/`MoveRange`/`IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal` 프로퍼티)는 시그니처를 그대로 유지해, `UnitSpawner`를 포함한 다른 파일은 한 줄도 고칠 필요가 없게 했다.
  - `Assets/Scripts/TacticsECS/View/Actions/` 신설: [`IUnitAction`](Assets/Scripts/TacticsECS/View/Actions/IUnitAction.cs)(`ActionType Type { get; }` 하나만 요구) + 이를 구현하는 6개 클래스(`MoveAction`/`AttackAction`/`DefendAction`/`HealAction`/`SelfDestructAction`/`CounterAction`, 전부 `[System.Serializable]` plain class). 각 값이 어떤 행동 때문에 존재하는지가 소속 클래스로 드러난다 — 예: `moveRange`/`ignoreTerrain`/`ignoreUnitBlocking`/`allowDiagonal`은 `MoveAction`, `healAmount`/`healRange`는 `HealAction`. `Defend`/`SelfDestruct`/`Counter`는 별도 값이 없어 마커 역할만 한다(방어 태세 보너스는 `CombatSystem.GuardDefenseBonus`, 자폭 피해량은 발동 시점 HP, 반격은 `AttackAction`의 공격력/사거리를 그대로 재사용 — 전부 기존 로직 그대로). 기본 방어력(`Defense`)과 `MaxHp`는 어느 행동을 갖는지와 무관하게 항상 적용되는 값이라 행동 스크립트로 옮기지 않고 `UnitDefinition`에 남겼다.
  - [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs)의 `attack`/`attackRange`/`healAmount`/`healRange`/`availableActions`/`moveRange`/`ignoreTerrain`/`ignoreUnitBlocking`/`allowDiagonal` 개별 필드를 전부 제거하고 `[SerializeReference] private List<IUnitAction> actions`로 교체 — 이 리스트가 곧 "행동의 집합"이다. `AvailableActions`는 리스트를 훑어 `ActionType` 비트마스크로 합친 계산값, `MoveRange`/`Attack`/`HealAmount`... 등은 `actions.OfType<T>().FirstOrDefault()`로 해당 행동을 찾아 값을 읽는 계산값 프로퍼티(없으면 기본값)로 바뀌었다.
  - **프리팹 마이그레이션**: `Assets/Prefabs/Units/Unit_Guard.prefab`/`Unit_Melee.prefab`/`Unit_Ranged.prefab`이 예전 필드에 저장해두고 있던 값(예: Guard의 `availableActions: 39` = Move+Attack+Defend+Counter)을 손실 없이 새 구조로 옮기기 위해, 각 프리팹 YAML의 `UnitDefinition` 블록을 Unity의 managed reference 직렬화 포맷(`actions: [{rid: ...}, ...]` + 같은 블록 하단의 `references: {version: 2, RefIds: [{rid, type: {class, ns, asm}, data: {...}}, ...]}`)에 맞춰 직접 재작성했다(Unity 에디터 GUI 없이).
  - **검증**: Unity CLI 헤드리스 실행(`unity run . -- -nographics`)으로 컴파일 에러 없음 확인. 이어서 1회성 에디터 스크립트(`Assets/Editor/VerifyUnitDefinitionsTemp.cs`, 확인 후 삭제)를 `-executeMethod`로 실행해 세 프리팹을 `AssetDatabase.LoadAssetAtPath`로 로드하고 `UnitDefinition`의 모든 프로퍼티 값을 로그로 출력 — Guard/Melee/Ranged 전부 기존 스탯·행동 조합(`AvailableActions=Move, Attack, Defend, Counter` 등)이 정확히 일치함을 확인해 수기로 작성한 YAML이 올바르게 역직렬화됨을 검증했다.
- 2026-09-11: 바로 위 `IUnitAction`을 "값보다 행동으로" 다시 설계 — 인터페이스를 메서드 중심으로 바꾸고, 실행 가능 여부/효과 판정 자체를 각 행동 클래스로 옮김. `ActionType`은 실행에 관여하지 않는 순수 플레이스홀더가 됨.
  - **동기**: "`IUnitAction` 인터페이스는 메서드로 정의해달라, 값보다 행동으로 구분해야 한다, `ActionType`은 그냥 플레이스홀더로 남겨달라(CSV로 써먹을 것)"는 요청. 어느 수준까지 바꿀지(식별만 메서드로 바꾸는 가벼운 안 vs. 행동 자체를 메서드로 구현해 `Systems`의 `HasFlag` 판정을 전부 교체하는 심화 안) 먼저 확인받았고, 심화 쪽으로 진행하기로 합의 — 이전에 합의했던 "`Systems`/`Data`는 그대로 둔다"는 범위를 이번 요청으로 넘어서는 것까지 포함해서.
  - **인터페이스**: [`IUnitAction`](Assets/Scripts/TacticsECS/Actions/IUnitAction.cs)이 `ActionType Type { get; }` 프로퍼티 대신 `GetActionType()`(식별 태그, CSV/UI 용도로만 쓰임)과 `CanExecute(world, unitId)`(생존/이번 턴 이동·행동 여부 등 행동마다 다른 조건을 행동 스스로 판단) 두 메서드를 요구하도록 재정의. 행동마다 실행에 필요한 매개변수 모양이 달라(이동=목적지, 공격/반격=대상, 방어/치유/자폭=자기 자신만) 실제 실행 메서드는 하위 인터페이스 3개로 분리: [`IMoveAction`](Assets/Scripts/TacticsECS/Actions/IMoveAction.cs)/[`ISelfAction`](Assets/Scripts/TacticsECS/Actions/ISelfAction.cs)/[`ITargetedAction`](Assets/Scripts/TacticsECS/Actions/ITargetedAction.cs).
  - **로직 이전**: `MovementSystem.TryMove`/`CombatSystem.TryAttack`의 비공개 `TryCounter`/`CombatSystem.TryDefend`/`AbilitySystem.TryHeal`/`AbilitySystem.TrySelfDestruct`에 있던 실행 가능 여부 판정(`AvailableActions.HasFlag(...)` 비교)과 효과 적용 로직을 각각 대응하는 행동 클래스의 `Execute`로 그대로 옮김 — `MoveAction`/`AttackAction`/`DefendAction`/`HealAction`/`SelfDestructAction`/`CounterAction`(전부 `Assets/Scripts/TacticsECS/Actions`, `View/Actions`에서 이동) 각자가 이제 "쓸 수 있는가"와 "쓰면 무슨 일이 일어나는가"를 전부 직접 안다. 위 System 5개는 껍데기만 남아 `UnitActionQueries.Find<T>`로 유닛의 행동 목록에서 필요한 타입을 찾아 위임할 뿐이다. `CombatSystem`의 순수 계산 함수(`IsInAttackRange`/`EffectiveDefense`/`CalculateDamage`/`GuardDefenseBonus`)는 여러 행동과 `BattleHud`가 공유하므로 그대로 유지.
  - **새 컴포넌트**: `Core/UnitComponents.cs`에 `UnitActions { IReadOnlyList<IUnitAction> Value }` 추가 — `UnitDefinition.actions` 리스트가 스폰 시 그대로 옮겨진다. [`UnitActionQueries.Find<T>`](Assets/Scripts/TacticsECS/Systems/UnitActionQueries.cs)(신설, 정적/무상태)가 이 컴포넌트에서 `OfType<T>().FirstOrDefault()`로 원하는 행동을 찾아준다. `AvailableActions`(`ActionType` 비트마스크) 컴포넌트는 삭제하지 않고 남겨뒀지만 이제 실행 판정에는 전혀 쓰이지 않는 태그 — `BattleHud`의 아이콘 매칭(어떤 행동에 어떤 버튼/배지를 보여줄지)과 향후 CSV 내보내기/불러오기 용도로만 남긴다.
  - **파급**: `AbilitySystem.TryHeal`/`CombatSystem.TryDefend`가 `SelfDestructAction`/`DefendAction`처럼 `GridWorld`가 필요한 행동과 시그니처를 맞추려고 `grid` 매개변수를 새로 받게 됨 — `BattleController`의 두 호출부(`HandleHealClicked`/`HandleDefendClicked`)를 함께 갱신. `BattleController.RecomputeHighlights`의 이동/공격 가능 범위 하이라이트 판정도 `available.HasFlag(...)` 대신 `UnitActionQueries.Find<MoveAction/AttackAction>(...).CanExecute(...)`로 교체해 판정 방식을 전체적으로 일관되게 맞췄다(`BattleHud.SetUnitActions`에 넘기는 `AvailableActions` 값 자체는 순수 표시용이라 그대로 유지). `Actions` 폴더가 `Core`(신설 `UnitActions` 컴포넌트)와 `Systems`(각 System의 위임 호출) 양쪽에서 참조되므로, `View` 밑이 아니라 `Assets/Scripts/TacticsECS/Actions`라는 새 최상위 폴더로 옮겼다 — `Core`가 `View`를 참조하는 방향은 만들지 않으면서도, CLAUDE.md 규칙 2(`Data`/`Core`는 로직 없이 순수 데이터만)는 어기지 않는다: `UnitActions` 구조체 자신은 필드 하나뿐인 순수 데이터이고, 로직(`Execute` 등)은 `Core`/`Data` 바깥인 이 새 폴더의 타입들이 가진다. 프리팹 YAML은 손대지 않았다 — 클래스 이름/네임스페이스/어셈블리가 그대로라 managed reference 직렬화(`type: {class, ns, asm}`)가 그대로 유효하다.
  - **검증**: Unity CLI 헤드리스 실행으로 컴파일 에러 없음 확인. 이어서 1회성 에디터 스크립트(`Assets/Editor/VerifyActionsRuntimeTemp.cs`, 확인 후 삭제)로 빈 `EntityWorld`/`GridWorld`에 세 프리팹으로 유닛을 직접 스폰해 이동/공격/반격/방어(성공·실패 둘 다)/치유/자폭을 전부 `-executeMethod`로 실제 실행 — 데미지 계산값, `HasActed`/`HasMoved` 갱신, 반격 발동 여부, 치유된 아군 id, 자폭 피해 대상 id까지 기대값과 정확히 일치함을 확인했다(로그 예: `Attack: attacked=True dmg=2`, `Counter: counterDmg2=2`, `Defend(guard, 이미 행동함): defendedGuard=False`).
- 2026-09-11: CSV로 유닛을 제작·합성해 인게임에서 배치·테스트할 수 있는 샌드박스 모드 추가.
  - **동기**: "유닛을 CSV에서 제작하고 합성해서 테스트해볼 수 있는 툴"을 요청받음 — 기존 6개 `IUnitAction`의
    조합만으로 새 유닛 타입을 CSV 값 조정만으로 만들고, 그 목록을 인게임에서 불러와 직접 배치해볼 수 있어야 함.
  - **CSV 스키마**: `Name`/`MaxHp`/`Defense`/`BaseVisual`(외형을 빌려올 기존 프리팹)/`PlayerColor`/`EnemyColor`/
    `Actions`(세미콜론 구분 행동 목록, `ActionType` 이름 재사용)와 행동별 파라미터 컬럼(`Move.*`/`Attack.*`/`Heal.*`).
    값 타입은 [`UnitCsvRow`](Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)(Data 계층, 값만). 자세한 표는
    [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md), 예시는 [`docs/sample_units.csv`](docs/sample_units.csv).
  - **파싱/변환**: [`UnitCsvSerializer`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs)(Parse/Write, Unity
    오브젝트 의존 없는 순수 문자열 변환)와 [`UnitCsvActionFactory`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvActionFactory.cs)
    (CSV 행 ↔ `IUnitAction` 목록 변환) 신설 — 둘 다 `Systems` 규칙대로 무상태 정적 클래스. `MoveAction`/`AttackAction`/
    `HealAction`에 CSV 파라미터로 인스턴스를 만드는 `FromCsv` 정적 팩토리를 추가해, 리플렉션 없이 같은 클래스 안에서
    private 필드를 채운다(Inspector용 필드/생성자는 그대로 유지).
  - **런타임 스폰 연동**: [`UnitDefinition.ApplyCsvOverrides`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 추가 —
    프리팹 에셋이 아니라 인스턴스 하나에만 CSV 값(스탯/행동/색)을 덮어쓴다(원본 프리팹은 오염되지 않음, 겉모습
    텍스처는 `BaseVisual` 프리팹 값을 그대로 둠). [`UnitSpawner`](Assets/Scripts/TacticsECS/View/UnitSpawner.cs)의
    기존 `Spawn`에서 "엔티티 생성 + 컴포넌트 채우기" 공통부를 `FinishSpawn`으로 뽑아내고, 그 위에 CSV 전용
    `SpawnFromCsv`(베이스 프리팹 인스턴스화 → `ApplyCsvOverrides` → `FinishSpawn` 공유)를 추가 — 기존 `Spawn`
    동작/시그니처는 그대로라 데모 편성은 변경 없이 계속 동작한다.
  - **샌드박스 배치 단계**: `BattleController`를 복제하지 않고 재사용 — 새 `sandboxMode` 인스펙터 플래그가 켜져 있으면
    데모 편성(`SpawnDemoFormation`) 대신 배치 단계로 들어가고, "전투 시작"을 누르면 기존 `TurnSystem.StartTurn`부터
    이어지는 완전히 동일한 턴제 전투 코드로 넘어간다(카메라/그리드/전투 로직 재사용, 화면 좌표→그리드 좌표 변환도
    `TryScreenToGridPos`로 공용화). 배치 단계 전용 로직은 [`UnitPlacementController`](Assets/Scripts/TacticsECS/Sandbox/UnitPlacementController.cs)
    (팔레트에서 고른 CSV 행 + 팀을 "브러시"로 삼아 빈 칸 클릭 시 스폰, 이미 유닛이 있는 칸 클릭 시 제거 — 제거는
    새 개념 없이 기존 "Hp=0이면 죽은 유닛" 규칙을 그대로 재사용), UI는 `BattleHud`와 같은 패턴의 얇은 View인
    [`SandboxHud`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)(CSV 경로 입력/불러오기·내보내기 버튼/팔레트/팀
    토글/전투 시작 버튼)가 맡는다. CSV 파일은 텍스트 경로 입력으로 읽고 쓴다(OS 네이티브 파일 다이얼로그는 v1
    범위 밖).
  - **씬 준비**: Unity 에디터 GUI를 직접 열어 씬을 만들지 않고, `Assets/Editor/SandboxSceneSetup.cs`(Editor 전용,
    `-executeMethod`로만 실행)가 `SampleScene.unity`를 복제해 `Assets/Scenes/Sandbox.unity`를 만들고
    `BattleController.sandboxMode`를 켠다(리플렉션으로 private 필드 설정 — `UnitPrefabSetup.cs`의 기존 패턴 재사용).
    기존 Melee/Ranged/Guard 프리팹 참조가 복제된 씬에 그대로 남아있어 추가 연결이 필요 없다.
  - **검증**: 클릭으로 배치하는 실제 UI 흐름은 상호작용 검증이라 자동화하지 않았지만, CSV 파싱/왕복과 스폰 연동은
    새 배치모드 스크립트 `Assets/Editor/UnitCsvVerification.cs`(`-executeMethod`)로 검증 — `docs/sample_units.csv`를
    Parse→Write→재파싱해 값이 보존되는지, `SpawnFromCsv`로 만든 엔티티의 `MaxHp`/`Defense`/`Attack`/`Actions`/그리드
    occupant가 CSV 행과 일치하는지 확인하고 `[UnitCsvVerification] ALL PASS`를 Console에 남기도록 실행해 확인했다.
    `unity run . -- -nographics -executeMethod TacticsECS.EditorTools.SandboxSceneSetup.Generate`로 `Sandbox.unity`
    생성도 에러 없이 확인(Unity Editor가 열려 있지 않음을 먼저 확인한 뒤 CLI로만 실행).
- 2026-09-11: 샌드박스 CSV 경로 입력창을 OS 파일 탐색기로 교체.
  - **동기**: "불러오는 파일 경로를 파일 탐색기를 따로 켜서 진행할 수 있도록 해달라, 이름 input 하는 공간은
    삭제해달라"는 요청.
  - [`SandboxHud`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)의 CSV 경로 `InputField`를 완전히 제거하고,
    **불러오기**/**내보내기** 버튼이 각각 `UnityEditor.EditorUtility.OpenFilePanel`/`SaveFilePanel`(에디터 전용
    네이티브 파일 탐색기)로 경로를 직접 고르도록 변경(`#if UNITY_EDITOR`로 감싸 빌드에서는 안내 문구만 남김).
    `BattleController`의 더 이상 쓰이지 않는 `defaultSandboxCsvPath` 필드도 함께 제거.
  - **검증**: `unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run` 재실행 — 컴파일
    에러 없음, `ALL PASS` 확인.
- 2026-09-11: `docs/sample_units.csv`에 Defend/SelfDestruct/Counter 예시를 반영하고, 전투 종료 화면에 배치
  단계(커스텀 화면)로 되돌아가는 "다시 시작" 버튼 추가.
  - **동기**: "기존 Defend, SelfDestruct, Counter의 예시도 이 CSV 방식에 적용해주고, 게임 종료 시 커스텀
    화면으로 되돌아갈 수 있도록 만들어달라"는 요청.
  - **CSV 예시 갱신**: `docs/sample_units.csv`의 Melee/Ranged/Guard 행이 실제 게임 프리팹과 같은 행동 조합을
    갖도록 맞췄다 — Melee에 `SelfDestruct`, Ranged에 `Heal`(회복 4/사거리 2), Guard에 `Counter`를 추가해
    (Guard는 사용자가 이미 `MaxHp`를 9로 직접 조정해둔 값 그대로 유지) 6개 행동이 예시 4행에 전부 한 번씩
    나타나게 했다. `docs/UnitCsvSandbox.md`에 이 대응 관계를 설명하는 문장 추가.
  - **"다시 시작" 버튼**: [`BattleHud`](Assets/Scripts/TacticsECS/View/BattleHud.cs)의 승/패 오버레이
    (`BuildBattleEndPanel`)에 버튼을 추가하고 `OnRestartClicked` 이벤트로 클릭을 알린다.
    [`BattleController.HandleReturnToSetup`](Assets/Scripts/TacticsECS/BattleController.cs)이 이 이벤트를 받아
    지금 전투에 쓰인 모든 자식 오브젝트(`GridView`/`BattleHud`/`UnitSpawner`와 그 아래 스폰된 유닛들, 샌드박스
    모드라면 `SandboxHud`까지)를 `Destroy`하고 선택/전투 상태 필드를 초기화한 뒤 `SetupBattle()`을 처음부터
    다시 호출한다 — 샌드박스 모드에서는 이 재시작이 곧 배치 화면(커스텀 화면)으로 되돌아가는 것이고, 데모
    모드에서는 데모 편성을 다시 스폰하는 것과 같다. `Camera.main`(Awake에서 한 번만 확보)과
    `EventSystem`(`BattleHud.EnsureEventSystem`이 `transform` 밖에 만듦)은 자식 파괴 대상이 아니라 그대로
    재사용된다.
  - **검증**: `unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run` 재실행 — 컴파일
    에러 없음, 갱신된 `sample_units.csv`로도 `ALL PASS` 확인. 전투를 실제로 승/패까지 진행해 "다시 시작" 버튼을
    눌러보는 상호작용 흐름 자체는 자동화하지 않았다 — 에디터에서 Play로 직접 확인 필요.
