# gugugaga

Unity 6000.3.20f1 기반 턴제 전술 전투 프로토타입.

## 구조 (야매 ECS)

- `Assets/Scripts/TacticsECS/Core`, `Data`: 순수 데이터 (struct/필드만). 로직 없음. [`EntityWorld`](Assets/Scripts/TacticsECS/Data/EntityWorld.cs)가 범용 엔티티-컴포넌트 저장소, [`UnitComponents.cs`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)가 그 안에 저장되는 컴포넌트 타입들(아래 참고).
- `Assets/Scripts/TacticsECS/Systems`: 정적 클래스. `Data`를 읽고 써서 판정/이동/전투/AI를 처리. 자체 상태 없음.
- `Assets/Scripts/TacticsECS/View`: 화면 표시 전용 MonoBehaviour. 로직 없음.
- `Assets/Scripts/TacticsECS/BattleController.cs`: 입력을 받아 System을 호출하고 View에 반영하는 조율자. `Assets/Scenes/SampleScene.unity`에 배치되어 있음.
- `Assets/Prefabs/Units`: 유닛 타입별 프리팹(`Unit_Melee`/`Unit_Ranged`/`Unit_Guard` 등). 각 프리팹은 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs) + [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트를 갖는다.
- `Assets/Prefabs/UI`: `HpDisplay`(유닛 머리 위 체력 표시)/`DamagePopup`(피해 팝업)/`EventSystem`/`PaletteButton`/`BattleHud`/`SandboxHud`/`CityResourceBar`(도시 발전 자원 바) 프리팹. [`UIPrefabSetup.cs`](Assets/Editor/UIPrefabSetup.cs)(`-executeMethod`로만 실행)가 생성하며, 각 View 스크립트는 이 프리팹을 인스턴스화한 뒤 자식을 이름으로 찾아 참조만 캐싱한다(하이어라키를 코드로 만들지 않음). `BattleHud`의 좌상단 유닛 로스터/우상단 행동 로그처럼 행 수가 계속 바뀌는 목록은 이렇게 프리팹으로 굽지 않고 코드로 그때그때 행을 만든다.

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
  UI/CSV용 플레이스홀더 태그), `MoveRange`(이동, 장애물 통과/유닛 통과/대각선 이동은 더 이상 별도 컴포넌트가
  아니라 값 없는 패시브 — 아래 [이동 방식](#이동-방식) 참고), `HasMoved`/`HasActed`/`IsGuarding`(턴 상태)로
  정의되어 있다. 각각 값 하나만 담는 아주 작은 타입이고, 서로 아무 관계도 없다.

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
[`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs) 컴포넌트에 스탯과 모델 겉감 텍스처를
인스펙터 값으로 들고 있고, `BattleController`는 이 프리팹 3개를 인스펙터에서 참조해 스폰한다(표시 색은
`UnitDefinition`이 아니라 팀별로 고정된 값 — 아래 [겉모습](#겉모습-3d-모델) 참고).
`UnitSpawner.Spawn`/`UnitView`/`UnitDefinition` 어디에도 타입별 `switch`는 없으며, 전부 프리팹에 붙은 값을 그대로 읽어 쓴다.
새 타입을 추가하려면 코드를 고칠 필요 없이 프리팹을 하나 더 만들고 `UnitDefinition` 값만 채우면 된다. 사거리는 맨해튼 거리 기준.
스폰 시 [`UnitSpawner`](Assets/Scripts/TacticsECS/View/UnitSpawner.cs)가 `UnitDefinition`의 값을 위 컴포넌트들로 하나씩 옮겨 담는다.
`UnitDefinition`이 갖는 "행동의 집합"(`actions`)에 대해서는 바로 아래 [사용 가능 행동](#사용-가능-행동-assetsscriptstacticsecsactions) 참고.

### 이동 방식

이동은 "이동 범위 몇 칸"(`MoveAction.MoveRange`, `Move.Range` CSV 컬럼)만 그 행동 자신의 값으로 갖는다.
장애물 통과/유닛 통과/대각선 이동은 대부분의 유닛에는 해당 없는 드문 케이스라, `MoveAction` 자신의 값이
아니라 Charge/Retreat/Infiltrate와 같은 **값 없는 순수 마커 패시브**로 따로 뺐다(예전엔 CSV의
`Move.IgnoreTerrain`/`Move.IgnoreUnitBlocking`/`Move.AllowDiagonal` 고정 컬럼이었다):

| 패시브 | 의미 |
| --- | --- |
| [`IgnoreTerrainAction`](Assets/Scripts/TacticsECS/Actions/IgnoreTerrainAction.cs) | 있으면 `Walkable = false`인 지형(벽/장애물) 및 자신의 `MoveDomain`과 다른 지형을 무시하고 이동 (비행 유닛 등) |
| [`IgnoreUnitBlockingAction`](Assets/Scripts/TacticsECS/Actions/IgnoreUnitBlockingAction.cs) | 있으면 다른 유닛(아군/적군 모두)이 있는 타일도 지나가거나 멈출 수 있음 (유령/투명체 등) |
| [`AllowDiagonalAction`](Assets/Scripts/TacticsECS/Actions/AllowDiagonalAction.cs) | 있으면 대각선을 포함한 8방향 이동, 없으면 상하좌우 4방향만 |

기본 3종 유닛은 셋 다 갖고 있지 않은 평범한 지상 4방향 이동이며,
[`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)과
[`MoveAction.Execute`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)(`MovementSystem.TryMove`가 찾아 호출)가
`UnitActionQueries.Find<T>`로 각 패시브의 보유 여부만 그때그때 확인해 판정한다(Infiltrate와 완전히 같은 방식).

| 타입 | HP | 공격력 | 방어력 | 이동 범위 | 사거리 | 특징 |
| --- | --- | --- | --- | --- | --- | --- |
| **Melee** (근접, `Unit_Melee.prefab`) | 12 | 5 | 1 | 3칸 | 1칸 | 이동력이 좋고 공격력이 높지만 방어가 약함. 적에게 바로 붙어 때리는 딜러. 이동/공격 외에 **자폭**을 쓸 수 있다. |
| **Ranged** (원거리, `Unit_Ranged.prefab`) | 8 | 4 | 0 | 2칸 | 3칸 | HP/방어력이 가장 낮은 대신 멀리서 공격 가능. 근접에게 붙잡히면 위험. 이동/공격 외에 **치유**(회복량 4, 사거리 2)를 쓸 수 있다. |
| **Guard** (방어, `Unit_Guard.prefab`) | 18 | 3 | 3 | 2칸 | 1칸 | HP/방어력이 가장 높은 탱커. 이동/공격 외에 **방어 태세**를 쓸 수 있고, 이번 턴 방어력이 +2 추가되어 총 5가 된다 (`CombatSystem.TryDefend`). 추가로 **반격** 패시브를 갖고 있어, 자신의 사거리 안에서 공격받으면 자동으로 공격한 대상에게 피해를 되돌려준다. |

### 사용 가능 행동 (Assets/Scripts/TacticsECS/Actions)

어떤 유닛이 이동/공격/방어/치유/자폭/반격/돌격/대피/기습/잠입/무리/전향/연타/정찰/스플래시/뻣뻣함/빙결/
요새화/은신/약탈/고정/수송 중 무엇을 쓸 수 있는지, 그리고 그 행동이 실제로 무엇을 하는지는 **값이 아니라
행동 자신**이 정한다. 마지막 다섯(요새화/은신/약탈/고정/수송)은 아직 게임플레이 효과가 정해지지 않은
플레이스홀더다 — 자세한 내용은 [플레이스홀더 패시브](#플레이스홀더-패시브-아직-효과-미정) 참고. 스물두 행동
([`MoveAction`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)/[`AttackAction`](Assets/Scripts/TacticsECS/Actions/AttackAction.cs)/
[`DefendAction`](Assets/Scripts/TacticsECS/Actions/DefendAction.cs)/[`HealAction`](Assets/Scripts/TacticsECS/Actions/HealAction.cs)/
[`SelfDestructAction`](Assets/Scripts/TacticsECS/Actions/SelfDestructAction.cs)/[`CounterAction`](Assets/Scripts/TacticsECS/Actions/CounterAction.cs)/
[`ChargeAction`](Assets/Scripts/TacticsECS/Actions/ChargeAction.cs)/[`RetreatAction`](Assets/Scripts/TacticsECS/Actions/RetreatAction.cs)/
[`AmbushAction`](Assets/Scripts/TacticsECS/Actions/AmbushAction.cs)/[`InfiltrateAction`](Assets/Scripts/TacticsECS/Actions/InfiltrateAction.cs)/
[`HerdAction`](Assets/Scripts/TacticsECS/Actions/HerdAction.cs)/[`ConvertAction`](Assets/Scripts/TacticsECS/Actions/ConvertAction.cs)/
[`ComboAction`](Assets/Scripts/TacticsECS/Actions/ComboAction.cs)/[`ScoutAction`](Assets/Scripts/TacticsECS/Actions/ScoutAction.cs)/
[`SplashAction`](Assets/Scripts/TacticsECS/Actions/SplashAction.cs)/[`StiffAction`](Assets/Scripts/TacticsECS/Actions/StiffAction.cs)/
[`FreezeAction`](Assets/Scripts/TacticsECS/Actions/FreezeAction.cs)/[`FortifyAction`](Assets/Scripts/TacticsECS/Actions/FortifyAction.cs)/
[`StealthAction`](Assets/Scripts/TacticsECS/Actions/StealthAction.cs)/[`PillageAction`](Assets/Scripts/TacticsECS/Actions/PillageAction.cs)/
[`AnchoredAction`](Assets/Scripts/TacticsECS/Actions/AnchoredAction.cs)/[`TransportAction`](Assets/Scripts/TacticsECS/Actions/TransportAction.cs))
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

**기습**/**잠입**/**무리**/**전향**/**연타**/**정찰**도 값 없는 순수 마커 패시브로, 각각 다른 지점에 훅을 건다:
- **기습**([`AmbushAction`](Assets/Scripts/TacticsECS/Actions/AmbushAction.cs), `CombatSystem.TryAttack`이 공격자 쪽에서
  보유 여부만 확인): 공격이 성사되면, 대상이 반격(`CounterAction`)을 갖고 있어도 발동시키지 않는다.
- **잠입**([`InfiltrateAction`](Assets/Scripts/TacticsECS/Actions/InfiltrateAction.cs), `PathfindingSystem.IsBlockedByOccupant`가
  경로 탐색/실제 이동 양쪽에서 공유해 참조): 적 유닛에 의한 이동 방해만 무시한다. 모든 유닛(아군 포함)을
  무시하는 [`IgnoreUnitBlockingAction`](Assets/Scripts/TacticsECS/Actions/IgnoreUnitBlockingAction.cs)과
  달리 아군에 의한 차단은 그대로 적용된다.
- **무리**([`HerdAction`](Assets/Scripts/TacticsECS/Actions/HerdAction.cs),
  [`PassiveAuraSystem.RefreshHerdAura`](Assets/Scripts/TacticsECS/Systems/PassiveAuraSystem.cs)가 유닛 이동 직후와 매 턴
  시작 시 다시 계산): 주변 1블록 내 아군(자신 제외)에게 **가속**(`Accelerated` 컴포넌트) 상태를 부여한다. 가속은
  이동 거리 +1 효과가 있고(`MovementSystem.EffectiveMoveRange`로 계산, `PathfindingSystem.GetReachable`/유닛 패널
  이동력 표시가 이 값을 씀), 무리 유닛이 범위를 벗어나도 유지되며 오직 **피격 시**(공격/반격/자폭으로 데미지를
  받는 시점) 해제된다.
- **전향**([`ConvertAction`](Assets/Scripts/TacticsECS/Actions/ConvertAction.cs), `CombatSystem.TryAttack`이 공격 성사 +
  대상 생존을 확인한 뒤 반격 판정보다 먼저 처리): 공격한 적 유닛의 팀을 공격자 팀으로 바꿔 아군으로 만든다.
  이 처리가 반격보다 먼저 일어나므로, 전향된 대상은 더 이상 반격하지 않는다(`CounterAction.Execute`에 팀이
  같으면 실패하는 방어 로직도 이번에 함께 추가 — 기존에는 팀 체크 자체가 없었다).
- **연타**([`ComboAction`](Assets/Scripts/TacticsECS/Actions/ComboAction.cs), `AttackAction.Execute`가 처치 성공 시
  보유 여부만 확인): 공격으로 대상을 처치하면 방금 세운 `HasActed`를 되돌려, 같은 턴에 추가로 공격할 수 있다.
  연속으로 처치하는 한 계속 이어질 수 있다.
- **정찰**([`ScoutAction`](Assets/Scripts/TacticsECS/Actions/ScoutAction.cs)): 시야 +1을 의도한 패시브이지만, 아직
  시야/포그오브워 시스템 자체가 프로젝트에 없어 지금은 실제 게임플레이 효과가 없는 **플레이스홀더**다.
  `VisionRange`(`Core/UnitComponents.cs`, 기본값 0) 컴포넌트만 미리 준비해뒀고, 나중에 시야 시스템이 생기면
  이 패시브 보유 여부로 `VisionRange + 1`을 계산해 쓰면 된다.
- **스플래시**([`SplashAction`](Assets/Scripts/TacticsECS/Actions/SplashAction.cs), `AttackAction.Execute`가 직접
  공격 처리 직후 보유 여부만 확인): 공격이 성사되면 대상(생사 무관, 마지막 위치 기준) 주변 1블록(맨해튼
  거리) 내의 다른 적 유닛(공격자 기준, 대상 자신 제외)에게도 `CombatSystem.CalculateDamage`로 계산한 피해를
  추가로 입힌다.
- **뻣뻣함**([`StiffAction`](Assets/Scripts/TacticsECS/Actions/StiffAction.cs), `CombatSystem.TryAttack`이 반격
  판정 직전 대상(피격자) 쪽에서 보유 여부만 확인): 공격받았을 때 반격(`CounterAction`)을 갖고 있어도
  발동시키지 않는다. 기습이 공격자 쪽에서 반격을 막는 것과 대칭이되, 이쪽은 피격자 스스로의 특성이다.
- **빙결**([`FreezeAction`](Assets/Scripts/TacticsECS/Actions/FreezeAction.cs), `AttackAction.Execute`가 대상 생존
  시 보유 여부만 확인): 공격이 성사되고 대상이 살아남으면 대상에게 `Frozen`(`Core/UnitComponents.cs`) 상태를
  건다. `Frozen`은 그 유닛의 다음 자기 팀 턴이 시작될 때(`TurnSystem.ResetUnitStates`) 그 턴의
  `HasMoved`/`HasActed`를 오히려 강제로 true로 세워 통째로 건너뛰게 만들고, 그 즉시 해제된다 — 정확히
  "1턴간 행동불능".

#### 플레이스홀더 패시브 (아직 효과 미정)

[`SandboxUnits.csv`](SandboxUnits.csv)의 육지/물 유닛 13종 예시를 만들면서, 기존 [`ScoutAction`](Assets/Scripts/TacticsECS/Actions/ScoutAction.cs)(정찰)과 같은 성격 — CSV/UI 연동에 필요한 태그는 있지만 아직 실제 게임플레이 효과가 정해지지 않은 — 패시브 5종을 추가했다. `UnitCsvActionFactory`/`ActionType`/CSV 왕복은 다른 행동과 동일하게 전부 동작하지만, `BattleHud`의 패시브 배지(`PassiveDefs`)에는 아직 연결하지 않았다(아이콘이 없고, 효과가 없는 채로 배지만 노출하는 것은 혼란을 줄 수 있어 보류).

- **요새화**([`FortifyAction`](Assets/Scripts/TacticsECS/Actions/FortifyAction.cs)): 보병/방패병/궁병 등에 쓰임. 효과 미정(예상: 제자리 방어 보너스). 상세: [docs/passives/Fortify.md](docs/passives/Fortify.md).
- **은신**([`StealthAction`](Assets/Scripts/TacticsECS/Actions/StealthAction.cs)): 스파이에 쓰임. 효과 미정(예상: 적에게 발견되지 않음). 상세: [docs/passives/Stealth.md](docs/passives/Stealth.md).
- **약탈**([`PillageAction`](Assets/Scripts/TacticsECS/Actions/PillageAction.cs)): 스파이에 쓰임. 효과 미정(예상: 자원 획득). 상세: [docs/passives/Pillage.md](docs/passives/Pillage.md).
- **고정**([`AnchoredAction`](Assets/Scripts/TacticsECS/Actions/AnchoredAction.cs)): 사제/함선류에 쓰임. 효과 미정. 상세: [docs/passives/Anchored.md](docs/passives/Anchored.md).
- **수송**([`TransportAction`](Assets/Scripts/TacticsECS/Actions/TransportAction.cs)): 함선류에 쓰임. 효과 미정(예상: 육지 유닛을 태우고 물을 건너는 것) — [지형(육지/물)](#지형-육지물)의 이동 제한과는 무관하게 이미 별도로 동작한다. 상세: [docs/passives/Transport.md](docs/passives/Transport.md).

### 지형 (육지/물)

타일마다 [`TerrainType`](Assets/Scripts/TacticsECS/Core/TerrainType.cs)(`Land`/`Water`) 값을 갖고([`TileData.Terrain`](Assets/Scripts/TacticsECS/Core/TileData.cs)), 유닛도 같은 타입의 [`MoveDomain`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs) 컴포넌트로 자신이 들어갈 수 있는 지형을 하나 갖는다(CSV의 `Domain` 컬럼, `UnitDefinition.domain`). [`IgnoreTerrainAction`](Assets/Scripts/TacticsECS/Actions/IgnoreTerrainAction.cs) 패시브가 없는 유닛은 자신의 `MoveDomain`과 다른 지형 타일에 들어갈 수 없다 — 육지 유닛은 물을, 물 유닛(뗏목/정찰선/충각선/범선)은 육지를 건널 수 없다. 판정은 [`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)(경로 탐색)과 [`MoveAction.Execute`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)(실제 이동)가 공유한다. 자세한 내용과 CSV 예시는 [`docs/UnitCsvSandbox.md`의 지형 섹션](docs/UnitCsvSandbox.md#지형육지물) 참고.

타일의 지형은 `BattleController` 인스펙터의 `waterTiles` 좌표 목록으로 지정한다(비어있으면 그리드 전체가
육지 — `Assets/Scenes/SampleScene.unity`는 비워둬서 기존 데모 전투에 영향이 없다). `Assets/Scenes/Sandbox.unity`는
그리드 오른쪽 1/3을 물로 채워둬서 물 유닛을 곧바로 테스트해볼 수 있다. 타일 모델은
[Kenney - Tower Defense Kit](https://kenney.nl/assets/tower-defense-kit)(CC0)의 평평한 타일 메시
(`Assets/Art/Tiles/Kenney/tile.fbx`, 라이선스 원문 [`Assets/Art/Tiles/Kenney/Kenney-License.txt`](Assets/Art/Tiles/Kenney/Kenney-License.txt))를
가져와 [`TileAssetSetup`](Assets/Editor/TileAssetSetup.cs)(CLI 전용)이 `Tile_Land`/`Tile_Water` 프리팹을 만들고,
[`GridView`](Assets/Scripts/TacticsECS/View/GridView.cs)가 타일마다 이 프리팹을 인스턴스화한 뒤 기존과 같은 방식으로
매번 새 런타임 머티리얼을 입힌다(하이라이트 시 타일마다 독립적으로 색이 바뀌어야 해서, 프리팹이 가진 머티리얼을
공유하지 않는다) — 기본색은 육지=회색 계열, 물=파란색, 이동/공격 하이라이트는 기존과 동일. 두 프리팹을 아직
연결하지 않은 씬(또는 `landTilePrefab`/`waterTilePrefab`을 비워둔 경우)은 예전처럼 프리미티브 큐브로
대체된다.

### 겉모습 (3D 모델)

유닛 모델은 [KayKit - Adventurers Character Pack](https://kaylousberg.itch.io/kaykit-adventurers)(Kay Lousberg 제작, CC0 — 저작자 표시 의무 없음)의 로우폴리 캐릭터를 `Assets/Art/KayKit/Characters`에 받아 사용한다. 라이선스 원문은 [`Assets/Art/KayKit/LICENSE.txt`](Assets/Art/KayKit/LICENSE.txt). 세 유닛에 실루엣이 뚜렷이 구분되도록 매칭했다: **Melee**=Barbarian(양손 도끼), **Ranged**=Rogue(석궁), **Guard**=Knight(한손검 + 사각 방패). 캐릭터 FBX 하나에 무기/방패 변형이 전부 함께 들어있어(예: Knight는 방패 4종 + 검 2종을 전부 포함), [`UnitPrefabSetup`](Assets/Editor/UnitPrefabSetup.cs)이 타입에 맞는 것만 자식 이름으로 찾아 켜고 나머지는 `SetActive(false)`로 꺼서 정리한다. 모델의 기본 정면은 카메라 반대쪽(뒤)을 보고 있어서, 고정 isometric 카메라에 얼굴/무기가 보이도록 프리팹에서 180도 돌려 붙였다. 가만히 서 있을 때뿐 아니라 이동할 때는 이동 방향으로, 공격할 때는 대상 쪽으로 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)가 자동으로 회전시킨다(`FaceTowards`/`MoveRoutine`).

샌드박스 CSV 전용으로 겉모습 4종을 추가했다 — 같은 Adventurers 팩의 `RogueHooded`/`Mage`, 그리고 자매 팩
[KayKit - Character Pack: Skeletons](https://kaylousberg.itch.io/kaykit-skeletons)(역시 Kay Lousberg 제작, CC0)의
`SkeletonWarrior`/`SkeletonMage`(`Assets/Art/KayKitSkeletons/Characters`, 라이선스
[`Assets/Art/KayKitSkeletons/LICENSE.txt`](Assets/Art/KayKitSkeletons/LICENSE.txt)). 데모 편성(`SpawnDemoFormation`)의
Melee/Ranged/Guard 3종에는 영향 없고, [`ExtraCharacterPrefabSetup`](Assets/Editor/ExtraCharacterPrefabSetup.cs)이 만든
`Unit_RogueHooded`/`Unit_Mage`/`Unit_SkeletonWarrior`/`Unit_SkeletonMage` 프리팹을 CSV의 `BaseVisual` 값으로만
골라 쓴다(자세한 배정은 [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 참고).

유닛 색은 CSV/`UnitDefinition`에 데이터로 두지 않고, [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)의 `PlayerColor`/`EnemyColor` 두 상수로 팀에만 묶어 고정한다 — 유닛 타입과 무관하게 같은 팀이면 전부 같은 색, 방어 태세 중인 유닛은 팀에 관계없이 노란색으로 표시된다. 이 색은 스폰 시 만드는 유닛별 런타임 머티리얼에서 모델의 겉감 텍스처(`UnitDefinition.bodyTexture`) 위에 곱해 틴트하는 방식이라, 모델의 음영/디테일은 남기면서 색을 입힌다(캐릭터의 몸통/팔다리/망토/무기 등 여러 Renderer가 이 머티리얼 하나를 공유). 유닛 머리 위 텍스트는 `현재HP/최대HP`.

데미지 공식은 `max(1, 공격자 공격력 - (대상 방어력 + 방어 태세 보너스))`.

## 조작법

- **유닛 선택**: 자기 팀(플레이어) 유닛을 좌클릭. 이미 이동+행동을 모두 마친 유닛이나 적/빈 타일을 클릭하면 선택이 풀린다.
- **이동**: 유닛 선택 시 파란색으로 하이라이트된 타일이 이동 가능 범위. 그 타일을 클릭하면 이동한다 (턴당 1회). 기본적으로 이번 턴 이미 공격한 유닛은 이동할 수 없다 — 대피(Retreat) 패시브가 있으면 예외.
- **공격**: 하이라이트된 빨간 타일 위의 적 유닛을 클릭하면 공격한다 (턴당 1회). 기본적으로 이번 턴 이미 이동한 유닛은 공격할 수 없다 — 돌격(Charge) 패시브가 있으면 예외.
- **행동 버튼**(화면 우하단): 선택한 유닛의 `AvailableActions`에 있는 행동만, 아직 행동하지 않았을 때만 나타난다 — 방어 태세(Guard), 치유(Ranged), 자폭(Melee)은 모두 대상 선택 없이 버튼 클릭 한 번으로 즉시 적용된다.
- **행동 설명 툴팁**: 행동 버튼(방어/치유/자폭/선택 해제/턴 종료) 위에 마우스를 올리면, 버튼 줄 바로 위 한 구역에 그 행동에 대한 설명이 뜬다.
- **유닛 로스터**(화면 좌상단, 턴 배지 바로 아래): 살아있는 모든 유닛(양 팀)을 체력 아이콘 + 현재 HP 숫자 한 줄씩으로 보여준다. 수가 많으면(스트레스 테스트) 스크롤된다.
- **행동 로그**(화면 우상단): 이동/공격/반격/방어/치유/자폭/대기/쓰러짐 등 모든 유닛의 행동을 팀 색으로 구분해 한 줄 요약으로 보여준다. 최근 9줄만 유지되며 오래된 줄은 자동으로 사라진다.
- **피해 팝업**: 유닛이 공격/반격/자폭으로 피해를 입으면 그 위에 "-숫자"가 잠깐 떠올랐다 사라진다.
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

## 도시 발전 자원 (플레이스홀더)

도시 발전도(⚙️)/인구(👤)/골드(🪙)/신앙(⚡) 네 가지 자원의 최소 구현. 타일/영토 시스템이 아직 없어
"채집형/설치형 자원", "영토 밖 특수 자원 채집", "수도 연결 보너스(도로/해로)"처럼 타일 소유권에 의존하는
규칙은 구현하지 않고 비워뒀다(`WaitAction.IsInOwnTerritory`와 같은 방식의 플레이스홀더).

- [`CityResourceData`](Assets/Scripts/TacticsECS/Core/CityResourceData.cs): 순수 데이터(발전도/인구
  상한/골드/골드 생산량/신앙/신앙 최대치/수도 여부). `GoldProduction`은 원래 도시 타일/건물 생산량의
  합이어야 하지만, 그 데이터가 없어 지금은 도시 하나의 고정값(인스펙터 설정)으로 대신한다.
- [`CityResourceSystem`](Assets/Scripts/TacticsECS/Systems/CityResourceSystem.cs): 매 턴(플레이어 턴 시작)
  골드/신앙 자동 생산(`ApplyTurnStart` — 골드는 생산량+수도 보너스, 신앙은 보유 유닛 수만큼 증가하되
  최대치 초과 불가), 인구 계산(`CountPopulation` — 지금은 팀 소속 유닛을 전부 세며, "중립/특수 유닛은
  인구 미소모" 구분은 유닛 데이터에 아직 없어 반영하지 않음), 수도 연결 여부 플레이스홀더
  (`IsConnectedToCapital`, 항상 `false`)를 제공하는 무상태 시스템.
- [`CityResourceHud`](Assets/Scripts/TacticsECS/View/CityResourceHud.cs) + `Assets/Prefabs/UI/CityResourceBar.prefab`:
  네 자원을 아이콘+숫자로 보여주는 재사용 가능한 독립 프리팹(BattleHud와 무관하게 어느 화면에도 배치
  가능). 발전도/인구/골드/신앙 전용 아이콘 아트는 아직 없어, `IconLibrary`에 이미 있는 아이콘 중 의미가
  가장 비슷한 것(발전도=`combo`, 인구=`herd`, 골드=`victory`, 신앙=`splash`)을 자원별 색으로 틴트해
  대신 쓴다 — 전용 아이콘이 추가되면 `CityResourceHud.SlotDefs` 표만 바꾸면 된다.
- `BattleController`의 `cityResourceHudPrefab` 필드로 연결하며, 지금은 `Assets/Scenes/Sandbox.unity`
  (커스텀 배치 화면)에만 배정되어 있다 — `SampleScene`에는 비워둬 생성 자체를 건너뛴다. 시작값(인구
  상한/골드 생산량/신앙 최대치/수도 여부)은 `BattleController` 인스펙터에서 조정 가능.
- 발전도(⚙️)는 원래 늘 0이었다(생산 로직이 없었음) — 기술트리가 실제로 쓸 자원이 필요해져
  `DevelopmentProduction`(고정값 플레이스홀더, `GoldProduction`과 같은 이유) 필드를 추가하고
  `ApplyTurnStart`가 매 플레이어 턴 시작마다 발전도를 그만큼 늘리도록 했다 — 아래 [기술트리](#기술트리-스킬트리-플레이스홀더) 참고.

## 기술트리 (스킬트리, 플레이스홀더)

폴리토피아 기반 5갈래(등산/채집/기마/사냥/낚시) x 1+2+2티어 = 25개 노드 기술트리. 도시 발전도(위
[도시 발전 자원](#도시-발전-자원-플레이스홀더))를 소모해 해금한다. 각 노드가 실제로 여는 효과(건물
건설, 유닛 훈련, 지형 방어 보너스 등)는 그 대상 시스템(건물/유닛 훈련/지형) 자체가 프로젝트에 아직
없어 전부 구현하지 않았다 — "이 기술이 해금됐는가"라는 사실만 관리하고, 해당 시스템이 생기면
`TechSystem.IsUnlocked`를 참조해 실제 효과를 적용하면 된다(`CityResourceSystem.IsConnectedToCapital`과
같은 방식의 플레이스홀더).

- [`TechId`](Assets/Scripts/TacticsECS/Core/TechId.cs): 25개 노드 식별자 enum(순수 태그).
- [`TechNodeData`](Assets/Scripts/TacticsECS/Core/TechNodeData.cs): 노드 하나의 고정 정의(이름/티어/
  선행 기술/비용/효과 요약 텍스트) — 순수 데이터.
- [`TechTreeData`](Assets/Scripts/TacticsECS/Core/TechTreeData.cs): 도시 하나가 해금한 기술 집합
  (`HashSet<TechId>`) — 순수 데이터. 지금은 도시가 하나뿐이라 `CityResourceData`처럼 `BattleController`가
  필드 하나로 직접 들고 있는다.
- [`TechTreeDefinition`](Assets/Scripts/TacticsECS/Data/TechTreeDefinition.cs): 25개 노드 전체를 채운
  고정 테이블(기획 문서 그대로 옮김). 비용은 티어별 고정값(1티어 3 / 2티어 5 / 3티어 8)으로, 정식
  밸런싱 전의 임시값이다.
- [`TechSystem`](Assets/Scripts/TacticsECS/Systems/TechSystem.cs): 해금 가능 여부 판정(`IsAvailable` —
  선행 기술 해금 여부, `CanUnlock` — 그리고 발전도 충분 여부)과 실제 해금(`Unlock` — 발전도 소모 +
  해금 집합에 추가)을 담당하는 무상태 시스템.
- [`TechTreeHud`](Assets/Scripts/TacticsECS/View/TechTreeHud.cs) + `Assets/Prefabs/UI/TechTreePanel.prefab`:
  `CityResourceHud`와 같은 패턴의 재사용 가능한 독립 프리팹. 토글 버튼으로 열고 닫는 패널 안에 **중앙
  허브(시작 노드)에서 5갈래가 방사형(각 72도)으로 퍼져나가는 그래프 레이아웃** — 1티어는 허브에 바로
  연결되고, 2티어는 좌우로 갈라지며(TechNodeData.Slot), 3티어는 부모와 같은 각도로 더 바깥쪽에 이어진다.
  각 기술은 **아이콘이 있는 원형 노드**(해금 상태에 따라 회색/파랑/초록으로 칠해짐)이고 바로 아래에
  이름 라벨이 붙는다. 노드 위치/반지름/연결선 전부 `TechTreeDefinition`의 Branch/Tier/Slot/ParentId만
  보고 `UIPrefabSetup.GenerateTechTreePanel`이 계산해서 굽는다(기술 추가/삭제 시 이 코드는 그대로 둬도
  됨). 원형 배경은 `RuntimeSprite.CreateCircle()`(BattleHud 패시브 배지와 공유하는 헬퍼로 분리),
  아이콘 스프라이트는 `IconLibrary.Get(TechNodeData.Icon)` — 둘 다 프리팹에 구울 수 없어(디스크 에셋이
  아님) `TechTreeHud.Init()`이 인스턴스화 직후 채운다. 노드를 클릭하면 하단 상세 패널에 이름/효과/비용/
  해금 가능 여부(해금 완료·선행 기술 필요·발전도 부족·해금 가능)가 표시되고, 해금 버튼을 누르면
  `OnUnlockRequested` 이벤트가 발생한다(`BattleHud.OnDefendClicked`와 같은 패턴 — 실제 해금 판정/소모는
  `BattleController.HandleTechUnlockRequested`가 `TechSystem.Unlock`으로 수행하고, 결과를 다시
  `SetState`로 반영).
- **아이콘**: 25개 노드 + 중앙 허브 전용 아이콘 26종을 전부 자체 제작했다(`Assets/Art/GameIcons/
  Resources/Icons`, `counter.png` 등 기존 패시브 아이콘과 같은 방식 — PowerShell + System.Drawing(GDI+)
  으로 512x512 흰색 실루엣/투명 배경 PNG를 직접 그려서 저장, Unity 에디터 미사용). game-icons.net에서
  받아오는 대신 이 방식을 택한 이유는 기존 12종 패시브 아이콘이 이미 이 프로젝트에서 검증된 방식이라
  라이선스/일관성 문제 없이 바로 재사용할 수 있었기 때문. 출처는 `Assets/Art/GameIcons/LICENSE.txt`
  참고.
- `BattleController`의 `techTreeHudPrefab` 필드로 연결하며, `cityResourceHudPrefab`이 켜진 경우에만
  같이 초기화된다(도시 발전도가 있어야 의미가 있으므로). `CityResourceBar`와 마찬가지로 지금은
  `Assets/Scenes/Sandbox.unity`에만 배정되어 있다.

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
- 2026-09-12: 기습/잠입/무리/전향/연타/정찰 패시브 6종 추가.
  - **기습**([`AmbushAction`](Assets/Scripts/TacticsECS/Actions/AmbushAction.cs)): `CombatSystem.TryAttack`이 공격
    성사 후 대상의 반격(`CounterAction`)을 발동시키기 전에 공격자 보유 여부를 확인해 건너뛴다.
  - **잠입**([`InfiltrateAction`](Assets/Scripts/TacticsECS/Actions/InfiltrateAction.cs)): 이동 차단 판정을
    `PathfindingSystem.IsBlockedByOccupant`로 뽑아내 `GetReachable`(경로 탐색)과 `MoveAction.Execute`(실제 이동)가
    공유하도록 리팩터링 — 기존에는 두 곳에 같은 판정이 중복돼 있었다. 이 헬퍼가 잠입 보유 시 "적 팀 점유자에
    의한 차단만" 무시하도록(아군 차단은 그대로 유지) 판정한다.
  - **무리 + 가속**([`HerdAction`](Assets/Scripts/TacticsECS/Actions/HerdAction.cs)): 턴 리셋과 별개로 유지되는
    지속 상태가 필요해 새 컴포넌트 `Accelerated`(`Core/UnitComponents.cs`)를 추가했다. 새 시스템
    [`PassiveAuraSystem.RefreshHerdAura`](Assets/Scripts/TacticsECS/Systems/PassiveAuraSystem.cs)가 무리 유닛 주변
    1블록 내 아군에게 가속을 부여하며, `MovementSystem.TryMove` 성공 직후와 `TurnSystem.StartTurn`에서 호출해
    배치 변화를 반영한다. 해제는 피격 시점(`AttackAction`/`CounterAction`/`SelfDestructAction`의 데미지 적용 지점)
    에서 처리. 이동 거리 +1 보너스는 `MoveRange`를 직접 고치지 않고 새 헬퍼 `MovementSystem.EffectiveMoveRange`로
    계산해 `PathfindingSystem.GetReachable`과 유닛 패널 이동력 표시(`BattleHud.ShowUnitPanel`, 기존에는 raw
    `MoveRange`를 그대로 표시하고 있었다)에 반영했다.
  - **전향**([`ConvertAction`](Assets/Scripts/TacticsECS/Actions/ConvertAction.cs)): `CombatSystem.TryAttack`이 공격
    성사 + 대상 생존을 확인한 뒤, 반격 판정보다 먼저 대상의 `Team`을 공격자 팀으로 바꾼다. 이 순서 때문에
    `CounterAction.Execute`가 팀이 같으면 실패하도록 방어 로직을 추가해야 했다(기존에는 반격에 팀 체크 자체가
    없어서 잠재 버그였다 — 계획 검토 시 확인해 함께 수정).
  - **연타**([`ComboAction`](Assets/Scripts/TacticsECS/Actions/ComboAction.cs)): `AttackAction.Execute`가 처치를
    확인하면 방금 세운 `HasActed`를 다시 `false`로 되돌려 같은 턴 추가 공격을 허용한다.
  - **정찰**([`ScoutAction`](Assets/Scripts/TacticsECS/Actions/ScoutAction.cs)): 시야/포그오브워 시스템 자체가
    프로젝트에 없어 실제 효과 없는 플레이스홀더로만 추가 — 새 컴포넌트 `VisionRange`(기본값 0)만 준비.
  - **공통 배선**: `ActionType`에 6개 플래그 추가, `UnitCsvActionFactory.BuildActions`/`BattleHud.PassiveDefs`에
    각각 한 줄씩 반영, `docs/sample_units.csv`에 6개 패시브를 하나씩 쓰는 예시 유닛(Assassin/Shaman/Cultist/
    Berserker/Ranger) 추가. 아이콘 6종(`ambush`/`infiltrate`/`herd`/`convert`/`combo`/`scout`)은 기존
    `counter`/`charge`/`retreat`와 같은 방식(흰색 실루엣, 자체 제작)으로 새로 그려 `Assets/Art/GameIcons/Resources/Icons`에
    추가하고 `LICENSE.txt`에 출처를 기록했다.
  - **검증**: Unity 에디터가 닫혀 있음을 사용자에게 확인받은 뒤 CLI로 검증. `unity run . -- -nographics`
    헤드리스 컴파일 통과. `docs/sample_units.csv`가 6행에서 10행으로 늘며 `UnitCsvVerification`의
    `VerifySpawnFromCsv`가 8칸 그리드 폭을 넘는 8/9번째 행에서 `GridWorld.InBounds` 밖으로 나가 occupant
    조회가 항상 실패하던 기존 버그를 노출시켜, [`UnitCsvVerification.cs`](Assets/Editor/UnitCsvVerification.cs)의
    배치 좌표를 `new Vector2Int(i, 0)`에서 `new Vector2Int(i % grid.Width, i / grid.Width)`로 수정(행 수가
    늘어도 안전) — 재실행 후 `ALL PASS (10 rows)` 확인. 6개 패시브 전용으로는 임시 배치모드 스크립트
    `Assets/Editor/PassiveSkillsVerification.cs`(`-executeMethod`)를 작성해 기습(반격 무효화)/잠입(적 차단만
    무시, 아군 차단 유지)/무리+가속(인접 아군 이동 거리 +1, 피격 시 해제)/전향(팀 전환 + 전환 후 반격 없음)/
    연타(처치 후 추가 공격)/정찰(플레이스홀더 컴포넌트 존재) 총 19개 assertion을 확인 — `ALL PASS (19)` 확인
    후 스크립트 삭제.
- 2026-09-12: 스플래시/뻣뻣함/빙결 패시브 3종 추가.
  - **스플래시**([`SplashAction`](Assets/Scripts/TacticsECS/Actions/SplashAction.cs)): `AttackAction.Execute`가
    직접 피해 적용 후 보유 여부를 확인해 새 private 메서드 `ApplySplashDamage`로 대상 주변 1블록 내 다른
    적(공격자 기준)에게도 `CombatSystem.CalculateDamage`로 피해를 입힌다. 대상이 그 직접 공격으로 죽었어도
    (occupant는 제거됐지만 `GridPosition` 값은 남아있어) 그 위치를 중심으로 그대로 적용된다.
  - **뻣뻣함**([`StiffAction`](Assets/Scripts/TacticsECS/Actions/StiffAction.cs)): `CombatSystem.TryAttack`의
    반격 발동 조건에 공격자의 기습(`AmbushAction`) 체크와 나란히 대상(피격자)의 `StiffAction` 보유 여부
    체크를 추가 — 대상이 반격을 갖고 있어도 발동하지 않는다.
  - **빙결**([`FreezeAction`](Assets/Scripts/TacticsECS/Actions/FreezeAction.cs)): 턴 리셋과 무관하게 걸렸다가
    다음 자기 턴 시작 시 소모되는 지속 상태가 필요해 새 컴포넌트 `Frozen`(`Core/UnitComponents.cs`) 추가.
    `AttackAction.Execute`가 공격 성사 + 대상 생존 시 공격자의 보유 여부를 확인해 대상에게 건다.
    [`TurnSystem.ResetUnitStates`](Assets/Scripts/TacticsECS/Systems/TurnSystem.cs)가 매 턴 시작마다
    `Frozen`인 유닛만 그 턴의 `HasMoved`/`HasActed`를 (false 대신) true로 세워 행동불능으로 만들고 그
    즉시 `Frozen`을 해제 — 정확히 한 턴만 건너뛴다.
  - **공통 배선**: `ActionType`에 3개 플래그 추가, `UnitCsvActionFactory.BuildActions`/`BattleHud.PassiveDefs`/
    `UnitSpawner.FinishSpawn`(`Frozen` 초기값)에 각각 반영. `docs/sample_units.csv`에 3개 패시브를 하나씩
    쓰는 예시 유닛(Catapult/Golem/IceMage) 추가. 아이콘 3종(`splash`=폭발 스파이크, `stiff`=금지 표시,
    `freeze`=눈 결정)도 기존과 같은 방식(흰색 실루엣, 자체 제작)으로 그려 추가하고 `LICENSE.txt` 갱신.
  - **검증**: `unity run . -- -nographics` 헤드리스 컴파일 통과. `UnitCsvVerification` 재실행 —
    `docs/sample_units.csv`가 14행으로 늘어난 채로도 `ALL PASS`. 3개 패시브 전용 임시 배치모드 스크립트
    `Assets/Editor/PassiveSkillsVerification2.cs`(`-executeMethod`)로 스플래시(인접 적만 광역 피해, 아군은
    무시)/뻣뻣함(반격 미발동)/빙결(얼어붙은 턴엔 강제 행동불능, 그다음 턴엔 정상 복귀) 총 13개 assertion
    전부 통과 확인 후 스크립트 삭제.
- 2026-09-12: 이번에 추가한 9개 패시브(기습/잠입/무리+가속/전향/연타/스플래시/뻣뻣함/빙결, 정찰 제외)를
  더 깊게 재검증 — 단발성 함수 호출 위주였던 기존 검증에서, 실제 System 진입점(`PathfindingSystem.
  GetReachable`/`TurnSystem.StartTurn`/`EnemyAI.RunTurn`)을 그대로 거치는 종단 시나리오와 패시브 조합
  상호작용까지 확장. 코드 변경은 없음 — 전부 검증 전용, 실패한 assertion은 없었다(버그 미발견).
  - **패시브별 추가 케이스**: 기습×뻣뻣함 2x2 매트릭스(어느 한쪽만 있어도 반격 억제) / 잠입은
    `IsBlockedByOccupant` 단위 호출이 아니라 `PathfindingSystem.GetReachable` 전체를 돌려 "적 유닛을
    통과하면 도달 가능하지만 우회하면 더 멀어서 원래 불가능한" 칸으로 검증(아군 차단은 여전히 유지) /
    무리+가속은 `MovementSystem.TryMove` 실제 이동으로 오라가 자동 갱신되는지, `GetReachable`의 실제
    도달 칸 수가 늘어나는지, 죽은 무리 유닛은 새로 오라를 주지 않는지 / 전향은 새 팀 턴에
    `TurnSystem.StartTurn`으로 정상 리셋되는지·즉사시킨 대상은 전향 안 되는지 / 연타는 HasMoved까지
    풀리진 않는지·처치 실패 시 재공격 불가(회귀)·스플래시로 죽인 대상은 카운트 안 되는지 / 스플래시는
    거리 2 무피해·대상별 방어력이 각자 반영된 서로 다른 피해량인지 / 빙결은 `HasMoved`/`HasActed` 값이
    아니라 `MoveAction`/`AttackAction.CanExecute` 자체로 확인·즉사 대상엔 안 걸리는지.
  - **조합 상호작용**: 빙결 걸린 턴엔 대피(HasActed 우회)가 있어도 `HasMoved`까지 강제로 막혀 탈출 구멍이
    없는지 / 가속을 걸어주던 바로 그 피격(빙결)이 "피격 시 해제" 규칙에 따라 가속도 함께 풀리는지(코드
    상 실제로 그렇게 동작함을 확인 — 계획 단계에서 예상한 것과 다른, 더 정확한 동작) / 전향+스플래시를
    함께 가진 공격이 각 대상에게 독립적으로 정상 적용되는지 / 잠입+가속을 함께 가진 유닛이 적을 통과하고
    늘어난 사거리까지 합쳐서 적용되는지(둘 중 하나만으론 못 가는 칸이 둘 다 있어야 도달 가능함을 확인) /
    `EnemyAI.RunTurn` 경유로도 기습(반격 무효)/스플래시(인접 아군 피해)/빙결(대상 동결)이 예외 없이
    똑같이 동작하는지(지금까지 전부 `CombatSystem`/`MovementSystem` 직접 호출로만 검증했었음).
  - **실행**: 임시 배치모드 스크립트 `Assets/Editor/PassiveSkillsVerification3.cs`(`-executeMethod`)로
    총 45개 assertion 작성 — 첫 실행에서 1건 실패("무리 유닛이 이동한 자리 자체가 대상 유닛의 직선
    경로를 막아버림", 실제 게임 로직 버그가 아니라 테스트 시나리오의 좌표 설계 실수)를 찾아 위치를
    수정한 뒤 재실행해 `ALL PASS (45)` 확인. `UnitCsvVerification`도 재실행해 회귀 없음 확인 후 스크립트
    삭제. 코드 변경 없이 검증만 강화된 커밋.

- 2026-09-12: 샌드박스 팔레트에 스크롤 레이아웃 적용, CSV 예시 유닛의 겉모습 중복을 줄이기 위해 캐릭터
  모델 4종 추가.
  - **동기**: "샌드박스 씬 왼쪽 개체 정보가 크기를 넘어설 때를 위해 스크롤 레이아웃을 적용해달라"와 "예시
    CSV에 있는 개체들과 중복되는 캐릭터 3D 모델링이 너무 많으니 로우폴리 무료 모델을 찾아 적용해달라"는
    요청. 후자는 다운로드가 필요해 사용자에게 후보(파일/출처/용량)를 먼저 확인받았다.
  - **스크롤 레이아웃**: [`SandboxHud`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)의 팔레트 패널을
    표준 UGUI `ScrollRect`(Viewport+`RectMask2D` / Content / 얇은 세로 `Scrollbar`) 구조로 교체. 팔레트
    행 수에 맞춰 Content 높이가 늘어나고, 패널 높이(300px)를 넘으면 스크롤되며, `SetPalette` 호출마다
    스크롤 위치를 맨 위로 리셋한다. 기존엔 목록이 넘치면 아래쪽이 그냥 잘려 안 보였음(문서에 v1 비목표로
    남아있던 부분).
  - **모델 중복 문제**: `Assets/Art/KayKit/Characters`에는 Barbarian/Knight/Rogue 3종뿐이라
    [`docs/sample_units.csv`](docs/sample_units.csv)의 13개 유닛이 전부 이 3개(Melee 계열 5유닛/Ranged
    계열 6유닛/Guard 계열 2유닛)로만 표시되고 있었다.
  - **에셋 선정**: 새 팩을 새로 검토하는 대신, 이미 라이선스를 확인해둔 KayKit Adventurers 팩(CC0) 안에서
    아직 안 쓴 `RogueHooded.fbx`/`Mage.fbx`(+`mage_texture.png`, 텍스처는 GitHub 미러
    `KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0`에서 총 약 37MB)를 우선 추가하고, 사용자가
    "추가 팩까지 검토"를 선택해 같은 작가(Kay Lousberg)의 CC0 자매 팩
    [KayKit Skeletons](https://kaylousberg.itch.io/kaykit-skeletons)에서 `Skeleton_Warrior.fbx`/
    `Skeleton_Mage.fbx`(+공유 `skeleton_texture.png`, 미러 `KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0`에서
    약 43MB)도 추가— 총 새 다운로드 약 80MB, 6파일. `Assets/Art/KayKitSkeletons/`에 별도 폴더 및
    `LICENSE.txt` 신설. 다운로드 전 파일명/출처/용량을 사용자에게 확인받았고, GitHub 트리 API로 받은
    파일 크기가 실제 다운로드 크기와 정확히 일치함을 확인.
  - **프리팹 생성**: 기존 [`UnitPrefabSetup`](Assets/Editor/UnitPrefabSetup.cs)은 `UnitDefinition`이
    `canGuard`/`attack`/`moveRange` 등을 직접 필드로 가지던 예전 버전 기준으로 작성돼 있어 지금 다시
    실행하면(그 필드들이 지금은 `actions` 리스트 안으로 옮겨가 존재하지 않아 리플렉션이 예외를 던짐)
    즉시 중단되고, 무엇보다 Guard 프리팹은 이후 사용자가 `MaxHp`를 9로 직접 조정해둔 값이 있어 재실행 시
    되돌아가 버린다 — 기존 3개(Melee/Ranged/Guard)는 건드리지 않고, 새 4개만 만드는 별도 도구
    [`ExtraCharacterPrefabSetup`](Assets/Editor/ExtraCharacterPrefabSetup.cs)을 신설(`MoveAction.FromCsv`/
    `AttackAction.FromCsv`로 `actions` 리스트를 직접 구성해 현재 `UnitDefinition` API와 맞춤). 손 소켓 아래
    무기/방패 변형을 정리하는 `hideNames`는 새로 받은 4개 FBX의 자식 Transform 이름을 1회성 조사
    스크립트로 덤프해 확보(RogueHooded는 기존 Rogue와 동일 리그라 같은 값 재사용, Mage는 지팡이+스펠북만
    남기고 완드/펼친책 숨김, Skeleton 두 종은 손 소켓이 아예 비어있어 맨손 그대로). `Assets/Scenes/
    Sandbox.unity`는 `SandboxSceneSetup`이 `SampleScene`을 복제해 만든 별도 파일이라 새 필드가 자동으로
    따라오지 않아, `ExtraCharacterPrefabSetup`이 두 씬 모두에 새 프리팹 4개를 연결하도록 처리.
  - **BaseVisual 재배정**: [`BattleController`](Assets/Scripts/TacticsECS/BattleController.cs)에 새 프리팹
    필드 4개와 샌드박스 팔레트용 `basePrefabsByName` 항목을 추가. `docs/sample_units.csv`의 BaseVisual을
    유닛 컨셉에 맞게 재배정(Duelist/Assassin→RogueHooded, Cleric/IceMage→Mage, Shaman/Cultist→
    SkeletonMage, Golem→SkeletonWarrior, 나머지는 기존 3종 유지) — 결과적으로 13개 유닛이 7종 모델에
    2~3개씩만 겹치도록 분산됨. [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md)의 `BaseVisual` 컬럼
    설명과 [`UnitPlacementController`](Assets/Scripts/TacticsECS/Sandbox/UnitPlacementController.cs)의
    안내 경고 문구도 새 7종 목록으로 갱신.
  - **검증**: Unity CLI 헤드리스 컴파일(`unity run . -- -nographics`) 통과. 새 프리팹 4개는 1회성 스크린샷
    스크립트로(`-nographics` 없이 실행해야 실제 렌더링됨 — `-nographics`는 널 그래픽스 디바이스라 빈
    회색 이미지만 나옴) 게임과 같은 isometric 각도로 렌더링해 텍스처/포즈/무기 정리가 올바른지 직접
    확인(스크립트는 확인 후 삭제). `unity run . -- -executeMethod TacticsECS.EditorTools.
    UnitCsvVerification.Run` 재실행 — 새 BaseVisual 매핑을 포함해 `round-trip PASS (13 rows)`/
    `spawn PASS (13 rows)`/`ALL PASS` 확인.
- 2026-09-13: 저장소 루트의 `SandboxUnits.csv`(불러오기 없이 바로 쓸 수 있는 샌드박스용 사본)를 현재까지
  구현된 17개 행동(Move/Attack/Defend/Heal/SelfDestruct/Counter/Charge/Retreat/Ambush/Infiltrate/Herd/
  Convert/Combo/Scout/Splash/Stiff/Freeze)을 전부 반영한 [`docs/sample_units.csv`](docs/sample_units.csv)와
  동일한 13개 유닛 내용으로 채움. `docs/sample_units.csv` 자체는 5a5522b 시점에 이미 최신 상태라 변경 없음.

- 2026-09-13: 샌드박스 팔레트 스크롤바 손잡이 가로 크기 버그 수정.
  - **동기**: "스크롤 가로 크기 이상해"라는 피드백. 이 HUD는 씬/프리팹에 미리 배치된 오브젝트가 아니라
    `SandboxHud.Init()`이 Play 시점에 전부 코드로 생성하는 구조라(직전 커밋 참고) 씬이나 프리팹을 직접
    편집해 고칠 대상이 없고, CLI에도 이런 런타임 UI 레이아웃을 편집하는 별도 기능은 없어 코드로 수정.
  - **원인**: [`SandboxHud.BuildPalette`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)에서 스크롤바
    "Handle"(손잡이) `RectTransform`에 anchor/size를 전혀 설정하지 않아 새 RectTransform 기본값(앵커
    (0,0)-(0,0), 크기 0)이 그대로 남아있었다. `Scrollbar` 컴포넌트는 스크롤 방향 축(세로)의 크기만
    콘텐츠 비율에 맞춰 자동 조절하고 가로 축은 직접 트랙 폭에 맞춰 앵커를 잡아줘야 하는데, 그게
    빠져 손잡이 가로폭이 사실상 0으로 찌그러져 있었다.
  - **수정**: Handle의 `anchorMin`/`anchorMax`를 (0,0)-(1,1)로 채워 트랙 전체 폭에 맞춘 뒤, `sizeDelta`를
    0으로 둬서 세로 크기 조절(Scrollbar가 자동으로 처리)만 남기고 가로는 항상 트랙 폭 그대로 유지되게
    했다.
  - **검증**: 캐릭터 모델 검증 때와 같은 방식(Unity CLI + Play Mode 없이 동기 렌더링)으로, `SandboxHud`를
    코드로 직접 생성해 팔레트에 20개 더미 행을 채운 뒤 Canvas를 `ScreenSpaceCamera`로 바꿔 `RenderTexture`에
    렌더링 — 스크롤바 손잡이가 트랙 폭 그대로 채워진 세로 막대로 정상 표시됨을 눈으로 확인(스크립트는 확인
    후 삭제). `unity run . -- -nographics` 헤드리스 컴파일과 `UnitCsvVerification.Run`(`ALL PASS`) 재검증
    통과.
- 2026-09-13: 런타임 코드로 생성하던 UI/표시 오브젝트를 전부 프리팹으로 전환.
  - **동기**: "오브젝트 생성해서 만드는 코드들 전부 점검 후, 프리팹으로 수정해서 적용할 수 있는 부분들
    있으면 그렇게 수정해서 진행해달라"는 요청. 전체 조사 범위는 [`BattleHud`](Assets/Scripts/TacticsECS/View/BattleHud.cs)/
    [`SandboxHud`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)(uGUI 전체를 매 Play마다 `new GameObject`+
    `AddComponent`로 절차 생성)와 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)(유닛마다 머리 위
    체력 표시를 `GameObject.CreatePrimitive`로 매번 새로 생성)까지로 정했다. `BattleController`의 `GridView`/
    `UnitSpawner` 같은 "빈 오브젝트 + 컴포넌트 하나"짜리 홀더와 `Camera.main` 폴백은 하이어라키가 없어
    프리팹화해도 이득이 없다고 판단해 그대로 뒀다.
  - **방법**: 기존 `Assets/Editor/UnitPrefabSetup.cs` 패턴(코드로 GameObject를 만들고
    `PrefabUtility.SaveAsPrefabAsset`으로 저장, `-executeMethod`로만 실행)을 그대로 따라
    [`UIPrefabSetup.cs`](Assets/Editor/UIPrefabSetup.cs)를 새로 작성 — `Assets/Prefabs/UI/`에
    `HpDisplay`/`EventSystem`/`PaletteButton`/`BattleHud`/`SandboxHud` 5개 프리팹을 생성하고,
    `SampleScene`/`Sandbox` 두 씬의 `BattleController` 필드와 기존 유닛 프리팹 7종의 `UnitView.hpDisplayPrefab`
    필드에 자동 연결한다(유닛 프리팹은 통째로 재생성하지 않고 이 필드 하나만 반사로 덧붙여, `Unit_Guard`의
    수동 조정값 같은 기존 데이터를 건드리지 않았다). `BattleHud`/`SandboxHud`.`Init()`은 이제 하이어라키를
    만드는 대신 프리팹 안의 자식을 이름으로 찾아(`Wire*`) 참조를 캐싱하고, 아이콘 스프라이트 지정·버튼
    클릭 이벤트 연결처럼 프리팹에 구울 수 없는 부분만 코드로 채운다(`IconLibrary`가 만드는 Sprite는 디스크
    에셋이 아니라서 프리팹에 구우면 참조가 끊긴다 — 기존 `IconLibrary` 주석과 같은 이유). CSV 행 수만큼
    늘어나는 샌드박스 팔레트 목록만 예외로, 행마다 `PaletteButton` 프리팹을 그대로 인스턴스화한다.
  - **버그 발견 및 수정**: 버튼/배지 툴팁을 담당하는 `TooltipTrigger`를 처음엔 `BattleHud`의 private 중첩
    클래스로 그대로 뒀는데, 프리팹에 저장했다가 다시 불러오면(배치 재실행 시 새 프로세스) 컴포넌트의
    `m_Script` 참조가 `fileID: 0`으로 끊겨 `GetComponent<TooltipTrigger>()`가 null을 반환하는 것을
    직접 실행해서 발견. `public`으로 바꿔도 같은 문제가 재현됐는데, 원인은 접근 제한자가 아니라 "한
    .cs 파일 안에 클래스 두 개"였다 — 그 파일이 이번 배치 실행에서 막 재컴파일된 경우, Unity가 두 번째
    클래스의 MonoScript fileID를 그 실행 안에서 안정적으로 해석하지 못했다. 이 프로젝트의 다른 모든
    MonoBehaviour처럼 [`TooltipTrigger.cs`](Assets/Scripts/TacticsECS/View/TooltipTrigger.cs)를 독립 파일로
    분리해 해결.
  - **검증**: `unity run . -- -nographics` 헤드리스 컴파일 통과. 1회성 스크립트(`UIPrefabVerify.Run`, 확인
    후 삭제)로 `BattleHud`/`SandboxHud`/유닛 프리팹을 실제 프로덕션 경로 그대로(`UnitSpawner.Spawn` 포함)
    인스턴스화해 `Init()`부터 `ShowUnitPanel`/`SetUnitActions`/`ShowBattleEnd`/`SetPalette`/`SetSelectedUnit`
    등 공개 메서드 전부를 예외 없이 호출 — `Wire*`의 `transform.Find` 경로가 하나라도 어긋나면 그 자리에서
    `NullReferenceException`이 나므로, 예외 없이 끝났다는 것 자체가 프리팹 하이어라키와 이름이 코드와
    정확히 일치한다는 검증이다.
- 2026-09-13: CSV에서 팀별 유닛 색(`PlayerColor`/`EnemyColor`)을 없애고 팀 구분 없는 단일 색으로 통일.
  - **동기**: "csv 에 플레이어와 적 색깔을 삭제하고, 두 팀의 유닛 색깔을 전부 통일해달라"는 요청.
  - **CSV 스키마**: `PlayerColor`/`EnemyColor` 두 컬럼을 `Color` 하나로 합침(값은 기존 `PlayerColor`를
    그대로 사용). [`UnitCsvRow`](Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)도 `PlayerColor`/`EnemyColor`
    필드 대신 `Color` 필드 하나로, [`UnitCsvSerializer`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs)의
    헤더/파싱/작성 로직과 [`UnitCsvActionFactory.ToRow`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvActionFactory.cs)도
    맞춰 갱신. [`docs/sample_units.csv`](docs/sample_units.csv)/[`SandboxUnits.csv`](SandboxUnits.csv)/
    [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 스키마 표도 함께 갱신.
  - **프리팹/View**: [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs)의 `playerColor`/`enemyColor`
    필드를 `color` 하나로 합치고, `ColorFor(Team)` 메서드를 팀 무관 `Color` 프로퍼티로 교체(호출부인
    [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)의 `Init`/`Refresh`도 함께 갱신, `Team` 조회 제거).
    기존 유닛 프리팹 7종(`Assets/Prefabs/Units/Unit_*.prefab`)의 YAML도 `playerColor`/`enemyColor` 두 줄을
    `color` 한 줄로 직접 수정(값은 기존 `playerColor` 그대로 유지). 프리팹을 생성하는 1회성 배치 도구
    [`UnitPrefabSetup.cs`](Assets/Editor/UnitPrefabSetup.cs)/[`ExtraCharacterPrefabSetup.cs`](Assets/Editor/ExtraCharacterPrefabSetup.cs)도
    시그니처를 맞춰 갱신(재실행 대비).
  - **검증**: `unity run . -- -nographics` 헤드리스 컴파일 통과. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UnitCsvVerification.Run`으로 갱신된 `docs/sample_units.csv`(13행) round-trip/스폰
    검증 모두 `PASS`.
- 2026-09-13: 무료 로우폴리 타일 에셋 도입 + 육지/물 지형 타입 추가, 그 위에서 테스트할 13유닛 CSV 구성.
  - **에셋 선정**: [Kenney - Tower Defense Kit](https://kenney.nl/assets/tower-defense-kit)(CC0, 정사각형
    그리드용 평평한 타일 메시 포함, 160개 파일)을 조사해 후보로 제시하고 사용자 확인 후 다운로드 —
    `tile.fbx` + 텍스처(`colormap.png`, 실제로는 쓰이지 않음, 참고용) + 라이선스 원문만
    `Assets/Art/Tiles/Kenney/`에 추가(전체 160개 대신 필요한 것만).
  - **지형 데이터**: [`TerrainType`](Assets/Scripts/TacticsECS/Core/TerrainType.cs)(`Land`/`Water`) 신설.
    [`TileData`](Assets/Scripts/TacticsECS/Core/TileData.cs)에 `Terrain` 필드, [`GridWorld`](Assets/Scripts/TacticsECS/Data/GridWorld.cs)에
    `GetTerrain`/`SetTerrain` 추가. 유닛 쪽은 같은 타입의 [`MoveDomain`](Assets/Scripts/TacticsECS/Core/UnitComponents.cs)
    컴포넌트(`UnitDefinition.domain` → `UnitSpawner`가 스폰 시 그대로 옮김)로 자신이 들어갈 수 있는 지형을 갖는다.
  - **이동 판정**: [`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)과
    [`MoveAction.Execute`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)가 `Move.IgnoreTerrain`이 꺼진 유닛에 한해
    `Walkable` 체크에 더해 "자신의 `MoveDomain`과 타일 지형이 같은가"까지 함께 판정하도록 확장 — 기본 차단만
    구현하고(육지 유닛은 물에, 물 유닛은 육지에 못 들어감), 함선이 육지 유닛을 태우는 수송 기능은 만들지 않음.
  - **CSV 스키마**: `UnitCsvRow`/`UnitCsvSerializer`/`UnitCsvActionFactory`에 `Domain` 컬럼 추가(Actions 다음,
    Move.* 앞). 기존 CSV(`docs/sample_units.csv`)도 전부 `Land`로 채워 갱신 — 컬럼이 하나 늘어난 파싱은
    위치 기반이라, `Domain` 없이 저장된 예전 CSV를 그대로 불러오면 그 뒤 컬럼이 밀린다는 점에 유의.
  - **새 플레이스홀더 패시브 5종**: 사용자가 준 유닛 표의 요새화/은신/약탈/고정/수송이 기존 17개 행동에
    없어, `ScoutAction`과 같은 성격(태그만 있고 효과 미정)의 `FortifyAction`/`StealthAction`/`PillageAction`/
    `AnchoredAction`/`TransportAction`을 추가(`ActionType`에 플래그 5개 추가, `UnitCsvActionFactory.BuildActions`
    연결). `BattleHud` 패시브 배지에는 아직 연결하지 않음 — 자세한 내용은 [플레이스홀더 패시브](#플레이스홀더-패시브-아직-효과-미정) 참고.
  - **테스트 CSV**: 사용자가 준 표(보병/방패병/검투사/기병/기사/궁병/투석기/사제/스파이 — 육지, 뗏목/정찰선/
    충각선/범선 — 물) 그대로 [`SandboxUnits.csv`](SandboxUnits.csv) 13유닛을 구성 — 표의 "패시브" 칸은 그대로
    `Actions`로, "이동" 칸(육지/물)은 `Domain`으로 옮겼다. 표의 숫자 칸은 사용자가 "비용"(자원 비용, 지금 CSV
    스키마에는 없는 개념) 의미로 확인해줘서 CSV의 `Attack.Attack`에는 반영하지 않았고, HP/이동 범위/사거리와
    함께 공격력도 자유롭게 새로 정했다. 표의 마지막 칸(방패/제련/기마/기사도/궁술/수학/철학/외교/낚시/배 타기/
    충파/항해)은 사용자 확인대로 게임 메커닉이 아닌 플레이버 텍스트로 보고 CSV에는 반영하지 않음.
  - **씬 연결**: [`GridView`](Assets/Scripts/TacticsECS/View/GridView.cs)가 프리미티브 큐브 대신(지정돼 있으면)
    지형별 타일 프리팹을 인스턴스화하도록 확장(가로/세로만 타일 크기로 스케일, 높이는 그대로) — 하이라이트는
    여전히 타일마다 매번 새로 만드는 런타임 머티리얼로 처리해 프리팹 공유로 인한 색 간섭이 없게 함.
    `BattleController`에 `waterTiles`/`landTilePrefab`/`waterTilePrefab` 인스펙터 필드 추가. 새 CLI 전용 도구
    [`TileAssetSetup`](Assets/Editor/TileAssetSetup.cs)이 `tile.fbx`로 `Tile_Land`/`Tile_Water` 프리팹을 만들고
    두 씬(`SampleScene`/`Sandbox`)의 `BattleController`에 연결 — `SampleScene`은 `waterTiles`를 비워 기존 데모
    전투에 영향이 없고, `Sandbox`는 그리드 오른쪽 1/3(6~7열)을 물로 채워 새 물 유닛을 바로 테스트할 수 있게 함.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI 배치모드로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.TileAssetSetup.GenerateAll`로 프리팹 생성/씬 연결(로그에 컴파일 에러 없음, 두 씬 모두
    필드 반영 확인). 1회성 점검 스크립트로 `Tile_Land`/`Tile_Water`의 실제 메시 바운드를 로그로 찍어 임포트
    스케일이 정확히 1×0.2×1(자식 없이 루트에 Renderer)임을 확인 후 스크립트 삭제 — FBX 단위 보정 문제 없음.
    `unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run`(`Domain` 필드 비교 추가)으로
    `docs/sample_units.csv` round-trip/스폰 `PASS`. `SandboxUnits.csv`는 15컬럼×13유닛 구조를 직접 확인.
- 2026-09-13: 유닛 색을 CSV 데이터에서 완전히 빼고, 팀별 고정 색 2개로 대체.
  - **동기**: 바로 앞 항목(CSV의 `PlayerColor`/`EnemyColor`를 없애고 단일 `Color` 컬럼으로 합침)이 원하던
    방향과 달랐다는 피드백 — "CSV에서 색 데이터를 전부 삭제하고, 대신 코드 안에 플레이어 팀 색/적 팀 색을
    지정해서 그 색을 모든 유닛에 통일 적용하라"로 재작업. 즉 색은 유닛 타입별 값이 아니라 순전히 팀에만
    묶인 고정값이어야 한다는 것.
  - **CSV/Data**: `UnitCsvRow.Color`, `UnitCsvSerializer`의 `Color` 컬럼(파싱/작성/헤더)과 `ParseColor`/
    `WriteColor` 헬퍼, `UnitCsvActionFactory.ToRow`의 `color` 매개변수를 전부 제거. [`SandboxUnits.csv`](SandboxUnits.csv)/
    [`docs/sample_units.csv`](docs/sample_units.csv)에서 `Color` 컬럼 자체를 삭제(다른 컬럼 값은 그대로).
  - **View**: [`UnitDefinition`](Assets/Scripts/TacticsECS/View/UnitDefinition.cs)의 `color` 필드/`Color`
    프로퍼티를 제거 — 이제 색에 관해서는 전혀 아는 게 없다. 대신 [`UnitView`](Assets/Scripts/TacticsECS/View/UnitView.cs)에
    `PlayerColor`/`EnemyColor` 두 `static readonly Color` 상수(기존 `GuardingColor`와 같은 패턴)를 추가하고,
    `Init`/`Refresh`가 `world.Get<Team>(id)`로 팀을 조회해 그 상수를 그대로 쓰도록 변경 — 유닛 타입과 무관하게
    같은 팀이면 항상 같은 색.
  - **정리**: 기존 유닛 프리팹 7종의 YAML에서 `color:` 줄을 제거, 이를 생성하는 1회성 배치 도구
    (`UnitPrefabSetup.cs`/`ExtraCharacterPrefabSetup.cs`)에서도 `color` 매개변수와 `SetPrivateField(def, "color", ...)`
    호출을 제거. `UnitCsvVerification`의 round-trip 비교에서도 `Color` 필드 비교를 뺐다.
    [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 스키마 표에서 `Color` 행을 빼고, 색이 팀 상수로 고정되어
    있다는 설명을 별도로 추가.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UnitCsvVerification.Run` — 컴파일 에러 없이 `docs/sample_units.csv`(13행, 컬럼 하나
    줄어든 새 스키마) round-trip/스폰 검증 모두 `PASS`.
- 2026-09-13: [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 기획자용으로 재정리, 플레이스홀더 패시브 5종을
  개별 문서로 분리.
  - **문서 재구성**: 기존 문서는 구현 세부사항(코드 경로, 판정 로직)이 사용법과 뒤섞여 있어, "빠른 시작(인게임
    사용법)" → "CSV로 유닛 만들기/수정하기(스키마 표·예시 조합·주의사항)" 순으로 앞쪽에 배치하고,
    코드 링크 모음(`관련 코드`)과 CLI 검증은 맨 아래로 내려 개발자 참고용으로만 남겼다.
  - **플레이스홀더 패시브 개별 문서화**: 요새화/은신/약탈/고정/수송 5종을 각각
    [`docs/passives/Fortify.md`](docs/passives/Fortify.md) / [`Stealth.md`](docs/passives/Stealth.md) /
    [`Pillage.md`](docs/passives/Pillage.md) / [`Anchored.md`](docs/passives/Anchored.md) /
    [`Transport.md`](docs/passives/Transport.md)로 분리 — 상태/사용 예시 유닛/예상 효과/CSV 표기법과, 나중에
    효과를 구현할 때 손대야 할 지점(`UnitActionQueries.Find<T>` 호출 위치, `BattleHud.PassiveDefs` 등)을
    정리했다. `README.md`의 기존 플레이스홀더 패시브 절과 `UnitCsvSandbox.md`에서 각 문서로 링크.
  - **부수 발견**: `SandboxUnits.csv`의 물 유닛 4종이 쓰는 `BaseVisual`(`Raft`/`ShipSmall`/`Galleon`)이
    `BattleController.StartPlacementPhase`의 `basePrefabsByName`에 아직 등록돼 있지 않아, 지금 샌드박스에서
    불러오면 이 4종은 팔레트에 나타나지 않는다(콘솔 경고) — 코드는 고치지 않고 문서(`UnitCsvSandbox.md`의
    `BaseVisual` 행)에 현재 상태로만 남겨둠. 새 `Assets/Art/Ships` 에셋과 함께 별도 작업으로 이어질 것으로 보임.
- 2026-09-13: 기획자 제공용 샌드박스 툴 빌드 환경 및 가이드 문서 정비.
  - **스탠드얼론 파일 대화상자 구현**: 에디터 전용(`EditorUtility.OpenFilePanel`/`SaveFilePanel`)이던 파일
    불러오기/내보내기 기능을 Windows 스탠드얼론 빌드본에서도 작동하도록 Win32 `comdlg32.dll` 기반
    네이티브 다이얼로그 헬퍼 [`StandaloneFileDialog.cs`](Assets/Scripts/TacticsECS/Sandbox/StandaloneFileDialog.cs)
    구현 및 [`SandboxHud.cs`](Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs) 연결.
  - **물 지형 삭제 및 땅 지형 통일**: `Sandbox.unity` 씬의 `waterTiles`를 빈 리스트(`[]`)로 수정하여 그리드 전체를
    땅 지형으로 일원화하고, `TileAssetSetup.cs`에서도 샌드박스용 데모 물 타일 자동 주입을 비활성화.
  - **물 유닛 및 플레이스홀더 정리**: 아직 외형 모델 및 수송 기능이 미비한 물 관련 유닛(뗏목, 정찰선, 충각선, 범선)을
    플레이스홀더로 전환하고, `SandboxUnits.csv`를 즉시 정상 플레이 가능한 육지 9종 유닛(보병, 방패병, 검투사, 기병,
    기사, 궁병, 투석기, 사제, 스파이)으로 정비.
  - **기획자용 요약 가이드 (`UnitCsvSandbox.md`) 작성**: 기획자가 알아야 할 최소한의 정보와 실제 수행 가능한 조작,
    완전히 구현된 행동(스킬) 16종 명세 및 필수 CSV 컬럼만을 선별하여 [`UnitCsvSandbox.md`](UnitCsvSandbox.md)
    (루트 및 [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md))로 재정리(미구현 플레이스홀더 및 물 요소 생략).
  - **검증**: `UnitCsvVerification.cs`에 `SandboxUnits.csv`의 왕복 파싱 및 런타임 스폰 검증을 추가하고,
    `unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run` 실행하여 13종 샘플 및 9종 샌드박스 유닛
    모두 `ALL PASS` 확인.
- 2026-09-15: 좌상단 유닛 로스터(체력 아이콘+숫자), 피해 팝업, 우상단 행동 로그, 공용 한글 폰트(Jua)를 추가.
  - **폰트**: 로우폴리 캐주얼 톤에 맞는 한글 지원 폰트 Jua(Google Fonts, OFL 라이선스)를
    `Assets/Fonts/Jua-Regular.ttf`(+`LICENSE.txt`)로 추가하고, `UIPrefabSetup.LoadUiFont`가 프로젝트의 모든
    UI Text/TextMesh(턴 배지, 유닛 패널, 툴팁, 승/패 화면, 샌드박스 팔레트/툴바, 유닛 머리 위 체력 숫자 등)에
    일괄 적용하도록 변경 — 기존 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 호출을 전부 제거했다.
  - **좌상단 유닛 로스터**: `BattleHud`에 `TurnBadge` 바로 아래 스크롤 가능한 패널을 추가했다
    ([`UIPrefabSetup.BuildUnitRoster`](Assets/Editor/UIPrefabSetup.cs) — 기존 `SandboxHud` 팔레트와 뼈대
    (Viewport/Content/Scrollbar)를 공유하도록 `BuildScrollPanel` 헬퍼로 추출). `BattleHud.SetRoster(world)`가
    살아있는 모든 유닛(양 팀)을 [팀 색 줄 | 체력 아이콘 | 현재 HP 숫자] 한 줄씩으로 다시 그리고,
    `BattleController`는 체력이 바뀌거나 유닛이 죽는 모든 행동(공격/반격/치유/자폭/대기/턴 종료 자동 대기)
    직후 `RefreshRoster()`로 갱신한다.
  - **피해 팝업**: 새 [`DamagePopup`](Assets/Scripts/TacticsECS/View/DamagePopup.cs) 컴포넌트 + 프리팹
    (`UIPrefabSetup.GenerateDamagePopup`)을 추가했다. `UnitView.ShowDamagePopup(amount)`가 맞은 유닛 위에
    "-숫자"를 잠깐 띄웠다 위로 떠오르며 사라지게 한다 — 맞은 유닛(UnitView)의 자식이 아니라 독립된
    오브젝트로 스폰해서, 그 자리에서 죽어 비활성화돼도 애니메이션이 끊기지 않는다. `BattleController`가
    `CombatSystem.TryAttack`의 `damageDealt`/`counterDamageDealt`(기존에는 버려지던 out 값)와, 자폭처럼 여러
    유닛이 한꺼번에 맞는 경우는 실행 전후 체력 스냅샷(`SnapshotHp`) 차이로 정확한 피해량을 구해 띄운다.
  - **우상단 행동 로그**: `BattleHud`에 스크롤 없는 고정 패널(`UIPrefabSetup.BuildActionLog`)을 추가했다 —
    `BattleHud.AddLogEntry`가 슈팅 게임 킬피드처럼 최근 9줄만 유지하며 위에 새 줄을 쌓는 방식이라 스크롤이
    필요 없다. 새 값 타입 [`BattleLogEntry`](Assets/Scripts/TacticsECS/Core/BattleLogEntry.cs)(순수 데이터,
    `Data`/`Core` 계층 규칙 준수)로 이동/공격/반격/방어/치유/자폭/대기/쓰러짐을 표현하고,
    `BattleController.FormatLogEntry`가 유닛 이름(새로 추가한 `UnitView.Label` — CSV 유닛은 `Name` 컬럼,
    데모 편성은 `SpawnUnit`에 지정한 한글 이름)과 팀 강조색(`<color>` 리치 텍스트)을 붙여 한 줄 문구로
    바꾼다. 플레이어 조작과 [`EnemyAI`](Assets/Scripts/TacticsECS/Systems/EnemyAI.cs)(반환 타입을
    `List<(int,int)>`에서 `List<BattleLogEntry>`로 변경해 이동/공격/반격/사망까지 전부 기록) 양쪽 모두 같은
    경로로 로그를 쌓는다.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIPrefabSetup.GenerateAll`로 프리팹 재생성(컴파일 에러 없음). 새 배치모드 검증
    스크립트 [`UIVerification.cs`](Assets/Editor/UIVerification.cs)(`UnitCsvVerification`과 같은 패턴)를 추가해
    `unity run . -- -executeMethod TacticsECS.EditorTools.UIVerification.Run` 실행 — 공용 폰트 배선, 로스터/로그
    줄 생성, 유닛 이름표/피해 팝업 프리팹 배선까지 `ALL PASS` 확인.
- 2026-09-15: 인게임 체력/데미지 라벨 크기 축소, CSV의 드문 이동 옵션 3종을 고정 컬럼에서 패시브(Actions)로 이동.
  - **라벨 크기**: 머리 위 체력 숫자([`UIPrefabSetup.GenerateHpDisplay`](Assets/Editor/UIPrefabSetup.cs)의
    `Number` localScale 0.3→0.15)와 피해 팝업(`GenerateDamagePopup`의 localScale 0.35→0.175)을 각각 절반
    크기로 줄였다(사용자 요청: "2배 줄여").
  - **동기**: CSV의 `Move.IgnoreTerrain`/`Move.IgnoreUnitBlocking`/`Move.AllowDiagonal`이 모든 유닛 행에
    고정으로 들어가는 컬럼이었는데, 실제로 쓰는 유닛은 거의 없는 드문 케이스라("샌드박스 13+9종 중 딱 1종만
    사용) Charge/Retreat/Infiltrate와 같은 성격의 값 없는 순수 마커 패시브로 옮겨 `Actions` 컬럼에서 필요한
    유닛만 선택적으로 켜도록 재구성했다(사용자 요청, 질문 없이 판단해 진행).
  - **새 패시브 3종**: [`IgnoreTerrainAction`](Assets/Scripts/TacticsECS/Actions/IgnoreTerrainAction.cs)(장애물
    통과)/[`IgnoreUnitBlockingAction`](Assets/Scripts/TacticsECS/Actions/IgnoreUnitBlockingAction.cs)(유닛
    통과)/[`AllowDiagonalAction`](Assets/Scripts/TacticsECS/Actions/AllowDiagonalAction.cs)(대각선 이동) 추가
    (`ActionType`에 플래그 3개 추가, `UnitCsvActionFactory.BuildActions`/`ToRow` 연결). [`MoveAction`](Assets/Scripts/TacticsECS/Actions/MoveAction.cs)은
    이제 `MoveRange`만 갖고, `Execute`가 `UnitActionQueries.Find<T>`로 이 세 패시브의 보유 여부만 그때그때
    확인한다(Infiltrate와 완전히 같은 방식) — [`PathfindingSystem.GetReachable`](Assets/Scripts/TacticsECS/Systems/PathfindingSystem.cs)도
    동일하게 변경. `Core/UnitComponents.cs`의 `IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal` 컴포넌트와
    `UnitDefinition`/`UnitSpawner`의 관련 배선은 더 이상 필요 없어 제거했다.
  - **CSV 스키마 변경(호환 깨짐)**: [`UnitCsvRow`](Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)에서 세
    필드를 제거하고, [`UnitCsvSerializer`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs)의 헤더에서
    `Move.IgnoreTerrain`/`Move.IgnoreUnitBlocking`/`Move.AllowDiagonal` 세 컬럼을 통째로 뺐다 — 컬럼 위치
    기반 파싱이라 **예전 15컬럼 CSV는 그대로 불러오면 `Move.Range` 뒤 컬럼이 밀린다**(기존 CSV는 `Actions`에
    필요한 경우만 `IgnoreTerrain`/`IgnoreUnitBlocking`/`AllowDiagonal`을 추가하고 12컬럼으로 다시 저장해야
    함). [`docs/sample_units.csv`](docs/sample_units.csv)/[`SandboxUnits.csv`](SandboxUnits.csv)를 새 12컬럼
    스키마로 갱신 — 유일하게 값이 있던 `SandboxUnits.csv`의 스파이(`Move.IgnoreTerrain=True`)는
    `Actions`에 `IgnoreTerrain`을 추가해 동작이 그대로 보존되도록 옮겼다. [`UnitCsvSandbox.md`](UnitCsvSandbox.md)/
    [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 스키마 표/패시브 목록도 갱신.
  - **부수 발견 및 수정**: 검증 중 `UnitCsvVerification.VerifySpawnFromCsv`의 `AvailableActions` 비교가
    (이 작업과 무관하게) 항상 실패하고 있던 걸 발견 — `UnitCsvActionFactory.BuildActions`가 CSV에 `Wait`가
    없어도 항상 `WaitAction`을 추가해주는데, 비교 대상인 `row.Actions`(원본 CSV 값)에는 그 비트가 없어
    모든 행이 항상 mismatch였다. 기대값도 `| ActionType.Wait`로 맞춰 수정.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIPrefabSetup.GenerateAll`(라벨 크기 반영, 컴파일 에러 없음),
    `unity run . -- -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run`(새 12컬럼 스키마로 13종
    샘플 + 9종 샌드박스 유닛 round-trip/스폰 `ALL PASS`, 스파이의 `IgnoreTerrainAction` 배선 포함),
    `unity run . -- -executeMethod TacticsECS.EditorTools.UIVerification.Run`(`ALL PASS`) 순으로 실행.

- 2026-09-15: 유닛 CSV에 `Id` 컬럼 추가.
  - [`UnitCsvRow`](Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)에 `Id`(문자열) 필드 추가 — `Name`(표시용
    텍스트, 기획 편의상 자유롭게 바뀔 수 있음)과 분리된, 유닛 타입을 가리키는 안정적인 키. 맨 앞 컬럼으로
    넣었다.
  - [`UnitCsvSerializer`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs) 헤더/파싱/쓰기에 `Id`
    컬럼을 첫 컬럼으로 추가(나머지 컬럼은 위치가 한 칸씩 밀림 — 컬럼 위치 기반 파싱이라 이전 12컬럼 CSV는
    그대로 불러오면 밀린다). [`UnitCsvActionFactory.ToRow`](Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvActionFactory.cs)도
    `id` 매개변수를 받도록 변경(현재 실제 호출부는 없음).
  - [`docs/sample_units.csv`](docs/sample_units.csv)(13종)/[`SandboxUnits.csv`](SandboxUnits.csv)(9종)에 각
    행마다 영문 소문자 `Id` 값을 채워 새 13컬럼 스키마로 갱신(예: `Melee`→`melee`, `보병`→`infantry`).
    [`docs/UnitCsvSandbox.md`](docs/UnitCsvSandbox.md) 스키마 표에 `Id` 행 추가.
  - `UnitCsvVerification.VerifyRoundTrip`의 필드별 비교에 `Id` 비교 추가.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UnitCsvVerification.Run` — 새 13컬럼 스키마로 13종 샘플 + 9종 샌드박스 유닛
    round-trip/스폰 `ALL PASS`.

- 2026-09-16: 도시 발전 자원(발전도/인구/골드/신앙) 최소 구현 + 재사용 가능한 표시 UI 프리팹 추가.
  - **동기**: 기획 문서(도시 발전도/인구/골드/신앙 네 자원의 획득·소모 규칙)를 받아 구현 요청. 타일/영토
    정보가 프로젝트에 아직 없어, 그에 의존하는 규칙(채집형/설치형 자원, 영토 밖 특수 자원, 수도 연결
    보너스)은 플레이스홀더로 남겨두고, UI는 프리팹으로 만들어 재사용 가능하게 해달라는 요청을 받음. "커스텀
    화면"은 프로젝트에 그 이름의 씬이 없어 사용자에게 확인한 결과 `Assets/Scenes/Sandbox.unity`의 배치
    단계 화면을 가리키는 것으로 확정.
  - 순수 데이터 [`CityResourceData`](Assets/Scripts/TacticsECS/Core/CityResourceData.cs)(Core)와 무상태
    시스템 [`CityResourceSystem`](Assets/Scripts/TacticsECS/Systems/CityResourceSystem.cs)(Systems) 추가 —
    CLAUDE.md의 Data/Systems 계층 분리 규칙 그대로 적용. 자세한 내용은 위 [도시 발전
    자원](#도시-발전-자원-플레이스홀더) 절 참고.
  - **UI**: [`CityResourceHud`](Assets/Scripts/TacticsECS/View/CityResourceHud.cs) View 스크립트 +
    `Assets/Editor/UIPrefabSetup.cs`에 `GenerateCityResourceBar` 추가해 `Assets/Prefabs/UI/CityResourceBar.prefab`을
    생성(BattleHud/SandboxHud와 같은 패턴 — 하이어라키는 프리팹에, 아이콘/값 바인딩만 Init()이 코드로
    채움). `BattleHud`에 종속시키지 않고 완전히 독립된 프리팹으로 만들어 재사용 가능하게 함.
  - `BattleController`에 `cityResourceHudPrefab`(+ 시작값 인스펙터 필드 4개) 추가, `HandleTurnStart`에서
    플레이어 턴이 시작될 때마다 `CityResourceSystem.ApplyTurnStart` 호출. `UIPrefabSetup.AssignToScene`이
    이 프리팹을 `Sandbox.unity`에만 배정하도록 수정(`SampleScene`은 비워둠 → `BattleController`가 null
    체크로 생성을 건너뜀).
  - [`UIVerification.cs`](Assets/Editor/UIVerification.cs)에 `VerifyCityResourceBar` 추가 — 프리팹을
    인스턴스화해 `SetResources`로 넘긴 값이 4개 슬롯(Development/Population/Gold/Faith) 텍스트에 그대로
    반영되는지 확인.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIPrefabSetup.GenerateAll`(컴파일 에러 없음, `CityResourceBar.prefab` 생성 및
    `Sandbox.unity`에만 배정됨을 씬 diff로 확인), `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIVerification.Run`(`ALL PASS`) 순으로 실행.

- 2026-09-16: 기술트리(스킬트리) 구현 — 도시 발전도 소모형, 5갈래 x 25노드.
  - **동기**: 폴리토피아 기반 기술트리 기획(등산/채집/기마/사냥/낚시 5갈래, 각 1+2+2티어) 다이어그램 +
    표를 받아 구현 요청. "도시 발전 자원을 소모해서 진행"하라는 요청에 따라, 위 [기술트리](#기술트리-스킬트리-플레이스홀더)
    절 참고. 미구현 기능은 플레이스홀더로 유지해달라는 요청대로, 노드 25개가 실제로 여는 효과(건물
    건설/유닛 훈련/지형 보너스)는 대상 시스템 자체가 없어 구현하지 않고 "해금 여부" 상태만 관리한다.
  - **부수 변경**: 기존 `CityResourceData.Development`(도시 발전도)는 생산 로직이 전혀 없어 항상 0으로
    고정돼 있었다 — 기술트리가 실제로 소모할 자원이 필요해져, `GoldProduction`과 같은 방식의 고정값
    플레이스홀더 `DevelopmentProduction` 필드를 추가하고 `CityResourceSystem.ApplyTurnStart`가 매
    플레이어 턴마다 발전도를 그만큼 늘리도록 했다(`CityResourceData.Create` 시그니처에 매개변수 추가 —
    기존 호출부 `BattleController`/`UIVerification` 갱신).
  - 순수 데이터 [`TechId`](Assets/Scripts/TacticsECS/Core/TechId.cs)/[`TechNodeData`](Assets/Scripts/TacticsECS/Core/TechNodeData.cs)/
    [`TechTreeData`](Assets/Scripts/TacticsECS/Core/TechTreeData.cs)(Core), 고정 테이블
    [`TechTreeDefinition`](Assets/Scripts/TacticsECS/Data/TechTreeDefinition.cs)(Data, 25노드 전체 정의),
    무상태 시스템 [`TechSystem`](Assets/Scripts/TacticsECS/Systems/TechSystem.cs)(Systems) 추가 —
    CityResourceData/CityResourceSystem과 똑같이 CLAUDE.md의 Data/Systems 계층 분리 규칙을 그대로 따름.
  - **UI**: [`TechTreeHud`](Assets/Scripts/TacticsECS/View/TechTreeHud.cs) View 스크립트 +
    `Assets/Editor/UIPrefabSetup.cs`에 `GenerateTechTreePanel` 추가해 `Assets/Prefabs/UI/TechTreePanel.prefab`을
    생성(`CityResourceBar`와 같은 패턴 — 노드 배치/연결선까지 전부 `TechTreeDefinition` 테이블만 보고
    코드로 계산해서 굽고, 해금 상태에 따른 색칠만 `TechTreeHud`가 런타임에 채움). `BattleController`에
    `techTreeHudPrefab` 필드 + `HandleTechUnlockRequested` 핸들러 추가, `UIPrefabSetup.AssignToScene`이
    `Sandbox.unity`에만 배정하도록 함.
  - [`UIVerification.cs`](Assets/Editor/UIVerification.cs)에 `VerifyTechTreePanel` 추가 — 선행 기술이
    해금된 2티어 노드는 해금 가능, 선행 기술이 없는 3티어 노드는 해금 불가로 표시되는지, 해금 버튼 클릭
    시 `OnUnlockRequested`가 올바른 `TechId`로 발생하는지 확인.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIPrefabSetup.GenerateAll`(컴파일 에러 없음, `TechTreePanel.prefab` 생성 및
    `Sandbox.unity`에만 배정됨을 로그로 확인), `unity run . -- -executeMethod
    TacticsECS.EditorTools.UIVerification.Run`(`ALL PASS`, 5개 검증 전부 통과) 순으로 실행. 생성된
    프리팹의 연결선 개수(20 = 25노드 - 5개 1티어 루트)와 25개 노드의 한글 라벨이 깨지지 않고 그대로
    저장됐는지 직접 확인.

- 2026-09-16: 기술트리 UI를 갈래x티어 격자에서 **중앙 허브 + 방사형(5방향) 그래프**로, 노드를 사각형에서
  **아이콘 있는 원형**으로 교체.
  - **동기**: 사용자가 스크린샷을 보고 "각 항목에 개별 아이콘 하나씩", "시작 노드를 중앙에 두고 5방향
    으로 퍼져나가는 그래프 형식", "각 항목은 동그라미 모양"으로 바꿔달라고 요청.
  - **아이콘 26종 자체 제작**: `Assets/Art/GameIcons/Resources/Icons`에 25개 기술 노드 + 중앙 허브용
    "tech_hub" 아이콘을 추가. game-icons.net에서 새로 받아오는 대신, 이미 이 프로젝트에 있던 "자체 제작
    아이콘" 방식(`counter.png`/`charge.png` 등 12종이 쓴 PowerShell + System.Drawing(GDI+) 흰색 실루엣/
    투명 배경 512x512 PNG)을 그대로 따랐다 — 다운로드 없이 바로 라이선스 문제 없는 일관된 스타일을 낼 수
    있어서다. 생성 스크립트(`GenerateTechIcons.ps1`)는 1회성 도구라 실행 후 삭제, 결과 PNG/meta만 남김.
    `farming.png`(농사)는 첫 시도가 뭉개진 모양이라 밀 이삭 모양으로 다시 그렸다. `Assets/Art/GameIcons/
    LICENSE.txt`에 26종 전부 자체 제작임을 기록.
  - [`TechNodeData`](Assets/Scripts/TacticsECS/Core/TechNodeData.cs)에 `Icon`(아이콘 이름) 필드 추가,
    [`TechTreeDefinition`](Assets/Scripts/TacticsECS/Data/TechTreeDefinition.cs) 25개 노드 전부에 매칭되는
    아이콘 이름을 채우고 허브용 `HubIcon` 상수 추가.
  - **원형 노드 + 방사형 배치**: `UIPrefabSetup.GenerateTechTreePanel`을 전면 재작성 — 중앙 허브(원점)에서
    5갈래가 72도씩 나뉘고, 갈래 안에서는 1티어(허브에 바로 연결) -> 2티어(TechNodeData.Slot로 좌우
    ±20도) -> 3티어(부모와 같은 각도로 더 바깥쪽) 순으로 반지름이 커지는 순수 계산 배치로 바꿨다(이전엔
    가로=갈래/세로=티어인 격자였다). 각 노드는 사각형 버튼 대신 원형 `Image`(배경) + 중앙 `Icon` + 바로
    아래 이름 `Label`으로 구성 — 배경 색은 해금 상태에 따라 런타임에 칠해지므로 여기서 굽지 않는다.
    2티어->3티어는 부모·자식이 같은 각도라 둘을 잇는 연결선이 2티어 라벨과 같은 직선 위에 놓이는 문제가
    있어(라벨 글자를 선이 가로지름), 그 구간의 반지름 간격만 더 넓혔다(1티어->2티어는 서로 각도가 달라
    문제 없음). 이 패널만 Canvas 기준 해상도를 세로로 키워(1280x950, 다른 프리팹은 1280x720 그대로)
    25개 노드+라벨이 겹치지 않을 공간을 확보했다.
  - **원형 스프라이트 공유 헬퍼 분리**: `BattleHud`가 패시브 배지용으로 갖고 있던 런타임 원형 스프라이트
    생성 코드(픽셀 직접 채우기 + 캐싱)를 `View/RuntimeSprite.cs`(`RuntimeMaterial`과 같은 성격의 공용
    유틸리티)로 빼내 `TechTreeHud`의 노드 배경과 공유하도록 리팩터링. `BattleHud.CircleSprite`
    프로퍼티/캐시 필드는 삭제하고 호출부를 `RuntimeSprite.CreateCircle()`로 교체.
  - [`TechTreeHud.Init()`](Assets/Scripts/TacticsECS/View/TechTreeHud.cs)이 허브/25개 노드 각각의 원형
    배경(`RuntimeSprite.CreateCircle()`)과 아이콘(`IconLibrary.Get(node.Icon)`)을 인스턴스화 직후
    채우도록 갱신 — Sprite.Create 결과는 프리팹에 구워둘 수 없어(CityResourceHud/IconLibrary와 같은 이유)
    코드로 매번 연결해야 한다.
  - **검증**: Unity 에디터가 닫혀 있음을 확인한 뒤 CLI로 진행. `UIPrefabSetup.GenerateAll`(컴파일 에러
    없음) → `UIVerification.Run`(`ALL PASS`) 순으로 실행. 배치를 눈으로 확인하기 위해 1회성 스크립트
    (`TechTreeScreenshot.cs`, Edit 모드에서 임시 Camera+RenderTexture로 패널을 렌더링해 PNG 저장, 확인
    후 삭제)로 스크린샷을 3차례 반복 캡처하며 라벨 겹침을 잡았다(처음엔 라벨 폭이 노드 간격보다 넓어
    사방이 뒤엉켰고, 두 번째 시도에선 2티어->3티어 연결선이 라벨을 가로질렀다 — 위 반지름 조정으로 해결).
