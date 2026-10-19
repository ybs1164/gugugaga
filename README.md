# gugugaga

Unity 6000.3.20f1 턴제 전술·경제 프로토타입(탑다운 픽셀 2D).

## 문서
| 무엇 | 어디 |
|---|---|
| 게임 규칙·CSV 형식 (**진입점**) | [`docs/spec`](docs/spec/README.md) |
| PC 배포본에서 CSV 고치는 순서 | [`docs/spec/csv-howto.md`](docs/spec/csv-howto.md) |
| 작업 규칙 | [`CLAUDE.md`](CLAUDE.md) |
| 위키 조사 요약 | [`docs/reference`](docs/reference/) |
| 지난 설계 기록·작업 로그, CSV 변경 정리 | [`docs/history`](docs/history/) |
| 2D 전환 기록과 화면 | [`docs/Pixel2DImplementation.md`](docs/Pixel2DImplementation.md), [`docs/Pixel2DMigrationPlan.md`](docs/Pixel2DMigrationPlan.md) |
| 사진으로 보는 전체 루프 | [`docs/ProjectLoop.html`](docs/ProjectLoop.html) |

## 실행
- `Assets/Scenes/SampleScene.unity`: 데모 전투. `Assets/Scenes/Sandbox.unity`: 샌드박스. `Assets/Scenes/Mobile.unity`: 빠른 한 판(시작 화면 → 이어하기/새로하기·종족·신·맵 크기 고르기, 자동 저장 — [한 판 흐름](docs/spec/game/session.md)).
- 모바일 씬 (다시) 만들기: `unity run . -- -logFile Logs/mobile_scene.log -executeMethod TacticsECS.EditorTools.MobileSceneSetup.Generate` — `Sandbox.unity`를 복제해 빠른 한 판으로 바꾼다.
- PC 빌드(`Sandbox.unity`만, Windows 개발 빌드 → `Builds/Pixel2D/gugugaga.exe`, 기본 표 사본 `Builds/Pixel2D/SandboxTables/`): `unity run . -- -logFile Logs/pc_build.log -executeMethod TacticsECS.EditorTools.Pixel2DBuild.Run` — 배포용 zip 하나 `Builds/gugugaga-pc.zip`도 함께 만든다(플레이어·기본 표 사본·라이선스·`README.md`·`docs/` 전체, 받는 사람의 시작 문서는 [CSV 고치는 법](docs/spec/csv-howto.md)).
- 안드로이드 빌드(`Mobile.unity`만, 개발 빌드 APK. 출시용은 `--args "-release"`. 에디터에 Android Build Support 모듈이 있어야 한다): `unity build --target Android --execute-method TacticsECS.EditorTools.MobileBuild.Run -o Builds/Android/gugugaga.apk` — 패키지 이름·IL2CPP·ARM64·자동 회전(가로·세로)은 빌드 때마다 `MobileBuild`가 적용한다.
- 모바일 화면 점검 캡처(휴대폰 가로 2400x1080·세로 1080x2400(400dpi)과 PC 1920x1080 세 벌: 시작 화면·종족 고르기·전투·유닛·수도·기술 트리·신앙·메뉴·결과, 화면 필요. 이어하기 파일을 덮어쓴다): `unity run . -- -logFile Logs/mobile_capture.log -executeMethod TacticsECS.EditorTools.MobileCapture.Run` → `Logs/mobile_capture/`
- 모바일 씬 검증(시작 화면·종족 고르기, 가상 터치로 끌기·핀치·탭, 턴 종료 자동 저장, 메인 화면 → 이어하기, 기술 트리 끌기·핀치, 새 게임. 이어하기 파일을 지운다). Play 모드를 쓰므로 `unity run`이 아니라 에디터 실행 파일을 `-quit` 없이 부른다(검증이 끝나면 스스로 종료): `"<Unity 설치 경로>/Editor/Unity.exe" -batchmode -projectPath . -logFile Logs/mobile_play.log -executeMethod TacticsECS.EditorTools.MobilePlayVerification.Run`
- 전체 검증:

```
unity run . -- -nographics -logFile Logs/verify_all.log -executeMethod TacticsECS.EditorTools.VerificationSuite.Run
```

- 신앙 AI 대전 시뮬레이션(화면 없이 AI끼리 24판, 불변식 검사, 약 5분): `unity run . -- -nographics -logFile Logs/faith_sim.log -executeMethod TacticsECS.EditorTools.FaithSimulation.Run` → `Logs/FaithSimulation/`
- 신앙 UI 상태별 캡처(화면 필요, `-nographics` 금지): `unity run . -- -logFile Logs/faith_ui.log -executeMethod TacticsECS.EditorTools.FaithUiCapture.Run` → `docs/images/faith/`
- 유닛 행동 GIF(데모 씬에서 고르기·이동·공격·처치·대기·적 턴을 실제로 눌러 찍는다, 화면 필요): `"<Unity 설치 경로>/Editor/Unity.exe" -batchmode -projectPath . -logFile Logs/action_ux.log -executeMethod TacticsECS.EditorTools.ActionUxCapture.Run` → `Logs/action_ux/frames`, 이어서 `python scripts/make_action_ux_gif.py` → `docs/images/ux/action_ux.gif`
- 유닛 스킬 화면 점검 캡처((!) 표시·중독 배지·얼음·침수 칸·침수/얼음 깨기/상륙/약탈 버튼·폭우 공격 칸, 화면 필요, `-nographics` 금지): `unity run . -- -logFile Logs/unit_skills_capture.log -executeMethod TacticsECS.EditorTools.UnitSkillsCapture.Run` → `Logs/unit_skills_capture/`
- 사용 전에 Unity 에디터를 닫는다(같은 프로젝트가 열려 있으면 실패한다).

