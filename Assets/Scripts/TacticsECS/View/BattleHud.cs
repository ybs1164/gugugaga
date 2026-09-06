using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 전투 화면 전체 HUD. 예전 OnGUI(텍스트 라벨 + 기본 스킨 버튼)를 대체한다.
    /// BattleController가 넘겨주는 값을 그대로 그리기만 하는 View — 능력치를 문장 대신
    /// 아이콘(Assets/Art/GameIcons, IconLibrary 참고, 출처는 GameIcons/LICENSE.txt)으로 표현해서
    /// 한눈에 읽히게 하는 것이 목적이다. 값을 스스로 판단하지 않고, 버튼 클릭은 이벤트로만 밖에 알린다.
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        public event Action OnEndTurnClicked;
        public event Action OnDefendClicked;
        public event Action OnDeselectClicked;

        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color PanelBackground = new Color(0.08f, 0.09f, 0.11f, 0.85f);
        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color GuardHighlight = Color.yellow;

        private Font _font;

        private Image _turnBadgeBg;
        private Text _turnText;

        private GameObject _unitPanel;
        private Image _unitPanelAccent;
        private Text _hpText;
        private Image _hpFill;
        private Text _attackText;
        private Text _defenseText;
        private Text _moveText;
        private Text _rangeText;

        private Button _endTurnButton;
        private Button _defendButton;
        private Button _deselectButton;

        private GameObject _battleEndPanel;
        private Image _battleEndIcon;
        private Text _battleEndText;

        public void Init()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = canvas.transform;
            BuildTurnBadge(root);
            BuildUnitPanel(root);
            BuildActionButtons(root);
            BuildBattleEndPanel(root);

            HideUnitPanel();
            SetDefendVisible(false);
            SetDeselectVisible(false);
        }

        /// <summary>새 Input System 기준(activeInputHandler=Input System Package)이라 uGUI 클릭을
        /// 받으려면 레거시 StandaloneInputModule이 아니라 InputSystemUIInputModule이 필요하다.</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- 공용 빌딩 블록 ----------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreatePanelImage(RectTransform rect, Color color)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>parent의 좌상단을 기준으로 놓이는 정사각 아이콘.</summary>
        private static Image CreateIcon(string name, Transform parent, string iconName, float size, Vector2 anchoredPos)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = IconLibrary.Get(iconName);
            img.preserveAspect = true;
            return img;
        }

        /// <summary>parent의 좌상단을 기준으로 놓이는 텍스트. 능력치는 숫자만 담당하고, 무엇에 대한
        /// 숫자인지는 항상 옆의 아이콘이 말해준다 — 라벨 문자열은 만들지 않는다.</summary>
        private Text CreateNumberText(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ---------- 턴 배지 (좌상단) ----------

        private void BuildTurnBadge(Transform root)
        {
            var panel = CreateRect("TurnBadge", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(92f, 44f);
            _turnBadgeBg = CreatePanelImage(panel, PlayerAccent);

            CreateIcon("Icon", panel, "turn", 28f, new Vector2(8f, -8f));
            _turnText = CreateNumberText("Number", panel, new Vector2(44f, -8f), new Vector2(40f, 32f));
            _turnText.fontSize = 24;
            _turnText.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>턴 배지 색(플레이어/적)만으로 누구 턴인지 보여준다 — "N턴 - 플레이어 턴" 문장 대신
        /// 색 + 숫자 하나로 줄인다.</summary>
        public void SetTurn(Team team, int turnNumber)
        {
            _turnBadgeBg.color = team == Team.Player ? PlayerAccent : EnemyAccent;
            _turnText.text = turnNumber.ToString();
        }

        public void SetEndTurnVisible(bool visible) => _endTurnButton.gameObject.SetActive(visible);

        // ---------- 선택 유닛 능력치 패널 (좌하단) ----------

        private void BuildUnitPanel(Transform root)
        {
            var panel = CreateRect("UnitPanel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 16f);
            panel.sizeDelta = new Vector2(150f, 168f);
            CreatePanelImage(panel, PanelBackground);
            _unitPanel = panel.gameObject;

            var accent = CreateRect("Accent", panel);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accent.anchoredPosition = Vector2.zero;
            _unitPanelAccent = CreatePanelImage(accent, PlayerAccent);

            const float rowH = 30f;
            float top = -8f;

            CreateIcon("HpIcon", panel, "hp", 22f, new Vector2(14f, top));
            _hpText = CreateNumberText("HpText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            var hpBarBg = CreateRect("HpBarBg", panel);
            hpBarBg.anchorMin = hpBarBg.anchorMax = new Vector2(0f, 1f);
            hpBarBg.pivot = new Vector2(0f, 1f);
            hpBarBg.anchoredPosition = new Vector2(14f, top - 22f);
            hpBarBg.sizeDelta = new Vector2(122f, 6f);
            CreatePanelImage(hpBarBg, new Color(1f, 1f, 1f, 0.15f));

            var hpBarFill = CreateRect("HpBarFill", hpBarBg);
            hpBarFill.anchorMin = Vector2.zero;
            hpBarFill.anchorMax = new Vector2(1f, 1f);
            hpBarFill.offsetMin = Vector2.zero;
            hpBarFill.offsetMax = Vector2.zero;
            _hpFill = CreatePanelImage(hpBarFill, HpColorScale.ForFraction(1f));

            top -= rowH + 10f;
            CreateIcon("AttackIcon", panel, "attack", 22f, new Vector2(14f, top));
            _attackText = CreateNumberText("AttackText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIcon("DefenseIcon", panel, "defense", 22f, new Vector2(14f, top));
            _defenseText = CreateNumberText("DefenseText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIcon("MoveIcon", panel, "move", 22f, new Vector2(14f, top));
            _moveText = CreateNumberText("MoveText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIcon("RangeIcon", panel, "range", 22f, new Vector2(14f, top));
            _rangeText = CreateNumberText("RangeText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));
        }

        /// <summary>선택된 유닛 하나의 능력치를 그대로 읽어서 보여준다. UnitView.Refresh와 같은 방식으로
        /// world+id를 받아 필요한 컴포넌트를 직접 조회한다(값 여러 개를 묶은 DTO를 새로 만들지 않는다).
        /// 방어 태세 보너스는 CombatSystem.EffectiveDefense를 그대로 써서, "방어 태세면 +2"라는 규칙이
        /// 이 View와 실제 데미지 계산 두 곳에 따로 적히지 않도록 한다.</summary>
        public void ShowUnitPanel(EntityWorld world, int unitId)
        {
            _unitPanel.SetActive(true);

            var team = world.Get<Team>(unitId);
            _unitPanelAccent.color = team == Team.Player ? PlayerAccent : EnemyAccent;

            int hp = world.Get<Hp>(unitId).Value;
            int maxHp = world.Get<MaxHp>(unitId).Value;
            float frac = maxHp > 0 ? (float)hp / maxHp : 0f;
            _hpText.text = $"{hp}/{maxHp}";
            _hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            _hpFill.color = HpColorScale.ForFraction(frac);

            _attackText.text = world.Get<Attack>(unitId).Value.ToString();

            bool guarding = world.Get<IsGuarding>(unitId).Value;
            _defenseText.text = CombatSystem.EffectiveDefense(world, unitId).ToString();
            _defenseText.color = guarding ? GuardHighlight : Color.white;

            _moveText.text = world.Get<MoveRange>(unitId).Value.ToString();
            _rangeText.text = world.Get<AttackRange>(unitId).Value.ToString();
        }

        public void HideUnitPanel() => _unitPanel.SetActive(false);

        // ---------- 행동 버튼 (우하단, 아이콘만) ----------

        private void BuildActionButtons(Transform root)
        {
            const float buttonSize = 52f;
            const float gap = 8f;

            // 항상 뜨는 "턴 종료"를 구석(코너) 자리에 고정해서, 선택이 없을 때도 버튼이 화면 끝에서
            // 붕 뜨지 않게 한다. 선택 시에만 나타나는 방어/선택해제는 그 왼쪽으로 자리를 넓혀간다.
            _deselectButton = CreateIconButton(root, "DeselectButton", "deselect", new Vector2(-16f - (buttonSize + gap) * 2f, 16f), buttonSize, ButtonIdle);
            _deselectButton.onClick.AddListener(() => OnDeselectClicked?.Invoke());

            _defendButton = CreateIconButton(root, "DefendButton", "guard", new Vector2(-16f - (buttonSize + gap), 16f), buttonSize, ButtonIdle);
            _defendButton.onClick.AddListener(() => OnDefendClicked?.Invoke());

            _endTurnButton = CreateIconButton(root, "EndTurnButton", "turn", new Vector2(-16f, 16f), buttonSize, ButtonIdle);
            _endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());
        }

        private static Button CreateIconButton(Transform root, string name, string iconName, Vector2 anchoredPosFromBottomRight, float size, Color bg)
        {
            var rect = CreateRect(name, root);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPosFromBottomRight;

            var bgImage = CreatePanelImage(rect, bg);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bgImage;

            var iconRect = CreateRect("Icon", rect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10f, 10f);
            iconRect.offsetMax = new Vector2(-10f, -10f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = IconLibrary.Get(iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            return button;
        }

        public void SetDefendVisible(bool visible) => _defendButton.gameObject.SetActive(visible);
        public void SetDeselectVisible(bool visible) => _deselectButton.gameObject.SetActive(visible);

        // ---------- 승/패 오버레이 ----------

        private void BuildBattleEndPanel(Transform root)
        {
            var panel = CreateRect("BattleEnd", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            CreatePanelImage(panel, new Color(0f, 0f, 0f, 0.55f));
            _battleEndPanel = panel.gameObject;

            var iconRect = CreateRect("Icon", panel);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0f);
            iconRect.sizeDelta = new Vector2(120f, 120f);
            iconRect.anchoredPosition = new Vector2(0f, 10f);
            _battleEndIcon = iconRect.gameObject.AddComponent<Image>();
            _battleEndIcon.preserveAspect = true;

            var textRect = CreateRect("Label", panel);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(400f, 50f);
            textRect.anchoredPosition = Vector2.zero;
            _battleEndText = textRect.gameObject.AddComponent<Text>();
            _battleEndText.font = _font;
            _battleEndText.fontSize = 36;
            _battleEndText.alignment = TextAnchor.MiddleCenter;
            _battleEndText.color = Color.white;

            _battleEndPanel.SetActive(false);
        }

        public void ShowBattleEnd(bool playerWon)
        {
            _battleEndPanel.SetActive(true);
            _battleEndIcon.sprite = IconLibrary.Get(playerWon ? "victory" : "defeat");
            _battleEndIcon.color = playerWon ? PlayerAccent : EnemyAccent;
            _battleEndText.text = playerWon ? "승리!" : "패배...";
        }
    }
}
