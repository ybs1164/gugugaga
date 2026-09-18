using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 샌드박스 배치 단계 전용 HUD. BattleHud와 같은 패턴 — 값을 스스로 판단하지 않고, 입력/클릭을
    /// 이벤트로만 밖(BattleController)에 알린다. 전투가 시작되면 gameObject를 비활성화해 화면을
    /// BattleHud에 넘긴다.
    ///
    /// 화면 구조(Canvas 이하 Toolbar/Palette 뼈대/StartBattleButton)는 더 이상 코드로 만들지 않는다.
    /// Assets/Prefabs/UI/SandboxHud.prefab(UIPrefabSetup.GenerateSandboxHud 참고, Unity CLI
    /// -executeMethod로만 생성)에 이미 만들어져 있고, 이 컴포넌트는 그 프리팹의 루트에 붙어
    /// 인스턴스화된다 — Init()은 자식을 이름으로 찾아(Wire*) 참조를 캐싱하고 버튼 클릭 이벤트만 연결한다.
    /// 팔레트 목록(SetPalette)은 CSV마다 행 수가 달라지는 진짜 동적 데이터라, 행 하나당
    /// Assets/Prefabs/UI/PaletteButton.prefab을 그대로 인스턴스화해서 쓴다.
    /// </summary>
    public class SandboxHud : MonoBehaviour
    {
        public event Action<string> OnLoadClicked;
        public event Action<string> OnExportClicked;
        public event Action<int> OnUnitSelected;
        public event Action<Team> OnTeamSelected;
        public event Action OnStartBattleClicked;
        public event Action<string> OnLoadBiomeClicked;
        public event Action OnGenerateTerrainClicked;
        public event Action OnMapSizeCycleClicked;

        [Tooltip("팔레트 목록 한 줄(버튼). CSV 행 수만큼 매번 이 프리팹을 인스턴스화한다. Assets/Prefabs/UI/PaletteButton.prefab.")]
        [SerializeField] private GameObject paletteButtonPrefab;

        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color ButtonSelected = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);

        private Text _statusText;
        private Text _mapSizeLabel;
        private Button _playerButton;
        private Button _enemyButton;
        private Image _playerButtonBg;
        private Image _enemyButtonBg;
        private RectTransform _paletteRoot;
        private RectTransform _paletteContent;
        private ScrollRect _paletteScrollRect;
        private readonly List<(Button Button, Image Bg)> _paletteButtons = new List<(Button, Image)>();

        private const float RowH = 30f;
        private const float PanelWidth = 220f;
        private const float PaletteHeight = 300f;
        private const float ScrollbarWidth = 6f;
        private const float ScrollbarGutter = ScrollbarWidth + 2f;

        public void Init()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[SandboxHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/SandboxHud.prefab을 통해 인스턴스화하세요.");
                return;
            }

            WireToolbar(canvas);
            WirePalette(canvas);
            WireStartButton(canvas);
        }

        // ---------- 툴바: 불러오기/내보내기(둘 다 OS 파일 탐색기) + 상태 텍스트 ----------

        private void WireToolbar(Transform canvas)
        {
            var panel = canvas.Find("Toolbar");
            panel.Find("불러오기Button").GetComponent<Button>().onClick.AddListener(HandleLoadClicked);
            panel.Find("내보내기Button").GetComponent<Button>().onClick.AddListener(HandleExportClicked);

            var playerButtonTransform = panel.Find("플레이어Button");
            _playerButton = playerButtonTransform.GetComponent<Button>();
            _playerButtonBg = playerButtonTransform.GetComponent<Image>();
            _playerButton.onClick.AddListener(() => OnTeamSelected?.Invoke(Team.Player));

            var enemyButtonTransform = panel.Find("적Button");
            _enemyButton = enemyButtonTransform.GetComponent<Button>();
            _enemyButtonBg = enemyButtonTransform.GetComponent<Image>();
            _enemyButton.onClick.AddListener(() => OnTeamSelected?.Invoke(Team.Enemy));

            panel.Find("바이옴불러오기Button").GetComponent<Button>().onClick.AddListener(HandleLoadBiomeClicked);

            var mapSizeButtonTransform = panel.Find("맵크기Button");
            mapSizeButtonTransform.GetComponent<Button>().onClick.AddListener(() => OnMapSizeCycleClicked?.Invoke());
            _mapSizeLabel = mapSizeButtonTransform.Find("Label").GetComponent<Text>();

            panel.Find("지형생성Button").GetComponent<Button>().onClick.AddListener(() => OnGenerateTerrainClicked?.Invoke());

            _statusText = panel.Find("Status").GetComponent<Text>();
        }

        /// <summary>"맵 크기" 버튼 라벨을 현재 선택된 프리셋으로 갱신한다(BattleController.HandleMapSizeCycle이
        /// 클릭마다 호출).</summary>
        public void SetMapSizeLabel(string name, int size) => _mapSizeLabel.text = $"크기: {name} ({size}x{size})";

        /// <summary>OS 파일 탐색기로 불러올 바이옴 CSV를 고른다. 취소하면 아무 일도 일어나지 않는다 —
        /// HandleLoadClicked(유닛 CSV)와 같은 패턴.</summary>
        private void HandleLoadBiomeClicked()
        {
            var path = StandaloneFileDialog.OpenFilePanel("불러올 바이옴 CSV 선택", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnLoadBiomeClicked?.Invoke(path);
        }

        /// <summary>OS 파일 탐색기(열기 대화상자)로 불러올 CSV를 고른다. 취소하면 아무 일도 일어나지 않는다.
        /// 에디터와 스탠드얼론 빌드(Windows) 양쪽에서 동작한다.</summary>
        private void HandleLoadClicked()
        {
            var path = StandaloneFileDialog.OpenFilePanel("불러올 CSV 선택", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnLoadClicked?.Invoke(path);
        }

        /// <summary>OS 파일 탐색기(저장 대화상자)로 내보낼 CSV 경로를 고른다.
        /// 에디터와 스탠드얼론 빌드(Windows) 양쪽에서 동작한다.</summary>
        private void HandleExportClicked()
        {
            var path = StandaloneFileDialog.SaveFilePanel("CSV로 내보내기", "", "SandboxUnits", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnExportClicked?.Invoke(path);
        }

        /// <summary>어느 팀에 배치할지 강조 표시만 바꾼다(선택 자체는 BattleController 쪽 상태가 갖고 있음).</summary>
        public void SetSelectedTeam(Team team)
        {
            _playerButtonBg.color = team == Team.Player ? PlayerAccent : ButtonIdle;
            _enemyButtonBg.color = team == Team.Enemy ? EnemyAccent : ButtonIdle;
        }

        public void SetStatus(string text) => _statusText.text = text;

        // ---------- 팔레트: 불러온 유닛 목록 ----------

        private void WirePalette(Transform canvas)
        {
            var panel = canvas.Find("Palette");
            _paletteRoot = (RectTransform)panel;
            _paletteContent = (RectTransform)panel.Find("Viewport/Content");
            _paletteScrollRect = panel.GetComponent<ScrollRect>();
        }

        /// <summary>불러온 CSV 행 목록으로 팔레트 버튼을 다시 만든다. 행마다 PaletteButton 프리팹을 그대로
        /// 인스턴스화한다(행 수가 CSV마다 달라지는 진짜 동적 데이터라 프리팹 하나를 반복 사용). Content 높이를
        /// 행 수에 맞게 늘려 패널 높이를 넘으면 ScrollRect로 스크롤해서 볼 수 있게 한다.</summary>
        public void SetPalette(IReadOnlyList<UnitCsvRow> rows)
        {
            foreach (var (button, _) in _paletteButtons) Destroy(button.gameObject);
            _paletteButtons.Clear();

            float buttonWidth = PanelWidth - ScrollbarGutter - 16f;
            for (int i = 0; i < rows.Count; i++)
            {
                int index = i;
                var row = rows[i];
                var label = $"{row.Name} (HP{row.MaxHp}/{row.BaseVisual})";

                var buttonGo = Instantiate(paletteButtonPrefab, _paletteContent);
                var rect = (RectTransform)buttonGo.transform;
                rect.sizeDelta = new Vector2(buttonWidth, RowH);
                rect.anchoredPosition = new Vector2(8f, -8f - i * (RowH + 4f));

                var button = buttonGo.GetComponent<Button>();
                var bg = buttonGo.GetComponent<Image>();
                buttonGo.GetComponentInChildren<Text>().text = label;
                button.onClick.AddListener(() => OnUnitSelected?.Invoke(index));
                _paletteButtons.Add((button, bg));
            }

            float contentHeight = rows.Count * (RowH + 4f) + 8f;
            _paletteContent.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, PaletteHeight));
            _paletteContent.anchoredPosition = Vector2.zero;
            _paletteScrollRect.verticalNormalizedPosition = 1f;
        }

        public void SetSelectedUnit(int index)
        {
            for (int i = 0; i < _paletteButtons.Count; i++)
                _paletteButtons[i].Bg.color = i == index ? ButtonSelected : ButtonIdle;
        }

        // ---------- 전투 시작 ----------

        private void WireStartButton(Transform canvas)
        {
            canvas.Find("StartBattleButton").GetComponent<Button>().onClick.AddListener(() => OnStartBattleClicked?.Invoke());
        }
    }
}
