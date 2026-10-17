# 스프라이트 CSV

파일: `Assets/Resources/Pixel2D/SpriteCatalog.csv` — `scripts/author_pixel_patterns.py`가 패턴과 함께 **생성**한다. 기본 표는 스크립트를 고쳐 다시 만든다(손으로 고친 행은 다음 생성 때 덮인다).
한 행 = 스프라이트 레이어 하나. 같은 `VisualId`의 행들이 아래에서 위로 겹쳐 한 표시가 된다. 로더: `View/PixelSpriteCatalog`(View 계층 — `GameDataLoader`가 아님).
Sandbox의 `Pixel2D/SpriteCatalog.csv` 항목으로 다른 파일을 선택할 수 있다. 선택 파일은 실제 파싱하고, 오류가 나면 이전 선택으로 복원한다. 월드 표시는 지형을 다시 생성할 때 갱신된다. 시트·패턴 이미지 자체는 프로젝트 Resources를 사용한다. 형식이 틀리면 경고가 아니라 **예외**가 난다(필수 칸, 색).

| 컬럼 | 타입 | 필수 | 기본 | 뜻 |
|---|---|---|---|---|
| `VisualId` | 문자열 | O | | 표시 키 — 아래 접두사 규칙 |
| `Sheet` | 문자열 | O | | `Pattern/<이름>`(아래 픽셀 패턴), 또는 `Assets/Resources/Pixel2D/Sheets/<이름>.png`(`Units` = 아래 유닛 시트, 나머지는 UI 틀) |
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
| `Ground.` | 지면 타일 | `Ground.<TileTypeId>`(숲·산 칸은 그 아래 맨땅 `Ground.<GroundTileId>`), 없으면 `Ground.Grass` |
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
- 디테일 픽셀: 타일 한 칸 = `PixelSpriteComposer.Tile` 픽셀. 지면 타일(`Ground.*`, `Terrain.Cloud`)은 그 행의 `Scale`로 키운 뒤 한 칸이 찰 때까지 반복한다.
- 그림 해상도: 모든 그림이 유닛 팩과 같은 한 칸 32픽셀, `Scale` 1이다. 패턴은 생성기에서 16픽셀 밑그림을 2배로 키운 뒤 팩처럼 1픽셀 음영(위·왼쪽 실루엣은 밝게, 아래·오른쪽과 색 경계 아래는 어둡게, 한 칸짜리 점은 그대로)을 넣어 저장한다. 패턴 `X`·`Y`는 생성기가 짝수 오프셋으로 계산한다(밑그림 격자 유지).
- 한 `VisualId`(또는 한 타일의 여러 표시)는 스프라이트 **한 장**으로 구워진다. 캔버스는 타일 한 칸 + 위쪽 여유(`PixelSpriteComposer.Headroom`) — 높은 그림은 윗칸으로 넘쳐도 잘리지 않는다. 아이콘(`Icon.*`)은 정사각 캔버스.
- 캔버스 밖으로 나가는 표시는 자르지 않고 표시 단위로 안쪽으로 밀어 넣는다. 밀어도 안 들어가면 진단 목록(`PixelSpriteComposer.Clipped`)에 남고 검증이 실패한다.
- 테두리: 레이어마다 원본 외곽선 띠(바깥에 닿은 어두운 픽셀, `PixelSpriteComposer.Stroke` 픽셀 깊이까지 — 유닛 시트 원본의 1px 검은 외곽선)를 지운다 — 패턴은 외곽선 없이 저장하고, 실루엣에 닿는 칸에는 어두운 색(밝기 0.26 미만, 눈 같은 안쪽 점 전용)을 쓰지 않는다. 완성된 실루엣 바깥에 같은 색(`PixelSpriteComposer.Outline`)·같은 두께(`PixelSpriteComposer.Stroke`)의 테두리를 **한 번만** 두르고, 앞 레이어가 뒤 레이어 그림을 덮는 곳에만 같은 두께의 구분선을 긋는다.
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
- 배에 탄 유닛은 `Boat.<배 Id>`만 그린다(배 그림이 한 칸을 채운다). 팀 색은 배에도 들어간다.

