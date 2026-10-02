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
1. **불러오기 / 내보내기**: [샌드박스 유닛 CSV](docs/spec/csv/units.md#샌드박스-유닛-csv)(불러오지 않으면 기본 [Units.csv](docs/spec/csv/units.md#unitscsv--육지-유닛)). **바이옴 불러오기**: [바이옴 CSV](docs/spec/csv/biomes.md). **기술 불러오기·내보내기**: [기술 단일 파일](docs/spec/csv/tech.md#techtreecsv--샌드박스-단일-파일-형식).
2. **맵 크기**, **습도 탭**: 맵 타입·물 비율 → 1차 지형 생성 → 바이옴·종족(아군/적) 선택 → **지형 생성**. 같은 땅 모양에 바이옴만 바꿔 다시 채울 수 있다.
3. 팔레트에서 유닛·팀을 골라 칸 클릭으로 배치(다시 클릭하면 제거) → **전투 시작** → **다시 시작**으로 복귀.

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
