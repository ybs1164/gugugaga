# 스프라이트 CSV

파일: `Assets/Resources/Pixel2D/SpriteCatalog.csv`
한 행 = 스프라이트 레이어 하나. 같은 `VisualId`의 행들이 아래에서 위로 겹쳐 한 표시가 된다. 로더: `View/PixelSpriteCatalog`(View 계층 — `GameDataLoader`가 아님).
형식이 틀리면 경고가 아니라 **예외**가 난다(필수 칸, 색).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `VisualId` | 문자열 | O | | 표시 키 — 아래 접두사 규칙 |
| `Sheet` | 문자열 | O | | `Assets/Resources/Pixel2D/Sheets/<이름>.png`, 또는 `Pattern/<이름>`(아래 픽셀 패턴) |
| `Column`, `Row` | 정수 | | 0 | 시트의 16px 격자 좌표 |
| `Width`, `Height` | 정수 | | 16 | 잘라낼 픽셀 크기 |
| `X`, `Y` | 실수 | | 0 | 레이어 오프셋 |
| `ScaleX`, `ScaleY` | 실수 | | 1 | 레이어 배율 |
| `Layer` | 정수 | | 0 | 그리는 순서 |
| `Color` | `RRGGBB` | | `FFFFFF` | 곱할 색 |
| `MinLevel`, `MaxLevel` | 정수 | | 0, 최대 | 이 레벨 범위일 때만 그림(건물·도시 레벨) |
| `Note` | 메모 | | | 출처 등 |

## VisualId 접두사
| 접두사 | 대상 | 코드가 만드는 키 |
|---|---|---|
| `Ground.` | 지면 타일 | `Ground.<TileTypeId>`, 없으면 `Ground.Grass` |
| `Terrain.` | 숲·산·바위·구름 오버레이 | `Terrain.Cloud`(시야) 등 |
| `Structure.` | 구조물 | `Structure.<StructureId>` |
| `Building.` | 건물 | `Building.<건물 Id>` |
| `City.` | 도시 장식 | `City.Houses`, `City.Capital`, `City.Wall`, `City.Workshop`, `City.Park`, `City.Flag` |
| `Unit.` | 육지 유닛 | `Unit.<유닛 Id>`, 없으면 `BaseVisual`의 외형 |
| `Boat.` | 배 | `Boat.<배 Id>` |
| `Icon.` | 아이콘 | `Icon.<이름>`(기술 `Icon`, 행동 배지) |
| `UI.` | UI 도형 | `UI.Missing`(없는 키 대체), `UI.Solid`, `UI.Shadow` |

없는 키는 진단 로그와 함께 `UI.Missing`(물음표)으로 그린다.

## 픽셀 패턴
새 그림은 `Assets/Resources/Pixel2D/Patterns/<이름>.txt`에 팔레트(`문자=RRGGBB`)와 픽셀 행으로 쓴다. 생성 도구: `scripts/author_pixel_patterns.py`, `Assets/Editor/Pixel2DSetup.cs`.
격자·외곽선·합성 규칙과 에셋 출처: [Pixel2DImplementation](../../Pixel2DImplementation.md).

## 레거시 3D 모델
파일: `Assets/Resources/Models/ModelPalette.csv`
파일: `Assets/Resources/Models/BuildingModels.csv`
파일: `Assets/Resources/Models/TerrainModels.csv`
파일: `Assets/Resources/Models/UnitModels.csv`

`GameDataLoader`가 아직 읽지만 **현재 화면에서는 쓰지 않는다**(2D 전환 전 로우폴리 모델). 형식은 `ModelCsvSerializer` 주석, 배경은 [history/ModelingPlan](../../history/ModelingPlan.md).
