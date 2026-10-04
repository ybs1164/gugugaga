# 스프라이트 CSV

파일: `Assets/Resources/Pixel2D/SpriteCatalog.csv`
한 행 = 스프라이트 레이어 하나. 같은 `VisualId`의 행들이 아래에서 위로 겹쳐 한 표시가 된다. 로더: `View/PixelSpriteCatalog`(View 계층 — `GameDataLoader`가 아님).
형식이 틀리면 경고가 아니라 **예외**가 난다(필수 칸, 색).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `VisualId` | 문자열 | O | | 표시 키 — 아래 접두사 규칙 |
| `Sheet` | 문자열 | O | | `Assets/Resources/Pixel2D/Sheets/<이름>.png`, 또는 `Pattern/<이름>`(아래 픽셀 패턴) |
| `Column`, `Row` | 정수 | | 0 | 시트의 16px 격자 좌표 |
| `Width`, `Height` | 정수 | | 16 | 잘라낼 픽셀 크기. `Pattern/Solid`는 칠할 사각형 크기(디테일 픽셀) |
| `X`, `Y` | 정수 | | 0 | 타일 중심에서 레이어 중심까지의 오프셋(디테일 픽셀, 위·오른쪽이 +) |
| `Scale` | 정수 | | 1 | 원본 1픽셀을 디테일 픽셀 몇 칸으로 그릴지. 1 이상 정수만 — 소수 배율은 없다 |
| `FlipX` | 0/1 | | 0 | 좌우 반전 |
| `Layer` | 정수 | | 0 | 같은 `VisualId` 안에서 그리는 순서(작은 값이 뒤) |
| `Color` | `RRGGBB` 또는 `Team` | | `FFFFFF` | 곱할 색. `Team`은 팀 색 |
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

## 합성 규칙
코드: `View/PixelSpriteComposer`, `View/TileVisuals`.
- 디테일 픽셀: 타일 한 칸 = `PixelSpriteComposer.Tile` 픽셀. 지면 타일(`Ground.*`, `Terrain.Cloud`)은 16px 칸을 2×2로 반복해 같은 격자에 맞춘다. 원본 16px 조각은 `Scale` 1이면 반 칸이다.
- 한 `VisualId`(또는 한 타일의 여러 표시)는 스프라이트 **한 장**으로 구워진다. 캔버스는 타일 한 칸 + 위쪽 여유(`PixelSpriteComposer.Headroom`) — 높은 그림은 윗칸으로 넘쳐도 잘리지 않는다. 아이콘(`Icon.*`)은 정사각 캔버스.
- 캔버스 밖으로 나가는 표시는 자르지 않고 표시 단위로 안쪽으로 밀어 넣는다. 밀어도 안 들어가면 진단 목록(`PixelSpriteComposer.Clipped`)에 남고 검증이 실패한다.
- 테두리: 레이어마다 원본 외곽선 띠(바깥에서 어두운 픽셀을 따라 `PixelSpriteComposer.Stroke`+1 픽셀 깊이까지 — Kenney 원본은 2~3px, 패턴은 외곽선 없이 저장)를 지운다. 완성된 실루엣 바깥에 같은 색(`PixelSpriteComposer.Outline`)·같은 두께(`PixelSpriteComposer.Stroke`)의 테두리를 **한 번만** 두르고, 앞 레이어가 뒤 레이어 그림을 덮는 곳에만 같은 두께의 구분선을 긋는다.
- 조각 사이 틈: 가로·세로로 `Stroke`의 2배 이하인 빈 틈은 `Stroke` 두께 이음선 하나만 남기고 나머지는 양쪽 그림으로 채운다 — 양쪽 테두리가 붙어 두꺼운 띠가 되지 않는다.
- 시트 칸은 그림 전체를 담아야 한다. 위 칸에 이어지는 그림(키 큰 나무 등)은 `Height`를 늘려 두 칸을 함께 자른다. 검증이 칸 윗줄에 일부만 칠해진 조각을 잡는다.

