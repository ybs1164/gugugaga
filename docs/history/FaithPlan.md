# 신앙 시스템 구현 계획

기획 원문(신앙 포인트·신전·기도·신전 패시브·신앙 펀치·신화 유닛·신앙 승리)을 이 저장소의 계층 규칙(Data=값, Systems=무상태, 수치는 CSV, 규칙은 `docs/spec`)에 맞춰 구현하는 계획. 구현이 끝나면 규칙은 `docs/spec/game/faith.md`로 옮기고 이 문서는 이력으로만 남긴다.

## 0. 현재 코드와의 접점

| 기획 요소 | 지금 있는 것 | 해야 할 일 |
|---|---|---|
| 신전 건물 | `Buildings.csv` 신전 4종(`Temple` 플래그, 점수 전용). 얼음 신전은 지형이 없어 제외 | 신앙 최대치·기도 거점 역할 추가 |
| 신앙 재화 | `CityResourceData` 주석에만 "별/신앙" 언급, 필드 없음 | 팀별 `Faith`, `MaxFaith` 추가 |
| 턴 시작 처리 | `BattleController.HandleTurnStart` → `CityResourceSystem.ApplyTurnStart`(별 수입) | 같은 자리에 신앙 수입·기도 보너스 |
| 신(God) | 없음 | `Gods.csv` 신설, 팀이 신을 고른다(종족과 무관) |
| 팀 수 | `Team` enum = `Player`, `Enemy` 둘뿐 | 순위 로직은 팀 수에 의존하지 않게 작성(아래 §6) |
| 과업·승리 | `Tasks.csv`(`Kind`+`Threshold`), 승패는 도시/유닛 전멸 | `FaithHeld` 종류 추가, 대신전 승리 판정 추가 |

## 1. 데이터 계층 (값만)

**`Core`/`Data` 신규 타입**
- `CityResourceData`에 `Faith`, `MaxFaith` 필드 추가(팀별 자원 딕셔너리가 이미 `EconomyWorld.Resources`).
- 컴포넌트(`UnitComponents.cs`): `Praying { bool Value }`, `Mythic { string GodId; int TurnsLeft; int OriginEntity }`, `Dormant { bool Value }`(강림 중인 원본 유닛, 그리드에서 빠져 있음).
- `EconomyWorld` 필드: `Dictionary<Team,string> GodIds`, `List<GreatTempleData> GreatTemples`(Id, Team, 타일 목록, 완공 턴, 상태).
- `GodDefinition`, `FaithPunchDefinition`, `MythicUnitDefinition`(정의 표, `Data/`).

**CSV 신설/확장** (수치는 전부 CSV, 한 칸에 한 값 — 규칙 6)
- `Tables/Gods.csv`: `Index, Id, PassiveKind, PassiveValue, PassivePerPrayer, MythicIndex, FaithPunch{n}`. `PassiveKind` = Aura/Buff/Damage/Resource/Gold/FaithCap.
- `Tables/FaithPunches.csv`: `Index, Id, Kind, Cost, Range, Value, Terrain...`. `Kind` = Strike/Boost/Resource/Terrain/Descend.
- `Tables/MythicUnits.csv`: `Index, Id, Upkeep` + 유닛 스탯(기존 `Units.csv` 열 재사용 또는 `Units.csv`에 `Mythic` 플래그 행) + 액티브 `ActiveCost`(유지 턴 소모량).
- `Buildings.csv`: `FaithCap` 열(신전 종류별 최대치 증가), `GreatTemple` 행 + `Flag` 태그 `GreatTemple`.
- `GameRules.csv` 키: `Faith.BaseGain`, `Faith.RankBonus1/2/3`, `Faith.TurnsPerScale`, `Faith.PrayGain`, `Faith.CityLevelCap`, `Faith.TempleCap`, `Faith.RuinCap`, `Faith.GreatTempleFaithRequired`, `Faith.GreatTempleMinTiles`, `Faith.GreatTempleProtectTurns`, `Faith.GreatTempleTieBreakTurns`, `Faith.VictoryScore`.
- `Tasks.csv`: `Kind=FaithHeld`, `Threshold`는 CSV 값, `Unlock` = 건축 기술 키.
- `Strings.csv`: 신·펀치·신화 유닛 이름/설명, HUD/메뉴 문구 키.

