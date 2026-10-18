# 표 팩

흐름(내보내기·가져오기·저장): [session](../game/session.md#표-팩). 코드: `TablePackSystem`(쓰기·검증·풀기), `View/TablePackPicker`(파일 고르기).

## zip 구조
- Sandbox가 다루는 표 전부(`GameDataLoader.SandboxCsvPaths`)를 `Assets/Resources`와 같은 경로로 담는다. 예: `Tables/Units.csv`, `Strings.csv`, `Pixel2D/SpriteCatalog.csv`.
- 각 표는 내보낼 때 실제로 쓰던 원문 그대로다(고른 파일 → 표 폴더 → 기본 표 순서로 찾은 것, [읽기 규칙](../csv-common.md)). UTF-8(BOM)으로 쓴다.
- 맨 위에 매니페스트 `Pack.csv`가 있다.

## Pack.csv — 매니페스트
한 행 = 메타 값 하나 또는 표 하나.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Kind` | `Meta` / `File` | O | | 행 종류 |
| `Key` | 문자열 | O | | `Meta`: `Format`·`Name`·`BuildVersion`. `File`: 표의 Resources 경로(확장자 없음, 예 `Tables/Units`) |
| `Value` | 문자열 | | | `Meta`: 그 값. `File`: 그 표 파일 바이트의 SHA-256(16진수) |

- `Format`: 매니페스트 형식 번호(`TablePackSystem.Format`). 앱이 읽을 수 있는 번호보다 크면 가져오지 않는다.
- `Name`: 내보낸 파일 이름(확장자 제외). `BuildVersion`: 내보낸 빌드의 `Application.version`.
- 팩 ID: `File` 행 전부(경로·해시)로 만든 해시의 앞 8자리. 같은 표면 같은 ID다. PC 상태 줄과 휴대폰 시작 화면에 보인다.
