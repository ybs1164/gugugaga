# gugugaga

Unity 6000.3.20f1 기반 턴제 전술·경제 프로토타입. 게임 규칙의 기준은 [The Battle of Polytopia Wiki](https://polytopia.fandom.com/wiki/The_Battle_of_Polytopia_Wiki)다.
화면은 CC0 픽셀 에셋 기반 탑다운 2D다.

## 문서
| 무엇 | 어디 |
|---|---|
| 게임 규칙·CSV 형식 (현재 상태, **진입점**) | [`docs/spec`](docs/spec/README.md) |
| 작업 규칙 | [`CLAUDE.md`](CLAUDE.md) |
| 위키 조사 요약 | [`docs/reference`](docs/reference/) |
| 지난 설계 기록·작업 로그 | [`docs/history`](docs/history/), git 커밋 메시지 |
| 2D 전환 기록과 화면 | [`docs/Pixel2DImplementation.md`](docs/Pixel2DImplementation.md), [`docs/Pixel2DMigrationPlan.md`](docs/Pixel2DMigrationPlan.md) |
| 사진으로 보는 전체 루프 | [`docs/ProjectLoop.html`](docs/ProjectLoop.html) |

정보는 한 곳에만 둔다 — 규칙은 [`docs/spec/README.md`](docs/spec/README.md#정보는-한-곳에만-단일-출처).

## 실행
- `Assets/Scenes/SampleScene.unity`: 데모 전투.
- `Assets/Scenes/Sandbox.unity`: 샌드박스(맵 생성·유닛 배치·CSV 불러오기). 빌드: `Builds/Pixel2D/gugugaga.exe`.
- Unity 작업(빌드·검증·에셋 생성)은 CLI로 한다 — `CLAUDE.md` 규칙 1. 전체 검증:

```
unity run . -- -nographics -logFile Logs/verify_all.log -executeMethod TacticsECS.EditorTools.VerificationSuite.Run
```

## 조작
- 좌클릭: 아군 유닛 선택 → 파란 칸 이동 / 빨간 칸의 적 공격. 적·건물·빈 칸은 정보 패널·건설 메뉴. 같은 칸을 다시 누르면 유닛 ↔ 칸 포커스 순환.
- 우하단 버튼: 선택 유닛의 행동, 선택 해제, 턴 종료. 버튼 위에 마우스를 올리면 설명.
- 카메라: `WASD`/방향키 이동, 마우스 휠 줌.

## 샌드박스
1. **불러오기 / 내보내기**: 유닛 CSV([형식](docs/spec/csv/units.md)). 기본은 루트의 `SandboxUnits.csv`.
2. **바이옴 불러오기 / 기술 불러오기·내보내기**: [바이옴](docs/spec/csv/biomes.md), [기술 단일 파일](docs/spec/csv/tech.md#techtreecsv--샌드박스-단일-파일-형식).
3. **맵 크기**, **습도 탭**: 맵 타입·물 비율 → 1차 지형 생성 → 바이옴·종족(아군/적) 선택 → **지형 생성**. 같은 땅 모양에 바이옴만 바꿔 다시 채울 수 있다.
4. 팔레트에서 유닛과 팀을 골라 칸을 클릭해 배치(다시 클릭하면 제거) → **전투 시작**. 끝나면 **다시 시작**으로 배치 화면 복귀.

## 코드 구조
- `Assets/Scripts/TacticsECS/Core`, `Data`: 값만 갖는 타입(`CLAUDE.md` 규칙 2). `EntityWorld`가 엔티티-컴포넌트 저장소, `GridWorld`가 타일 저장소.
- `Systems`: 상태 없는 정적 로직(규칙 3). `Systems/Csv`: CSV 로더.
- `Actions`: 유닛 행동 — 실행 가능 여부와 효과를 행동이 스스로 판단.
- `View`: 표시 전용. `BattleController`: 입력 → System 호출 → View 갱신.
- `Assets/Editor`: CLI 전용 에셋 생성·검증 스크립트.
