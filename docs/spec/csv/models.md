# 모델 CSV

보관된 3D 모델 형식이다. 현재 게임 로더와 Sandbox 입력 목록에서는 사용하지 않는다. 파서·에디터 도구용 자료로 남겨 둔다.

## 색상 팔레트
파일: `Assets/Resources/Models/ModelPalette.csv`

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Name` | 문자열 | O | | 다른 모델 파일의 Color가 참조할 팔레트 이름 |
| `Color` | HTML 색상 | O | | #RRGGBB; ColorUtility.TryParseHtmlString으로 읽음 |
| `Note` | 메모 | | | 로더 무시 |

## 모델 부품
파일: `Assets/Resources/Models/UnitModels.csv`

한 행 = 모델 부품 하나. Model이 비면 바로 위에서 지정한 모델에 붙는다. 첫 부품에는 Model을 지정해야 한다. 공통 CSV 규칙과 달리 첫 칸이 빈 행도 읽는다.

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `Model` | 문자열 | 첫 부품 O | 앞 행의 Model | 모델 Id |
| `Shape` | Box/Gable/Pyramid/Cone/Cylinder/Tapered/Sphere/Frustum | O | Box | 부품 도형 |
| `X`, `Y`, `Z` | 실수 | | 0 | 부품 중심 좌표 |
| `SX`, `SY` | 실수 | | 1 | 부품 가로·높이 |
| `SZ` | 실수 | | SX | 부품 깊이 |
| `RX`, `RY`, `RZ` | 실수 | | 0 | 부품 회전 각도 |
| `Color` | 팔레트 이름 또는 HTML 색상 또는 Team | | #FFFFFF | ModelPalette.Name 참조; Team은 팀 색상 |
| `MinLevel` | 정수 | | 0 | 표시할 최소 레벨 |
| `MaxLevel` | 정수 | | 0 | 표시할 최대 레벨; 0은 상한 없음 |
| `Note` | 메모 | | | 로더 무시 |

파일: `Assets/Resources/Models/BuildingModels.csv`
컬럼 상속: `UnitModels.csv`

건물 모델 부품. 같은 컬럼을 사용한다.

파일: `Assets/Resources/Models/TerrainModels.csv`
컬럼 상속: `UnitModels.csv`

지형 모델 부품. 같은 컬럼을 사용한다.
