# CSV 유닛 제작·합성 테스트 툴

CSV 한 장으로 유닛을 정의하고(스탯 + 기존 `IUnitAction`들의 조합), 그 목록을 인게임에서 불러와 그리드에
자유 배치한 뒤 실제 턴제 전투로 동작을 확인하는 샌드박스 모드. 새 게임플레이 규칙은 추가하지 않는다 —
기존 8개 행동(Move/Attack/Defend/Heal/SelfDestruct/Counter/Charge/Retreat)의 조합과 파라미터 값만 CSV로
표현한다.

## CSV 스키마

한 행 = 유닛 타입 하나. 예시: [`docs/sample_units.csv`](sample_units.csv) — Melee/Ranged/Guard는 실제 게임
프리팹과 같은 조합(각각 SelfDestruct/Heal/Defend+Counter)으로 6개 행동을 전부 한 번씩 보여주고, Cleric은
Heal+Defend+Counter를, Duelist는 Charge+Retreat를 한 유닛에 합성한 커스텀 예시다.

| 컬럼 | 의미 | 비고 |
|---|---|---|
| `Name` | 유닛 이름(표시/구분용) | |
| `MaxHp`, `Defense` | 기본 스탯 | |
| `BaseVisual` | 외형(모델/머티리얼)을 빌려올 기존 프리팹 | `Melee` / `Ranged` / `Guard` 중 하나 |
| `PlayerColor`, `EnemyColor` | 팀별 틴트 색 | `#RRGGBB` 형식 |
| `Actions` | 이 유닛이 가진 행동 목록 | 세미콜론(`;`)으로 구분, 예: `Move;Attack;Counter` |
| `Move.Range` / `Move.IgnoreTerrain` / `Move.IgnoreUnitBlocking` / `Move.AllowDiagonal` | Move 파라미터 | `Actions`에 `Move`가 있을 때만 사용 |
| `Attack.Attack` / `Attack.Range` | Attack 파라미터 | `Actions`에 `Attack`이 있을 때만 사용. Counter는 별도 값 없이 이 값을 그대로 재사용 |
| `Heal.Amount` / `Heal.Range` | Heal 파라미터 | `Actions`에 `Heal`이 있을 때만 사용 |

`Defend` / `SelfDestruct` / `Counter` / `Charge` / `Retreat`는 자체 파라미터가 없다 — `Actions`에 이름만
넣으면 된다.

**Charge(돌격)/Retreat(대피)**: 기본적으로 유닛은 턴당 이동 또는 공격 중 하나만 할 수 있다(이동하면 그
턴엔 공격 불가, 공격하면 그 턴엔 이동 불가). `Charge`는 이동한 뒤에도 공격할 수 있게, `Retreat`는 공격한
뒤에도 이동할 수 있게 그 제약을 풀어주는 예외 패시브다. 둘 다 가지고 있어도 이동/공격은 여전히 턴당
1회씩으로 제한된다 — 예를 들어 공격 → (Retreat로) 이동까지 한 뒤에는, Charge가 있어도 이미 이번 턴
공격을 마쳤으므로 다시 공격할 수 없다.

**Actions 컬럼이 세미콜론인 이유**: CSV 컬럼 구분자는 쉼표라서, 한 컬럼 안에 여러 행동 이름을 담으려면
쉼표와 겹치지 않는 다른 구분자가 필요하다.

### 예시 행동 조합

- 근접 딜러: `Move;Attack`
- 반격형 탱커: `Move;Attack;Defend;Counter`
- 서포터: `Move;Heal;Defend` (공격 없이 회복/방어만)
- 자폭 유닛: `Move;SelfDestruct`
- 돌격형 듀얼리스트: `Move;Attack;Charge;Retreat` (이동/공격 순서에 상관없이 둘 다 할 수 있는 유닛)

### 비목표 (v1)

- 바디 텍스처를 CSV로 지정하는 것 — 모델 텍스처는 `BaseVisual` 프리팹의 값을 그대로 쓴다.
- 쉼표/따옴표가 포함된 필드 값 이스케이프 — 값이 전부 숫자/불리언/영문 이름이라 단순 분리로 충분하다.
- 빌드(Standalone)에서의 파일 탐색기 지원 — `EditorUtility.OpenFilePanel`/`SaveFilePanel`은 에디터 전용 API라,
  이 툴은 에디터 Play 모드에서 쓰는 것을 전제로 한다.

