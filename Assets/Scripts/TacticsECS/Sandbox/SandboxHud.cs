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
    /// </summary>
    public class SandboxHud : MonoBehaviour
    {
        public event Action<string> OnLoadClicked;
        public event Action<string> OnExportClicked;
        public event Action<int> OnUnitSelected;
        public event Action<Team> OnTeamSelected;
        public event Action OnStartBattleClicked;

        private static readonly Color PanelBackground = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color ButtonSelected = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);

        private Font _font;
        private Text _statusText;
        private Button _playerButton;
        private Button _enemyButton;
        private Image _playerButtonBg;
        private Image _enemyButtonBg;
        private RectTransform _paletteRoot;
        private readonly List<(Button Button, Image Bg)> _paletteButtons = new List<(Button, Image)>();

        private const float RowH = 30f;
        private const float PanelWidth = 220f;

        public void Init()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = canvas.transform;
            BuildToolbar(root);
            BuildPalette(root);
            BuildStartButton(root);
        }

        // ---------- 공용 빌딩 블록 (BattleHud와 같은 스타일, 이 클래스 전용으로 복사) ----------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreatePanelImage(RectTransform rect, Color color)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private Button CreateTextButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, Color bg, out Image bgImage)
        {
            var rect = CreateRect(label + "Button", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            bgImage = CreatePanelImage(rect, bg);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bgImage;

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 2f);
            textRect.offsetMax = new Vector2(-6f, -2f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = label;

            return button;
        }

        // ---------- 툴바: 불러오기/내보내기(둘 다 OS 파일 탐색기) + 상태 텍스트 ----------

        private void BuildToolbar(Transform root)
        {
            var panel = CreateRect("Toolbar", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(PanelWidth, 96f);
            CreatePanelImage(panel, PanelBackground);

            var loadButton = CreateTextButton(panel, "불러오기", new Vector2(8f, -8f), new Vector2((PanelWidth - 24f) / 2f, 28f), ButtonIdle, out _);
            loadButton.onClick.AddListener(HandleLoadClicked);

            var exportButton = CreateTextButton(panel, "내보내기", new Vector2(8f + (PanelWidth - 24f) / 2f + 8f, -8f), new Vector2((PanelWidth - 24f) / 2f, 28f), ButtonIdle, out _);
            exportButton.onClick.AddListener(HandleExportClicked);

            _playerButton = CreateTextButton(panel, "플레이어", new Vector2(8f, -44f), new Vector2((PanelWidth - 24f) / 2f, 28f), PlayerAccent, out _playerButtonBg);
            _playerButton.onClick.AddListener(() => OnTeamSelected?.Invoke(Team.Player));

            _enemyButton = CreateTextButton(panel, "적", new Vector2(8f + (PanelWidth - 24f) / 2f + 8f, -44f), new Vector2((PanelWidth - 24f) / 2f, 28f), ButtonIdle, out _enemyButtonBg);
            _enemyButton.onClick.AddListener(() => OnTeamSelected?.Invoke(Team.Enemy));

            var statusRect = CreateRect("Status", panel);
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(8f, -78f);
            statusRect.sizeDelta = new Vector2(PanelWidth - 16f, 18f);
            _statusText = statusRect.gameObject.AddComponent<Text>();
            _statusText.font = _font;
            _statusText.fontSize = 13;
            _statusText.color = new Color(1f, 1f, 1f, 0.75f);
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <summary>OS 파일 탐색기(열기 대화상자)로 불러올 CSV를 고른다. 취소하면 아무 일도 일어나지 않는다.
        /// 에디터 밖(빌드)에서는 이 대화상자를 쓸 수 없어 상태 텍스트로만 안내한다 — 이 툴은 에디터
        /// Play 모드 전용이라 실사용에는 영향이 없다.</summary>
        private void HandleLoadClicked()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("불러올 CSV 선택", "", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnLoadClicked?.Invoke(path);
#else
            SetStatus("파일 탐색기는 에디터에서만 지원합니다.");
#endif
        }

        /// <summary>OS 파일 탐색기(저장 대화상자)로 내보낼 CSV 경로를 고른다.</summary>
        private void HandleExportClicked()
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.SaveFilePanel("CSV로 내보내기", "", "SandboxUnits", "csv");
            if (string.IsNullOrEmpty(path)) return;
            OnExportClicked?.Invoke(path);
#else
            SetStatus("파일 탐색기는 에디터에서만 지원합니다.");
#endif
        }

        /// <summary>어느 팀에 배치할지 강조 표시만 바꾼다(선택 자체는 BattleController 쪽 상태가 갖고 있음).</summary>
        public void SetSelectedTeam(Team team)
        {
            _playerButtonBg.color = team == Team.Player ? PlayerAccent : ButtonIdle;
            _enemyButtonBg.color = team == Team.Enemy ? EnemyAccent : ButtonIdle;
        }

        public void SetStatus(string text) => _statusText.text = text;

        // ---------- 팔레트: 불러온 유닛 목록 ----------

        private void BuildPalette(Transform root)
        {
            var panel = CreateRect("Palette", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -120f);
            panel.sizeDelta = new Vector2(PanelWidth, 300f);
            CreatePanelImage(panel, PanelBackground);
            _paletteRoot = panel;
        }

        /// <summary>불러온 CSV 행 목록으로 팔레트 버튼을 다시 만든다. 목록이 패널 높이를 넘으면 아래쪽은
        /// 잘려 보이지 않는다(스크롤은 v1 범위 밖 — 문서의 비목표 참고).</summary>
        public void SetPalette(IReadOnlyList<UnitCsvRow> rows)
        {
            foreach (var (button, _) in _paletteButtons) Destroy(button.gameObject);
            _paletteButtons.Clear();

            for (int i = 0; i < rows.Count; i++)
            {
                int index = i;
                var row = rows[i];
                var label = $"{row.Name} (HP{row.MaxHp}/{row.BaseVisual})";
                var button = CreateTextButton(_paletteRoot, label, new Vector2(8f, -8f - i * (RowH + 4f)), new Vector2(PanelWidth - 16f, RowH), ButtonIdle, out var bg);
                button.onClick.AddListener(() => OnUnitSelected?.Invoke(index));
                _paletteButtons.Add((button, bg));
            }
        }

        public void SetSelectedUnit(int index)
        {
            for (int i = 0; i < _paletteButtons.Count; i++)
                _paletteButtons[i].Bg.color = i == index ? ButtonSelected : ButtonIdle;
        }

        // ---------- 전투 시작 ----------

        private void BuildStartButton(Transform root)
        {
            var rect = CreateRect("StartBattleButton", root);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(140f, 52f);

            var bg = CreatePanelImage(rect, PlayerAccent);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnStartBattleClicked?.Invoke());

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "전투 시작";
        }
    }
}
