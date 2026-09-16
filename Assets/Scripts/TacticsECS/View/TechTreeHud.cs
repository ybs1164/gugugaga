using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 기술트리 패널. BattleHud/CityResourceHud와 마찬가지로 값을 스스로 판단하지 않고, 넘겨받은
    /// TechTreeData/CityResourceData를 그대로 그리기만 하는 View다 — 해금 시도(발전도 소모/선행 기술
    /// 판정)는 TechSystem이 계산하고, 그 결과를 다시 SetState로 받아 그린다. CityResourceHud와 마찬가지로
    /// BattleHud와 독립된 별도 프리팹(Assets/Prefabs/UI/TechTreePanel.prefab, UIPrefabSetup.
    /// GenerateTechTreePanel 참고, Unity CLI -executeMethod로만 생성)이다.
    ///
    /// 각 기술 노드 버튼은 UIPrefabSetup이 TechTreeDefinition.Nodes 순서대로, 이름을 TechId.ToString()
    /// 으로 구워둔다 — Init()이 그 이름으로 자식을 찾아(Wire) 참조를 캐싱하므로, 노드를 추가/삭제할 땐
    /// TechTreeDefinition 한 곳만 고치면 된다(단, UIPrefabSetup을 다시 실행해야 프리팹도 갱신된다).
    /// </summary>
    public class TechTreeHud : MonoBehaviour
    {
        [Tooltip("유닛 로스터/도시 자원 바와 같은 공용 한글 폰트. UIPrefabSetup.GenerateAll이 채운다.")]
        [SerializeField] private Font uiFont;

        private static readonly Color LockedColor = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color AvailableColor = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        private static readonly Color UnlockedColor = new Color(0.30f, 0.70f, 0.35f, 0.95f);

        private GameObject _panel;
        private readonly Dictionary<TechId, Image> _nodeBg = new Dictionary<TechId, Image>();

        private Text _detailName;
        private Text _detailEffect;
        private Text _detailStatus;
        private Button _unlockButton;
        private Text _unlockLabel;

        private TechId _selectedId = TechId.None;
        private TechTreeData _tech;
        private CityResourceData _city;
        private bool _hasState;

        /// <summary>해금 버튼을 눌렀을 때 발생한다. 실제 해금(TechSystem.Unlock 호출 + 도시 발전도 소모)은
        /// 호출자(BattleController)가 처리하고, 그 결과를 SetState로 다시 넘겨준다 — BattleHud의
        /// OnDefendClicked 등과 같은 패턴이다.</summary>
        public event Action<TechId> OnUnlockRequested;

        public void Init()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[TechTreeHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/TechTreePanel.prefab을 통해 인스턴스화하세요.");
                return;
            }

            var toggleButton = canvas.Find("ToggleButton").GetComponent<Button>();
            _panel = canvas.Find("Panel").gameObject;
            toggleButton.onClick.AddListener(TogglePanel);

            var closeButton = _panel.transform.Find("CloseButton").GetComponent<Button>();
            closeButton.onClick.AddListener(() => _panel.SetActive(false));

            var tree = _panel.transform.Find("Tree");
            foreach (var node in TechTreeDefinition.Nodes)
            {
                var nodeRect = tree.Find(node.Id.ToString());
                _nodeBg[node.Id] = nodeRect.GetComponent<Image>();

                var id = node.Id; // 클로저 캡처용 로컬 복사(foreach 변수를 그대로 캡처하면 안 됨)
                nodeRect.GetComponent<Button>().onClick.AddListener(() => SelectNode(id));
            }

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

        private void TogglePanel() => _panel.SetActive(!_panel.activeSelf);

        private void SelectNode(TechId id)
        {
            _selectedId = id;
            RefreshNodeColors();
            RefreshDetail();
        }

        private void RequestUnlock()
        {
            if (_selectedId != TechId.None) OnUnlockRequested?.Invoke(_selectedId);
        }

        public void SetState(TechTreeData tech, CityResourceData city)
        {
            _tech = tech;
            _city = city;
            _hasState = true;
            RefreshNodeColors();
            RefreshDetail();
        }

        private void RefreshNodeColors()
        {
            foreach (var node in TechTreeDefinition.Nodes)
            {
                var baseColor = _hasState && TechSystem.IsUnlocked(_tech, node.Id) ? UnlockedColor
                    : _hasState && TechSystem.IsAvailable(_tech, node.Id) ? AvailableColor
                    : LockedColor;
                // 선택된 노드는 밝게 강조해서 구분한다(별도 테두리 이미지 없이 색으로만 표시).
                _nodeBg[node.Id].color = node.Id == _selectedId ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
            }
        }

        private void RefreshDetail()
        {
            if (_selectedId == TechId.None || !_hasState)
            {
                _detailName.text = "기술을 선택하세요.";
                _detailEffect.text = string.Empty;
                _detailStatus.text = string.Empty;
                _unlockButton.interactable = false;
                _unlockLabel.text = "해금";
                return;
            }

            var node = TechSystem.Find(_selectedId).Value;
            _detailName.text = $"{node.Name} ({node.Tier}티어)";
            _detailEffect.text = node.Effect;
            _unlockLabel.text = $"해금 ({node.Cost})";

            if (TechSystem.IsUnlocked(_tech, _selectedId))
            {
                _detailStatus.text = "해금 완료";
                _unlockButton.interactable = false;
            }
            else if (!TechSystem.IsAvailable(_tech, _selectedId))
            {
                var parent = TechSystem.Find(node.ParentId);
                _detailStatus.text = parent != null ? $"선행 기술 필요: {parent.Value.Name}" : "선행 기술 필요";
                _unlockButton.interactable = false;
            }
            else if (_city.Development < node.Cost)
            {
                _detailStatus.text = $"발전도 부족 ({_city.Development}/{node.Cost})";
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