## 2. 시스템 계층 (무상태)

| System | 책임 |
|---|---|
| `FaithSystem` | 최대치 계산(도시 레벨·신전·기술·유적·특수 건물 합산, 줄면 현재량 클램프), 팀에 신앙 가산(초과분 버림), 턴 시작 수입(유닛 수 순위 → 공동 순위 → 턴 배수), 기도 보너스 |
| `PrayerSystem` | 기도 가능 판정(신전 주변 8칸의 자기 유닛, 행동 가능), 기도 시작(행동 종료, `Praying` 세움), 해제(피격 시) |
| `TemplePassiveSystem` | 신 → 신전 인접 칸 효과. 오라/버프/피해/자원/골드/신앙형. 기도 유닛 수에 따라 강약 |
| `FaithPunchSystem` | 발동 절차(메뉴 → 선택 → 대상 확인 → 비용 → 적용). 종류별 적용기 5개 |
| `MythicSystem` | 강림(팀당 1기 제한, 원본 `Dormant`로 보존·그리드 이탈, 신화 유닛 스폰), 턴 시작 유지 턴 -1, 액티브 사용 시 추가 감소, 사망/만료 시 원본 제거 or 복귀 |
| `GreatTempleSystem` | 건설 조건 검사(과업·빈 평지 N칸·영토), 위치 전체 공개, 보호 판정, 타이브레이크, 승리 판정 |

기존 시스템 수정 지점
- `CombatSystem`: 기도 중이면 방어 0. 피격 지점(`AttackAction`/`CounterAction`/`SelfDestructAction`/스플래시 — `Accelerated` 해제하는 곳과 동일)에서 `Praying` 해제 → 해제 훅을 한 함수로 모은다.
- `TurnSystem.ResetUnitStates`: 신화 유닛 유지 턴 감소·기도 유닛 신앙 가산은 여기가 아니라 `HandleTurnStart`에서 `FaithSystem`/`MythicSystem`을 호출(경제 상태가 필요하므로).
- `TileImprovementSystem`: 신전 건설/파괴 시 `MaxFaith` 재계산 호출. 대신전은 다중 타일이라 별도 경로.
- `ScoreSystem`: 신앙 승리 보너스 점수.
- `TaskSystem`: `FaithHeld` 판정.
- `VisionSystem`: 대신전 타일을 모든 팀의 `ExploredMask`에 표시.
- `RuinSystem`: 유적 보상에 신앙/최대 신앙 추가(`CityRewards.csv` 형식 확장 여부 확인 필요).

## 3. 단계별 작업 순서

각 단계 끝에 `docs/spec` 갱신 + `python scripts/check_spec.py` + 커밋·push(규칙 5).

1. **신앙 포인트 기반** — `Faith`/`MaxFaith`, `GameRules` 키, 최대치 계산, 순위 수입, `FaithHud`(§3.5 A). 단위 검증(순위·공동 순위·초과분 버림·최대치 하락 클램프).
2. **신전 + 기도** — `Buildings.csv` `FaithCap`, `Praying`/`PrayerSystem`, 방어 0·피격 해제, 유닛 메뉴 "기도", 턴 시작 +1.
3. **신(God) 선택 + 신전 패시브** — `Gods.csv`, 샌드박스 팀별 신 선택 UI(종족 탭과 같은 곳), 패시브 6종. 자원형/골드형은 별 수입·타일 구조물 생성에 연결.
4. **신앙 펀치** — `FaithPunches.csv`, `FaithPunchHud`(§3.5 B), 5종 효과. 지형 변화 예외 처리(§4).
5. **신화 유닛** — 강림, 원본 보존, 유지 턴, 액티브/패시브, 사망·복귀.
6. **신앙 승리** — 대신전 과업·건설·보호·타이브레이크·승리 연출/점수.
6.5. **UI 다듬기** — §3.5 D.
7. **AI** — `EconomyAI`(신전 건설, 신앙 펀치 사용), `EnemyAI`(기도·강림). 마지막에 둔다.
8. **정리** — `sandbox-csv-examples`/`sandbox-input-csv` 스냅샷 갱신(`scripts/export_sandbox_csv_spec.py`), `GameDataLoader.SandboxCsvPaths`에 신규 표 등록, README "요소 지도"에 신앙 행 추가.