## UI 해상도
코드: `View/UiScale`, `View/PixelUIScaler`. 그림 생성: `scripts/author_pixel_ui.py`.
- 캔버스 배율(UI 1단위 = 화면 몇 픽셀, `UiScale.ScaleFactor`)은 "1280x720 화면에 맞춘 배율"(가로·세로 비의 기하평균)과 "실제 크기 배율"(dpi ÷ 160, 1단위 ≈ 1dp) 중 큰 값이다. PC 모니터는 앞의 값(1080p = 1.5), 휴대폰은 뒤의 값이 이긴다. dpi를 모르면 96으로 본다. 모든 HUD 캔버스가 이 규칙 하나를 쓴다.
- 캔버스 바로 아래 `Safe` 자식은 안전 영역(`Screen.safeArea`)에 맞춘다 — HUD 화면은 모두 그 안에 놓인다.
- 모든 UI 픽셀 그림은 화면에서 한 픽셀이 `UiScale.TexelSize`(배율 × 720/540, 반올림, 최소 1) 화면 픽셀이다 — 배율이 커지면 틀·아이콘도 같이 커진다. 캔버스 배율이 정수가 아니어도 그렇다.
- 틀(`Sheets/Panel`, `Button`, `Badge`)은 9-slice(`Image.Type.Sliced`, 테두리 `PixelUIScaler.FrameBorder`)로 늘리고, 테두리는 월드와 같은 색·두께(`PixelSpriteComposer.Outline`, `Stroke`)다. 그림은 회색조라 `Image.color`로 칠한다.
- 아이콘(`Icon.*`)은 18×18 이하 패턴으로 합성하고(캔버스는 그림 크기에 맞춘다) 그린 픽셀만 남겨 자른 뒤, UI에서는 원래 픽셀 크기 그대로 놓는다(배치된 자리의 가운데 기준). 늘리거나 줄이지 않으므로 아이콘을 놓는 자리는 그 크기를 감안해 잡는다.
- 글자(Jua 폰트)는 픽셀 그림이 아니라 이 규칙 밖이다.
- 프로젝트는 선형 색 공간이라 어두운 반투명 상자가 알파 값보다 훨씬 비쳐 보인다 — 글을 읽는 패널·창 배경은 불투명하게 칠한다(`UiKit.PanelColor`). 화면 배치는 [session](../game/session.md#화면-배치).

## 유닛 시트
`Sheets/Units.png`는 `scripts/author_pixel_patterns.py`가 서드파티 유닛 팩 원본(`Assets/Art/Pixel2D/ThirdParty/b03AllUnits/`)에서 잘라 만든다. 어느 팩 줄을 어느 키에 쓰는지는 스크립트의 `REFERENCE` 표에 있고, 카탈로그 `Note`에 팩 줄 번호가 남는다.
- 유닛·배 전부와 마을·수도·도시 집(레벨별)·성벽·신전·탑·항구·시장·대장간·대사관, 동물·금속·광석 자원이 이 시트를 쓴다. 지형·나머지 자원과 건물·아이콘은 위 음영 규칙을 거친 패턴이다.
- 팩의 10열은 팀 색 바꿈이다. 빨강 열과 파랑 열이 다른 픽셀이 팀 픽셀(회색조, 아래 팀 픽셀과 같은 표시)이고, 나머지는 회색 열 그대로다. 아군·적 색(`UnitView.PlayerColor`·`EnemyColor`)은 이 회색조에 곱해 팩의 파랑·빨강이 되도록 맞춘 값이다.
- 시트 칸은 32×48이고 그림은 칸 아래 32픽셀에 선다(`Y` +8). 팩에서 윗칸과 나눠 쓰던 꼭대기 외곽선 줄도 함께 옮긴다. 팩의 이웃 칸에서 넘어온 작은 조각은 버린다.

## 픽셀 패턴
새 그림은 `Assets/Resources/Pixel2D/Patterns/<이름>.txt`에 팔레트(`문자=RRGGBB`)와 픽셀 행으로 쓴다. 팔레트 값 `@RRGGBB`는 **팀 픽셀**이다 — 합성할 때 그 행의 `Color` 대신 팀 색을 곱한다(유닛 투구·두건·돛, 도시 지붕·깃발). 유닛 시트의 팀 픽셀도 같은 표시(알파 `PixelSpriteCatalog.TeamAlpha`)다. 팀 색은 유닛(`UnitView`, 아군·적 색)과 도시·건물(소유 팀) 합성에 넘어가고, 팀이 없으면 회색조 그대로 보인다. 생성 도구: `scripts/author_pixel_patterns.py`(패턴 + 카탈로그), `Assets/Editor/Pixel2DSetup.cs`(파생 Sprite·프리팹).
에셋 출처: [Pixel2DImplementation](../../Pixel2DImplementation.md).

## 레거시 3D 모델
[보관된 모델 형식](models.md). 현재 게임 로더와 Sandbox에서는 읽지 않는다. 배경은 [history/ModelingPlan](../../history/ModelingPlan.md).
