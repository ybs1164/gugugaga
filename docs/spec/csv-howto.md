# CSV 고치는 법 (PC 배포본)

PC 배포 zip(`gugugaga-pc.zip`)을 받은 사람이 표를 고쳐 보는 순서. 각 단계의 규칙은 링크한 곳에만 있다.
zip 안의 폴더 구조는 저장소와 같다(`README.md`, `docs/`) — 이 문서의 링크는 zip을 푼 폴더에서도 열린다.

## 순서
1. zip을 풀고 `gugugaga.exe`를 실행한다(샌드박스 씬). 조작: [README 조작](../../README.md#조작).
2. 고칠 표를 고른다. 기본 표 사본은 exe 옆 `SandboxTables/`에 있다 — 원본은 손대지 말고 복사해서 고친다. 파일 목록과 기본 경로: [입력 목록](sandbox-input-csv/formats.csv).
3. 컬럼 뜻·허용값은 표마다 [`csv/<표>.md`](csv/), 한 파일만 보려면 [`sandbox-input-csv/<파일명>.spec.csv`](sandbox-input-csv/). 어떤 규칙이 어느 표에 있는지: [요소 지도](README.md#요소-지도).
4. 엑셀로 열고 저장할 때는 공통 형식을 지킨다(UTF-8 BOM, 한 칸에 한 값, `Index` 규칙): [CSV 공통 형식](csv-common.md).
5. 게임에서 읽힌다: 샌드박스 **CSV 데이터 설정** → 파일 선택 → **다시 읽기** → 지형 생성 → 전투 시작. 자세히: [README 샌드박스](../../README.md#샌드박스).
6. 잘못된 칸은 경고 후 기본값으로 읽힌다 — 경고 형식: [읽기 규칙](csv-common.md#읽기-규칙).
7. 휴대폰에서 같은 표로 하려면 **팩 내보내기** → 앱의 **표 가져오기**: [표 팩](game/session.md#표-팩).

## 참고
- 지난 빌드 이후 CSV 형식이 바뀐 점: [`../history/`](../history/)의 `CsvChanges-<날짜>.md`.
- 그림으로 보는 한 판: [ProjectLoop](../ProjectLoop.html).
