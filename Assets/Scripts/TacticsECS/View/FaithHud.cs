using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 포인트 표시 전용 HUD(현재/최대, 게이지, 다음 턴 예상 수입과 순위). CityResourceHud와 별개의 독립 패널이며
    /// 값만 받아 그리고 계산은 모른다(FaithSystem). 프리팹 없이 코드로 만든다 — 패널이 단순해 UIPrefabSetup을 거치지 않는다.
    /// 규칙: docs/spec/game/faith.md
    /// </summary>
    public class FaithHud : MonoBehaviour
    {
        private const float PanelWidth = 280f;
        private const float PanelHeight = 44f;
        private const float TopOffset = -92f;

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.85f);
        private static readonly Color FillColor = new Color(0.93f, 0.80f, 0.35f);
        private static readonly Color FullColor = new Color(1f, 0.95f, 0.65f);

        public event System.Action OnClicked;

        private Font _font;
        private Text _valueText;
        private Text _incomeText;
        private RectTransform _fill;
        private Image _fillImage;
        private float _lastRatio = -1f;

        public void Init(Font font)
        {
            _font = font;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = NewRect("Panel", canvasGo.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, TopOffset);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panel.gameObject.AddComponent<Button>().onClick.AddListener(() => OnClicked?.Invoke());

            var label = NewText("Label", panel, 16, TextAnchor.MiddleLeft);
            label.text = LocalizationSystem.T("UI.Faith.Label");
            label.color = FillColor;
            Place(label.rectTransform, 8f, 0f, 48f, PanelHeight);

            _valueText = NewText("Value", panel, 18, TextAnchor.MiddleLeft);
            Place(_valueText.rectTransform, 58f, 0f, 70f, PanelHeight);

            // 게이지: 배경 + 채우기(Fill). 이름에 "Fill"/"Icon"이 들어간 이미지는 PixelUISkin이 건드리지 않는다.
            var gauge = NewRect("Gauge", panel);
            gauge.anchorMin = gauge.anchorMax = new Vector2(0f, 0.5f);
            gauge.pivot = new Vector2(0f, 0.5f);
            gauge.anchoredPosition = new Vector2(132f, 6f);
            gauge.sizeDelta = new Vector2(PanelWidth - 142f, 8f);
            var gaugeBg = gauge.gameObject.AddComponent<Image>();
            gaugeBg.color = new Color(0.20f, 0.22f, 0.27f);
            var fill = NewRect("Fill", gauge);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            _fill = fill;
            _fillImage = fill.gameObject.AddComponent<Image>();
            _fillImage.color = FillColor;

            _incomeText = NewText("Income", panel, 13, TextAnchor.MiddleLeft);
            Place(_incomeText.rectTransform, 132f, -10f, PanelWidth - 140f, 18f);
            _incomeText.color = new Color(0.82f, 0.84f, 0.88f);

            PixelUISkin.Apply(gameObject);
        }

        /// <summary>현재/최대와 게이지. 상한이 0이면 게이지는 비어 있다.</summary>
        public void SetFaith(int current, int max)
        {
            if (_valueText == null) return;
            _valueText.text = LocalizationSystem.F("UI.Faith.Value", current, max);
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (!Mathf.Approximately(ratio, _lastRatio))
            {
                _fill.anchorMax = new Vector2(ratio, 1f);
                _lastRatio = ratio;
            }
            _fillImage.color = max > 0 && current >= max ? FullColor : FillColor;
        }

        /// <summary>다음 턴 시작 예상 수입과 내 순위. 순위가 0 이하면 숨긴다.</summary>
        public void SetIncomePreview(int gain, int rank)
        {
            if (_incomeText == null) return;
            _incomeText.text = rank > 0 ? LocalizationSystem.F("UI.Faith.Income", gain, rank) : string.Empty;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
            return text;
        }

        /// <summary>패널 왼쪽 가장자리·세로 중앙 기준 배치.</summary>
        private static void Place(RectTransform rt, float x, float yFromCenter, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, yFromCenter);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
