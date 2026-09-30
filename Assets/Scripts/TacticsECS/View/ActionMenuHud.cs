using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>[아이콘][숫자] 칩 하나(메뉴 머리의 도시 정보, 버튼 결과 미리보기).</summary>
    public struct ActionMenuChip
    {
        public string Icon;
        public string Text;
        public Color Tint;
        /// <summary>칩에 손가락/마우스를 올리면 정보 줄에 보여줄 말(비우면 없음).</summary>
        public string Tooltip;
    }

    /// <summary>메뉴 머리: 대상 아이콘 + 이름 + 레벨 배지 + (도시면) 인구 칸 게이지 + 상태 칩 줄. 문장 없이 그림으로만 요약한다.</summary>
    public struct ActionMenuHeader
    {
        public string Icon;
        public string Title;
        /// <summary>0보다 크면 제목 옆 원 안에 숫자(도시 레벨).</summary>
        public int Level;
        public Color Accent;
        /// <summary>GaugeTotal &gt; 0이면 칸 게이지(도시 인구: 채운 칸 = 인구, 칸 수 = 다음 레벨까지 필요한 인구).</summary>
        public int GaugeFilled;
        public int GaugeTotal;
        public Color GaugeColor;
        /// <summary>게이지 오른쪽 끝 아이콘(도시 = population).</summary>
        public string GaugeIcon;
        public List<ActionMenuChip> Chips;
    }

    /// <summary>ActionMenuHud에 넘기는 버튼 하나. 버튼에는 아이콘 + 한 단어 이름 + 비용 배지만 보이고, Detail/Yields/막힌 이유는
    /// 누르고 있거나 마우스를 올렸을 때 정보 줄에 나타난다.</summary>
    public struct ActionMenuOption
    {
        /// <summary>버튼 아래 한 단어 이름.</summary>
        public string Label;
        /// <summary>정보 줄 설명 문장(툴팁).</summary>
        public string Detail;
        public string Icon;
        /// <summary>0이면 비용 배지 없음.</summary>
        public int Cost;
        /// <summary>비용 아이콘(기본 "star" = 골드).</summary>
        public string CostIcon;
        public bool Affordable;
        public bool Enabled;
        /// <summary>None이 아니면 오른쪽 위 이유 아이콘 + 정보 줄 이유 문구.</summary>
        public BlockReason Block;
        /// <summary>하면 얻는 것(정보 줄 칩: [+2 인구], [+1 골드] …).</summary>
        public List<ActionMenuChip> Yields;
        /// <summary>왼쪽 위 빨간 숫자 배지(예: 받을 보상 수).</summary>
        public int Badge;
        /// <summary>버튼 원 색(비우면 기본 파랑).</summary>
        public Color? Color;
        public Action OnClick;
    }

    /// <summary>
    /// "상황별 메뉴" — 도시(유닛 훈련/레벨업 보상), 타일(채집/건설), 유닛(점령/유적 탐험/해산)처럼 무엇을 선택했느냐에 따라 버튼 개수와
    /// 내용이 매번 바뀌는 목록. 프리팹 없이 코드로 계층을 만든다(내용 전체가 가변 목록 — BattleHud 로스터/로그와 같은 예외).
    ///
    /// Polytopia 레퍼런스(docs/UxIconizationPlan.md 2.1): 예전의 세로 텍스트 목록("농장 (골드 5)\n작물 위. 인구 +2.")을
    /// 원형 아이콘 버튼 격자 + ⭐비용 배지 + 회색/이유 아이콘으로 바꾸고, 도시 정보 문장("인구 2/3 · 유닛 1/3 · 골드 +3/턴 …")은
    /// 칸 게이지와 [아이콘][숫자] 칩으로 바꿨다. 설명 문장은 버튼을 누르고 있거나 마우스를 올렸을 때 아래 정보 줄에만 나온다.
    ///
    /// 모바일: 가로 화면에서는 오른쪽 가운데 패널, 세로 화면에서는 화면 아래쪽 전체 폭 시트(유닛 패널 위)로 바뀐다
    /// (ResponsiveCanvas). 내용이 화면보다 길면 세로로 스크롤한다. 오른쪽 위 X로 닫는다(터치에서는 바깥 클릭이 곧 다른 칸 선택이라).
    /// </summary>
    public class ActionMenuHud : MonoBehaviour
    {
        private const float LandscapeWidth = 300f;
        private const float Padding = 10f;
        private const float HeaderHeight = 28f;
        private const float CellWidth = 70f;
        private const float CircleSize = 50f;
        private const float CompactCircleSize = 56f;
        private const float InfoMinHeight = 34f;

        /// <summary>세로 화면에서 시트 아래쪽 여백 — BattleHud 유닛 패널(높이 206, 아래 여백 16) 바로 위.</summary>
        private const float PortraitBottom = 16f + 206f + 8f;
        /// <summary>세로 화면에서 시트가 차지할 수 있는 최대 높이 비율(나머지는 지도).</summary>
        private const float PortraitMaxHeightRatio = 0.42f;
        private const float LandscapeMaxHeightRatio = 0.78f;

        private Font _font;
        private ResponsiveCanvas _responsive;
        private RectTransform _panel;
        private RectTransform _content;
        private ScrollRect _scroll;
        private RectTransform _info;
        private Text _infoText;
        private RectTransform _infoChips;

        /// <summary>정보 줄을 뺀 패널 높이(내용 + 여백, 최대 높이로 자른 값). 정보 줄이 나타나면 그만큼 패널이 커진다.</summary>
        private float _bodyHeight;

        private ActionMenuHeader _header;
        private IReadOnlyList<ActionMenuOption> _options;

        public bool IsVisible => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>X 버튼으로 닫혔을 때(BattleController가 "지금 메뉴가 보여주는 칸" 기록을 지운다).</summary>
        public event Action Closed;

        public void Init(Font font)
        {
            _font = font;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _responsive = ResponsiveCanvas.Attach(canvasGo.transform);

            _panel = UiKit.Rect("Panel", _responsive.SafeArea);
            _panel.gameObject.AddComponent<Image>().color = UiKit.PanelColor;

            // 스크롤 영역(내용이 화면보다 길 때만 실제로 움직인다).
            var viewport = UiKit.Rect("Viewport", _panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.gameObject.AddComponent<RectMask2D>();
            _content = UiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _scroll = _panel.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 20f;

            // 정보 줄(툴팁): 패널 맨 아래 고정. 스크롤 영역 밖이라 항상 보인다.
            _info = UiKit.Rect("Info", _panel);
            _info.anchorMin = new Vector2(0f, 0f);
            _info.anchorMax = new Vector2(1f, 0f);
            _info.pivot = new Vector2(0.5f, 0f);
            var infoBg = _info.gameObject.AddComponent<Image>();
            infoBg.color = new Color(0f, 0f, 0f, 0.45f);
            infoBg.raycastTarget = false;
            _infoChips = UiKit.Rect("Chips", _info);
            _infoText = UiKit.Label(_info, _font, string.Empty, 12, UiKit.MutedColor, TextAnchor.UpperLeft);
            _infoText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var close = UiKit.Image(_panel, "Close", RuntimeSprite.CreateCircle(), new Color(1f, 1f, 1f, 0.12f), raycast: true);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 1f);
            close.rectTransform.sizeDelta = new Vector2(30f, 30f);
            close.rectTransform.anchoredPosition = new Vector2(-6f, -6f);
            var closeIcon = UiKit.Image(close.transform, "Icon", IconLibrary.Get("deselect"), Color.white);
            UiKit.Stretch(closeIcon.rectTransform, 7f);
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;
            closeButton.onClick.AddListener(() => { Hide(); Closed?.Invoke(); });

            _panel.gameObject.SetActive(false);
            _responsive.LayoutChanged += _ => { if (IsVisible) Rebuild(); };
        }

        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        public void Show(ActionMenuHeader header, IReadOnlyList<ActionMenuOption> options)
        {
            _header = header;
            _options = options;
            _panel.gameObject.SetActive(true);
            _responsive.Refresh();
            Rebuild();
        }

        // ---------- 그리기 ----------

        private void Rebuild()
        {
            UiKit.DestroyChildren(_content);

            bool portrait = _responsive.Portrait;
            var area = _responsive.Size;
            float width = portrait ? Mathf.Max(200f, area.x - 24f) : LandscapeWidth;
            float inner = width - Padding * 2f;
            float circle = _responsive.Compact ? CompactCircleSize : CircleSize;
            float cellWidth = circle + 20f;

            float y = Padding;
            y = BuildHeader(inner, y);

            if (_options != null && _options.Count > 0)
            {
                var grid = UiKit.TopLeft(UiKit.Rect("Options", _content), new Vector2(inner, 0f));
                grid.anchoredPosition = new Vector2(Padding, -y);
                int columns = Mathf.Max(1, Mathf.FloorToInt(inner / Mathf.Max(cellWidth, CellWidth)));
                float colWidth = inner / columns;
                float cellHeight = 0f;
                for (int i = 0; i < _options.Count; i++)
                {
                    var cell = BuildOption(grid, _options[i], colWidth, circle);
                    cellHeight = cell.sizeDelta.y;
                    cell.anchoredPosition = new Vector2((i % columns) * colWidth, -(i / columns) * (cellHeight + 6f));
                }
                int rows = (_options.Count + columns - 1) / columns;
                float gridHeight = rows * (cellHeight + 6f) - 6f;
                grid.sizeDelta = new Vector2(inner, gridHeight);
                y += gridHeight + Padding;
            }

            _content.sizeDelta = new Vector2(0f, y);

            float maxHeight = area.y * (portrait ? PortraitMaxHeightRatio : LandscapeMaxHeightRatio);
            _bodyHeight = Mathf.Min(y, Mathf.Max(120f, maxHeight));
            ((RectTransform)_scroll.viewport).offsetMax = Vector2.zero;
            _content.anchoredPosition = Vector2.zero;

            if (portrait)
            {
                _panel.anchorMin = new Vector2(0.5f, 0f);
                _panel.anchorMax = new Vector2(0.5f, 0f);
                _panel.pivot = new Vector2(0.5f, 0f);
                _panel.anchoredPosition = new Vector2(0f, PortraitBottom);
            }
            else
            {
                _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 0.5f);
                _panel.pivot = new Vector2(1f, 0.5f);
                _panel.anchoredPosition = new Vector2(-12f, -20f);
            }
            _panel.sizeDelta = new Vector2(width, _bodyHeight);
            ShowInfo(null, null, BlockReason.None);
        }

        private float BuildHeader(float inner, float y)
        {
            var accent = UiKit.Image(_content, "Accent", null, _header.Accent.a > 0f ? _header.Accent : Color.white);
            UiKit.TopLeft(accent.rectTransform, new Vector2(4f, HeaderHeight));
            accent.rectTransform.anchoredPosition = new Vector2(Padding, -y);

            float x = Padding + 10f;
            if (!string.IsNullOrEmpty(_header.Icon))
            {
                var icon = UiKit.Icon(_content, _header.Icon, Color.white, HeaderHeight - 2f);
                icon.rectTransform.anchoredPosition = new Vector2(x, -y - 1f);
                x += HeaderHeight + 4f;
            }
            var title = UiKit.Label(_content, _font, _header.Title, 17, Color.white);
            UiKit.TopLeft(title.rectTransform, new Vector2(0f, HeaderHeight));
            float tw = Mathf.Min(UiKit.TextWidth(title), inner - (x - Padding) - 70f);
            title.rectTransform.sizeDelta = new Vector2(tw, HeaderHeight);
            title.rectTransform.anchoredPosition = new Vector2(x, -y);
            x += tw + 6f;
            if (_header.Level > 0)
            {
                var badge = UiKit.NumberBadge(_content, _font, _header.Level, _header.Accent.a > 0f ? _header.Accent : UiKit.ButtonColor, 24f);
                badge.anchoredPosition = new Vector2(x, -y - 2f);
            }
            y += HeaderHeight + 6f;

            if (_header.GaugeTotal > 0)
            {
                var gauge = UiKit.SegmentGauge(_content, _header.GaugeFilled, _header.GaugeTotal,
                    _header.GaugeColor.a > 0f ? _header.GaugeColor : UiKit.PopulationColor, inner - 36f, 10f);
                gauge.anchoredPosition = new Vector2(Padding, -y);
                if (!string.IsNullOrEmpty(_header.GaugeIcon))
                {
                    var gi = UiKit.Icon(_content, _header.GaugeIcon, _header.GaugeColor.a > 0f ? _header.GaugeColor : UiKit.PopulationColor, 14f);
                    gi.rectTransform.anchoredPosition = new Vector2(Padding + inner - 30f, -y + 2f);
                }
                y += 16f;
            }

            if (_header.Chips != null && _header.Chips.Count > 0)
            {
                var row = UiKit.TopLeft(UiKit.Rect("Chips", _content), new Vector2(inner, 0f));
                row.anchoredPosition = new Vector2(Padding, -y);
                foreach (var c in _header.Chips)
                {
                    var chip = UiKit.Chip(row, _font, c.Icon, c.Text, c.Tint.a > 0f ? c.Tint : Color.white, 22f);
                    if (!string.IsNullOrEmpty(c.Tooltip))
                    {
                        var img = chip.gameObject.GetComponent<Image>();
                        if (img == null)
                        {
                            img = chip.gameObject.AddComponent<Image>();
                            img.color = new Color(0f, 0f, 0f, 0f);
                        }
                        img.raycastTarget = true;
                        var trigger = chip.gameObject.AddComponent<TooltipTrigger>();
                        string tip = c.Tooltip;
                        trigger.Text = tip;
                        trigger.OnEnter = t => ShowInfo(t, null, BlockReason.None);
                        trigger.OnExit = HideInfoSoon;
                    }
                }
                float h = UiKit.Flow(row, inner, 10f, 4f);
                row.sizeDelta = new Vector2(inner, h);
                y += h + 8f;
            }
            return y + 2f;
        }

        private RectTransform BuildOption(RectTransform grid, ActionMenuOption option, float colWidth, float circle)
        {
            var spec = new IconButtonSpec
            {
                Icon = option.Icon,
                Caption = option.Label,
                Enabled = option.Enabled,
                Color = option.Color ?? UiKit.ButtonColor,
                Cost = option.Cost,
                CostIcon = option.CostIcon,
                Affordable = option.Affordable || option.Block != BlockReason.NotEnoughGold,
                StatusIcon = option.Block != BlockReason.None && option.Block != BlockReason.NotEnoughGold ? MenuIcons.ForBlock(option.Block) : null,
                StatusTint = MenuIcons.BlockTint(option.Block),
                Badge = option.Badge,
            };
            var cell = UiKit.IconButton(grid, _font, spec, colWidth, circle, out var button);
            var onClick = option.OnClick;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            // 설명은 버튼이 아니라 정보 줄에: 마우스를 올리거나 손가락으로 누르고 있을 때(터치는 뗀 뒤 잠시 유지).
            var trigger = button.gameObject.AddComponent<TooltipTrigger>();
            var detail = option.Detail;
            var yields = option.Yields;
            var block = option.Block;
            var name = option.Label;
            trigger.Text = name;
            trigger.OnEnter = _ => ShowInfo(string.IsNullOrEmpty(detail) ? name : $"<b>{name}</b>  {detail}", yields, block);
            trigger.OnExit = HideInfoSoon;
            return cell;
        }

        // ---------- 정보 줄 ----------

        private float _hideAt = -1f;

        private void HideInfoSoon()
        {
            // 터치는 손가락을 떼면 곧바로 Exit가 오므로 잠깐 남겨 두어 읽을 수 있게 한다.
            _hideAt = Time.unscaledTime + (ScreenLayout.IsTouch ? 2.5f : 0f);
            if (!ScreenLayout.IsTouch) ShowInfo(null, null, BlockReason.None);
        }

        private void Update()
        {
            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                _hideAt = -1f;
                ShowInfo(null, null, BlockReason.None);
            }
        }

        private void ShowInfo(string text, List<ActionMenuChip> chips, BlockReason block)
        {
            _hideAt = -1f;
            UiKit.DestroyChildren(_infoChips);

            float inner = _panel.sizeDelta.x > 0f ? _panel.sizeDelta.x - Padding * 2f : LandscapeWidth - Padding * 2f;
            bool empty = string.IsNullOrEmpty(text) && (chips == null || chips.Count == 0) && block == BlockReason.None;
            float y = 6f;

            UiKit.TopLeft(_infoChips, new Vector2(inner, 0f));
            _infoChips.anchoredPosition = new Vector2(Padding, -y);
            if (block != BlockReason.None)
                UiKit.Chip(_infoChips, _font, MenuIcons.ForBlock(block), MenuIcons.BlockText(block), UiKit.WarnColor, 18f);
            if (chips != null)
                foreach (var c in chips) UiKit.Chip(_infoChips, _font, c.Icon, c.Text, c.Tint.a > 0f ? c.Tint : Color.white, 18f);
            float chipsH = UiKit.Flow(_infoChips, inner, 10f, 2f);
            _infoChips.sizeDelta = new Vector2(inner, chipsH);
            if (chipsH > 0f) y += chipsH + 3f;

            _infoText.text = empty ? string.Empty : text ?? string.Empty;
            UiKit.TopLeft(_infoText.rectTransform, new Vector2(inner, 0f));
            _infoText.rectTransform.anchoredPosition = new Vector2(Padding, -y);
            float textH = string.IsNullOrEmpty(_infoText.text) ? 0f : Mathf.Ceil(_infoText.cachedTextGeneratorForLayout.GetPreferredHeight(
                _infoText.text, _infoText.GetGenerationSettings(new Vector2(inner, 0f))) / _infoText.pixelsPerUnit);
            _infoText.rectTransform.sizeDelta = new Vector2(inner, textH);
            y += textH + 6f;

            float h = empty ? 0f : Mathf.Max(InfoMinHeight, y);
            _info.sizeDelta = new Vector2(0f, h);
            _info.gameObject.SetActive(!empty);
            if (_scroll != null) ((RectTransform)_scroll.viewport).offsetMin = new Vector2(0f, h);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, _bodyHeight + h);
        }
    }
}
