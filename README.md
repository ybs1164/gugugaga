# gugugaga

Unity 6000.3.20f1 턴제 전술·경제 프로토타입(탑다운 픽셀 2D).

## 문서
| 무엇 | 어디 |
|---|---|
| 게임 규칙·CSV 형식 (**진입점**) | [`docs/spec`](docs/spec/README.md) |
| 작업 규칙 | [`CLAUDE.md`](CLAUDE.md) |
| 위키 조사 요약 | [`docs/reference`](docs/reference/) |
| 지난 설계 기록·작업 로그 | [`docs/history`](docs/history/) |
| 2D 전환 기록과 화면 | [`docs/Pixel2DImplementation.md`](docs/Pixel2DImplementation.md), [`docs/Pixel2DMigrationPlan.md`](docs/Pixel2DMigrationPlan.md) |
| 사진으로 보는 전체 루프 | [`docs/ProjectLoop.html`](docs/ProjectLoop.html) |

## 실행
- `Assets/Scenes/SampleScene.unity`: 데모 전투. `Assets/Scenes/Sandbox.unity`: 샌드박스. 빌드: `Builds/Pixel2D/gugugaga.exe`.
- 전체 검증:

```
unity run . -- -nographics -logFile Logs/verify_all.log -executeMethod TacticsECS.EditorTools.VerificationSuite.Run
```

## 조작
- 좌클릭: 아군 선택 → 파란 칸 이동 / 빨간 칸 공격. 다른 칸·유닛은 정보 패널·건설 메뉴. 같은 칸을 다시 누르면 유닛 ↔ 칸 전환.
- 우하단 버튼: 행동, 선택 해제, 턴 종료(마우스를 올리면 설명).
- 카메라: `WASD`/방향키 이동, 휠 줌.
- 우상단 언어 버튼: 한국어 ↔ English(화면을 다시 연다) — [번역 표](docs/spec/csv/strings.md).

## 샌드박스
1. 왼쪽 위 **CSV 데이터 설정** 목록에서 바꿀 CSV의 **파일 선택**을 누르고 탐색기로 파일을 고른다. 선택하지 않은 항목은 `Assets/Resources` 기본 CSV를 쓴다. 파일을 수정한 뒤 **다시 읽기**, 항목별 **기본 파일** 또는 전체 **기본 표**로 복원한다. 선택은 같은 실행에서 언어 변경·다시 시작 후에도 유지된다. [입력 규칙](docs/spec/csv-common.md#파일), [파일별 CSV 명세](docs/spec/sandbox-input-csv/formats.csv).
2. **맵 크기**, **습도 탭**: 맵 타입·물 비율 → 바이옴·종족(아군/적) 선택 → **지형 생성**. 바이옴 드롭다운에서 바이옴을 누를 때마다 선택에 넣고 빼며(여러 개 가능), 고른 바이옴만으로 지형을 만든다. 아무것도 고르지 않으면 자동(전체 바이옴, 종족이 있으면 종족 바이옴). 누를 때마다 육지·물 모양과 수도 위치부터 새로 생성한다. **1차 지형 생성**은 육지·물 아웃라인만 미리 확인할 때 사용한다.
3. **전투 시작**: CSV의 종족 시작 조건에 따라 유닛을 배치한다(종족 미선택 시 기본 시작 유닛). **다시 시작**으로 설정에 복귀한다.

## 폴더
| 경로 | 내용 |
|---|---|
| `Assets/Scripts/TacticsECS/Core`, `Data` | 데이터 타입 |
| `…/Systems`, `…/Systems/Csv` | 게임 로직, CSV 로더 |
| `…/Actions` | 유닛 행동 |
| `…/View`, `…/BattleController.cs` | 표시, 입력 → System → View 조율 |
| `Assets/Editor` | CLI용 에셋 생성·검증 |
| `Assets/Resources` | 게임 데이터 CSV |
| `scripts` | 픽셀 패턴 생성, spec 검사 |
