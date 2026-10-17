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

        [Header("화면(프리팹 안)")]
        [Tooltip("캔버스 — 열고 닫는 단위.")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform safe;
        [SerializeField] private Text titleText;
        [SerializeField] private Text starsText;
        [SerializeField] private Button closeButton;
        [Tooltip("끌어서 옮기는 창(ScrollRect)과 그 안의 내용.")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform detail;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailEffect;
        [SerializeField] private Text detailStatus;
        [SerializeField] private Button unlockButton;

        [Header("부품 프리팹")]
        [Tooltip("노드 하나(둥근 틀 + 아이콘 NodeSlot/NodeIcon + 이름 LabelBack/Label). Prefabs/UI/Items/TechNode.prefab.")]
        [SerializeField] private RectTransform nodePrefab;
        [Tooltip("연결선(틀 없는 단색 상자). Prefabs/UI/Elements/Fill.prefab.")]
        [SerializeField] private Image fillPrefab;

        private readonly Dictionary<string, Image> _nodeBg = new Dictionary<string, Image>();
        private readonly List<(RectTransform Rect, Vector2 Position, float Size)> _nodeRects = new List<(RectTransform, Vector2, float)>();
        private readonly List<(RectTransform Rect, Vector2 From, Vector2 To)> _connectors = new List<(RectTransform, Vector2, Vector2)>();
        private IReadOnlyList<TechNodeData> _nodes = new List<TechNodeData>();
        private float _extent = 400f;
        private float _zoom = 1f;
        private Vector2 _fittedViewport;
        private bool? _compact;
        private float _lastPinchDistance = -1f;

        private string _selectedId;
        private TechTreeData _tech;
        private CityResourceData _resources;
        private int _cityCount;
        private bool _hasState;

        public event Action<string> OnUnlockRequested;

        public bool IsOpen => root != null && root.activeSelf;
        public float Zoom => _zoom;

        public void Init(IReadOnlyList<TechNodeData> nodes)
        {
            _nodes = nodes ?? new List<TechNodeData>();
            titleText.text = LocalizationSystem.T("UI.TechTree.Toggle");
            closeButton.onClick.AddListener(Close);
            unlockButton.onClick.AddListener(RequestUnlock);

            BuildTree();
            RefreshNodeColors();
            RefreshDetail();
            PixelUIScaler.Attach(gameObject);
            Canvas.willRenderCanvases += OnWillRender;
            root.SetActive(false);
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        public void SetNodes(IReadOnlyList<TechNodeData> nodes)
        {
            if (content == null) return;
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
            if (root == null) return;
            root.SetActive(true);
            _fittedViewport = Vector2.zero;
            PixelUIScaler.Attach(gameObject);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // ---------- 배치 ----------

        /// <summary>그리기 직전마다: 넓은/좁은 배치를 고르고, 창 크기가 바뀌었으면 나무를 그 크기에 맞춰 다시 맞춘다.</summary>
        private void OnWillRender()
        {
            if (root == null || !root.activeSelf) return;
            bool compact = UiKit.IsCompact(safe);
            if (_compact != compact)
            {
                _compact = compact;
                UiKit.LayoutDetail(detail, compact, UiKit.TopBarHeight, CompactDetailHeight);
                UiKit.LayoutMain(viewport, compact, UiKit.TopBarHeight, CompactDetailHeight);
            }
            var size = viewport.rect.size;
            if (size.x > 1f && size.y > 1f && (size - _fittedViewport).sqrMagnitude > 1f)
            {
                _fittedViewport = size;
                float fit = Mathf.Min(size.x, size.y) / (_extent * 2f);
                SetZoom(Mathf.Clamp(fit, MinFitZoom, 1f));
                content.anchoredPosition = Vector2.zero; // 허브를 가운데로
            }
        }

        /// <summary>확대 배율을 바꾸고 노드·연결선 자리와 내용 크기를 다시 계산한다. 보고 있던 지점이 화면 가운데에 남도록 내용 위치도 같은 비율로.</summary>
        public void SetZoom(float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            float ratio = zoom / _zoom;
            _zoom = zoom;
            // 내용 = 나무 + 창 하나만큼의 여유: 어떤 배율에서도 끌 수 있고, 나무 가장자리가 창 가운데까지 온다.
            var view = viewport.rect.size;
            content.sizeDelta = new Vector2(_extent * 2f * zoom + view.x, _extent * 2f * zoom + view.y);
            content.anchoredPosition *= ratio;
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
                if (!Mathf.Approximately(wheel, 0f) && RectTransformUtility.RectangleContainsScreenPoint(viewport, mouse.position.ReadValue(), null))
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
            UiKit.ClearChildren(content);
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
                var line = UiKit.Fill(fillPrefab, content, "Connector", ConnectorColor).rectTransform;
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
                _connectors.Add((line, from, positions[n.Id]));
            }

            var hub = Node("Hub", Vector2.zero, HubDiameter, TechTreeDefinition.HubIcon, null);
            hub.color = HubColor;
            hub.GetComponent<Button>().enabled = false;
            foreach (var n in _nodes)
            {
                var bg = Node(n.Id, positions[n.Id], Diameter(n.Tier), n.Icon, n.Name);
                var id = n.Id;
                bg.GetComponent<Button>().onClick.AddListener(() => SelectNode(id));
                _nodeBg[n.Id] = bg;
            }
            SetZoom(_zoom);
        }

        private Image Node(string name, Vector2 position, float size, string icon, string label)
        {
            var rect = UiKit.Spawn(nodePrefab, content, name);
            var bg = rect.GetComponent<Image>();
            var slot = UiKit.Place((RectTransform)rect.Find("NodeSlot"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 12f, size - 12f));
            UiKit.SetIcon(slot.GetChild(0).GetComponent<Image>(), icon);
            // 이름은 창 배경색 띠(LabelBack — 연결선이 글자를 지나가지 않게) 위에. 허브는 이름이 없다.
            var back = (RectTransform)rect.Find("LabelBack");
            back.gameObject.SetActive(label != null);
            if (label != null)
            {
                var text = back.GetComponentInChildren<Text>();
                text.text = label;
                back.sizeDelta = new Vector2(Mathf.Min(LabelWidth, text.preferredWidth + 6f), back.sizeDelta.y);
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
            if (starsText != null) starsText.text = LocalizationSystem.F("UI.Hud.Stars", resources.Stars);
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
            if (detailName == null) return;
            var found = TechSystem.Find(_nodes, _selectedId);
            if (found == null || !_hasState)
            {
                detailName.text = LocalizationSystem.T("UI.TechTree.Select");
                detailEffect.text = string.Empty;
                detailStatus.text = string.Empty;
                UiKit.SetEnabled(unlockButton, false, UiKit.PrimaryColor);
                UiKit.Label(unlockButton).text = LocalizationSystem.T("UI.TechTree.Unlock");
                return;
            }

            var node = found.Value;
            int cost = TechSystem.Cost(_nodes, _tech, node, _cityCount);
            detailName.text = LocalizationSystem.F("UI.TechTree.Title", node.Name, node.Tier);
            detailEffect.text = node.Effect;
            UiKit.Label(unlockButton).text = LocalizationSystem.F("UI.TechTree.UnlockCost", cost);

            bool can = false;
            if (TechSystem.IsUnlocked(_tech, node.Id)) detailStatus.text = LocalizationSystem.T("UI.TechTree.Unlocked");
            else if (!TechSystem.IsAvailable(_nodes, _tech, node.Id))
            {
                var parent = TechSystem.Find(_nodes, node.ParentId);
                detailStatus.text = parent != null ? LocalizationSystem.F("UI.TechTree.NeedsParentOf", parent.Value.Name) : LocalizationSystem.T("UI.TechTree.NeedsParent");
            }
            else if (_resources.Stars < cost) detailStatus.text = LocalizationSystem.F("UI.TechTree.NotEnoughStars", _resources.Stars, cost);
            else { detailStatus.text = LocalizationSystem.T("UI.TechTree.Available"); can = true; }
            UiKit.SetEnabled(unlockButton, can, UiKit.PrimaryColor);
        }
    }
}
