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
        private const float LabelWidth = 110f;
        private const float LabelHeight = 30f;
        private const float ConnectorThickness = 3f;

        private GameObject _panel;
        private readonly Dictionary<string, Image> _nodeBg = new Dictionary<string, Image>();
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

            BuildTree((RectTransform)_panel.transform.Find("Tree"));

            var detail = _panel.transform.Find("Detail");
            _detailName = detail.Find("Name").GetComponent<Text>();
            _detailEffect = detail.Find("Effect").GetComponent<Text>();
            _detailStatus = detail.Find("Status").GetComponent<Text>();
            _unlockButton = detail.Find("UnlockButton").GetComponent<Button>();
            _unlockLabel = detail.Find("UnlockButton/Label").GetComponent<Text>();
            _unlockButton.onClick.AddListener(RequestUnlock);

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

                var label = NewRect("Label", node);
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0f);
                label.pivot = new Vector2(0.5f, 1f);
                label.anchoredPosition = new Vector2(0f, -4f);
                label.sizeDelta = new Vector2(LabelWidth, LabelHeight);
                var text = label.gameObject.AddComponent<Text>();
                text.font = uiFont;
                text.fontSize = 12;
                text.alignment = TextAnchor.UpperCenter;
                text.color = Color.white;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.raycastTarget = false;
                text.text = n.Name;
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
                var baseColor = _hasState && TechSystem.IsUnlocked(_tech, node.Id) ? UnlockedColor
                    : _hasState && TechSystem.IsAvailable(_nodes, _tech, node.Id) ? AvailableColor
                    : LockedColor;
                bg.color = node.Id == _selectedId ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
            }
        }

        private void RefreshDetail()
        {
            var found = TechSystem.Find(_nodes, _selectedId);
            if (found == null || !_hasState)
            {
                _detailName.text = "기술을 선택하세요.";
                _detailEffect.text = string.Empty;
                _detailStatus.text = string.Empty;
                _unlockButton.interactable = false;
                _unlockLabel.text = "해금";
                return;
            }

            var node = found.Value;
            int cost = TechSystem.Cost(_nodes, _tech, node, _cityCount);
            _detailName.text = $"{node.Name} ({node.Tier}티어)";
            _detailEffect.text = node.Effect;
            _unlockLabel.text = $"해금 ({cost})";

            if (TechSystem.IsUnlocked(_tech, node.Id))
            {
                _detailStatus.text = "해금 완료";
                _unlockButton.interactable = false;
            }
            else if (!TechSystem.IsAvailable(_nodes, _tech, node.Id))
            {
                var parent = TechSystem.Find(_nodes, node.ParentId);
                _detailStatus.text = parent != null ? $"선행 기술 필요: {parent.Value.Name}" : "선행 기술 필요";
                _unlockButton.interactable = false;
            }
            else if (_resources.Development < cost)
            {
                _detailStatus.text = $"발전도 부족 ({_resources.Development}/{cost})";
                _unlockButton.interactable = false;
            }
            else
            {
                _detailStatus.text = "해금 가능";
                _unlockButton.interactable = true;
            }
        }
    }
}
