using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 기술 트리(전체 화면 창, PC·모바일 공용). 가운데 허브에서 갈래가 방사형으로 뻗고, 티어마다 바깥 원으로 나간다(2티어부터는 갈래 안에서 Slot 순서로 좌우).
    /// 나무는 끌어서 옮기고(ScrollRect), 마우스 휠·두 손가락 핀치로 확대/축소한다. 확대는 노드 자리·틀 크기를 바꾸는 방식이라 도트 아이콘은 늘 원래 픽셀 크기다.
    /// 창 크기가 바뀌면(화면 회전) 나무가 들어가도록 다시 맞추고 허브를 가운데로. 상세(설명·상태·해금)는 넓은 배치에서 오른쪽, 좁은 배치에서 아래.
    /// 해금 판정/별 소모는 BattleController → TechSystem이 한다(OnUnlockRequested).
    /// </summary>
    public class TechTreeHud : MonoBehaviour
    {
        public const int SortingOrder = 20;

        private static readonly Color LockedColor = new Color(0.20f, 0.21f, 0.25f, 1f);
        private static readonly Color AvailableColor = new Color(0.30f, 0.55f, 0.95f, 1f);
        private static readonly Color UnlockedColor = new Color(0.30f, 0.70f, 0.35f, 1f);
        private static readonly Color ConnectorColor = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color HubColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color BackgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);

        // 방사형 배치(확대 1 기준, UI 단위).
        private const float HubDiameter = 56f;
        private static readonly float[] TierDiameter = { 48f, 44f, 40f };
        private static readonly float[] TierRadius = { 115f, 205f, 315f };
        private const float TierRadiusStep = 100f; // 4티어 이상이 생기면 이 간격으로 더 바깥에.
        private const float BranchSpreadDeg = 20f;
        private const float LabelWidth = 100f;
        private const float ConnectorThickness = 3f;
        /// <summary>나무 바깥 여백(라벨 포함).</summary>
        private const float Margin = 60f;

        private const float MinZoom = 0.6f;
        private const float MaxZoom = 1.8f;
        /// <summary>처음 열 때 이보다 작게 줄이지 않는다(노드가 손가락 크기에 가깝게). 나머지는 끌어서 본다.</summary>
        private const float MinFitZoom = 0.8f;
        private const float WheelZoomPerUnit = 0.0012f;
        private const float CompactDetailHeight = 210f;

        private Font _font;
        private GameObject _root;
        private RectTransform _safe, _header, _viewport, _content, _detail;
        private ScrollRect _scroll;
        private Text _starsText;
        private readonly Dictionary<string, Image> _nodeBg = new Dictionary<string, Image>();
        private readonly List<(RectTransform Rect, Vector2 Position, float Size)> _nodeRects = new List<(RectTransform, Vector2, float)>();
        private readonly List<(RectTransform Rect, Vector2 From, Vector2 To)> _connectors = new List<(RectTransform, Vector2, Vector2)>();
        private IReadOnlyList<TechNodeData> _nodes = new List<TechNodeData>();
        private float _extent = 400f;
        private float _zoom = 1f;
        private Vector2 _fittedViewport;
        private bool? _compact;
        private float _lastPinchDistance = -1f;

        private Text _detailName, _detailEffect, _detailStatus;
        private Button _unlockButton;

        private string _selectedId;
        private TechTreeData _tech;
        private CityResourceData _resources;
        private int _cityCount;
        private bool _hasState;

        public event Action<string> OnUnlockRequested;

        public bool IsOpen => _root != null && _root.activeSelf;
        public float Zoom => _zoom;

        public void Init(Font font, IReadOnlyList<TechNodeData> nodes)
        {
            _font = font;
            _nodes = nodes ?? new List<TechNodeData>();
            _safe = UiKit.CreateCanvas(transform, SortingOrder, out var canvas);
            _root = canvas.gameObject;
            var background = UiKit.Stretch(UiKit.Rect("Background", canvas.transform));
            background.SetAsFirstSibling();
            UiKit.Fill(background, BackgroundColor, raycast: true);

            // 머리줄: 제목 · 별 · 닫기
            _header = UiKit.Row(UiKit.Rect("Header", _safe), 0f, UiKit.TopBarHeight);
            var title = UiKit.Text(_header, "Title", _font, UiKit.TitleText + 2, TextAnchor.MiddleLeft);
            title.text = LocalizationSystem.T("UI.TechTree.Toggle");
            UiKit.Stretch(title.rectTransform, UiKit.Gap * 2f, 0f, 200f, 0f);
            _starsText = UiKit.Text(_header, "Stars", _font, UiKit.BodyText + 1, TextAnchor.MiddleRight, UiKit.StarColor);
            UiKit.Place(_starsText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-UiKit.TouchSize - UiKit.Gap * 3f, 0f), new Vector2(140f, UiKit.TopBarHeight));
            var close = UiKit.Button(_header, "CloseButton", _font, "X", UiKit.ButtonColor, Close);
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(-UiKit.Gap, 0f), new Vector2(UiKit.TouchSize, UiKit.TouchSize));

            // 나무: 끌어서 옮기는 창(Tree) 안의 내용(Content).
            _viewport = UiKit.Rect("Tree", _safe);
            _viewport.gameObject.AddComponent<RectMask2D>();
            UiKit.Fill(UiKit.Stretch(UiKit.Rect("DragArea", _viewport)), Color.clear, raycast: true);
            _content = UiKit.Rect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 0f; // 휠은 확대에 쓴다

            BuildDetail();
            BuildTree();
            RefreshNodeColors();
            RefreshDetail();
            PixelUISkin.Apply(gameObject);
            Canvas.willRenderCanvases += OnWillRender;
            _root.SetActive(false);
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        private void BuildDetail()
        {
            _detail = UiKit.Rect("Detail", _safe);
            UiKit.Panel(_detail, UiKit.PanelColor);
            _detailName = UiKit.Text(_detail, "Name", _font, UiKit.TitleText, TextAnchor.UpperLeft);
            UiKit.Row(_detailName.rectTransform, 12f, 30f, 14f, 14f);
            _detailEffect = UiKit.Text(_detail, "Effect", _font, UiKit.SmallText + 1, TextAnchor.UpperLeft, UiKit.MutedText);
            _detailEffect.rectTransform.anchorMin = Vector2.zero;
            _detailEffect.rectTransform.anchorMax = Vector2.one;
            _detailEffect.rectTransform.offsetMin = new Vector2(14f, 100f);
            _detailEffect.rectTransform.offsetMax = new Vector2(-14f, -46f);
            _detailEffect.verticalOverflow = VerticalWrapMode.Truncate;
            _detailStatus = UiKit.Text(_detail, "Status", _font, UiKit.SmallText + 1, TextAnchor.MiddleLeft, UiKit.FaithColor);
            var statusRect = _detailStatus.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.offsetMin = new Vector2(14f, 64f);
            statusRect.offsetMax = new Vector2(-14f, 98f);
            _unlockButton = UiKit.Button(_detail, "UnlockButton", _font, LocalizationSystem.T("UI.TechTree.Unlock"), UiKit.PrimaryColor, RequestUnlock, UiKit.BodyText + 1);
            var unlockRect = (RectTransform)_unlockButton.transform;
            unlockRect.anchorMin = new Vector2(0f, 0f);
            unlockRect.anchorMax = new Vector2(1f, 0f);
            unlockRect.pivot = new Vector2(0.5f, 0f);
            unlockRect.offsetMin = new Vector2(12f, 12f);
            unlockRect.offsetMax = new Vector2(-12f, 12f + 48f);
        }

        public void SetNodes(IReadOnlyList<TechNodeData> nodes)
        {
            if (_content == null) return;
            _nodes = nodes ?? new List<TechNodeData>();
            _selectedId = null;
            BuildTree();
            _fittedViewport = Vector2.zero; // 다음 그리기에서 다시 맞춘다
            RefreshNodeColors();
            RefreshDetail();
            PixelUIScaler.Attach(gameObject);
        }

        public void Open()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _fittedViewport = Vector2.zero;
            PixelUIScaler.Attach(gameObject);
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // ---------- 배치 ----------

        /// <summary>그리기 직전마다: 넓은/좁은 배치를 고르고, 창 크기가 바뀌었으면 나무를 그 크기에 맞춰 다시 맞춘다.</summary>
        private void OnWillRender()
        {
            if (_root == null || !_root.activeSelf) return;
            bool compact = UiKit.IsCompact(_safe);
            if (_compact != compact)
            {
                _compact = compact;
                UiKit.LayoutDetail(_detail, compact, UiKit.TopBarHeight, CompactDetailHeight);
                UiKit.LayoutMain(_viewport, compact, UiKit.TopBarHeight, CompactDetailHeight);
            }
            var size = _viewport.rect.size;
            if (size.x > 1f && size.y > 1f && (size - _fittedViewport).sqrMagnitude > 1f)
            {
                _fittedViewport = size;
                float fit = Mathf.Min(size.x, size.y) / (_extent * 2f);
                SetZoom(Mathf.Clamp(fit, MinFitZoom, 1f));
                _content.anchoredPosition = Vector2.zero; // 허브를 가운데로
            }
        }

        /// <summary>확대 배율을 바꾸고 노드·연결선 자리와 내용 크기를 다시 계산한다. 보고 있던 지점이 화면 가운데에 남도록 내용 위치도 같은 비율로.</summary>
        public void SetZoom(float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            float ratio = zoom / _zoom;
            _zoom = zoom;
            // 내용 = 나무 + 창 하나만큼의 여유: 어떤 배율에서도 끌 수 있고, 나무 가장자리가 창 가운데까지 온다.
            var view = _viewport.rect.size;
            _content.sizeDelta = new Vector2(_extent * 2f * zoom + view.x, _extent * 2f * zoom + view.y);
            _content.anchoredPosition *= ratio;
            foreach (var n in _nodeRects)
            {
                n.Rect.anchoredPosition = n.Position * zoom;
                n.Rect.sizeDelta = new Vector2(n.Size * zoom, n.Size * zoom);
            }
            foreach (var c in _connectors)
            {
                var from = c.From * zoom;
                var to = c.To * zoom;
                var delta = to - from;
                c.Rect.anchoredPosition = from + delta * 0.5f;
                c.Rect.sizeDelta = new Vector2(delta.magnitude, ConnectorThickness);
                c.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>휠(PC)·두 손가락 핀치(터치) 확대. 손가락 하나로 끄는 것은 ScrollRect가 한다.</summary>
        private void Update()
        {
            if (!IsOpen) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(wheel, 0f) && RectTransformUtility.RectangleContainsScreenPoint(_viewport, mouse.position.ReadValue(), null))
                    SetZoom(_zoom * (1f + wheel * WheelZoomPerUnit));
            }

            var touch = Touchscreen.current;
            if (touch == null) { _lastPinchDistance = -1f; return; }
            Vector2? a = null, b = null;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed) continue;
                if (a == null) a = t.position.ReadValue();
                else { b = t.position.ReadValue(); break; }
            }
            if (a == null || b == null) { _lastPinchDistance = -1f; return; }
            float distance = Vector2.Distance(a.Value, b.Value);
            if (_lastPinchDistance > 0f && distance > 0f) SetZoom(_zoom * distance / _lastPinchDistance);
            _lastPinchDistance = distance;
        }

        // ---------- 노드 ----------

        private void BuildTree()
        {
            UiKit.ClearChildren(_content);
            _nodeBg.Clear();
            _nodeRects.Clear();
            _connectors.Clear();

            var branches = new List<string>();
            foreach (var n in _nodes)
                if (!branches.Contains(n.Branch)) branches.Add(n.Branch);

            var positions = new Dictionary<string, Vector2>();
            float step = branches.Count > 0 ? 360f / branches.Count : 0f;
            float maxRadius = 0f;
            foreach (var n in _nodes)
            {
                float branchAngle = 90f - branches.IndexOf(n.Branch) * step;
                float angle = n.Tier <= 1 ? branchAngle : branchAngle + (n.Slot == 0 ? -BranchSpreadDeg : BranchSpreadDeg);
                float radius = Radius(n.Tier);
                float rad = angle * Mathf.Deg2Rad;
                positions[n.Id] = new Vector2(radius * Mathf.Cos(rad), radius * Mathf.Sin(rad));
                maxRadius = Mathf.Max(maxRadius, radius);
            }
            _extent = maxRadius + Margin;

            // 연결선을 먼저 만들어 노드 아래에 깔리게 한다. 선행 기술이 없거나 목록에 없으면 허브에서 잇는다.
            foreach (var n in _nodes)
            {
                var from = !string.IsNullOrEmpty(n.ParentId) && positions.TryGetValue(n.ParentId, out var p) ? p : Vector2.zero;
                var line = UiKit.Rect("Connector", _content);
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
                UiKit.Fill(line, ConnectorColor);
                _connectors.Add((line, from, positions[n.Id]));
            }

            Node("Hub", Vector2.zero, HubDiameter, TechTreeDefinition.HubIcon, null).color = HubColor;
            foreach (var n in _nodes)
            {
                var bg = Node(n.Id, positions[n.Id], Diameter(n.Tier), n.Icon, n.Name);
                var button = bg.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var id = n.Id;
                button.onClick.AddListener(() => SelectNode(id));
                _nodeBg[n.Id] = bg;
            }
            SetZoom(_zoom);
        }

        private Image Node(string name, Vector2 position, float size, string icon, string label)
        {
            var rect = UiKit.Rect(name, _content);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = PixelUISkin.Badge; bg.type = Image.Type.Sliced;
            UiKit.Icon(rect, "Node", icon, new Vector2(0.5f, 0.5f), Vector2.zero, size - 12f);
            if (label != null)
            {
                var text = UiKit.Text(rect, "Label", _font, UiKit.SmallText + 1, TextAnchor.MiddleCenter);
                text.text = label;
                float width = Mathf.Min(LabelWidth, text.preferredWidth + 6f);
                // 글자 뒤를 창 배경색으로 덮어 연결선이 글자를 지나가지 않게 한다.
                var back = UiKit.Place(UiKit.Rect("LabelBack", rect), new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(width, 18f));
                UiKit.Fill(back, BackgroundColor);
                text.transform.SetParent(back, false);
                UiKit.Stretch(text.rectTransform, 0f, -6f, 0f, -6f);
            }
            _nodeRects.Add((rect, position, size));
            return bg;
        }

        private static float Radius(int tier) =>
            tier <= TierRadius.Length ? TierRadius[Mathf.Max(0, tier - 1)] : TierRadius[TierRadius.Length - 1] + (tier - TierRadius.Length) * TierRadiusStep;

        private static float Diameter(int tier) => TierDiameter[Mathf.Clamp(tier - 1, 0, TierDiameter.Length - 1)];

        // ---------- 상태 ----------

        private void SelectNode(string id)
        {
            _selectedId = id;
            RefreshNodeColors();
            RefreshDetail();
        }

        private void RequestUnlock()
        {
            if (!string.IsNullOrEmpty(_selectedId)) OnUnlockRequested?.Invoke(_selectedId);
        }

        public void SetState(TechTreeData tech, CityResourceData resources, int cityCount)
        {
            _tech = tech;
            _resources = resources;
            _cityCount = cityCount;
            _hasState = true;
            if (_starsText != null) _starsText.text = LocalizationSystem.F("UI.Hud.Stars", resources.Stars);
            RefreshNodeColors();
            RefreshDetail();
        }

        private void RefreshNodeColors()
        {
            foreach (var node in _nodes)
            {
                if (!_nodeBg.TryGetValue(node.Id, out var bg)) continue;
                var baseColor = _hasState && TechSystem.IsUnlocked(_tech, node.Id) ? UnlockedColor
                    : _hasState && TechSystem.IsAvailable(_nodes, _tech, node.Id) ? AvailableColor
                    : LockedColor;
                bg.color = node.Id == _selectedId ? Color.Lerp(baseColor, Color.white, 0.4f) : baseColor;
            }
        }

        private void RefreshDetail()
        {
            if (_detailName == null) return;
            var found = TechSystem.Find(_nodes, _selectedId);
            if (found == null || !_hasState)
            {
                _detailName.text = LocalizationSystem.T("UI.TechTree.Select");
                _detailEffect.text = string.Empty;
                _detailStatus.text = string.Empty;
                UiKit.SetEnabled(_unlockButton, false, UiKit.PrimaryColor);
                UiKit.Label(_unlockButton).text = LocalizationSystem.T("UI.TechTree.Unlock");
                return;
            }

            var node = found.Value;
            int cost = TechSystem.Cost(_nodes, _tech, node, _cityCount);
            _detailName.text = LocalizationSystem.F("UI.TechTree.Title", node.Name, node.Tier);
            _detailEffect.text = node.Effect;
            UiKit.Label(_unlockButton).text = LocalizationSystem.F("UI.TechTree.UnlockCost", cost);

            bool can = false;
            if (TechSystem.IsUnlocked(_tech, node.Id)) _detailStatus.text = LocalizationSystem.T("UI.TechTree.Unlocked");
            else if (!TechSystem.IsAvailable(_nodes, _tech, node.Id))
            {
                var parent = TechSystem.Find(_nodes, node.ParentId);
                _detailStatus.text = parent != null ? LocalizationSystem.F("UI.TechTree.NeedsParentOf", parent.Value.Name) : LocalizationSystem.T("UI.TechTree.NeedsParent");
            }
            else if (_resources.Stars < cost) _detailStatus.text = LocalizationSystem.F("UI.TechTree.NotEnoughStars", _resources.Stars, cost);
            else { _detailStatus.text = LocalizationSystem.T("UI.TechTree.Available"); can = true; }
            UiKit.SetEnabled(_unlockButton, can, UiKit.PrimaryColor);
        }
    }
}
