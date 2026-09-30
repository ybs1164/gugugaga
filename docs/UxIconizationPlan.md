# UX 개선 계획: 설명문을 아이콘/그림으로 대체 (Polytopia UI 레퍼런스)

작성일 2026-09-30. 같은 날 1~5단계 대부분과 모바일 레이아웃을 구현했다 — 구현 상태는 맨 아래 [9. 구현 상태](#9-구현-상태-2026-09-30)와 [10. 모바일 레이아웃](#10-모바일-레이아웃).

## 0. 문제 요약

지금 화면은 정보를 대부분 **한글 문장**으로 전달한다. 도시 메뉴 본문 한 줄에 "아군 도시 · 인구 2/3 · 유닛 1/3 · 골드 +3/턴 · 영토 반경 1 · 수도 · 수도 연결 · 공방"처럼 여덟 가지 값이 문장으로 이어지고, 훈련 버튼마다 "체력 10 · 공격 2 · 이동 1"이 붙으며, 버튼이 비활성인 이유도 "기술 필요", "자기 영토 안에서만" 같은 문장으로 나온다.
Polytopia는 같은 정보를 **아이콘+숫자**, **칸이 나뉜 게이지**, **색/잠금 표시**로 보여주고, 문장은 길게 누르거나 정보 버튼을 눌렀을 때만 띄운다.
이 계획은 그 방식을 이 프로젝트의 uGUI HUD(`BattleHud`/`ActionMenuHud`/`CityResourceHud`/`TechTreeHud`/`SandboxHud`)에 적용하는 순서를 정리한다.

## 1. Polytopia UI에서 가져올 원칙

| # | Polytopia에서 하는 방식 | 이 프로젝트에 적용할 규칙 |
| --- | --- | --- |
| P1 | 상단 바에 점수, 별(+수입), 턴을 **아이콘+숫자**로만 표시하고 라벨 단어는 쓰지 않는다 | 자원이나 능력치 앞에 "골드", "체력" 같은 단어를 붙이지 않고 `[아이콘][숫자]` 칩 하나로 표시한다 |
| P2 | 도시 이름 배너 아래에 **인구 게이지**를 두는데, 칸 수가 다음 레벨까지 필요한 인구와 같고 채운 칸이 현재 인구다 | "인구 2/3" 문장을 칸 게이지로 바꾼다. 수도 👑, 성벽, 공방도 배너 옆 작은 아이콘으로 표시한다 |
| P3 | 칸이나 유닛을 누르면 하단에 **원형 아이콘 버튼 줄**이 뜨고, 버튼마다 이름 한 단어와 ⭐ 비용 배지가 붙는다 | `ActionMenuHud`의 세로 텍스트 목록을 가로 아이콘 버튼 줄로 바꾸고, 비용은 아이콘 배지로 붙인다 |
| P4 | 쓸 수 없는 버튼은 **회색+자물쇠**로, 살 돈이 없으면 **비용 숫자를 빨간색**으로 표시한다 | 비활성 이유 문장을 없애고 이유 종류별 **작은 상태 아이콘**(🔒 기술, 💰 부족, 🚫 조건)을 붙인다. 문장은 툴팁에만 남긴다 |
| P5 | 레벨업 보상은 **그림 카드 두 장**(공방/탐험가 등) 중 하나를 고르는 모달로 뜬다 | "Lv2 보상: 공방 / 골드 수입 +1/턴" 텍스트 행을 두 장짜리 카드 모달로 바꾼다 |
| P6 | 기술 노드는 원형 아이콘이고, 노드 위에 **⭐비용**을 표시한다. 연구한 노드는 부족 색으로, 잠긴 노드는 회색으로 칠한다. 노드를 누르면 **해금되는 유닛/건물/능력이 아이콘 줄**로 뜬다 | `TechTreeHud` 상세 패널의 `Effect` 문장을 "해금 아이콘 줄"로 바꾼다. 상태도 문장 대신 노드 색이나 배지로 표시한다 |
| P7 | 유닛 정보 창은 공격/방어/이동/사거리/체력을 **아이콘 행**으로 보여주고, 스킬은 **아이콘 칩**으로 나열한다 | 이미 `BattleHud` 유닛 패널에 구현되어 있다. 훈련 버튼이나 배 업그레이드 옵션에서도 같은 스탯 행을 쓰도록 **재사용**한다 |
| P8 | 공격 대상을 고르면 적 머리 위에 **예상 피해 숫자**를 띄운다 | (선택) 공격 하이라이트 칸에 예상 피해와 반격 피해 숫자를 띄운다 |
| P9 | 알림은 **아이콘이 붙은 짧은 토스트**로 띄운다 | 행동 로그 한 줄을 `[행위자 색점][행동 아이콘][대상][숫자]`로 바꾼다 |

> **저작권 주의**: Polytopia는 **레이아웃과 정보 구조만** 참고하고, Polytopia의 아이콘이나 그림 파일은 가져오지 않는다. 새 아이콘은 지금 쓰는 세트와 같은 출처(game-icons.net, CC BY 3.0, `Assets/Art/GameIcons/LICENSE.txt`)에서 받거나 직접 그리고, 출처는 LICENSE.txt에 추가한다.

## 2. 현재 설명문 목록 → 대체안

우선순위는 P0(가장 자주 보고 가장 장황함), P1, P2 순이다.

### 2.1 칸/도시/유닛 메뉴: `ActionMenuHud` + `BattleController.ShowTileMenu`/`ShowUnitMenu` (P0)

| 현재 텍스트 (위치) | 대체안 |
| --- | --- |
| 제목 `"{도시명} (Lv {n})"` (`BattleController.cs` ShowTileMenu) | 도시명 + **레벨 배지**(원 안의 숫자) |
| 본문 `"아군 도시 · 인구 a/b · 유닛 c/d · 골드 +e/턴"` | 팀 색 테두리(아군/적)로 소속을 구분한다. `[인구 게이지 ■■□]` `[유닛 아이콘 c/d]` `[⭐ +e]` |
| 본문 `"영토 반경 r · 수도 · 수도 연결 · 공방 · 성벽 · 공원 k"` | 상태 아이콘 줄 `👑` `🔗(도로)` `🔨공방` `🧱성벽` `🌳×k`. 조건에 해당하는 것만 표시한다. 영토 반경은 맵의 국경선으로 이미 보이므로 뺀다 |
| `"과업: 이름 3/5턴 · …"` | 과업 아이콘 + **원형 진행 링** (아이콘에 마우스를 올리면 이름이 뜬다) |
| 옵션 `"Lv2 보상: 공방"` + Detail `"골드 수입 +1/턴"` | §2.2의 **보상 카드 모달**로 옮긴다 |
| 옵션 `"{유닛} 훈련 (골드 n)"` + Detail `"체력 · 공격 · 이동"` | **유닛 초상 버튼** + `⭐n` 배지. 스탯은 버튼 아래에 작은 아이콘 행으로 둔다(P7 재사용) |
| 칸 메뉴 제목 `"숲 (x, y)"`, 본문 `"아군 영토 · 벌목장 (인구 1) · 도로"` | 지형 아이콘 + 이름. 좌표는 디버그용이라 뺀다. 건물은 `[건물 아이콘][+1 인구]` 칩으로 |
| 건설/채집 옵션 `"{이름} (골드 n)"` + Detail `"인구 +2."` | **원형 아이콘 버튼** + `⭐n` 배지 + 결과 칩 `[+2 👥]` |
| 비활성 Detail `"기술 필요"`/`"도시당 1개"`/`"인접 조건: …"`/`"자기 영토 안에서만"` | 버튼을 회색으로 칠하고 **이유 아이콘** 🔒/①/🧩/🏳을 붙인다. 문장은 툴팁에만 남긴다 |
| 유닛 메뉴 `"점령"` + `"이 정착지를 우리 도시로 만든다(턴 종료)."` | `capture` 아이콘 버튼과 "점령" 한 단어 |
| `"승급 (최대 체력 +5, 완전 회복)"` | ⭐ 승급 아이콘 + `[+5 ❤]` 칩 |
| `"해산 (골드 +n)"` + 설명 | 해산 아이콘 + `[+n ⭐]` 칩 |
| 유닛 메뉴 본문 `"소속: 도시명 · 처치 2/3"` | 소속 도시 배지(🏠 도시명) + **처치 진행 점** `●●○` (베테랑이면 별 배지) |

**구조 변경**: `ActionMenuOption`에 `Icon`(string), `Cost`(int), `Yield`(아이콘+숫자 칩 목록), `BlockReason`(enum)을 추가하고 `ActionMenuHud`는 이 값으로 가로 버튼 줄을 그린다. 기존 `Label`/`Detail`은 **툴팁 문장**으로만 남긴다.

### 2.2 도시 레벨업 보상: `CityRewards.csv` (P0)

| 현재 | 대체안 |
| --- | --- |
| 메뉴 행 텍스트 `"Lv{n} 보상: {Name}"` / `Description` 문장 | 도시를 레벨업하면 **카드 2장짜리 모달**을 띄운다. 카드마다 큰 아이콘, 이름 한 단어, 효과 칩(`[+1 ⭐/턴]`, `[+3 👥]`, `[반경 2]`)을 넣는다 |

`CityRewards.csv`에 `Icon`, `EffectIcon`, `EffectAmount` 열을 추가한다(CLAUDE.md §6에 따라 한 칸에 한 값).

### 2.3 상단 자원 바: `CityResourceHud` (P0)

| 현재 | 대체안 |
| --- | --- |
| 발전도, 인구, 골드를 비슷한 뜻의 대체 아이콘(`combo`/`herd`/`victory`)에 틴트해서 표시 | **전용 아이콘** `star`(골드), `population`, `research`로 교체한다. `(+n)`은 괄호 없이 작은 초록 숫자로 따로 둔다 |
| 점수 한 줄 텍스트 (`SetScoreLine`) | 트로피 아이콘 + 숫자. 상세 내역은 툴팁으로 옮긴다 |

### 2.4 기술 트리: `TechTreeHud` (P1)

| 현재 텍스트 | 대체안 |
| --- | --- |
| 노드 아래 이름 라벨 (항상 표시) | 이름은 선택하거나 마우스를 올렸을 때만 표시하고, 노드 위에 **⭐비용 배지**를 붙인다 |
| `"{이름} ({n}티어)"` | 이름만 표시한다. 티어는 원의 크기와 동심원 위치로 이미 드러나므로 뺀다 |
| `Effect` 문장 (`TechTree.csv` Effect 열, 예: "산 타일 진입/이동 가능. 숨겨진 광맥 발견. 산 타일 유닛 방어력 +1.") | `Unlock1~4` 열을 아이콘으로 매핑한 **해금 아이콘 줄**(유닛 초상, 건물 아이콘, 능력 아이콘). 문장은 툴팁에만 남긴다 |
| `"해금 완료"` / `"선행 기술 필요: X"` / `"발전도 부족 (a/b)"` / `"해금 가능"` | 상태는 노드 색으로 구분한다(완료=팀 색, 가능=흰 테두리 반짝임, 잠김=회색+🔒). 부족한 경우 해금 버튼의 비용 숫자를 빨갛게 표시하고 선행 노드와의 연결선을 강조한다 |
| 버튼 `"해금 ({cost})"` | `[⭐ cost]` 아이콘 버튼 |

`Unlock` 키(`Build.X`, `Move.Mountain`, `Harvest.Fruit`, `Train.X` 등)마다 아이콘을 정하는 **해금 키 → 아이콘 표**가 필요하다. 이 표는 Data 계층에 `UnlockIconTable`로 두거나 CSV로 만든다.

### 2.5 전투 HUD: `BattleHud` (P1)

| 현재 | 대체안 |
| --- | --- |
| 행동 로그 `"<색>전사</색> 공격 → <색>궁수</색> (3)"` | `[●팀색][유닛 미니 초상][⚔][대상 초상][-3]`. 동사 자리에 기존 행동 아이콘(`attack`/`counter`/`guard`/`heal`/`move`…)을 쓴다 |
| 경제 로그 `"아군 건설: 농장"`, `"적 도시 레벨 업 (Lv 3)"` | `[●][건설 아이콘][농장 아이콘]`, `[●][🏙][Lv3 배지]` |
| 선택 구조물 패널 이름/설명 문장 (`StructureDefinition.Description`: "이 지역을 다스리는 도시의 중심입니다.") | 구조물 아이콘 + 이름 + **가능한 행동 아이콘**(예: 과일 → 채집 가능 🔒/✅)으로 바꾼다. 분위기 문장은 뺀다 |
| 승/패 `"승리!"`/`"패배..."` | 이미 `victory`/`defeat` 아이콘이 있으므로 텍스트는 줄이고 아이콘을 크게 보여준다 |
| 행동, 패시브 툴팁 문장 (`OptionalActionDefs`, `PassiveDefs`) | 아이콘을 이미 쓰고 있으므로 **유지**한다. 툴팁 첫 줄을 이름 한 단어로 굵게 하고, 수치는 칩 형태로 정리한다 |

### 2.6 맵 위 표시 (P1)

- **도시 배너**: 도시 칸 위에 이름, 레벨 배지, 인구 게이지, 수도 👑를 월드 스페이스로 띄운다(Polytopia의 가장 핵심적인 UI). 지금은 도시를 눌러야 메뉴 본문에서 정보를 볼 수 있다.
- **유닛 HP 배지**: 지금은 `HpDisplay`에 숫자만 있다. Polytopia처럼 팀 색 방패 안에 숫자를 넣고, 베테랑이면 별 테두리를 두르고, 행동을 마친 유닛은 흐리게 표시한다.
- **이동/공격 하이라이트**: 이동 가능 칸은 점, 공격 가능 칸은 빨간 조준 원으로 **모양**을 다르게 해서 색각 이상 사용자도 구분할 수 있게 한다. (P8) 공격 칸에 예상 피해 숫자를 표시한다.
- **자원 칸**: 과일, 사냥감, 물고기 칸 위에 수확할 수 있으면 작은 수확 아이콘을 띄워서, 클릭하지 않고도 할 수 있는 일이 보이게 한다.

### 2.7 샌드박스 HUD: `SandboxHud` (P2, 개발자 도구)

개발 도구이므로 텍스트를 유지해도 괜찮다. 다만 `SetStatus`의 긴 문장은 `[✅/⚠] + 짧은 문장` 형태로 줄인다. 이번 계획에서는 우선순위가 가장 낮다.

## 3. 공통 UI 부품 (View 계층에 새로 만들 것)

모두 `Assets/Scripts/TacticsECS/View/Widgets/`에 두는 MonoBehaviour이거나 정적 생성 헬퍼다. 정해진 형태의 부품은 `UIPrefabSetup`으로 프리팹을 만들고, 개수가 바뀌는 줄만 코드로 인스턴스화한다(README의 기존 원칙과 같음).

| 부품 | 역할 | 쓰이는 곳 |
| --- | --- | --- |
| `IconValueChip` | `[아이콘][숫자]`. 양수나 음수, 부족 상태에 따라 색을 바꾼다 | 자원 바, 메뉴 결과, 로그 |
| `CostBadge` | ⭐ 비용 배지. 살 돈이 부족하면 숫자가 빨갛게 바뀐다 | 모든 구매/건설/연구 버튼 |
| `SegmentGauge` | 칸이 나뉜 게이지(인구, 처치 수, 과업 진행) | 도시 배너, 도시 메뉴, 유닛 메뉴 |
| `IconActionButton` | 원형 아이콘, 이름 한 단어, `CostBadge`, 상태 아이콘, 툴팁 | `ActionMenuHud` 가로 줄 |
| `StatRow` | 체력/공격/방어/이동/사거리 아이콘 행 (지금 `BattleHud` 유닛 패널 로직을 분리한 것) | 유닛 패널, 훈련 버튼, 배 업그레이드 |
| `ChoiceCardModal` | 카드 2장 중 하나를 고르는 모달 | 도시 레벨업 보상 |
| `CityBanner` | 월드 스페이스 도시 배너 | 맵 |

## 4. 데이터 쪽 변경 (CLAUDE.md §2, §3, §6 준수)

1. **아이콘 이름은 CSV 열로 둔다**. 한 칸에 한 값을 넣는다.
   - `Buildings.csv`, `TileActions.csv`, `CityRewards.csv`, `Tasks.csv`, 유닛 CSV에 `Icon` 열을 추가한다.
   - 결과 칩용 `YieldIcon1`, `YieldAmount1`, `YieldIcon2`, `YieldAmount2` 열을 추가한다. `Population`, `GoldGain`처럼 이미 있는 숫자 열은 그대로 쓰고 아이콘은 View에서 고정 매핑한다.
2. **비활성 이유를 문자열에서 enum으로 바꾼다**. 지금은 Systems가 `reason = "기술 필요"` 같은 한글 문장을 직접 만든다(`CitySystem.CanTrain`, `TileImprovementSystem.GetOptions`, `EmbarkSystem.CanUpgrade`). 이를 `Core`에 `BlockReason` enum(`NeedTech`, `NotEnoughGold`, `OnePerCity`, `NeedAdjacent`, `OutsideTerritory`, `TileOccupied`, `NotOwnCity`, `NotTrainable` …)으로 정의하고 Systems는 enum만 반환한다. 아이콘과 툴팁 문장은 View가 매핑한다. 이렇게 하면 로직 계층에서 표시 문구가 빠지는 효과도 있다.
3. **해금 키 → 아이콘 표**(`UnlockIcons.csv`: `UnlockKey,Icon` 두 열)를 만든다. 기술 트리 해금 아이콘 줄에 쓴다.
4. `CityResourceHud.SlotDefs`의 대체 아이콘을 전용 아이콘으로 교체한다.

## 5. 필요한 아이콘

이미 있는 아이콘(`Assets/Art/GameIcons/Resources/Icons`, 50종): 행동과 패시브(attack, defense, move, range, hp, guard, heal, counter, charge …), 기술(farming, mining, forestry, sailing …), turn, victory, defeat, deselect.

**새로 필요한 아이콘** (game-icons.net에서 받거나 직접 제작):

| 분류 | 아이콘 |
| --- | --- |
| 자원/상태 | `star`(골드), `population`, `research`(발전도), `trophy`(점수), `crown`(수도), `lock`, `check`, `warning`, `level`(배지 틀) |
| 도시 | `city`, `village`, `ruin`, `wall`, `workshop`, `park`, `explorer`, `border`, `road`, `bridge` |
| 건물 | `lumber_hut`, `farm`, `mine`, `port`, `sawmill`, `windmill`, `forge`, `market`, `temple`(+숲/산/해양 변형은 틴트로 구분), `monument` |
| 칸 행동 | `harvest`(과일/사냥/낚시 공용 + 자원 아이콘 겹치기), `clear_forest`, `burn_forest`, `grow_forest`, `destroy` |
| 자원 칸 | `fruit`, `crop`, `animal`, `metal`, `fish`, `starfish` |
| 지형 | `field`, `forest`, `mountain`, `shallow_water`, `ocean` |
| 유닛 행동 | `capture`, `promote`(베테랑 별), `disband`, `upgrade_ship`, `explore_ruin` |
| 유닛 초상 | 유닛 종류별 미니 초상. 3D 모델을 렌더 텍스처로 캡처하는 Editor 스크립트(`-executeMethod`)로 자동 생성하는 방안이 1순위이고, 아이콘 세트에서 골라 쓰는 방안이 2순위다 |

## 6. 단계별 진행

각 단계가 끝날 때마다 README를 갱신하고 커밋/푸시한다(CLAUDE.md §5). 프리팹은 `UIPrefabSetup`을 Unity CLI 배치모드로 재생성한다(CLAUDE.md §1).

| 단계 | 범위 | 완료 기준 |
| --- | --- | --- |
| **1. 기반** | §5 아이콘 1차분(자원/상태/도시/건물/칸 행동) 추가 + LICENSE 갱신, `IconValueChip`/`CostBadge`/`SegmentGauge`, `BlockReason` enum으로 이유 문자열 제거 | Systems에서 표시용 한글 `reason` 문자열 0개. 자원 바가 전용 아이콘 사용 |
| **2. 메뉴 아이콘화** (가장 효과가 큼) | `ActionMenuHud`를 가로 `IconActionButton` 줄로 교체, CSV `Icon` 열, 도시/칸/유닛 메뉴 본문을 칩과 게이지로 교체 | 메뉴 본문에 "·"로 이어진 문장이 없고, 버튼 라벨은 한 단어 + 비용 배지 |
| **3. 보상 카드 & 기술 트리** | `ChoiceCardModal`, 기술 노드 비용 배지와 상태 색, `UnlockIcons.csv` + 해금 아이콘 줄 | 기술 상세에 `Effect` 문장이 기본으로 보이지 않음(툴팁으로만) |
| **4. 맵 위 표시** | `CityBanner`, HP 방패 배지, 하이라이트 모양 구분, 수확 가능 표시 | 도시를 누르지 않고도 레벨/인구/수도를 파악할 수 있음 |
| **5. 로그 & 마감** | 아이콘 로그 줄, 구조물 패널 아이콘화, 승/패 화면 정리, (선택) 예상 피해 숫자 | 로그에 동사 텍스트가 없음 |

## 7. 텍스트를 남기는 곳 (의도적으로)

- **툴팁**: 모든 아이콘에는 이름과 한 줄 설명을 담은 툴팁을 남긴다. 신규 플레이어가 아이콘 뜻을 배울 수 있는 경로가 필요하다(Polytopia도 정보 버튼으로 문장 설명을 제공한다).
- **고유명사**: 도시 이름, 유닛 이름, 기술 이름은 한 단어로 표시한다.
- **샌드박스 도구**: 개발자용이므로 문장을 유지한다.

## 8. 원문(Polytopia)과 다른 점

- Polytopia는 모바일 게임이라 길게 누르기나 ⓘ 버튼으로 설명을 연다. 이 프로젝트는 PC(마우스)가 기준이라 **마우스를 올리면 뜨는 툴팁**(`TooltipTrigger`)을 쓴다.
- 이 프로젝트의 "발전도(Development)" 자원은 위키에 없는 자원이다(기술 연구 비용). 그래서 별(⭐=골드)과 별도로 `research` 아이콘을 둔다.
- 아이콘 아트는 Polytopia 원본이 아니라 CC BY 3.0 세트를 쓴다(§1 저작권 주의).

## 9. 구현 상태 (2026-09-30)

| 단계 | 상태 | 구현 |
| --- | --- | --- |
| 1. 기반 | 완료 | 아이콘 55종 추가(`Assets/Art/GameIcons/Resources/Icons`, 출처 LICENSE.txt), 공용 부품 [`UiKit`](../Assets/Scripts/TacticsECS/View/Ui/UiKit.cs)(칩/⭐비용 배지/칸 게이지/숫자 배지/원형 아이콘 버튼/Flow 배치), 값→아이콘 매핑 [`MenuIcons`](../Assets/Scripts/TacticsECS/View/Ui/MenuIcons.cs), [`BlockReason`](../Assets/Scripts/TacticsECS/Core/BlockReason.cs) enum — `CitySystem.CanTrain`/`EmbarkSystem.CanUpgrade`/`TileImprovementSystem.GetOptions`가 더 이상 한글 이유 문장을 만들지 않는다. `TaskSystem.ProgressText`(문장) → `TaskSystem.Progress`([`TaskProgress`](../Assets/Scripts/TacticsECS/Core/TaskProgress.cs) 숫자). 자원 바 전용 아이콘(research/population/star), 수입은 괄호 대신 작은 초록 `+n`, 점수는 트로피 아이콘 + 숫자 |
| 2. 메뉴 아이콘화 | 완료 | [`ActionMenuHud`](../Assets/Scripts/TacticsECS/View/ActionMenuHud.cs) 전면 교체: 머리(대상 아이콘 + 이름 + 레벨 배지 + 인구 칸 게이지 + 상태 칩), 원형 아이콘 버튼 격자(한 단어 이름 + ⭐비용 배지(부족하면 빨강) + 막힌 이유 아이콘 + 대기 보상 수 배지), 설명·결과 칩(`[+2 인구]`)·막힌 이유는 버튼을 누르고 있거나 마우스를 올렸을 때 아래 정보 줄에만. CSV `Icon` 열(건물/타일 행동/보상/과업/해금 내역) |
| 3. 보상 카드 & 기술 트리 | 완료 | [`RewardCardHud`](../Assets/Scripts/TacticsECS/View/RewardCardHud.cs) — 우리 도시가 레벨업하면 카드 모달이 바로 뜨고, 고르기 전까지 다른 행동(지도/HUD/기술트리/턴 종료)이 막힌다(닫기 버튼 없음, `BattleController.EnsureRewardChoice`). 기술트리: 노드 이름 라벨 → `[전구 비용]` 배지 + 완료 ✓/잠김 🔒 아이콘, 상세 패널 효과 문장 → 해금 아이콘 줄(`TechUnlocks.csv` Icon), 상태 문장 → 아이콘 + 선행 기술 이름/부족 수치, 해금 버튼 → `[전구 n]` |
| 4. 맵 위 표시 | 대부분 | [`CityBannerHud`](../Assets/Scripts/TacticsECS/View/CityBannerHud.cs)(도시 칸 위 이름 + 수도 왕관 + 레벨 배지 + 인구 칸 게이지 + 보상 대기 아이콘), 이동 칸 = 가운데 점 / 공격 칸 = 네 모서리 조준 괄호(`GridView` — 색과 모양 둘 다), 이번 턴 이동·행동을 모두 마친 아군 유닛은 옷 색을 어둡게(`UnitView`). **남음**: HP 방패 배지(HpDisplay 프리팹 교체가 필요 — Unity CLI 필요), 수확 가능 칸 표시 |
| 5. 로그 & 마감 | 대부분 | 행동 로그 한 줄 = `[팀 색 점][행위자][행동 아이콘][대상][-3]`(`BattleHud.AddLogEntry(LogLine)`), 경제 로그도 종류 아이콘(건설은 건물 아이콘), 구조물 패널 = 아이콘 + 이름(분위기 문장 제거), 승/패 화면 = 큰 아이콘만. **남음**: 예상 피해 숫자(P8), 유닛 초상 |

레벨업 보상은 Polytopia 원문대로 고르기 전까지 다른 행동을 막는다(처음 구현에서는 X로 닫고 나중에 고를 수 있게 했다가 원문대로 되돌렸다).

## 10. 모바일 레이아웃

| 항목 | 구현 |
| --- | --- |
| 세이프 에어리어 | [`ResponsiveCanvas`](../Assets/Scripts/TacticsECS/View/Ui/ResponsiveCanvas.cs)가 각 HUD 캔버스 자식을 런타임에 `SafeArea` 아래로 옮기고 `Screen.safeArea`에 맞춘다(노치/홈 인디케이터). 프리팹은 그대로라 다시 생성할 필요 없음 |
| 화면 방향 | 세로 화면이면 CanvasScaler 기준 해상도를 1280x720 → 720x1280으로 바꾸고 폭 기준으로 맞춘다. HUD는 `LayoutChanged(portrait)`로 배치를 바꾼다 — 행동 로그는 자원 바 아래로 내려가 5줄로, 상황별 메뉴는 오른쪽 패널 → 유닛 패널 위 전체 폭 하단 시트(길면 스크롤), 기술트리는 폭에 맞게 축소 |
| 휴대폰 크기 | 짧은 변 물리 길이 4.2인치 미만(`Screen.dpi`, 모르면 `Application.isMobilePlatform`)이면 기준 해상도 x0.75 → UI 약 1.33배. 52px 행동 버튼이 1080p 휴대폰에서 약 9mm(권장 터치 크기) |
| 터치 입력 | `BattleController.Update`: 탭 = 손가락을 뗄 때 거의 안 움직였으면(160dpi 기준 10px), 한 손가락 끌기 = 카메라 이동(손가락 아래 지면이 따라옴), 두 손가락 = 핀치 줌. 마우스도 같은 규칙(왼쪽 버튼 끌기 = 이동). UI 위에서 시작한 누름은 지도로 새지 않는다(좌표로 직접 UI 레이캐스트 — `ScreenLayout.IsOverUi`) |
| 툴팁 | 터치에는 호버가 없어서, 누르고 있는 동안 보이고 뗀 뒤 2.5초 남는다(`BattleHud`/`ActionMenuHud`/기술트리). 상황별 메뉴는 오른쪽 위 X로 닫는다 |
| 방향 설정 | `ProjectSettings` 자동 회전(세로/가로 모두 허용)은 이미 켜져 있었다 |

검증: 이 작업 환경에는 Unity 에디터가 없어 Unity 배치모드를 돌리지 못했다. 대신 Unity 2021.3 참조 어셈블리(NuGet `UnityEngine.Modules`) + uGUI 소스 + Input System 최소 스텁으로
런타임 스크립트 전체를 Roslyn으로 컴파일해 오류 0개를 확인했고, 에디터 스크립트도 UnityEditor API를 뺀 나머지 바인딩 오류가 없음을 확인했다.
Unity에서 `unity run . -- -nographics -executeMethod TacticsECS.EditorTools.VerificationSuite.Run`(새 `MobileUiVerification` 포함)을 한 번 돌려야 한다.
