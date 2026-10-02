# 과업 CSV

파일: `Assets/Resources/Tasks.csv`
한 행 = 과업 하나(달성하면 기념물 하나를 무료로 지을 수 있다). 키형(`Id`). 로더: `GameTableCsvSerializer.ParseTasks` → `TaskDefinition.All`. 규칙: [task-score](../game/task-score.md).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Id` | 문자열 | O | | 과업 키. 건물 CSV `Task`가 가리킨다 |
| `Kind` | 아래 표 | O | `Kills` | 달성 판정 방식(판정은 코드 `TaskSystem.IsMet`) |
| `Threshold` | 정수 | | 0 | 판정 기준값 — 뜻은 `Kind`마다 |
| `Unlock` | 해금 키 | | 없음 | 이 키를 가진 기술이 있어야 과업이 열린다 |
| `Wiki`, `Note` | 메모 | | | 로더 무시 |

## Kind
| 값 | 달성 조건 (`Threshold` 사용) |
|---|---|
| `TurnsWithoutAttack` | `Threshold` 턴 연속 공격하지 않음(반격은 공격 아님) |
| `StarsHeld` | 별 `Threshold` 이상을 한 번에 보유 |
| `AllLighthouses` | 맵의 등대 전부 발견 |
| `Kills` | 적 유닛 `Threshold` 기 처치 |
| `ConnectedCities` | 수도와 연결된 도시 `Threshold` 개 |
| `CityLevel` | 레벨 `Threshold` 이상 도시 보유 |
| `AllTech` | 기술 전부 연구 |
