# CSV 유닛 제작·합성 테스트 툴

CSV 한 장으로 유닛(스탯 + 행동 조합)을 정의하고, 인게임 샌드박스에서 그리드에 자유 배치해 실제 턴제
전투로 바로 확인해보는 툴. 코드를 고치지 않고 CSV 값만 바꿔서 유닛을 만들고 테스트할 수 있다.

## 빠른 시작

1. [씬 준비](#씬-준비-최초-1회)가 안 되어 있으면 먼저 한다(최초 1회만).
2. `Assets/Scenes/Sandbox.unity`를 열고 Play.
3. 좌상단 **불러오기** → CSV 파일 선택(기본: 저장소 루트의 [`SandboxUnits.csv`](../SandboxUnits.csv)).
4. 팔레트에서 유닛 종류 → 팀(플레이어/적) 선택 → 그리드 클릭으로 배치(다시 클릭하면 치워짐).
5. 우하단 **전투 시작** → 평소와 같은 턴제 전투 진행.
6. **내보내기**로 지금 불러온 유닛 목록(팔레트)을 CSV로 다시 저장 가능 — 값 조정 → 재시험 반복용.
7. 전투 종료 후 **다시 시작**으로 씬을 다시 Play하지 않고 배치 화면으로 바로 복귀.

> 불러오기/내보내기 대화상자(`OpenFilePanel`/`SaveFilePanel`)는 **에디터 Play 모드 전용**이다 — 빌드에서는 동작하지 않는다.

## CSV로 유닛 만들기 / 수정하기

한 행 = 유닛 하나. 실제 샌드박스가 불러오는 파일은 [`SandboxUnits.csv`](../SandboxUnits.csv)(육지 9종 +
물 4종, 13유닛 예시) — 컬럼을 참고할 땐 이 파일을 열어보는 게 제일 빠르다. [`docs/sample_units.csv`](sample_units.csv)는
행동 조합 예시용 별도 파일이다.

| 컬럼 | 의미 | 값 |
|---|---|---|
| `Name` | 유닛 이름 | 자유 텍스트 |
| `MaxHp`, `Defense` | 기본 스탯 | 숫자 |
| `BaseVisual` | 외형(모델) | `Melee` / `Ranged` / `Guard` / `RogueHooded` / `Mage` / `SkeletonWarrior` / `SkeletonMage` 중 하나(대소문자까지 정확히 일치해야 함). 이 중 하나가 아니면 팔레트에 표시되지 않고 콘솔에 경고가 뜬다 — **주의**: `SandboxUnits.csv`의 물 유닛 4종(뗏목/정찰선/충각선/범선)은 `Raft`/`ShipSmall`/`Galleon`을 쓰는데, 아직 이 목록에 없어 현재는 팔레트에 뜨지 않는다(함선 프리팹/아트 작업 진행 중) |
| `Actions` | 행동 목록 | 아래 [사용 가능 행동](#사용-가능-행동) 이름을 **세미콜론(`;`)**으로 나열. 예: `Move;Attack;Counter` |
| `Domain` | 다닐 수 있는 지형 | `Land` 또는 `Water`(빈 값/오타는 `Land`로 처리) |
| `Move.Range` 등 `Move.*` | 이동 파라미터 | `Actions`에 `Move` 있을 때만 |
| `Attack.Attack` / `Attack.Range` | 공격 파라미터 | `Actions`에 `Attack` 있을 때만(반격도 이 값 재사용) |
| `Heal.Amount` / `Heal.Range` | 회복 파라미터 | `Actions`에 `Heal` 있을 때만 |
| `Transport.Capacity` | 수송 정원 | `Actions`에 `Transport` 있을 때만. 아직 정원 값만 저장될 뿐 실제 탑승 기능은 없음([상세](passives/Transport.md)) |

`Defend`/`SelfDestruct`/`Counter`/`Charge`/`Retreat`/`Fortify`/`Stealth`/`Pillage`/`Anchored` 등 값이 없는
행동·패시브는 `Actions`에 이름만 넣으면 된다.

### 사용 가능 행동

`Move` · `Attack` · `Defend` · `Heal` · `SelfDestruct` · `Counter`(반격) · `Charge`(돌격) · `Retreat`(대피) ·
`Ambush`(기습) · `Infiltrate`(잠입) · `Herd`(무리) · `Convert`(전향) · `Combo`(연타) · `Scout`(정찰, 효과 미구현) ·
`Splash`(스플래시) · `Stiff`(뻣뻣함) · `Freeze`(빙결) + [플레이스홀더 패시브 5종](#플레이스홀더-패시브) — 자세한
동작은 [README의 사용 가능 행동](../README.md#사용-가능-행동-assetsscriptstacticsecsactions) 참고.

**Charge/Retreat**: 기본적으로 한 턴에 이동 또는 공격 중 하나만 가능하다. `Charge`는 이동 후에도 공격을,
`Retreat`는 공격 후에도 이동을 허용한다(이동/공격 자체는 여전히 턴당 1회).

### 예시 조합

- 근접 딜러: `Move;Attack`
- 반격형 탱커: `Move;Attack;Defend;Counter`
- 서포터(공격 없음): `Move;Heal;Defend`
- 자폭 유닛: `Move;SelfDestruct`
- 돌격형 듀얼리스트: `Move;Attack;Charge;Retreat`

### 지형 (육지/물)

타일마다 `Land`/`Water` 지형이 있고, 유닛은 `Domain` 컬럼으로 자신이 다닐 수 있는 지형을 정한다.
`Move.IgnoreTerrain=True`가 아니면, 육지 유닛은 물 타일에 물 유닛(뗏목/정찰선/충각선/범선)은 육지 타일에
들어갈 수 없다. 타일 배치는 `BattleController`의 `waterTiles` 목록으로 정하며, `Sandbox.unity`는 그리드
오른쪽 1/3이 물이라 바로 테스트해볼 수 있다.

수송 함선이 육지 유닛을 태우고 물을 건너는 기능은 아직 없다 — [수송 패시브 상세](passives/Transport.md) 참고.

### 알아두면 좋은 것

- **색은 CSV에 없다.** 팀(플레이어/적)에 따라 고정 2색으로만 표시되고, 유닛 타입과는 무관하다
  ([`UnitView`](../Assets/Scripts/TacticsECS/View/UnitView.cs)의 `PlayerColor`/`EnemyColor`).
- **`Actions`가 세미콜론인 이유**: CSV 컬럼 구분자가 쉼표라서, 한 컬럼에 여러 행동을 넣으려면 다른
  구분자가 필요하다.
- **바디 텍스처는 CSV로 못 바꾼다** — `BaseVisual` 프리팹의 텍스처를 그대로 쓴다.
- **쉼표/따옴표가 든 값은 지원하지 않는다** — 값이 전부 숫자/불리언/영문 이름이라는 전제.

### 플레이스홀더 패시브

CSV/코드 연동(파싱·내보내기·스폰)은 정상 동작하지만, 아직 실제 전투 효과가 없는 패시브 5종. 각 문서에
현재 상태와 구현 시 참고할 내용을 정리해뒀다.

| 패시브 | 예상 효과 | 문서 |
|---|---|---|
| 요새화 | 제자리 방어 보너스(예상) | [Fortify.md](passives/Fortify.md) |
| 은신 | 적에게 발견되지 않음(예상) | [Stealth.md](passives/Stealth.md) |
| 약탈 | 자원 획득(예상) | [Pillage.md](passives/Pillage.md) |
| 고정 | 미정 | [Anchored.md](passives/Anchored.md) |
| 수송 | 육지 유닛을 태우고 물을 건넘(예상, 정원 값은 이미 동작) | [Transport.md](passives/Transport.md) |

## 씬 준비 (최초 1회)

Unity 에디터 GUI로 직접 만들지 않고 CLI로 생성한다(에디터가 이미 켜져 있으면 먼저 종료 요청):

```bash
unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.SandboxSceneSetup.Generate -quit
```

`Assets/Editor/SandboxSceneSetup.cs`가 `SampleScene.unity`를 복제해 `Sandbox.unity`를 만들고
`BattleController.sandboxMode`를 켠다.

## 자동 검증 (CLI)

클릭 배치 자체는 자동화하지 않지만, CSV 파싱/왕복과 스폰 연동은 배치모드로 검증할 수 있다:

```bash
unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run -quit
```

`docs/sample_units.csv`를 Parse → Write → 재파싱해 값이 보존되는지, 스폰된 엔티티 컴포넌트가 CSV 행과
일치하는지 확인하고 Console에 `PASS`/`FAIL`을 남긴다.

## 비목표 (v1)

- 바디 텍스처를 CSV로 지정하는 것
- 쉼표/따옴표가 포함된 필드 값 이스케이프
- 빌드(Standalone)에서의 파일 탐색기 지원 — 에디터 Play 모드 전용 툴

## 관련 코드 (개발자용)

- CSV 파싱/작성: [`UnitCsvSerializer`](../Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvSerializer.cs)
- CSV 행 ↔ 행동 목록 변환: [`UnitCsvActionFactory`](../Assets/Scripts/TacticsECS/Systems/Csv/UnitCsvActionFactory.cs)
- CSV 행 값 타입: [`UnitCsvRow`](../Assets/Scripts/TacticsECS/Data/Csv/UnitCsvRow.cs)
- 런타임 스폰: [`UnitSpawner.SpawnFromCsv`](../Assets/Scripts/TacticsECS/View/UnitSpawner.cs) / [`UnitDefinition.ApplyCsvOverrides`](../Assets/Scripts/TacticsECS/View/UnitDefinition.cs)
- 배치 단계 로직/UI: [`UnitPlacementController`](../Assets/Scripts/TacticsECS/Sandbox/UnitPlacementController.cs), [`SandboxHud`](../Assets/Scripts/TacticsECS/Sandbox/SandboxHud.cs)
- 씬/검증 CLI 스크립트: `Assets/Editor/SandboxSceneSetup.cs`, `Assets/Editor/UnitCsvVerification.cs`
- 지형 타일 프리팹 생성 CLI: [`TileAssetSetup`](../Assets/Editor/TileAssetSetup.cs)
- 지형 데이터/판정: [`TerrainType`](../Assets/Scripts/TacticsECS/Core/TerrainType.cs), [`TileData.Terrain`](../Assets/Scripts/TacticsECS/Core/TileData.cs), [`MoveDomain`](../Assets/Scripts/TacticsECS/Core/UnitComponents.cs)