## 3.5 UI (신앙 포인트 / 신앙 펀치 각각 별도 HUD)

기존 UI 관례를 따른다: `View/`의 `CityResourceHud`(별 표시줄)·`TechTreeHud` 처럼 **패널 하나 = 스크립트 하나 = 프리팹 하나**, 프리팹은 `Editor/UIPrefabSetup`이 생성하고 `BattleController`가 이벤트로 연결한다. 스타일은 `PixelUISkin`/`PixelUIScaler`, 아이콘은 `IconLibrary`/`SpriteCatalog.csv`, 문구는 `Strings.csv` 키만 쓴다. 두 HUD는 `BattleHud`에 섞지 않는다(`BattleHud`는 이미 비대하다).

**A. `FaithHud` — 신앙 포인트** (1단계에서 만든다)
- 위치: 별 표시줄(`CityResourceHud`) 바로 옆. 신앙 아이콘 + `현재/최대` 숫자 + 게이지 바.
- 표시: 이번 턴 예상 수입(순위 보너스 + 기도 유닛), 내 순위, 최대치 구성(툴팁: 도시/신전/기술/유적).
- 연출: 증가·감소 시 숫자 팝업(`DamagePopup` 재사용), 최대치 도달 시 게이지 강조, 초과분 버려짐 시 짧은 표시.
- 인터페이스: `SetFaith(current, max)`, `SetIncomePreview(gain, rank)`, 클릭 시 `OnFaithPunchOpenClicked`(펀치 HUD 열기). 값만 받고 로직은 모른다.
- 전투 준비 중에는 `SetBattlePanelsVisible`과 같이 숨긴다.

**B. `FaithPunchHud` — 신앙 펀치** (4단계에서 만든다)
- 발동 절차 5단계와 1:1 화면 상태: 메뉴 목록 → 펀치 선택 → 효과 확인(범위 미리보기) → 대상 선택 → 확정/취소.
- 목록: 내 신의 펀치 카드(이름, 종류 아이콘, 비용, 설명). 신앙이 모자라면 비활성 + 이유 문구(기존 건물 메뉴처럼 숨김/비활성 구분).
- 대상 선택: `GridView`에 범위/유효 타일 하이라이트(종류별 색: 피해/강화/자원/지형/강림), 마우스 오버 시 영향받는 유닛·타일 미리보기, 지형 변화는 변경 후 지형 표시, 강림은 대상 유닛 선택 후 신화 유닛 스탯 미리보기.
- 확정 직전 비용 확인 줄("신앙 −N → 남은 M"), 취소 시 소모 없음. 우클릭/Esc 취소.
- 인터페이스: `ShowList(options)`, `ShowPreview(punch, targetInfo)`, 이벤트 `OnPunchSelected`, `OnTargetConfirmed`, `OnCancelled`. 판정은 `FaithPunchSystem`이 하고 HUD는 결과만 표시.

**C. 그 밖의 연결 UI** (같은 단계에서 최소 구현)
- 유닛 메뉴 "기도" 버튼(`ActionMenuHud`): 신전 8칸 안일 때만 표시, 기도 중 유닛 배지.
- 신전 타일 메뉴: 현재 신 패시브·기도 인원 표시(`UI.TileMenu.Temple` 확장).
- 신화 유닛 패널: 남은 유지 턴, 액티브 능력 버튼과 유지 턴 소모량.
- 대신전: 진행 상태 표시(보호 남은 턴/타이브레이크), 전 팀 공개 위치 표시.
- 샌드박스: 팀별 신 선택 드롭다운(종족/바이옴 탭 옆).

