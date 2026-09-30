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
        public event Action<string> OnLoadTechClicked;
        public event Action<string> OnExportTechClicked;
        public event Action OnGenerateTerrainClicked;
        public event Action OnMapSizeCycleClicked;
        public event Action OnWetnessCycleClicked;

        // 습도 탭(GenerationTab): 맵 타입/물 비율/1차 지형/바이옴 선택/팀별 종족.
        public event Action<int> OnMapTypeSelected;
        public event Action<float> OnWaterRatioChanged;
        public event Action OnGenerateOutlineClicked;
        /// <summary>0 = 자동(전체 바이옴 또는 종족 바이옴), k = 불러온 바이옴 목록의 k-1번째 하나만.</summary>
        public event Action<int> OnBiomeChoiceSelected;
        /// <summary>종족 Index(Tribes.csv), -1 = 종족 없음.</summary>
        public event Action<Team, int> OnTribeSelected;

        [Tooltip("팔레트 목록 한 줄(버튼). CSV 행 수만큼 매번 이 프리팹을 인스턴스화한다. Assets/Prefabs/UI/PaletteButton.prefab.")]
        [SerializeField] private GameObject paletteButtonPrefab;

        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color ButtonSelected = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);

        private Text _statusText;
        private Text _mapSizeLabel;
        private Text _wetnessLabel;
        private GameObject _generationTab;
        private Dropdown _mapTypeDropdown;
        private Slider _waterSlider;
        private Text _waterValueText;
        private Dropdown _biomeDropdown;
        private Dropdown _playerTribeDropdown;
        private Dropdown _enemyTribeDropdown;
        private Text _tribeInfoText;
        private Button _playerButton;
        private Button _enemyButton;
        private Image _playerButtonBg;
        private Image _enemyButtonBg;
        private RectTransform _paletteRoot;
        private RectTransform _paletteContent;
        private ScrollRect _paletteScrollRect;
        private readonly List<(Button Button, Image Bg)> _paletteButtons = new List<(Button, Image)>();

        private GameObject _structurePanel;
        private Image _structurePanelAccent;
        private Text _structureNameText;
        private Text _structureDescriptionText;

        /// <summary>BattleHud.StructureAccentColors와 같은 값 — StructureAssetSetup이 3D 모델을 칠할 때
        /// 쓰는 색과 맞춘 정보 패널 강조색. Core/Data 계층은 UI 색을 몰라야 하므로 표시 전용인 이 값은
        /// View(SandboxHud/BattleHud)가 각자 갖는다(PlayerAccent/EnemyAccent도 이미 두 클래스에 따로
        /// 있는 것과 같은 이유).</summary>
        private static readonly Dictionary<string, Color> StructureAccentColors = new Dictionary<string, Color>
        {
            ["Capital"] = new Color(0.85f, 0.7f, 0.15f),
            ["Village"] = new Color(0.75f, 0.62f, 0.42f),
            ["Ruin"] = new Color(0.6f, 0.6f, 0.6f),
            ["Resource_Food"] = new Color(0.8f, 0.25f, 0.3f),
            ["Resource_Ore"] = new Color(0.3f, 0.75f, 0.75f),
            ["Resource_Fruit"] = new Color(0.8f, 0.25f, 0.3f),
            ["Resource_Crop"] = new Color(0.85f, 0.75f, 0.3f),
            ["Resource_Animal"] = new Color(0.6f, 0.45f, 0.3f),
            ["Resource_Metal"] = new Color(0.3f, 0.75f, 0.75f),
            ["Resource_Fish"] = new Color(1f, 0.85f, 0.3f),
            ["Lighthouse"] = new Color(0.95f, 0.95f, 0.9f),
            ["Starfish"] = new Color(0.9f, 0.5f, 0.2f),
        };

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
            WireGenerationTab(canvas);
            WirePalette(canvas);
            WireStructurePanel(canvas);
            WireStartButton(canvas);

            HideStructurePanel();
            // 모바일: 세이프 에어리어 + 휴대폰 크기 보정(ResponsiveCanvas). 자식을 이름으로 찾는 Wire*가 끝난 뒤에 감싼다.
            ResponsiveCanvas.Attach(canvas);
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
            WireOptionalButton(panel, "기술불러오기Button", HandleLoadTechClicked);
            WireOptionalButton(panel, "기술내보내기Button", HandleExportTechClicked);

            var mapSizeButtonTransform = panel.Find("맵크기Button");
            mapSizeButtonTransform.GetComponent<Button>().onClick.AddListener(() => OnMapSizeCycleClicked?.Invoke());
            _mapSizeLabel = mapSizeButtonTransform.Find("Label").GetComponent<Text>();

            var wetnessButtonTransform = panel.Find("습도Button");
            // 습도 탭이 있으면 버튼은 탭을 열고 닫는다. 옛 프리팹(탭 없음)이면 예전처럼 맵 타입을 순환한다.
            wetnessButtonTransform.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (_generationTab != null) _generationTab.SetActive(!_generationTab.activeSelf);
                else OnWetnessCycleClicked?.Invoke();
            });
            _wetnessLabel = wetnessButtonTransform.Find("Label").GetComponent<Text>();

            panel.Find("지형생성Button").GetComponent<Button>().onClick.AddListener(() => OnGenerateTerrainClicked?.Invoke());

            _statusText = panel.Find("Status").GetComponent<Text>();
        }

        /// <summary>"맵 크기" 버튼 라벨을 현재 선택된 프리셋으로 갱신한다(BattleController.HandleMapSizeCycle이
        /// 클릭마다 호출).</summary>
        public void SetMapSizeLabel(string name, int size) => _mapSizeLabel.text = $"크기: {name} ({size}x{size})";

        /// <summary>"습도" 버튼 라벨을 현재 선택된 프리셋으로 갱신한다(BattleController.HandleWetnessCycle이
        /// 클릭마다 호출).</summary>
        public void SetWetnessLabel(string name) => _wetnessLabel.text = $"습도 탭: {name}";

        // ---------- 습도 탭 ----------

        private void WireGenerationTab(Transform canvas)
        {
            var tab = canvas.Find("GenerationTab");
            if (tab == null) { Debug.LogWarning("[SandboxHud] GenerationTab이 프리팹에 없습니다 — UIPrefabSetup.GenerateAll로 다시 생성하세요."); return; }
            _generationTab = tab.gameObject;
            _mapTypeDropdown = tab.Find("MapTypeDropdown").GetComponent<Dropdown>();
            _waterSlider = tab.Find("WaterSlider").GetComponent<Slider>();
            _waterValueText = tab.Find("WaterValue").GetComponent<Text>();
            _biomeDropdown = tab.Find("BiomeDropdown").GetComponent<Dropdown>();
            _playerTribeDropdown = tab.Find("PlayerTribeDropdown").GetComponent<Dropdown>();
            _enemyTribeDropdown = tab.Find("EnemyTribeDropdown").GetComponent<Dropdown>();
            _tribeInfoText = tab.Find("TribeInfo").GetComponent<Text>();
            tab.Find("1차지형생성Button/Label").GetComponent<Text>().text = "1차 지형 생성 (육지/물 아웃라인)";

            _mapTypeDropdown.onValueChanged.AddListener(i => OnMapTypeSelected?.Invoke(i));
            _waterSlider.onValueChanged.AddListener(v => { _waterValueText.text = Percent(v); OnWaterRatioChanged?.Invoke(v); });
            tab.Find("1차지형생성Button").GetComponent<Button>().onClick.AddListener(() => OnGenerateOutlineClicked?.Invoke());
            _biomeDropdown.onValueChanged.AddListener(i => OnBiomeChoiceSelected?.Invoke(i));
            _playerTribeDropdown.onValueChanged.AddListener(i => OnTribeSelected?.Invoke(Team.Player, i - 1));
            _enemyTribeDropdown.onValueChanged.AddListener(i => OnTribeSelected?.Invoke(Team.Enemy, i - 1));
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

        private static void SetOptions(Dropdown dropdown, IReadOnlyList<string> options, int selected)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            var list = new List<string>(options);
            dropdown.AddOptions(list);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, Mathf.Max(0, list.Count - 1)));
            dropdown.RefreshShownValue();
        }

        public void SetMapTypeOptions(IReadOnlyList<string> names, int selected) => SetOptions(_mapTypeDropdown, names, selected);

        /// <summary>슬라이더 값만 바꾼다(이벤트 없음) — 맵 타입을 고르면 그 타입의 대표 물 비율로 되돌릴 때 쓴다.</summary>
        public void SetWaterRatio(float value)
        {
            if (_waterSlider == null) return;
            _waterSlider.SetValueWithoutNotify(value);
            _waterValueText.text = Percent(value);
        }

        /// <summary>바이옴 드롭다운: 0번은 "자동", 그 뒤로 바이옴 이름들.</summary>
        public void SetBiomeOptions(IReadOnlyList<string> biomeNames, int selected)
        {
            var options = new List<string> { "자동 (전체 / 종족 바이옴)" };
            options.AddRange(biomeNames);
            SetOptions(_biomeDropdown, options, selected);
        }

        /// <summary>종족 드롭다운 두 개: 0번은 "종족 없음", 그 뒤로 종족 이름들. selected는 종족 Index(-1 = 없음).</summary>
        public void SetTribeOptions(IReadOnlyList<string> tribeNames, int playerSelected, int enemySelected)
        {
            var options = new List<string> { "종족 없음 (기본 규칙)" };
            options.AddRange(tribeNames);
            SetOptions(_playerTribeDropdown, options, playerSelected + 1);
            SetOptions(_enemyTribeDropdown, options, enemySelected + 1);
        }

        public void SetTribeInfo(string text)
        {
            if (_tribeInfoText != null) _tribeInfoText.text = text;
        }

        /// <summary>기술트리 버튼은 나중에 추가돼, 옛 프리팹(UIPrefabSetup.GenerateAll을 다시 돌리기 전)에는 없을 수 있다 —
        /// 없으면 경고만 남기고 나머지 HUD는 그대로 동작하게 한다.</summary>
        private static void WireOptionalButton(Transform panel, string name, UnityEngine.Events.UnityAction onClick)
        {
            var t = panel.Find(name);
            if (t == null) { Debug.LogWarning($"[SandboxHud] {name}이(가) 프리팹에 없습니다 — UIPrefabSetup.GenerateAll로 다시 생성하세요."); return; }
            t.GetComponent<Button>().onClick.AddListener(onClick);
        }

        /// <summary>OS 파일 탐색기로 불러올 기술트리 CSV(TechTree.csv 형식)를 고른다. 취소하면 아무 일도 없다.</summary>
        private void HandleLoadTechClicked()
        {
            var path = StandaloneFileDialog.OpenFilePanel("불러올 기술트리 CSV 선택", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnLoadTechClicked?.Invoke(path);
        }

        /// <summary>지금 쓰는 기술트리(기본 TechTree.csv 또는 불러온 파일)를 CSV로 내보낸다 — 편집용 템플릿.</summary>
        private void HandleExportTechClicked()
        {
            var path = StandaloneFileDialog.SaveFilePanel("기술트리 CSV로 내보내기", "", TechTreeDefinition.SandboxFileName, "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnExportTechClicked?.Invoke(path);
        }

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

        // ---------- 구조물 정보 패널 (좌하단, 마우스로 가리키면 뜬다) ----------

        private void WireStructurePanel(Transform canvas)
        {
            var panel = canvas.Find("StructurePanel");
            _structurePanel = panel.gameObject;
            _structurePanelAccent = panel.Find("Accent").GetComponent<Image>();
            _structureNameText = panel.Find("NameText").GetComponent<Text>();
            _structureDescriptionText = panel.Find("DescriptionText").GetComponent<Text>();
        }

        /// <summary>BattleHud.ShowStructurePanel과 같은 방식(StructureDefinition.All 선형 탐색) — 전투
        /// 시작 전 배치 단계에서는 클릭이 이미 유닛 배치에 쓰이므로, BattleController가 매 프레임 마우스가
        /// 가리키는 칸을 검사해(호버) 이 메서드를 호출한다.</summary>
        public void ShowStructurePanel(string structureId)
        {
            foreach (var info in StructureDefinition.All)
            {
                if (info.Id != structureId) continue;

                _structurePanel.SetActive(true);
                _structurePanelAccent.color = StructureAccentColors.TryGetValue(structureId, out var color) ? color : Color.white;
                _structureNameText.text = info.Name;
                _structureDescriptionText.text = info.Description;
                return;
            }

            HideStructurePanel();
        }

        public void HideStructurePanel() => _structurePanel.SetActive(false);

        // ---------- 전투 시작 ----------

        private void WireStartButton(Transform canvas)
        {
            canvas.Find("StartBattleButton").GetComponent<Button>().onClick.AddListener(() => OnStartBattleClicked?.Invoke());
        }
    }
}