## 샌드박스 사용법 (인게임)

1. `Assets/Scenes/Sandbox.unity`를 연다(없다면 아래 "씬 준비" 참고). Play를 누르면 배치 단계로 시작한다.
2. 좌상단 **불러오기**를 누르면 OS 파일 탐색기(열기 대화상자)가 뜬다 — 불러올 CSV 파일을 고른다.
3. 팔레트에서 배치할 유닛을, 그 아래 버튼에서 팀(플레이어/적)을 고른다.
4. 그리드의 빈 칸을 클릭하면 그 자리에 유닛이 놓인다. 이미 유닛이 있는 칸을 클릭하면 치워진다.
5. 배치가 끝나면 우하단 **전투 시작**을 누른다 — 이후로는 평소와 동일한 턴제 전투(이동/공격/방어/치유/자폭/반격/
   돌격/대피)가 그대로 진행된다.
6. 언제든 **내보내기**를 누르면 OS 파일 탐색기(저장 대화상자)가 뜨고, 고른 경로로 마지막에 불러온 유닛 목록이
   CSV로 저장된다(배치된 유닛의 위치가 아니라, 팔레트로 쓰인 "유닛 정의 목록" 자체를 그대로 내보내는 것 — CSV에서
   값을 조정하고 다시 불러오는 반복 실험용).
7. 전투가 끝나면(승리/패배) 화면 중앙에 뜨는 **다시 시작** 버튼으로 배치 화면(커스텀 화면)으로 곧장 되돌아갈 수
   있다 — CSV를 고쳐 다시 불러오고, 새로 배치해서 곧바로 재시험하는 흐름이 씬을 다시 Play할 필요 없이 이어진다.

**불러오기/내보내기 대화상자는 Unity 에디터 Play 모드에서만 동작한다**(`UnityEditor.EditorUtility.OpenFilePanel`/
`SaveFilePanel` 사용 — 이 툴은 에디터 전용 테스트 도구라 빌드에서 쓰는 것은 범위 밖).

## 씬 준비 (최초 1회)

Unity 에디터 GUI를 직접 열어 씬을 만들지 않고, CLI로 `SampleScene.unity`를 복제해 `Sandbox.unity`를 만든다
(CLAUDE.md 규칙 1 — 배치모드 CLI 사용, 이미 에디터가 열려 있으면 먼저 종료 후 실행):

```bash
unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.SandboxSceneSetup.Generate -quit
```

`Assets/Editor/SandboxSceneSetup.cs`가 씬을 복제하고 `BattleController.sandboxMode`를 켜준다. 기존
Melee/Ranged/Guard 프리팹 참조는 복제된 씬에 그대로 남아있으므로 추가로 연결할 것이 없다.

## 자동 검증 (CLI)

클릭으로 배치하는 실제 UI 흐름은 상호작용 검증이라 자동화하지 않지만, CSV 파싱/왕복과 스폰 연동은
배치모드 스크립트로 검증할 수 있다:

```bash
unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run -quit
```

`docs/sample_units.csv`를 Parse → Write → 재파싱해 값이 보존되는지, `UnitSpawner.SpawnFromCsv`로 만든
엔티티의 컴포넌트 값이 CSV 행과 일치하는지 확인하고 Console에 `PASS`/`FAIL`을 남긴다.

## 관련 코드

- CSV 파싱/작성: [`UnitCsvSerializer`](../Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs)
- CSV 행 <-> 행동 목록 변환: [`UnitCsvActionFactory`](../Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvActionFactory.cs)
- CSV 행 값 타입: [`UnitCsvRow`](../Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)
- 런타임 스폰: [`UnitSpawner.SpawnFromCsv`](../Assets/Scripts/TacticsECS/View/UnitSpawner.cs) / [`UnitDefinition.ApplyCsvOverrides`](../Assets/Scripts/TacticsECS/View/UnitDefinition.cs)
- 배치 단계 로직: [`UnitPlacementController`](../Assets/Scripts/TacticsECS/Sandbox/UnitPlacementController.cs)
- 배치 단계 UI: [`SandboxHud`](../Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)
- 씬 배선/검증 CLI 스크립트: `Assets/Editor/SandboxSceneSetup.cs`, `Assets/Editor/UnitCsvVerification.cs`