## 타일 표시 우선순위
타일 값은 [tile](../game/tile.md#타일-값). 한 칸에는 아래 중 **하나의 합성 스프라이트**만 그린다(위가 우선).
| 순위 | 조건 | 그리는 것 |
|---|---|---|
| 1 | 도시가 있음 | `City.Houses`(도시 레벨로 집이 늘어남) + 수도·성벽·공방·공원 표시 + `City.Flag` |
| 2 | `BuildingId` 있음 | `Building.<Id>` — 지형 장식·구조물은 그리지 않는다 |
| 3 | 지형 장식과 구조물이 둘 다 있음 | 지형 장식을 뒤(왼쪽 위)에, 구조물을 앞(오른쪽 아래)에 — 오프셋은 `TileVisuals` 상수 |
| 4 | 하나만 있음 | 그 표시를 가운데에 |

- 지형 장식: `TileTypeId`가 `Forest`·`Mountain`·`Rock`이면 `Terrain.<TileTypeId>`. 숲은 칸마다 좌우 반전을 섞는다.
- 기술로 숨긴 구조물은 없는 것으로 친다.
- 칸끼리·유닛·UI·구름과의 겹침 순서는 [tile](../game/tile.md#표시-순서).
- 배에 탄 유닛은 `Boat.<배 Id>` 위에 유닛을 올린 합성 한 장이다.

## UI 해상도
코드: `View/PixelUIScaler`, `View/PixelUISkin`. 그림 생성: `scripts/author_pixel_ui.py`.
- 모든 UI 픽셀 그림은 화면에서 한 픽셀이 `PixelUIScaler.TexelSize`(화면 높이 ÷ `PixelUIScaler.ReferenceHeight`, 반올림, 최소 1) 화면 픽셀이다 — 기본 줌의 월드 픽셀과 같은 크기. 캔버스 배율(`CanvasScaler`)이 정수가 아니어도 그렇다.
- 틀(`Sheets/Panel`, `Button`, `Badge`)은 9-slice(`Image.Type.Sliced`, 테두리 `PixelUIScaler.FrameBorder`)로 늘리고, 테두리는 월드와 같은 색·두께(`PixelSpriteComposer.Outline`, `Stroke`)다. 그림은 회색조라 `Image.color`로 칠한다.
- 아이콘(`Icon.*`)은 `Scale` 1로 합성하고 그린 픽셀만 남겨 자른 뒤, UI에서는 원래 픽셀 크기 그대로 놓는다(배치된 자리의 가운데 기준). 늘리거나 줄이지 않으므로 아이콘을 놓는 자리는 그 크기를 감안해 잡는다.
- 글자(Jua 폰트)는 픽셀 그림이 아니라 이 규칙 밖이다.
- 기술트리 열기 버튼은 자원 바 오른쪽에 놓아 점수 줄과 겹치지 않는다. 기술트리 배경은 뒤쪽 맵이 희미하게만 보이도록 표시하고, 상세 설명은 줄바꿈과 내용에 따른 높이 확장으로 해금 상태·버튼 위에 표시한다.

## 픽셀 패턴
새 그림은 `Assets/Resources/Pixel2D/Patterns/<이름>.txt`에 팔레트(`문자=RRGGBB`)와 픽셀 행으로 쓴다. 생성 도구: `scripts/author_pixel_patterns.py`, `Assets/Editor/Pixel2DSetup.cs`.
에셋 출처: [Pixel2DImplementation](../../Pixel2DImplementation.md).

## 레거시 3D 모델
파일: `Assets/Resources/Models/ModelPalette.csv`
파일: `Assets/Resources/Models/BuildingModels.csv`
파일: `Assets/Resources/Models/TerrainModels.csv`
파일: `Assets/Resources/Models/UnitModels.csv`

`GameDataLoader`가 아직 읽지만 **현재 화면에서는 쓰지 않는다**(2D 전환 전 로우폴리 모델). 형식은 `ModelCsvSerializer` 주석, 배경은 [history/ModelingPlan](../../history/ModelingPlan.md).