**D. UI 다듬기 단계** (6단계 뒤, 7단계 AI 전)
1. 스크린샷 검증: 기존 방식(Edit Mode + RenderTexture, `-nographics` 제외)으로 `FaithHud`/`FaithPunchHud` 각 상태(기본, 신앙 부족, 최대치, 범위 미리보기, 취소)를 캡처해 `docs/images/`에 보관.
2. 해상도/스케일: `PixelUIScaler` 기준 여러 해상도에서 겹침·잘림 확인(별 표시줄, 유닛 패널, 펀치 목록).
3. 폰트/문구: 한·영 전환(`LanguagePreference`) 시 길이 초과 확인, 모든 문구가 `Strings.csv` 키인지 검사.
4. 입력: 키보드 단축키, 모바일식 큰 터치 영역, 포커스/툴팁(`TooltipTrigger`) 일관성.
5. 피드백 연출: 펀치 발동 이펙트, 신앙 증감 팝업, 강림 전환 연출의 타이밍 조정. 연출이 길어도 입력이 막히지 않게 확인.
6. 접근성: 범위 하이라이트를 색에만 의존하지 않고 아이콘/외곽선도 쓴다.

## 4. 결정이 필요한 부분 / 기획 모호점

1. **팀이 둘뿐인데 1·2·3위 보너스?** `Team`이 `Player`/`Enemy` 둘이라 지금은 1·2위만 쓰인다. 순위 계산은 "팀 목록을 받아 순위 배열 반환"으로 만들어 팀 수에 의존하지 않게 하고, 3위 이하 값은 CSV에만 둔다. (다인 대전을 계획 중이면 알려주세요.)
2. **대신전 보호 1안 vs 2안.** 1안(인접 칸 적 침입 시 중단)은 기존 이동/시야 판정만으로 가능. 2안(체력, 파괴 시 실패)은 "건물을 공격 대상으로" 하는 전투 확장이 필요. **권장: 1안으로 시작**하고 `GreatTempleData`에 체력 필드를 자리만 잡아 둔다.
3. **지형 변화 부작용.** 땅→물: 위 유닛·건물·구조물·도로 처리, 도시 칸 금지, 영토/수도 연결 재계산 필요. 산→땅, 물→산도 같은 재계산. **권장:** 도시·대신전·유닛이 있는 칸은 대상 불가, 건물/구조물은 제거, 변경 뒤 `CitySystem` 연결 재계산.
4. **원본 유닛 보존 방식.** `EntityWorld`는 엔티티 제거가 없으므로 원본을 `Dormant`로 두고 그리드·점유·점수·수용량에서 제외해야 한다. 점수/수용량/시야/AI 쿼리가 `Dormant`를 걸러내도록 `UnitQueries` 한 곳에서 처리.
5. **"신전 인접" vs "주변 8칸".** 원문이 같은 뜻으로 섞여 쓰이므로 체비쇼프 1(8칸)로 통일한다.
6. **대신전 타일 모양.** "빈 평지 4칸 이상"이 정사각 2×2인지 연결된 N칸인지 불분명. **권장:** 서로 인접한 연결 N칸(`Faith.GreatTempleMinTiles`).
7. **신 개수와 신화 유닛·펀치 목록**은 기획이 없어 `Gods.csv` 행은 샘플 2~3종만 만들고 구조를 검증한다. 실제 신 설계는 별도 기획 필요.
8. **얼음 신전**은 얼음 지형이 생길 때까지 계속 제외("원문과 다른 점"에 유지).

## 5. 위키와의 관계

신앙은 Polytopia 위키에 없는 원작 확장이다. 규칙 4에 따라 `docs/spec/game/faith.md`의 "원문과 다른 점"이 아니라 **"위키에 없는 확장"** 절을 두고 기존 신전(점수 전용) 규칙과 함께 정리한다.

## 6. 검증

- Editor 검증 클래스 `FaithVerification`을 추가해 `VerificationSuite`에 등록: 순위 수입, 최대치 클램프, 기도 방어 0·피격 해제, 펀치 5종, 강림→만료 복귀/사망 동시 제거, 대신전 중단·타이브레이크.
- 실행은 Unity CLI 배치모드(에디터가 열려 있으면 먼저 닫아야 함 — 규칙 1).
- 화면 확인은 기존 방식(Edit Mode + RenderTexture, `-nographics` 제외)으로 HUD·메뉴 스크린샷.
