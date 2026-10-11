using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>펀치 목록의 카드 하나(FaithPunchHud.ShowList가 그린다). 값만 담는다.</summary>
    public struct FaithPunchOption
    {
        public string Name;
        public string Description;
        public int Cost;
        public bool Enabled;

        /// <summary>비활성일 때 이유 문구(없으면 빈 문자열).</summary>
        public string Reason;
    }

    /// <summary>
    /// 신앙 펀치 전용 HUD. 발동 절차에 맞춘 세 화면: 목록(펀치 고르기) → 대상 선택 안내 → 확인(비용·영향·발동/취소).
    /// 값만 받아 그리고 판정은 모른다(FaithPunchSystem) — 선택·확정·취소는 이벤트로 알린다. FaithHud와 별개의 독립 패널이며
    /// 프리팹 없이 코드로 만든다. 규칙: docs/spec/game/faith.md
    /// </summary>
    public class FaithPunchHud : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float TopOffset = -146f;
        private const float CardHeight = 58f;

        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        private static readonly Color CardColor = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color Accent = new Color(0.93f, 0.80f, 0.35f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.55f);

        public event Action<int> OnPunchSelected;
        public event Action OnConfirmClicked;
        public event Action OnCancelClicked;

        private Font _font;
        private RectTransform _panel;
        private RectTransform _content;
        private readonly List<GameObject> _dynamic = new List<GameObject>();

        public void Init(Font font)
        {
            _font = font;
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // 다른 HUD 위
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = NewRect("Panel", canvasGo.transform);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, TopOffset);
            _panel.sizeDelta = new Vector2(PanelWidth, 100f);
            _panel.gameObject.AddComponent<Image>().color = PanelColor;
            _content = _panel;

            PixelUISkin.Apply(gameObject);
            Hide();
        }

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Hide()
        {
            Clear();
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        /// <summary>1단계: 펀치 카드 목록. 비활성 카드는 눌러도 반응이 없고 이유를 보여준다. message가 있으면 목록 대신 그 안내만.</summary>
        public void ShowList(IReadOnlyList<FaithPunchOption> options, string message = null)
        {
            Begin();
            float y = -8f;
            AddText("Title", LocalizationSystem.T("UI.FaithPunch.Title"), 16, Accent, TextAnchor.MiddleLeft, new Vector2(10f, y), new Vector2(PanelWidth - 20f, 22f));
            y -= 26f;
            if (!string.IsNullOrEmpty(message))
            {
                AddText("Message", message, 13, Muted, TextAnchor.UpperLeft, new Vector2(10f, y), new Vector2(PanelWidth - 20f, 36f));
                y -= 40f;
            }
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var o = options[i];
                var card = AddButton("Card" + i, new Vector2(8f, y), new Vector2(PanelWidth - 16f, CardHeight), o.Enabled, () => OnPunchSelected?.Invoke(index));
                AddText("Name", o.Name, 15, o.Enabled ? Color.white : Muted, TextAnchor.UpperLeft, new Vector2(8f, -4f), new Vector2(PanelWidth - 110f, 20f), card);
                AddText("Cost", LocalizationSystem.F("UI.FaithPunch.CardCost", o.Cost), 13, o.Enabled ? Accent : Muted, TextAnchor.UpperRight, new Vector2(PanelWidth - 110f, -5f), new Vector2(86f, 20f), card);
                string line = o.Enabled || string.IsNullOrEmpty(o.Reason) ? o.Description : o.Reason;
                AddText("Desc", line, 12, Muted, TextAnchor.UpperLeft, new Vector2(8f, -25f), new Vector2(PanelWidth - 32f, 30f), card);
                y -= CardHeight + 4f;
            }
            AddButton("Close", new Vector2(8f, y), new Vector2(PanelWidth - 16f, 28f), true, () => OnCancelClicked?.Invoke(), LocalizationSystem.T("UI.FaithPunch.Close"));
            y -= 32f;
            Finish(-y + 4f);
        }

        /// <summary>2단계: 대상 칸을 고르라는 안내와 취소 버튼.</summary>
        public void ShowTargeting(string hint)
        {
            Begin();
            AddText("Hint", hint, 14, Color.white, TextAnchor.UpperLeft, new Vector2(10f, -8f), new Vector2(PanelWidth - 20f, 40f));
            AddButton("Cancel", new Vector2(8f, -52f), new Vector2(PanelWidth - 16f, 28f), true, () => OnCancelClicked?.Invoke(), LocalizationSystem.T("UI.FaithPunch.Cancel"));
            Finish(86f);
        }

        /// <summary>3단계: 확인. summary는 무엇이 어디에 영향을 주는지, costLine은 신앙 변화. canConfirm이 false면 이유(reason)를 보여주고 발동은 막는다.</summary>
        public void ShowConfirm(string summary, string costLine, bool canConfirm, string reason)
        {
            Begin();
            AddText("Summary", summary, 14, Color.white, TextAnchor.UpperLeft, new Vector2(10f, -8f), new Vector2(PanelWidth - 20f, 36f));
            AddText("Cost", canConfirm ? costLine : reason, 13, canConfirm ? Accent : new Color(1f, 0.55f, 0.5f), TextAnchor.UpperLeft, new Vector2(10f, -46f), new Vector2(PanelWidth - 20f, 22f));
            float half = (PanelWidth - 24f) / 2f;
            AddButton("Confirm", new Vector2(8f, -74f), new Vector2(half, 28f), canConfirm, () => OnConfirmClicked?.Invoke(), LocalizationSystem.T("UI.FaithPunch.Confirm"));
            AddButton("Cancel", new Vector2(16f + half, -74f), new Vector2(half, 28f), true, () => OnCancelClicked?.Invoke(), LocalizationSystem.T("UI.FaithPunch.Cancel"));
            Finish(112f);
        }

        // ---------- 그리기 도우미 ----------

        private void Begin()
        {
            Clear();
            _panel.gameObject.SetActive(true);
        }

        private void Finish(float height) => _panel.sizeDelta = new Vector2(PanelWidth, height);

        private void Clear()
        {
            foreach (var go in _dynamic) if (go != null) Destroy(go);
            _dynamic.Clear();
        }

        private RectTransform AddButton(string name, Vector2 topLeft, Vector2 size, bool enabled, Action onClick, string label = null)
        {
            var rt = NewRect(name, _content);
            _dynamic.Add(rt.gameObject);
            Place(rt, topLeft, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = enabled ? CardColor : new Color(CardColor.r, CardColor.g, CardColor.b, 0.5f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.interactable = enabled;
            button.onClick.AddListener(() => onClick());
            if (label != null)
                AddText("Label", label, 14, enabled ? Color.white : Muted, TextAnchor.MiddleCenter, Vector2.zero, size, rt);
            return rt;
        }

        private Text AddText(string name, string text, int size, Color color, TextAnchor anchor, Vector2 topLeft, Vector2 box, RectTransform parent = null)
        {
            var rt = NewRect(name, parent != null ? parent : _content);
            if (parent == null) _dynamic.Add(rt.gameObject);
            Place(rt, topLeft, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>부모 왼쪽 위 모서리 기준(아래로 내려갈수록 y가 음수).</summary>
        private static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }
    }
}