## 조작
- 좌클릭(놓을 때 처리): 아군 선택 → 파란 칸 이동 / 빨간 칸 공격. 다른 칸·유닛은 오른쪽 정보 패널(정보 + 할 수 있는 행동 목록). 같은 칸을 다시 누르면 유닛 ↔ 칸 전환. 패널의 X는 선택 해제.
- 오른쪽 아래 **턴 종료**. 상단 막대 **메뉴**·**기술트리**·신앙. 화면 배치: [session](docs/spec/game/session.md#화면-배치).
- 카메라: `WASD`/방향키 이동, 휠 줌, 끌기(마우스 왼쪽·한 손가락) 이동, 두 손가락 핀치 줌.
- 기술 트리: 끌어서 나무를 옮기고 휠·두 손가락 핀치로 확대한다.
- 터치: 탭 = 좌클릭(손가락을 조금 움직이면 끌기로 본다). 패시브·버튼 설명은 누르고 있는 동안 왼쪽 아래에 보인다.
- `Esc`(안드로이드 뒤로 가기): 열린 것부터 닫고(대화 상자 → 신앙 펀치 → 기술 트리 → 정보 패널), 없으면 메뉴.
- 신앙: 상단 막대의 신앙을 누르거나 `F` — 신앙 펀치 목록. 자세한 규칙은 [신앙](docs/spec/game/faith.md).
- 언어: 메뉴(또는 시작 화면)의 **언어** — 한국어 ↔ English(화면을 다시 연다) — [번역 표](docs/spec/csv/strings.md).

## 샌드박스
1. 왼쪽 위 **CSV 데이터 설정** 목록에서 바꿀 CSV의 **파일 선택**을 누르고 탐색기로 파일을 고른다. 선택하지 않은 항목은 `Assets/Resources` 기본 CSV를 쓴다. 파일을 수정한 뒤 **다시 읽기**, 항목별 **기본 파일** 또는 전체 **기본 표**로 복원한다. 선택은 같은 실행에서 언어 변경·다시 시작 후에도 유지된다. [입력 규칙](docs/spec/csv-common.md#파일), [파일별 CSV 명세](docs/spec/sandbox-input-csv/formats.csv).
2. **맵 크기**, **습도 탭**(맵 타입·물 비율, **1차 지형 생성**으로 육지·물 아웃라인만 미리 확인), **종족 · 신** 탭(아군/적 종족·아군/적 신) → **지형 생성**. 두 탭은 툴바 오른쪽 같은 자리에 하나씩 열린다. 영역 지형은 그 팀 종족의 `TribeTerrain.csv` 묶음이 정한다 — [영역 지형](docs/spec/game/tribe.md#영역-지형). **지형 생성**은 누를 때마다 육지·물 모양과 수도 위치부터 새로 만든다.
3. **전투 시작**: CSV의 종족 시작 조건에 따라 유닛을 배치한다(종족 미선택 시 기본 시작 유닛). **다시 시작**으로 설정에 복귀한다.
4. **팩 내보내기**: 지금 쓰는 표 전부를 zip 하나로 저장한다. 그 zip을 휴대폰에 보내고(메신저·드라이브 등), 설치된 앱의 시작 화면 **표 가져오기**로 고르면 같은 값으로 플레이한다. 양쪽에 보이는 팩 ID가 같으면 같은 표다 — [표 팩](docs/spec/game/session.md#표-팩).

## 폴더
| 경로 | 내용 |
|---|---|
| `Assets/Scripts/TacticsECS/Core`, `Data` | 데이터 타입 |
| `…/Systems`, `…/Systems/Csv` | 게임 로직, CSV 로더 |
| `…/Actions` | 유닛 행동 |
| `…/View`, `…/BattleController.cs` | 표시, 입력 → System → View 조율 |
| `Assets/Prefabs/Units` | 유닛 외형 프리팹(`BaseVisual` — 스탯은 [Units.csv](docs/spec/csv/units.md)) |
| `Assets/Prefabs/UI` | HUD 화면 프리팹(원본 — 고칠 때는 프리팹을 고친다), 공용 부품 `Elements`, 목록 줄·카드 `Items` |
| `Assets/Editor` | CLI용 에셋 생성·검증 |
| `Assets/Resources` | 게임 데이터 CSV |
| `scripts` | 픽셀 패턴 생성, spec 검사 |
