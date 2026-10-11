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
        private const float PanelHeight = 54f;
        private const float TopOffset = -92f;

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.85f);
        private static readonly Color FillColor = new Color(0.93f, 0.80f, 0.35f);
        private static readonly Color FullColor = new Color(1f, 0.95f, 0.65f);

        public event System.Action OnClicked;

        private Font _font;
        private Text _valueText;
        private Text _incomeText;
        private Text _statusText;
        private RectTransform _statusBox;
        private RectTransform _fill;
        private Image _fillImage;
        private float _lastRatio = -1f;
        private int _shownFaith = -1;
        private RectTransform _panelRect;

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
            _panelRect = panel;
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
            gauge.anchoredPosition = new Vector2(132f, 9f);
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
            Place(_incomeText.rectTransform, 132f, -12f, PanelWidth - 140f, 20f);
            _incomeText.color = new Color(0.82f, 0.84f, 0.88f);

            // 대신전 상태 줄: 패널 바로 아래(없으면 숨김).
            _statusBox = NewRect("StatusBox", canvasGo.transform);
            _statusBox.anchorMin = _statusBox.anchorMax = new Vector2(0.5f, 1f);
            _statusBox.pivot = new Vector2(0.5f, 1f);
            _statusBox.anchoredPosition = new Vector2(0f, TopOffset - PanelHeight - 2f);
            _statusBox.sizeDelta = new Vector2(PanelWidth, 40f);
            _statusBox.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.7f);
            _statusText = NewText("Status", _statusBox, 13, TextAnchor.MiddleCenter);
            var statusRect = _statusText.rectTransform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(6f, 2f);
            statusRect.offsetMax = new Vector2(-6f, -2f);
            _statusText.color = FillColor;
            _statusBox.gameObject.SetActive(false);

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

            // 값이 바뀌면 숫자 옆에 증감 팝업을 잠깐 띄운다(처음 값은 제외).
            if (_shownFaith >= 0 && current != _shownFaith && Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(DeltaPopup(current - _shownFaith));
            _shownFaith = current;
        }

        private System.Collections.IEnumerator DeltaPopup(int delta)
        {
            var text = NewText("Delta", _panelRect, 20, TextAnchor.MiddleLeft);
            text.text = delta > 0 ? "+" + delta : delta.ToString();
            text.color = delta > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.5f);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(60f, 24f);
            const float duration = 1.1f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rt.anchoredPosition = new Vector2(112f, 4f + 26f * k);
                var c = text.color; c.a = 1f - k * k; text.color = c;
                yield return null;
            }
            Destroy(text.gameObject);
        }

        /// <summary>대신전 진행 상태 줄(여러 줄 가능). 비면 숨긴다.</summary>
        public void SetStatus(string text)
        {
            if (_statusText == null) return;
            _statusText.text = text ?? string.Empty;
            _statusBox.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) _statusBox.sizeDelta = new Vector2(PanelWidth, 10f + 17f * text.Split((char)10).Length);
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
