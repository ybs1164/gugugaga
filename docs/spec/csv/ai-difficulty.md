# AI 난이도 CSV

규칙: [ai](../game/ai.md#난이도). 배열형 표(`Assets/Resources/Tables/`). 로더: `ArrayTableCsvSerializer.ParseAIDifficulties` → `GameTables.AIDifficulties`, 모델은 `GameDataLoader.LoadAIModels` → `PolicyModels.All`.

## AIDifficulty.csv — 난이도
파일: `Assets/Resources/Tables/AIDifficulty.csv`
한 행 = 난이도 하나. 행 순서가 시작 화면 난이도 버튼의 순서다(쉬운 것부터). 처음 고른 값은 [GameRules](game-rules.md) `AI.DefaultDifficulty`.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Index` | 정수 | O | | 배열 위치(저장 파일·`-recordDifficulty`가 가리킨다) |
| `Id` | 문자열 | O | | 이름표(중복 불가). 이름·설명은 [번역 표](strings.md#키-규칙) `AIDifficulty.<Id>.Name`/`.Desc` |
| `Model` | 문자열 | | 비움 | 모델 파일 이름(`Assets/Resources/AI/<Model>.txt`, 확장자 없이). 비우면 규칙 기반 AI. 파일이 없거나 틀리면 경고 후 규칙 AI |
| `Temperature` | 실수 ≥ 0 | | 0 | 후보 고르기의 무작위성. 0 = 늘 점수가 가장 높은 후보, 클수록 점수 차를 덜 믿고 고른다(softmax 온도) |
| `BlunderChance` | 0..1 | | 0 | 결정마다 이 확률로 가장 좋은 후보 대신 두 번째 후보를 둔다(어이없는 수가 아니라 덜 좋은 수) |
| `Note` | 메모 | | | 로더 무시. 측정한 실력(규칙 AI 상대 승률)을 적어 둔다 |

`Model`이 빈 행에서는 `Temperature`·`BlunderChance`를 쓰지 않는다(규칙 AI는 늘 같은 수를 둔다).

## 모델 파일
`Assets/Resources/AI/<Model>.txt` — 학습기(`tools/ai/train.py`)가 쓰는 텍스트. 표 팩·표 폴더로 바꿀 수 없다(빌드에 든 파일만). 줄 순서:

| 줄 | 내용 |
|---|---|
| `PolicyModel 1` | 형식 표시 |
| `layers <입력> <은닉…> 1` | 층 크기. 입력은 `PolicySystem.InputSize`와 같아야 한다 |
| `mean …`, `std …` | 입력 정규화(입력 수만큼) |
| `w …`, `b …` | 층마다 한 쌍: 가중치(`[출력][입력]` 행 우선), 편향 |

입력 특징의 뜻과 순서는 `PolicySystem.Global`·각 결정 지점(`EconomyAI`·`EnemyAI`)의 주석에 있다 — 특징을 바꾸면 모델을 다시 학습한다.
