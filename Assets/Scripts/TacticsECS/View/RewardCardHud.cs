using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>보상 카드 한 장(순수 값).</summary>
    public struct RewardCard
    {
        public string Icon;
        public string Name;
        public string EffectIcon;
        public string EffectText;
        public Color EffectTint;
        /// <summary>카드를 누르고 있거나 마우스를 올렸을 때 아래에 보여줄 설명 문장.</summary>
        public string Detail;
    }

    /// <summary>
    /// 도시 레벨업 보상 선택 모달 — Polytopia처럼 "그림 카드 두 장 중 하나"를 고른다(docs/UxIconizationPlan.md 2.2). 예전에는 도시
    /// 메뉴 안에 "Lv2 보상: 공방 / 골드 수입 +1/턴" 텍스트 행으로 나열됐다. 카드마다 큰 아이콘 + 한 단어 이름 + 효과 칩([+1 ⭐/턴])만
    /// 보이고, 설명 문장은 카드를 누르고 있을 때(마우스는 올렸을 때) 아래 한 줄에 나온다. X로 닫으면 나중에 도시 메뉴의 보상 버튼으로 다시 연다.
    /// 값은 판단하지 않고 BattleController가 넘긴 카드를 그리기만 한다. 프리팹 없이 코드로 만든다(카드 수가 가변).
    /// </summary>
    public class RewardCardHud : MonoBehaviour
    {
        private const float CardWidth = 180f;
        private const float CardHeight = 220f;
        private const float CardGap = 20f;

        private Font _font;
        private ResponsiveCanvas _responsive;
        private GameObject _root;
        private RectTransform _cards;
        private Text _title;
        private Text _detail;
        private RectTransform _levelBadgeHolder;

        private IReadOnlyList<RewardCard> _shown;
        private Action<int> _onPick;
        private int _level;
        private Color _accent;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Init(Font font)
        {
            _font = font;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8; // 상황별 메뉴(5) 위, 기술트리(10) 아래.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _responsive = ResponsiveCanvas.Attach(canvasGo.transform);

            // 화면 전체를 덮는 어둡게 깔린 배경(세이프 에어리어 밖까지). 뒤의 지도 클릭을 막는다.
            var dim = UiKit.Image(canvasGo.transform, "Dim", null, new Color(0f, 0f, 0f, 0.6f), raycast: true);
            UiKit.Stretch(dim.rectTransform);
            dim.transform.SetAsFirstSibling();
            _root = canvasGo;

            var box = UiKit.Rect("Box", _responsive.SafeArea);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);

            _levelBadgeHolder = UiKit.Rect("LevelBadge", box);
            _title = UiKit.Label(box, _font, string.Empty, 22, Color.white, TextAnchor.MiddleCenter);
            UiKit.Outline(_title);
            _cards = UiKit.Rect("Cards", box);
            _detail = UiKit.Label(box, _font, string.Empty, 14, UiKit.MutedColor, TextAnchor.UpperCenter);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;

            var close = UiKit.Image(box, "Close", RuntimeSprite.CreateCircle(), new Color(1f, 1f, 1f, 0.15f), raycast: true);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 1f);
            close.rectTransform.sizeDelta = new Vector2(36f, 36f);
            var closeIcon = UiKit.Image(close.transform, "Icon", IconLibrary.Get("deselect"), Color.white);
            UiKit.Stretch(closeIcon.rectTransform, 8f);
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;
            closeButton.onClick.AddListener(Hide);

            _root.SetActive(false);
            _responsive.LayoutChanged += _ => { if (IsVisible) Rebuild(); };
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>onPick(카드 index)는 카드를 고르면 불린다(모달은 스스로 닫는다).</summary>
        public void Show(string cityName, int level, Color accent, IReadOnlyList<RewardCard> cards, Action<int> onPick)
        {
            _shown = cards;
            _onPick = onPick;
            _level = level;
            _accent = accent;
            _title.text = cityName;
            _root.SetActive(true);
            _responsive.Refresh();
            Rebuild();
        }

        private void Rebuild()
        {
            UiKit.DestroyChildren(_cards);
            UiKit.DestroyChildren(_levelBadgeHolder);
            if (_shown == null) return;

            // 세로 화면에서 카드가 옆으로 다 안 들어가면 카드를 줄인다.
            var area = _responsive.Size;
            int n = Mathf.Max(1, _shown.Count);
            float scale = Mathf.Min(1f, (area.x - 32f) / (n * CardWidth + (n - 1) * CardGap));
            float cw = CardWidth * scale, ch = CardHeight * scale, gap = CardGap * scale;
            float rowWidth = n * cw + (n - 1) * gap;

            var box = (RectTransform)_cards.parent;
            box.sizeDelta = new Vector2(Mathf.Max(rowWidth, 300f), ch + 130f);

            var badge = UiKit.NumberBadge(_levelBadgeHolder, _font, _level, _accent, 34f);
            _levelBadgeHolder.anchorMin = _levelBadgeHolder.anchorMax = _levelBadgeHolder.pivot = new Vector2(0.5f, 1f);
            _levelBadgeHolder.sizeDelta = new Vector2(34f, 34f);
            _levelBadgeHolder.anchoredPosition = Vector2.zero;
            badge.anchoredPosition = Vector2.zero;

            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(0f, 30f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -38f);

            _cards.anchorMin = _cards.anchorMax = _cards.pivot = new Vector2(0.5f, 1f);
            _cards.sizeDelta = new Vector2(rowWidth, ch);
            _cards.anchoredPosition = new Vector2(0f, -76f);

            for (int i = 0; i < _shown.Count; i++)
                BuildCard(_shown[i], i, cw, ch).anchoredPosition = new Vector2(i * (cw + gap), 0f);

            _detail.rectTransform.anchorMin = new Vector2(0f, 1f);
            _detail.rectTransform.anchorMax = new Vector2(1f, 1f);
            _detail.rectTransform.pivot = new Vector2(0.5f, 1f);
            _detail.rectTransform.sizeDelta = new Vector2(0f, 44f);
            _detail.rectTransform.anchoredPosition = new Vector2(0f, -76f - ch - 10f);
            _detail.text = string.Empty;
        }

        private RectTransform BuildCard(RewardCard card, int index, float w, float h)
        {
            var bg = UiKit.Image(_cards, "Card", null, new Color(0.14f, 0.16f, 0.22f, 0.97f), raycast: true);
            UiKit.TopLeft(bg.rectTransform, new Vector2(w, h));
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = _accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var circle = UiKit.Image(bg.transform, "Circle", RuntimeSprite.CreateCircle(), _accent);
            circle.rectTransform.anchorMin = circle.rectTransform.anchorMax = circle.rectTransform.pivot = new Vector2(0.5f, 1f);
            float d = w * 0.62f;
            circle.rectTransform.sizeDelta = new Vector2(d, d);
            circle.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.08f);
            var icon = UiKit.Image(circle.transform, "Icon", IconLibrary.Get(card.Icon), Color.white);
            UiKit.Stretch(icon.rectTransform, d * 0.18f);

            var name = UiKit.Label(bg.transform, _font, card.Name, Mathf.RoundToInt(18f * w / CardWidth), Color.white, TextAnchor.MiddleCenter);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.sizeDelta = new Vector2(0f, 26f);
            name.rectTransform.anchoredPosition = new Vector2(0f, h * 0.2f);

            if (!string.IsNullOrEmpty(card.EffectIcon) || !string.IsNullOrEmpty(card.EffectText))
            {
                var chip = UiKit.Chip(bg.transform, _font, card.EffectIcon, card.EffectText, card.EffectTint.a > 0f ? card.EffectTint : Color.white,
                    24f, new Color(0f, 0f, 0f, 0.5f));
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0f);
                chip.pivot = new Vector2(0.5f, 0f);
                chip.anchoredPosition = new Vector2(0f, h * 0.06f);
            }

            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            int picked = index;
            button.onClick.AddListener(() =>
            {
                Hide();
                _onPick?.Invoke(picked);
            });
            var trigger = bg.gameObject.AddComponent<TooltipTrigger>();
            trigger.Text = card.Detail;
            trigger.OnEnter = t => _detail.text = t ?? string.Empty;
            trigger.OnExit = () => { if (!ScreenLayout.IsTouch) _detail.text = string.Empty; };
            return bg.rectTransform;
        }
    }
}
