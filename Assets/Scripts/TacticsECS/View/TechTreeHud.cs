using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 기술트리 패널. BattleHud/CityResourceHud와 마찬가지로 값을 스스로 판단하지 않고, 넘겨받은 기술 정의/
    /// 해금 상태/자원을 그대로 그리기만 하는 View다 — 해금 시도(발전도 소모/선행 기술 판정)는 TechSystem이
    /// 계산하고, 그 결과를 다시 SetState로 받아 그린다.
    ///
    /// 고정 뼈대(토글 버튼/반투명 패널/닫기 버튼/빈 "Tree" 컨테이너/하단 Detail 패널)는 프리팹
    /// (Assets/Prefabs/UI/TechTreePanel.prefab, UIPrefabSetup.GenerateTechTreePanel)에 있다. 노드는 더 이상
    /// 프리팹에 굽지 않는다 — 기술 목록이 CSV(Assets/Resources/TechTree.csv)로 바뀌어 기획자가 행을 더하거나
    /// 빼면 노드 수가 달라지므로, BattleHud의 유닛 로스터처럼 "개수가 바뀌는 목록은 코드로 만든다"는 예외에
    /// 해당한다. Init(nodes)가 중앙 허브 + 갈래별 방사형(갈래 수만큼 각도를 등분, 1티어 → 2티어(Slot으로 좌우) →
    /// 3티어(부모와 같은 각도, 더 바깥)) 배치로 노드/연결선을 만든다. 예전 프리팹에 구워져 있던 노드가 남아
    /// 있어도 Init이 Tree 아래를 비우고 다시 만든다.
    ///
    /// Polytopia 레퍼런스(docs/UxIconizationPlan.md 2.4): 노드 아래 이름 라벨 대신 노드 위 [전구 비용] 배지 + 상태 아이콘(완료 ✓ /
    /// 잠김 🔒)으로 상태를 보여주고, 상세 패널의 효과 문장(TechTree.csv Effect) 대신 "해금되는 것" 아이콘 줄(TechUnlocks.csv Icon)을
    /// 보여준다. 아이콘을 누르고 있거나 마우스를 올리면 그 해금의 이름/설명이 상태 줄에 나온다. 해금 버튼도 "해금 (8)" 대신 [전구 8].
    /// </summary>
    public class TechTreeHud : MonoBehaviour
    {
        [Tooltip("유닛 로스터/도시 자원 바와 같은 공용 한글 폰트. UIPrefabSetup.GenerateAll이 채운다.")]
        [SerializeField] private Font uiFont;

        private static readonly Color LockedColor = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color AvailableColor = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        private static readonly Color UnlockedColor = new Color(0.30f, 0.70f, 0.35f, 0.95f);
        private static readonly Color ConnectorColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color HubColor = new Color(1f, 1f, 1f, 0.9f);

        // 방사형 배치 수치(예전 UIPrefabSetup의 값 그대로).
        private const float HubDiameter = 110f;
        private static readonly float[] TierDiameter = { 80f, 70f, 64f };
        private static readonly float[] TierRadius = { 115f, 205f, 315f };
        private const float TierRadiusStep = 100f; // 4티어 이상이 생기면 이 간격으로 더 바깥에.
        private const float BranchSpreadDeg = 20f;
        private const float IconInsetRatio = 0.58f;
        private const float ConnectorThickness = 3f;

        private GameObject _panel;
        private readonly Dictionary<string, Image> _nodeBg = new Dictionary<string, Image>();
        private readonly Dictionary<string, Image> _nodeIcon = new Dictionary<string, Image>();
        private readonly Dictionary<string, RectTransform> _nodeCost = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Image> _nodeStatus = new Dictionary<string, Image>();
        private RectTransform _tree;

        private RectTransform _unlockRow;
        private Image _statusIcon;
        private Image _unlockIcon;
        private IReadOnlyList<TechNodeData> _nodes = new List<TechNodeData>();

        private Text _detailName;
        private Text _detailEffect;
        private Text _detailStatus;
        private Button _unlockButton;
        private Text _unlockLabel;

        private string _selectedId;
        private TechTreeData _tech;
        private CityResourceData _resources;
        private int _cityCount;
        private bool _hasState;

        /// <summary>해금 버튼을 눌렀을 때 발생한다(기술 Id). 실제 해금은 호출자(BattleController)가 처리하고
        /// 결과를 SetState로 다시 넘겨준다.</summary>
        public event Action<string> OnUnlockRequested;

        public void Init(IReadOnlyList<TechNodeData> nodes)
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[TechTreeHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/TechTreePanel.prefab을 통해 인스턴스화하세요.");
                return;
            }
            _nodes = nodes ?? new List<TechNodeData>();

            var toggleButton = canvas.Find("ToggleButton").GetComponent<Button>();
            _panel = canvas.Find("Panel").gameObject;
            toggleButton.onClick.AddListener(TogglePanel);

            var closeButton = _panel.transform.Find("CloseButton").GetComponent<Button>();
            closeButton.onClick.AddListener(() => _panel.SetActive(false));

            _tree = (RectTransform)_panel.transform.Find("Tree");
            BuildTree(_tree);

            var detail = _panel.transform.Find("Detail");
            _detailName = detail.Find("Name").GetComponent<Text>();
            _detailEffect = detail.Find("Effect").GetComponent<Text>();
            _detailStatus = detail.Find("Status").GetComponent<Text>();
            _unlockButton = detail.Find("UnlockButton").GetComponent<Button>();
            _unlockLabel = detail.Find("UnlockButton/Label").GetComponent<Text>();
            _unlockButton.onClick.AddListener(RequestUnlock);
            WireIconDetail(detail);

            var responsive = ResponsiveCanvas.Attach(canvas);
            responsive.LayoutChanged += FitTree;
            FitTree(responsive.Portrait);

            RefreshNodeColors();
            RefreshDetail();
        }

        /// <summary>효과 문장 자리에 해금 아이콘 줄을, 상태 문장 앞에 상태 아이콘을, 해금 버튼 안에 전구 아이콘을 런타임에 덧붙인다
        /// (프리팹 계층은 그대로 — 다시 생성할 필요 없음).</summary>
        private void WireIconDetail(Transform detail)
        {
            var effectRect = _detailEffect.rectTransform;
            _unlockRow = UiKit.Rect("UnlockIcons", detail);
            _unlockRow.anchorMin = effectRect.anchorMin;
            _unlockRow.anchorMax = effectRect.anchorMax;
            _unlockRow.pivot = effectRect.pivot;
            _unlockRow.anchoredPosition = effectRect.anchoredPosition;
            _unlockRow.sizeDelta = effectRect.sizeDelta;
            _detailEffect.gameObject.SetActive(false);

            var statusRect = _detailStatus.rectTransform;
            _statusIcon = UiKit.Image(detail, "StatusIcon", null, Color.white);
            _statusIcon.rectTransform.anchorMin = _statusIcon.rectTransform.anchorMax = statusRect.anchorMin;
            _statusIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            _statusIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);
            _statusIcon.rectTransform.anchoredPosition = statusRect.anchoredPosition + new Vector2(0f, statusRect.sizeDelta.y * 0.5f);
            statusRect.anchoredPosition += new Vector2(26f, 0f);
            _detailStatus.supportRichText = true;

            _unlockIcon = UiKit.Image(_unlockButton.transform, "Icon", IconLibrary.Get("research"), Color.white);
            _unlockIcon.rectTransform.anchorMin = _unlockIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _unlockIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            _unlockIcon.rectTransform.sizeDelta = new Vector2(22f, 22f);
            _unlockIcon.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            _unlockLabel.supportRichText = true;
        }

        /// <summary>세로 화면(휴대폰)에서는 방사형 트리가 폭에 맞게 줄어들도록 크기를 맞춘다.</summary>
        private void FitTree(bool portrait)
        {
            if (_tree == null) return;
            var area = ((RectTransform)_panel.transform).rect.size;
            float need = 2f * (Radius(3) + TierDiameter[TierDiameter.Length - 1] * 0.5f + 24f);
            float scale = portrait && area.x > 0f ? Mathf.Min(1f, area.x / need) : 1f;
            _tree.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>노드 목록만 바꿔 트리를 다시 그린다(샌드박스 "기술 불러오기" — 버튼 연결은 Init에서 한 번만). 선택은 풀린다.</summary>
        public void SetNodes(IReadOnlyList<TechNodeData> nodes)
        {
            if (_panel == null) return;
            _nodes = nodes ?? new List<TechNodeData>();
            _selectedId = null;
            BuildTree(_tree);
            RefreshNodeColors();
            RefreshDetail();
        }

        // ---------- 노드 배치 ----------

        private void BuildTree(RectTransform tree)
        {
            for (int i = tree.childCount - 1; i >= 0; i--)
            {
                var child = tree.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _nodeBg.Clear();
            _nodeIcon.Clear();
            _nodeCost.Clear();
            _nodeStatus.Clear();

            var branches = new List<string>();
            foreach (var n in _nodes)
                if (!branches.Contains(n.Branch)) branches.Add(n.Branch);

            var positions = new Dictionary<string, Vector2>();
            float step = branches.Count > 0 ? 360f / branches.Count : 0f;
            foreach (var n in _nodes)
            {
                float branchAngle = 90f - branches.IndexOf(n.Branch) * step;
                float angle = n.Tier <= 1 ? branchAngle : branchAngle + (n.Slot == 0 ? -BranchSpreadDeg : BranchSpreadDeg);
                float radius = Radius(n.Tier);
                float rad = angle * Mathf.Deg2Rad;
                positions[n.Id] = new Vector2(radius * Mathf.Cos(rad), radius * Mathf.Sin(rad));
            }

            // 연결선을 먼저 만들어 노드 아래에 깔리게 한다. 선행 기술이 없거나 목록에 없으면 허브에서 잇는다.
            foreach (var n in _nodes)
            {
                var from = !string.IsNullOrEmpty(n.ParentId) && positions.TryGetValue(n.ParentId, out var p) ? p : Vector2.zero;
                CreateConnector(tree, from, positions[n.Id]);
            }

            var hub = CreateCircle(tree, "Hub", Vector2.zero, HubDiameter, TechTreeDefinition.HubIcon);
            hub.GetComponent<Image>().color = HubColor;

            foreach (var n in _nodes)
            {
                float d = Diameter(n.Tier);
                var node = CreateCircle(tree, n.Id, positions[n.Id], d, n.Icon);
                var bg = node.GetComponent<Image>();
                var button = node.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var id = n.Id;
                button.onClick.AddListener(() => SelectNode(id));
                _nodeBg[n.Id] = bg;
                _nodeIcon[n.Id] = node.Find("Icon").GetComponent<Image>();

                // 이름 라벨 대신: 오른쪽 아래 상태 아이콘(완료/잠김), 아래쪽 [전구 비용] 배지(비용은 SetState 뒤에 채운다).
                var status = UiKit.Image(node, "Status", RuntimeSprite.CreateCircle(), new Color(0f, 0f, 0f, 0.85f));
                status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1f, 0f);
                status.rectTransform.pivot = new Vector2(0.8f, 0.2f);
                status.rectTransform.sizeDelta = new Vector2(22f, 22f);
                var statusIcon = UiKit.Image(status.transform, "Icon", null, Color.white);
                UiKit.Stretch(statusIcon.rectTransform, 4f);
                _nodeStatus[n.Id] = statusIcon;

                var costHolder = NewRect("CostHolder", node);
                costHolder.anchorMin = costHolder.anchorMax = new Vector2(0.5f, 0f);
                costHolder.pivot = new Vector2(0.5f, 0.5f);
                costHolder.sizeDelta = Vector2.zero;
                _nodeCost[n.Id] = costHolder;
            }
        }

        private static float Radius(int tier) =>
            tier <= TierRadius.Length ? TierRadius[Mathf.Max(0, tier - 1)] : TierRadius[TierRadius.Length - 1] + (tier - TierRadius.Length) * TierRadiusStep;

        private static float Diameter(int tier) => TierDiameter[Mathf.Clamp(tier - 1, 0, TierDiameter.Length - 1)];

        private static RectTransform CreateCircle(RectTransform parent, string name, Vector2 pos, float diameter, string icon)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(diameter, diameter);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = RuntimeSprite.CreateCircle();

            float inset = diameter * (1f - IconInsetRatio) * 0.5f;
            var iconRect = NewRect("Icon", rect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(inset, inset);
            iconRect.offsetMax = new Vector2(-inset, -inset);
            var iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.sprite = IconLibrary.Get(icon);
            return rect;
        }

        /// <summary>두 점을 잇는 얇은 Image를 회전시켜 직선처럼 보이게 하는 표준 uGUI 트릭.</summary>
        private static void CreateConnector(RectTransform parent, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            var rect = NewRect("Connector", parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, ConnectorThickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = ConnectorColor;
            img.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ---------- 상태 ----------

        private void TogglePanel() => _panel.SetActive(!_panel.activeSelf);

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

        /// <summary>cityCount는 비용 공식(티어 x 도시 수 + 4)에 쓰인다.</summary>
        public void SetState(TechTreeData tech, CityResourceData resources, int cityCount)
        {
            _tech = tech;
            _resources = resources;
            _cityCount = cityCount;
            _hasState = true;
            RefreshNodeColors();
            RefreshDetail();
        }

        private void RefreshNodeColors()
        {
            foreach (var node in _nodes)
            {
                if (!_nodeBg.TryGetValue(node.Id, out var bg)) continue;
                bool unlocked = _hasState && TechSystem.IsUnlocked(_tech, node.Id);
                bool available = _hasState && !unlocked && TechSystem.IsAvailable(_nodes, _tech, node.Id);
                var baseColor = unlocked ? UnlockedColor : available ? AvailableColor : LockedColor;
                bg.color = node.Id == _selectedId ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
                if (_nodeIcon.TryGetValue(node.Id, out var icon)) icon.color = unlocked || available ? Color.white : new Color(1f, 1f, 1f, 0.45f);

                if (_nodeStatus.TryGetValue(node.Id, out var status))
                {
                    status.sprite = unlocked ? IconLibrary.Get("check") : !available ? IconLibrary.Get("lock") : null;
                    status.color = unlocked ? UiKit.GainColor : UiKit.MutedColor;
                    status.transform.parent.gameObject.SetActive(status.sprite != null);
                }

                if (_nodeCost.TryGetValue(node.Id, out var holder))
                {
                    UiKit.DestroyChildren(holder);
                    if (_hasState && !unlocked)
                    {
                        int cost = TechSystem.Cost(_nodes, _tech, node, _cityCount);
                        var badge = UiKit.CostBadge(holder, uiFont, cost, _resources.Development >= cost, 18f, "research");
                        badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0.5f, 0.5f);
                        badge.anchoredPosition = new Vector2(0f, -2f);
                    }
                }
            }
        }

        private void RefreshDetail()
        {
            var found = TechSystem.Find(_nodes, _selectedId);
            UiKit.DestroyChildren(_unlockRow);
            if (found == null || !_hasState)
            {
                _detailName.text = string.Empty;
                SetStatus("info", UiKit.MutedColor, "기술을 선택하세요.");
                _unlockButton.interactable = false;
                _unlockLabel.text = string.Empty;
                _unlockIcon.enabled = false;
                return;
            }

            var node = found.Value;
            int cost = TechSystem.Cost(_nodes, _tech, node, _cityCount);
            _detailName.text = node.Name;
            BuildUnlockIcons(node);
            _unlockIcon.enabled = true;
            bool affordable = _resources.Development >= cost;
            _unlockLabel.text = $"      <color=#{ColorUtility.ToHtmlStringRGB(affordable ? Color.white : UiKit.WarnColor)}>{cost}</color>";

            if (TechSystem.IsUnlocked(_tech, node.Id))
            {
                SetStatus("check", UiKit.GainColor, string.Empty);
                _unlockButton.interactable = false;
                _unlockLabel.text = string.Empty;
                _unlockIcon.sprite = IconLibrary.Get("check");
            }
            else if (!TechSystem.IsAvailable(_nodes, _tech, node.Id))
            {
                var parent = TechSystem.Find(_nodes, node.ParentId);
                SetStatus("lock", UiKit.MutedColor, parent != null ? parent.Value.Name : string.Empty);
                _unlockButton.interactable = false;
                _unlockIcon.sprite = IconLibrary.Get("research");
            }
            else
            {
                SetStatus(affordable ? null : "research", UiKit.WarnColor, affordable ? string.Empty : $"{_resources.Development}/{cost}");
                _unlockButton.interactable = affordable;
                _unlockIcon.sprite = IconLibrary.Get("research");
            }
        }

        private void SetStatus(string icon, Color tint, string text)
        {
            _statusIcon.sprite = string.IsNullOrEmpty(icon) ? null : IconLibrary.Get(icon);
            _statusIcon.color = tint;
            _statusIcon.enabled = _statusIcon.sprite != null;
            _detailStatus.text = text ?? string.Empty;
            _detailStatus.color = tint == UiKit.MutedColor ? Color.white : tint;
        }

        /// <summary>효과 문장 대신 "해금되는 것" 아이콘 줄. 아이콘을 누르고 있거나 마우스를 올리면 상태 줄에 이름/설명을 보여준다.</summary>
        private void BuildUnlockIcons(TechNodeData node)
        {
            if (node.Unlocks == null) return;
            const float size = 34f;
            float x = 0f;
            foreach (var key in node.Unlocks)
            {
                if (string.IsNullOrEmpty(key)) continue;
                var info = MenuIcons.ForUnlock(key);
                var circle = UiKit.Image(_unlockRow, key, RuntimeSprite.CreateCircle(), new Color(1f, 1f, 1f, 0.12f), raycast: true);
                UiKit.TopLeft(circle.rectTransform, new Vector2(size, size));
                circle.rectTransform.anchoredPosition = new Vector2(x, -4f);
                var icon = UiKit.Image(circle.transform, "Icon", IconLibrary.Get(info.Icon), Color.white);
                UiKit.Stretch(icon.rectTransform, 6f);
                var trigger = circle.gameObject.AddComponent<TooltipTrigger>();
                trigger.Text = string.IsNullOrEmpty(info.Description) ? info.Name : $"<b>{info.Name}</b> {info.Description}";
                trigger.OnEnter = t => SetStatus(info.Icon, Color.white, t);
                trigger.OnExit = () => { if (!ScreenLayout.IsTouch) RefreshDetail(); };
                x += size + 8f;
            }
        }
    }
}
