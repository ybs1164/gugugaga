using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 기술 트리(전체 화면 창, PC·모바일 공용). 위에서 아래로 뻗는 나무: 맨 위 허브 → 1티어 → 2티어 …, 같은 갈래의 자식은 Slot 순서로 나란히.
    /// 노드 자리는 나무 영역에 대한 비율(앵커)로만 정해서 어떤 화면 크기에서도 겹치지 않고, 연결선은 ㄱ자 꺾은선이다.
    /// 오른쪽은 고른 기술의 설명·상태·해금 버튼. 해금 판정/별 소모는 BattleController → TechSystem이 한다(OnUnlockRequested).
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

        private const float NodeSize = 44f;
        private const float ConnectorThickness = 3f;

        private Font _font;
        private GameObject _root;
        private RectTransform _tree;
        private Text _starsText;
        private readonly Dictionary<string, Image> _nodeBg = new Dictionary<string, Image>();
        private IReadOnlyList<TechNodeData> _nodes = new List<TechNodeData>();

        private Text _detailName, _detailEffect, _detailStatus;
        private Button _unlockButton;

        private string _selectedId;
        private TechTreeData _tech;
        private CityResourceData _resources;
        private int _cityCount;
        private bool _hasState;

        public event Action<string> OnUnlockRequested;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Init(Font font, IReadOnlyList<TechNodeData> nodes)
        {
            _font = font;
            _nodes = nodes ?? new List<TechNodeData>();
            var safe = UiKit.CreateCanvas(transform, SortingOrder, out var canvas);
            _root = canvas.gameObject;
            var background = UiKit.Stretch(UiKit.Rect("BackgroundFill", canvas.transform));
            background.SetAsFirstSibling();
            UiKit.Fill(background, BackgroundColor, raycast: true);

            // 머리줄: 제목 · 별 · 닫기
            var header = UiKit.Row(UiKit.Rect("Header", safe), 0f, UiKit.TopBarHeight);
            var title = UiKit.Text(header, "Title", _font, UiKit.TitleText + 2, TextAnchor.MiddleLeft);
            title.text = LocalizationSystem.T("UI.TechTree.Toggle");
            UiKit.Stretch(title.rectTransform, UiKit.Gap * 2f, 0f, 260f, 0f);
            _starsText = UiKit.Text(header, "Stars", _font, UiKit.BodyText + 1, TextAnchor.MiddleRight, UiKit.StarColor);
            UiKit.Place(_starsText.rectTransform, new Vector2(1f, 0.5f), new Vector2(-UiKit.TouchSize - UiKit.Gap * 3f, 0f), new Vector2(200f, UiKit.TopBarHeight));
            var close = UiKit.Button(header, "CloseButton", _font, "X", UiKit.ButtonColor, Close);
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(-UiKit.Gap, 0f), new Vector2(UiKit.TouchSize, UiKit.TouchSize));

            _tree = UiKit.Rect("Tree", safe);
            UiKit.Stretch(_tree, UiKit.Gap, UiKit.Gap, UiKit.SidePanelWidth + UiKit.Gap * 2f, UiKit.TopBarHeight + UiKit.Gap);

            BuildDetail(safe);
            BuildTree();
            RefreshNodeColors();
            RefreshDetail();
            PixelUISkin.Apply(gameObject);
            _root.SetActive(false);
        }

        private void BuildDetail(RectTransform safe)
        {
            var detail = UiKit.Rect("Detail", safe);
            detail.anchorMin = new Vector2(1f, 0f);
            detail.anchorMax = new Vector2(1f, 1f);
            detail.pivot = new Vector2(1f, 0.5f);
            detail.offsetMin = new Vector2(-UiKit.Gap - UiKit.SidePanelWidth, UiKit.Gap);
            detail.offsetMax = new Vector2(-UiKit.Gap, -UiKit.TopBarHeight - UiKit.Gap);
            UiKit.Panel(detail, UiKit.PanelColor);

            _detailName = UiKit.Text(detail, "Name", _font, UiKit.TitleText, TextAnchor.UpperLeft);
            UiKit.Row(_detailName.rectTransform, 12f, 52f, 14f, 14f);
            _detailEffect = UiKit.Text(detail, "Effect", _font, UiKit.SmallText + 1, TextAnchor.UpperLeft, UiKit.MutedText);
            _detailEffect.rectTransform.anchorMin = Vector2.zero;
            _detailEffect.rectTransform.anchorMax = Vector2.one;
            _detailEffect.rectTransform.offsetMin = new Vector2(14f, 110f);
            _detailEffect.rectTransform.offsetMax = new Vector2(-14f, -68f);
            _detailEffect.verticalOverflow = VerticalWrapMode.Truncate;
            _detailStatus = UiKit.Text(detail, "Status", _font, UiKit.SmallText + 1, TextAnchor.MiddleLeft, UiKit.FaithColor);
            UiKit.Place(_detailStatus.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 64f), new Vector2(UiKit.SidePanelWidth - 28f, 40f));
            _unlockButton = UiKit.Button(detail, "UnlockButton", _font, LocalizationSystem.T("UI.TechTree.Unlock"), UiKit.PrimaryColor, RequestUnlock, UiKit.BodyText + 1);
            var unlockRect = (RectTransform)_unlockButton.transform;
            unlockRect.anchorMin = new Vector2(0f, 0f);
            unlockRect.anchorMax = new Vector2(1f, 0f);
            unlockRect.pivot = new Vector2(0.5f, 0f);
            unlockRect.offsetMin = new Vector2(12f, 12f);
            unlockRect.offsetMax = new Vector2(-12f, 12f + 48f);
        }

        public void SetNodes(IReadOnlyList<TechNodeData> nodes)
        {
            if (_tree == null) return;
            _nodes = nodes ?? new List<TechNodeData>();
            _selectedId = null;
            BuildTree();
            RefreshNodeColors();
            RefreshDetail();
            PixelUIScaler.Attach(gameObject);
        }

        public void Open()
        {
            if (_root == null) return;
            _root.SetActive(true);
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

        // ---------- 노드 배치 ----------

        /// <summary>잎 노드마다 한 열, 부모는 자식 열들의 가운데. 행 = 티어(허브는 0행). 좌표는 0..1 비율.</summary>
        private Dictionary<string, Vector2> Layout(out int leafCount, out int depth)
        {
            var ids = new HashSet<string>();
            foreach (var n in _nodes) ids.Add(n.Id);
            var children = new Dictionary<string, List<TechNodeData>> { [string.Empty] = new List<TechNodeData>() };
            foreach (var n in _nodes)
            {
                string parent = !string.IsNullOrEmpty(n.ParentId) && ids.Contains(n.ParentId) ? n.ParentId : string.Empty;
                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<TechNodeData>();
                list.Add(n);
            }
            foreach (var list in children.Values) list.Sort((a, b) => a.Slot.CompareTo(b.Slot)); // 안정 정렬이 아니어도 같은 Slot은 표 순서와 무관하게 괜찮다

            var column = new Dictionary<string, float>();
            var level = new Dictionary<string, int> { [string.Empty] = 0 };
            int leaves = 0, maxLevel = 0;
            float Visit(string id, int lv)
            {
                level[id] = lv;
                maxLevel = Mathf.Max(maxLevel, lv);
                if (!children.TryGetValue(id, out var kids) || kids.Count == 0) return column[id] = leaves++;
                float sum = 0f;
                foreach (var kid in kids) sum += Visit(kid.Id, lv + 1);
                return column[id] = sum / kids.Count;
            }
            Visit(string.Empty, 0);

            leafCount = Mathf.Max(1, leaves);
            depth = maxLevel;
            var positions = new Dictionary<string, Vector2>();
            foreach (var kv in column)
            {
                float x = (kv.Value + 0.5f) / leafCount;
                float y = 1f - (level[kv.Key] + 0.5f) / (maxLevel + 1f);
                positions[kv.Key] = new Vector2(x, y);
            }
            return positions;
        }

        private void BuildTree()
        {
            UiKit.ClearChildren(_tree);
            _nodeBg.Clear();
            var positions = Layout(out _, out int depth);
            float rowStep = 1f / (depth + 1f);

            // 연결선(노드 아래에 깔린다): 부모 아래 → 중간 줄 → 자식 위. 허브(빈 Id)도 부모가 될 수 있다.
            foreach (var n in _nodes)
            {
                string parent = !string.IsNullOrEmpty(n.ParentId) && positions.ContainsKey(n.ParentId) ? n.ParentId : string.Empty;
                var from = positions[parent];
                var to = positions[n.Id];
                float midY = to.y + rowStep * 0.5f;
                Segment(new Vector2(from.x, from.y), new Vector2(from.x, midY));
                Segment(new Vector2(Mathf.Min(from.x, to.x), midY), new Vector2(Mathf.Max(from.x, to.x), midY));
                Segment(new Vector2(to.x, midY), new Vector2(to.x, to.y));
            }

            var hub = Node(string.Empty, "Hub", positions[string.Empty], TechTreeDefinition.HubIcon, null);
            hub.color = HubColor;
            foreach (var n in _nodes)
            {
                var bg = Node(n.Id, n.Id, positions[n.Id], n.Icon, n.Name);
                var button = bg.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var id = n.Id;
                button.onClick.AddListener(() => SelectNode(id));
                _nodeBg[n.Id] = bg;
            }
        }

        private Image Node(string id, string name, Vector2 at, string icon, string label)
        {
            var rect = UiKit.Rect(name, _tree);
            rect.anchorMin = rect.anchorMax = at;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, label != null ? 8f : 0f);
            rect.sizeDelta = new Vector2(NodeSize, NodeSize);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = PixelUISkin.Badge; bg.type = Image.Type.Sliced;
            UiKit.Icon(rect, "Node", icon, new Vector2(0.5f, 0.5f), Vector2.zero, NodeSize - 12f);
            if (label != null)
            {
                var text = UiKit.Text(rect, "Label", _font, UiKit.SmallText + 1, TextAnchor.MiddleCenter);
                text.text = label;
                float width = Mathf.Min(88f, text.preferredWidth + 6f);
                // 글자 뒤를 창 배경색으로 덮어 아래로 이어지는 연결선이 글자를 지나가지 않게 한다.
                var back = UiKit.Place(UiKit.Rect("LabelBack", rect), new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(width, 18f));
                UiKit.Fill(back, BackgroundColor);
                text.transform.SetParent(back, false);
                UiKit.Stretch(text.rectTransform, 0f, -6f, 0f, -6f);
            }
            return bg;
        }

        private void Segment(Vector2 a, Vector2 b)
        {
            var rect = UiKit.Rect("ConnectorFill", _tree);
            rect.anchorMin = Vector2.Min(a, b);
            rect.anchorMax = Vector2.Max(a, b);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float half = ConnectorThickness * 0.5f;
            rect.offsetMin = new Vector2(-half, -half);
            rect.offsetMax = new Vector2(half, half);
            UiKit.Fill(rect, ConnectorColor);
        }

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
