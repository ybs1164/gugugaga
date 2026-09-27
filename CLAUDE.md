# CLAUDE.md

이 저장소에서 작업할 때 지켜야 할 규칙.

## 1. Unity CLI 사용
Unity 관련 작업(빌드, 테스트 실행, 패키지 갱신 등)은 Unity 에디터 GUI를 직접 조작하지 말고
Unity CLI(`unity -projectPath ... -batchmode ...` 형태의 명령어)를 사용한다.
Unity 에디터(GUI)를 직접 실행하지 않는다 — 배치모드 CLI 명령이라도 같은 프로젝트가 이미 에디터로
열려 있으면 "another Unity instance is running" 오류로 실패하므로, CLI 실행 전 사용자에게 에디터가
열려 있는지 먼저 확인/종료를 요청한다.

## 2. Data는 값만 가진다
`Data`/`Core` 계층(예: `Assets/Scripts/TacticsECS/Data`, `Assets/Scripts/TacticsECS/Core`)의 타입은
순수 데이터(필드/struct)만 담는다. 이 계층에는 함수(메서드/로직)를 두지 않는다.
(예외: 생성자, `UnitData.Create` 같은 단순 팩토리성 정적 생성 메서드는 값 초기화 목적이므로 허용)

## 3. Systems는 상태를 갖지 않는다
`Systems` 계층(예: `Assets/Scripts/TacticsECS/Systems`)의 타입은 `Data`를 읽고 쓰는 로직만 담당하며,
자체적으로 값(필드/프로퍼티)을 저장하지 않는다. 필요한 상태는 항상 `Data` 계층의 값을 인자로 받거나
반환값으로 넘긴다.

## 4. 게임 규칙 기준 = Polytopia Wiki
게임 규칙(지형/건물/기술/유닛/도시 등)의 기준 자료는 [The Battle of Polytopia Wiki](https://polytopia.fandom.com/wiki/The_Battle_of_Polytopia_Wiki)다.
- 주요 문서: [Buildings](https://polytopia.fandom.com/wiki/Buildings), [City](https://polytopia.fandom.com/wiki/City),
  [Population](https://polytopia.fandom.com/wiki/Population), [Technology](https://polytopia.fandom.com/wiki/Technology),
  [Movement](https://polytopia.fandom.com/wiki/Movement), [Map Generation](https://polytopia.fandom.com/wiki/Map_Generation)
- Fandom은 WebFetch에 402를 돌려주므로 MediaWiki API로 원문 위키텍스트를 받는다:
  `curl -s "https://polytopia.fandom.com/api.php?action=parse&page=<문서명>&prop=wikitext&format=json&formatversion=2&redirects=1"`
- 위키와 다르게 구현하는 부분은 코드 주석/문서에 "원문과 다른 점"으로 이유를 남긴다.

## 5. 작업 완료 시 README.md 갱신 + git push
작업 단위가 끝날 때마다:
1. `README.md`에 이번에 한 작업 내용을 업데이트한다.
2. 변경 사항을 git에 커밋하고 push한다.
