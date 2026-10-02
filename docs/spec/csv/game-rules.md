# 게임 규칙 CSV

파일: `Assets/Resources/GameRules.csv`
한 행 = 스칼라 규칙 값 하나. 키형(`Key`). 로더: `GameRulesCsvSerializer.Apply` → [`GameRules`](../../../Assets/Scripts/TacticsECS/Data/GameRules.cs)의 정적 필드.
각 규칙의 뜻은 그 행의 `Description`에 있다 — 여기에 다시 적지 않는다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Key` | `도메인.이름` | O | | `GameRules`의 중첩 클래스(도메인)와 필드(이름) |
| `Value` | 필드 타입 | O | | 정수/실수/불리언/문자열/열거형 |
| `Wiki` | 문자열 | | | 위키 원문 값. 비우면 프로젝트 고유 규칙 |
| `Description` | 문자열 | | | 규칙 설명(단일 출처) |
| `Note` | 문자열 | △ | | `Value`≠`Wiki`이거나 `Wiki`가 비었으면 **필수** — 원문과 다른 이유 |

## 검사
- 모르는 키, 읽을 수 없는 값, `GameRules`에 있는데 CSV에 없는 필드는 경고(없는 필드는 코드 기본값).
- `Note`가 필요한데 비었으면 경고 — `CLAUDE.md` 규칙 4를 데이터로 강제한다.

## 규칙 추가
`GameRules`에 `public static` 필드 하나 + CSV 행 하나. 로더는 리플렉션이라 고치지 않는다.
