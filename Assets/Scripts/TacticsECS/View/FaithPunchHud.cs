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
    /// 신앙 펀치 전용 패널. 발동 절차에 맞춘 세 화면: 목록(펀치 고르기) → 대상 선택 안내 → 확인(비용·영향·발동/취소).
    /// 값만 받아 그리고 판정은 모른다(FaithPunchSystem) — 선택·확정·취소는 이벤트로 알린다. 자리는 BattleHud 정보 패널과 같다
    /// (UiKit.LayoutSheetArea — 넓은 배치는 오른쪽 열, 좁은 배치는 아래 시트). 펀치를 여는 동안 정보 패널은 닫혀 있다. 규칙: docs/spec/game/faith.md
    /// </summary>
    public class FaithPunchHud : MonoBehaviour
    {
        public const int SortingOrder = 5;
        private const float Pad = 12f;
        private const float IconInset = 40f;
        private const float CardHeight = 64f;
        private const float ButtonHeight = 44f;

        public event Action<int> OnPunchSelected;
        public event Action OnConfirmClicked;
        public event Action OnCancelClicked;

        [Header("화면(프리팹 안)")]
        [SerializeField] private RectTransform safe;
        [Tooltip("패널이 차지할 수 있는 칸(UiKit.LayoutSheetArea).")]
        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform content;

        [Header("부품 프리팹")]
        [SerializeField] private Text textPrefab;
        [SerializeField] private Button buttonPrefab;
        [Tooltip("펀치 카드(버튼 + 오른쪽 Cost 글자). Prefabs/UI/Items/FaithPunchCard.prefab.")]
        [SerializeField] private Button cardPrefab;

        private bool? _compact;
        private float _height;

        public void Init()
        {
            UiKit.ApplyTheme(transform);
            PixelUIScaler.Attach(gameObject);
            Canvas.willRenderCanvases += Fit;
            Hide();
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= Fit;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public void Hide()
        {
            if (panel == null) return;
            UiKit.ClearChildren(content);
            panel.gameObject.SetActive(false);
        }

        /// <summary>1단계: 펀치 카드 목록. 비활성 카드는 눌러도 반응이 없고 이유를 보여준다. message가 있으면 목록 위에 안내.</summary>
        public void ShowList(IReadOnlyList<FaithPunchOption> options, string message = null)
        {
            float y = Begin(LocalizationSystem.T("UI.FaithPunch.Title"), UiKit.TextColor);
            if (!string.IsNullOrEmpty(message)) y = Paragraph(message, y, UiKit.MutedText);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var o = options[i];
                var card = UiKit.Button(cardPrefab, content, "Card" + i, string.Empty, UiKit.ButtonColor, () => OnPunchSelected?.Invoke(index));
                UiKit.SetEnabled(card, o.Enabled, UiKit.ButtonColor);
                var label = UiKit.Label(card);
                string line = o.Enabled || string.IsNullOrEmpty(o.Reason) ? o.Description : o.Reason;
                var muted = ColorUtility.ToHtmlStringRGB(UiKit.MutedText);
                label.text = $"{o.Name}\n<size={UiKit.SmallText - 1}><color=#{muted}>{line}</color></size>";
                label.color = o.Enabled ? UiKit.TextColor : UiKit.DisabledText;
                // 설명이 여러 줄이면 카드를 그만큼 키운다.
                float h = Mathf.Max(CardHeight, UiKit.PreferredHeight(label, UiKit.SheetWidth(area) - 8f - (Pad - 4f) * 2f - 80f) + 14f);
                UiKit.Row((RectTransform)card.transform, y, h, Pad - 4f, Pad - 4f);
                var cost = card.transform.Find("Cost").GetComponent<Text>();
                cost.color = o.Enabled ? UiKit.TextColor : UiKit.DisabledText;
                cost.text = o.Cost.ToString();
                HudArt.UseNumberFont(cost, 24);
                Icon(card.transform, "flame", 8f, o.Enabled ? UiKit.TextColor : UiKit.DisabledText);
                UiKit.Stretch(label.rectTransform, IconInset, 2f, 56f, 2f);
                y += h + 4f;
            }
            y = Button(LocalizationSystem.T("UI.FaithPunch.Close"), y, true, () => OnCancelClicked?.Invoke());
            Finish(y);
        }

        /// <summary>2단계: 고른 펀치(이름·비용)와 취소 버튼만 — 대상 칸을 고르라는 말은 길잡이가 한다.</summary>
        public void ShowTargeting(string name, int cost)
        {
            UiKit.ClearChildren(content);
            panel.gameObject.SetActive(true);
            float y = Header(name, cost.ToString(), Pad, true);
            y = Button(LocalizationSystem.T("UI.FaithPunch.Cancel"), y, true, () => OnCancelClicked?.Invoke());
            Finish(y);
        }

        /// <summary>3단계: 확인. 머리줄에 펀치 이름과 영향받는 수(affected), 그 아래 신앙 변화(faith → faithAfter) 또는 costLine(신화 유닛 액티브처럼 다른 비용).
        /// canConfirm이 false면 이유(reason)를 보여주고 발동은 막는다.</summary>
        public void ShowConfirm(string name, string affected, int faith, int faithAfter, string costLine, bool canConfirm, string reason)
        {
            UiKit.ClearChildren(content);
            panel.gameObject.SetActive(true);
            float y = Header(name, affected, Pad, false);
            if (!canConfirm) y = Paragraph(reason, y, UiKit.EnemyColor);
            else if (!string.IsNullOrEmpty(costLine)) y = Paragraph(costLine, y, UiKit.TextColor);
            else
            {
                var row = UiKit.Text(textPrefab, content, "Faith", 24, TextAnchor.MiddleLeft, UiKit.TextColor);
                HudArt.UseNumberFont(row, 24);
                row.supportRichText = true;
                row.text = $"{faith} → <color=#{ColorUtility.ToHtmlStringRGB(UiKit.EnemyColor)}>{faithAfter}</color>";
                UiKit.Row(row.rectTransform, y, 28f, Pad + 32f, Pad);
                Icon(content, "flame", Pad, UiKit.TextColor, y + 14f);
                y += 28f + 8f;
            }
            y = Button(LocalizationSystem.T("UI.FaithPunch.Confirm"), y, canConfirm, () => OnConfirmClicked?.Invoke(), primary: true);
            y = Button(LocalizationSystem.T("UI.FaithPunch.Cancel"), y, true, () => OnCancelClicked?.Invoke());
            Finish(y);
        }

        /// <summary>머리줄: 왼쪽 이름, 오른쪽 값(비용이면 숫자 글꼴과 신앙 아이콘).</summary>
        private float Header(string name, string value, float y, bool cost)
        {
            var title = UiKit.Text(textPrefab, content, "Title", UiKit.TitleText, TextAnchor.MiddleLeft, UiKit.TextColor);
            title.text = name;
            UiKit.Row(title.rectTransform, y, 28f, Pad + (cost ? 32f : 0f), Pad + 64f);
            if (cost) Icon(content, "flame", Pad, UiKit.TextColor, y + 14f);
            var right = UiKit.Text(textPrefab, content, "Value", cost ? 24 : UiKit.BodyText, TextAnchor.MiddleRight, UiKit.TextColor);
            if (cost) HudArt.UseNumberFont(right, 24);
            right.text = value ?? string.Empty;
            UiKit.Row(right.rectTransform, y, 28f, Pad, Pad);
            return y + 28f + 8f;
        }

        /// <summary>아이콘 하나를 parent의 왼쪽(left)에 놓는다. top이 없으면 세로 가운데, 있으면 위에서 그만큼 내려온 곳이 가운데.</summary>
        private static void Icon(Transform parent, string icon, float left, Color color, float top = -1f)
        {
            var rect = (RectTransform)HudArt.PlaceIcon(parent, HudArt.Icon(icon), Vector2.zero, Vector2.zero, color).transform.parent;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, top < 0f ? 0.5f : 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, top < 0f ? 0f : -top);
        }

        // ---------- 그리기 도우미 ----------

        private float Begin(string title, Color color)
        {
            UiKit.ClearChildren(content);
            panel.gameObject.SetActive(true);
            var text = UiKit.Text(textPrefab, content, "Title", UiKit.BodyText + 1, TextAnchor.UpperLeft, color);
            text.text = title;
            float h = Mathf.Max(24f, UiKit.PreferredHeight(text, UiKit.SheetWidth(area) - 8f - Pad * 2f));
            UiKit.Row(text.rectTransform, Pad, h, Pad, Pad);
            return Pad + h + 6f;
        }

        private float Paragraph(string body, float y, Color color)
        {
            var text = UiKit.Text(textPrefab, content, "Body", UiKit.SmallText + 1, TextAnchor.UpperLeft, color);
            text.text = body;
            float h = UiKit.PreferredHeight(text, UiKit.SheetWidth(area) - 8f - Pad * 2f);
            UiKit.Row(text.rectTransform, y, h, Pad, Pad);
            return y + h + 6f;
        }

        private float Button(string label, float y, bool enabled, Action onClick, bool primary = false)
        {
            var color = primary ? UiKit.PrimaryColor : UiKit.ButtonColor;
            var button = UiKit.Button(buttonPrefab, content, label, label, color, onClick);
            UiKit.Row((RectTransform)button.transform, y, ButtonHeight, Pad - 4f, Pad - 4f);
            UiKit.SetEnabled(button, enabled, color);
            return y + ButtonHeight + 4f;
        }

        private void Finish(float y)
        {
            _height = y + Pad - 4f;
            UiKit.SetContentHeight(content, _height);
            Fit();
            PixelUIScaler.Attach(gameObject);
        }

        /// <summary>정보 패널과 같은 자리(UiKit.LayoutSheetArea): 넓은 배치는 오른쪽 열, 좁은 배치는 아래 시트. 높이는 내용만큼.</summary>
        private void Fit()
        {
            if (panel == null || area == null) return;
            bool compact = UiKit.IsCompact(safe);
            if (_compact != compact) { _compact = compact; UiKit.LayoutSheetArea(area, compact); }
            UiKit.FitSheet(area, panel, _height + 8f, compact);
        }
    }
}
