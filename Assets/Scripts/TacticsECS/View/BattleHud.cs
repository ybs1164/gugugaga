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
        public event Action OnHealClicked;
        public event Action OnSelfDestructClicked;
        public event Action OnDeselectClicked;
        /// <summary>승/패 화면의 "다시 시작" 버튼. 샌드박스 모드에서는 이 버튼이 곧 배치 화면(커스텀 화면)으로
        /// 되돌아가는 진입점이고, 데모 모드에서는 데모 편성을 다시 스폰하는 재시작이다 — 어느 쪽이든
        /// BattleController가 알아서 처리하고, BattleHud는 클릭했다는 사실만 알린다.</summary>
        public event Action OnRestartClicked;

        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color PanelBackground = new Color(0.08f, 0.09f, 0.11f, 0.85f);
        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);
        private static readonly Color GuardHighlight = Color.yellow;
        /// <summary>패시브 배지 전용 배경색. 클릭 가능한 행동 버튼(ButtonIdle, 네모 배경)과 같은 색을 쓰면
        /// "누를 수 있는 것"처럼 보이므로, 자동 발동 패시브라는 걸 색으로도 한 번 더 구분한다.</summary>
        private static readonly Color PassiveBadgeBg = new Color(0.42f, 0.24f, 0.55f, 0.95f);

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

        /// <summary>유닛이 가진 패시브 하나를 나타내는 배지(동그라미 배경). 행동 버튼과 달리 클릭할 수
        /// 없고, 선택된 유닛의 AvailableActions에 Flag가 있을 때만 나타난다(ShowUnitPanel).</summary>
        private struct PassiveBadge
        {
            public ActionType Flag;
            public RectTransform Rect;
        }

        private PassiveBadge[] _passiveBadges;
        private float _passiveRowTop;

        /// <summary>유닛별로 있을 수도, 없을 수도 있는 행동 버튼 하나. Flag가 선택된 유닛의
        /// AvailableActions에 있을 때만 화면에 나타난다(LayoutActionBar).</summary>
        private struct OptionalActionButton
        {
            public ActionType Flag;
            public Button Button;
            public RectTransform Rect;
        }

        private Button _endTurnButton;
        private Button _deselectButton;
        private RectTransform _deselectRect;
        private OptionalActionButton[] _optionalActions;

        private GameObject _tooltipPanel;
        private Text _tooltipText;

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
            BuildTooltip(root);
            BuildBattleEndPanel(root);

            HideUnitPanel();
            SetUnitActions(ActionType.None, hasActed: true);
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

        private static Sprite _circleSprite;

        /// <summary>가득 찬 흰 원 스프라이트(1개만 만들어 재사용). 패시브 배지의 "동그라미 배경"에 쓴다 —
        /// 행동 버튼(CreateIconButton)의 네모 배경과 모양으로 구분하기 위해서다. 텍스처 임포트 설정을
        /// 건드리지 않고(IconLibrary와 같은 이유) 런타임에 픽셀을 직접 채워 만든다.</summary>
        private static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;

                const int res = 64;
                var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { name = "CircleBadge" };
                var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
                float radius = res * 0.5f;
                var pixels = new Color32[res * res];
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        // 가장자리 1px만 부드럽게(anti-alias) 처리해 확대해도 계단 현상이 덜하게 한다.
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();

                _circleSprite = Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), 100f);
                return _circleSprite;
            }
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

        /// <summary>유닛이 가질 수 있는 패시브 전부와, 그 아이콘·툴팁 설명. 새 패시브가 생기면
        /// (OptionalActionDefs와 마찬가지로) 이 배열에 한 줄만 추가하면 되고 BattleController는 몰라도 된다.</summary>
        private static readonly (ActionType Flag, string Icon, string Tooltip)[] PassiveDefs =
        {
            (ActionType.Counter, "counter", "반격(패시브): 공격을 받으면 자동으로 공격한 대상에게 피해를 되돌려줍니다."),
            (ActionType.Charge, "charge", "돌격(패시브): 이번 턴 이동한 뒤에도 공격할 수 있습니다."),
            (ActionType.Retreat, "retreat", "대피(패시브): 이번 턴 공격한 뒤에도 이동할 수 있습니다."),
            (ActionType.Ambush, "ambush", "기습(패시브): 공격 시 대상의 반격을 발동시키지 않습니다."),
            (ActionType.Infiltrate, "infiltrate", "잠입(패시브): 적 유닛에 의한 이동 방해 페널티가 없습니다."),
            (ActionType.Herd, "herd", "무리(패시브): 주변 1블록 내 아군에게 가속을 부여합니다(이동 거리 +1, 피격 시 해제)."),
            (ActionType.Convert, "convert", "전향(패시브): 공격한 적 유닛을 아군으로 전환합니다."),
            (ActionType.Combo, "combo", "연타(패시브): 적을 처치하면 같은 턴에 추가로 공격할 수 있습니다."),
            (ActionType.Scout, "scout", "정찰(패시브): 시야 +1."),
            (ActionType.Splash, "splash", "스플래시(패시브): 공격한 대상 주변 1블록 내 적 유닛들에게도 광역 피해를 입힙니다."),
            (ActionType.Stiff, "stiff", "뻣뻣함(패시브): 공격받으면 반격을 갖고 있어도 발동시키지 않습니다."),
            (ActionType.Freeze, "freeze", "빙결(패시브): 공격 시 대상을 다음 턴 동안 행동불능으로 만듭니다."),
        };

        private void BuildUnitPanel(Transform root)
        {
            var panel = CreateRect("UnitPanel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 16f);
            panel.sizeDelta = new Vector2(150f, 206f);
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

            // ---- 패시브 배지 줄 (동그라미 배경 — 위 능력치 아이콘의 네모 자리와 모양으로 구분) ----
            top -= rowH;
            _passiveRowTop = top;
            _passiveBadges = new PassiveBadge[PassiveDefs.Length];
            for (int i = 0; i < PassiveDefs.Length; i++)
            {
                var def = PassiveDefs[i];
                var rect = CreatePassiveBadge(panel, def.Icon, def.Tooltip);
                _passiveBadges[i] = new PassiveBadge { Flag = def.Flag, Rect = rect };
            }
        }

        private const float PassiveBadgeSize = 26f;
        private const float PassiveBadgeGap = 6f;

        /// <summary>패시브 하나를 나타내는 동그라미 배경 배지. 행동 버튼(CreateIconButton, 네모 배경 +
        /// Button)과 달리 클릭할 수 없는 순수 정보 표시용이라 Button 컴포넌트 없이 호버 시 툴팁만 띄운다.</summary>
        private RectTransform CreatePassiveBadge(Transform parent, string iconName, string tooltip)
        {
            var rect = CreateRect("Passive_" + iconName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(PassiveBadgeSize, PassiveBadgeSize);

            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = CircleSprite;
            bg.color = PassiveBadgeBg;

            var iconRect = CreateRect("Icon", rect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = IconLibrary.Get(iconName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var trigger = rect.gameObject.AddComponent<TooltipTrigger>();
            trigger.Text = tooltip;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;

            return rect;
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

            _moveText.text = MovementSystem.EffectiveMoveRange(world, unitId).ToString();
            _rangeText.text = world.Get<AttackRange>(unitId).Value.ToString();

            // 패시브 배지: 이 유닛이 실제로 가진 것만, 왼쪽부터 빈틈없이 채워서 보여준다
            // (행동 버튼 줄, SetUnitActions와 같은 방식).
            var available = world.Get<AvailableActions>(unitId).Value;
            float px = 14f;
            foreach (var badge in _passiveBadges)
            {
                bool show = (available & badge.Flag) != 0;
                badge.Rect.gameObject.SetActive(show);
                if (!show) continue;
                badge.Rect.anchoredPosition = new Vector2(px, _passiveRowTop);
                px += PassiveBadgeSize + PassiveBadgeGap;
            }
        }

        public void HideUnitPanel() => _unitPanel.SetActive(false);

        // ---------- 행동 버튼 (우하단, 아이콘만) ----------

        private const float ActionButtonSize = 52f;
        private const float ActionButtonGap = 8f;

        /// <summary>방어/치유/자폭처럼 "이 유닛이 가지고 있을 수도, 없을 수도 있는" 행동 버튼을
        /// 어떤 순서로 나열할지 + 아이콘 + 툴팁 설명을 한곳에 모아둔 표. 새 행동을 추가할 때
        /// (예: 향후 다른 특수 행동) 이 배열에 한 줄만 추가하면 되고, BattleController는 몰라도 된다.</summary>
        private static readonly (ActionType Flag, string Icon, string Tooltip)[] OptionalActionDefs =
        {
            (ActionType.Defend, "guard", "방어 태세: 받는 피해를 줄입니다. (방어력 +" + CombatSystem.GuardDefenseBonus + ")"),
            (ActionType.Heal, "heal", "치유: 사거리 내의 모든 아군 유닛(자신 제외)의 체력을 회복시킵니다."),
            (ActionType.SelfDestruct, "selfdestruct", "자폭: 스스로를 희생해 주위 1칸의 모든 적에게 남은 체력만큼 피해를 입힙니다."),
        };

        private void BuildActionButtons(Transform root)
        {
            // 항상 뜨는 "턴 종료"를 구석(코너) 자리에 고정해서, 선택이 없을 때도 버튼이 화면 끝에서
            // 붕 뜨지 않게 한다. 선택 시에만 나타나는 행동 버튼/선택해제는 LayoutActionBar가 그 왼쪽으로
            // 자리를 동적으로 배치한다(유닛마다 쓸 수 있는 행동 개수가 다르므로).
            _endTurnButton = CreateIconButton(root, "EndTurnButton", "turn", new Vector2(-16f, 16f), ButtonIdle, "턴 종료: 현재 팀의 턴을 마칩니다.");
            _endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());

            _optionalActions = new OptionalActionButton[OptionalActionDefs.Length];
            for (int i = 0; i < OptionalActionDefs.Length; i++)
            {
                var def = OptionalActionDefs[i];
                var button = CreateIconButton(root, def.Flag + "Button", def.Icon, Vector2.zero, ButtonIdle, def.Tooltip);
                switch (def.Flag)
                {
                    case ActionType.Defend: button.onClick.AddListener(() => OnDefendClicked?.Invoke()); break;
                    case ActionType.Heal: button.onClick.AddListener(() => OnHealClicked?.Invoke()); break;
                    case ActionType.SelfDestruct: button.onClick.AddListener(() => OnSelfDestructClicked?.Invoke()); break;
                }
                _optionalActions[i] = new OptionalActionButton { Flag = def.Flag, Button = button, Rect = (RectTransform)button.transform };
            }

            _deselectButton = CreateIconButton(root, "DeselectButton", "deselect", Vector2.zero, ButtonIdle, "선택 해제: 유닛 선택을 취소합니다.");
            _deselectRect = (RectTransform)_deselectButton.transform;
            _deselectButton.onClick.AddListener(() => OnDeselectClicked?.Invoke());
        }

        private Button CreateIconButton(Transform root, string name, string iconName, Vector2 anchoredPosFromBottomRight, Color bg, string tooltip)
        {
            var rect = CreateRect(name, root);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(ActionButtonSize, ActionButtonSize);
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

            var trigger = rect.gameObject.AddComponent<TooltipTrigger>();
            trigger.Text = tooltip;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;

            return button;
        }

        /// <summary>선택된 유닛이 실제로 쓸 수 있는 행동(hasActed면 전부 숨김)만 화면에 나타나게 하고,
        /// 보이는 만큼만 자리를 차지하도록 다시 배치한다. "유닛의 사용 가능 행동 목록을 UI로 보여준다"는
        /// 요구사항의 핵심 진입점 — BattleController는 AvailableActions 값만 넘기면 된다.</summary>
        public void SetUnitActions(ActionType available, bool hasActed)
        {
            var visible = hasActed ? ActionType.None : available;

            float x = -16f - (ActionButtonSize + ActionButtonGap);
            foreach (var action in _optionalActions)
            {
                bool show = (visible & action.Flag) != 0;
                action.Button.gameObject.SetActive(show);
                if (!show) continue;
                action.Rect.anchoredPosition = new Vector2(x, 16f);
                x -= ActionButtonSize + ActionButtonGap;
            }

            _deselectRect.anchoredPosition = new Vector2(x, 16f);
        }

        public void SetDeselectVisible(bool visible) => _deselectButton.gameObject.SetActive(visible);

        // ---------- 툴팁 (행동 버튼 바로 위 한 구역에 고정) ----------

        /// <summary>버튼에 올라간 마우스 진입/이탈만 BattleHud로 전달하는 얇은 컴포넌트.
        /// 어떤 텍스트를 보여줄지, 실제로 어떻게 보여줄지는 전혀 모른다(로직 없는 View 보조 도구).</summary>
        private class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public string Text;
            public Action<string> OnEnter;
            public Action OnExit;
            public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke(Text);
            public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
        }

        /// <summary>행동 버튼 하나에 대응하는 설명 하나만, 항상 같은 자리(행동 버튼 줄 바로 위)에 띄운다.
        /// 버튼마다 개별 풍선말을 띄우는 대신 화면 한 구역에 모아두는 이유는, isometric 3D 화면 위에서
        /// 버튼 근처에 즉석으로 말풍선을 붙이면 카메라/그리드와 겹쳐 가려지기 쉽기 때문이다.</summary>
        private void BuildTooltip(Transform root)
        {
            var panel = CreateRect("Tooltip", root);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-16f, 16f + ActionButtonSize + 10f);
            panel.sizeDelta = new Vector2(360f, 56f);
            CreatePanelImage(panel, PanelBackground);
            _tooltipPanel = panel.gameObject;

            var textRect = CreateRect("Text", panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            _tooltipText = textRect.gameObject.AddComponent<Text>();
            _tooltipText.font = _font;
            _tooltipText.fontSize = 15;
            _tooltipText.alignment = TextAnchor.MiddleRight;
            _tooltipText.color = Color.white;
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;

            _tooltipPanel.SetActive(false);
        }

        private void ShowTooltip(string text)
        {
            _tooltipText.text = text;
            _tooltipPanel.SetActive(true);
        }

        private void HideTooltip() => _tooltipPanel.SetActive(false);

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

            var restartRect = CreateRect("RestartButton", panel);
            restartRect.anchorMin = restartRect.anchorMax = new Vector2(0.5f, 0.5f);
            restartRect.pivot = new Vector2(0.5f, 1f);
            restartRect.sizeDelta = new Vector2(180f, 44f);
            restartRect.anchoredPosition = new Vector2(0f, -60f);
            var restartBg = CreatePanelImage(restartRect, ButtonIdle);
            var restartButton = restartRect.gameObject.AddComponent<Button>();
            restartButton.targetGraphic = restartBg;
            restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());

            var restartLabelRect = CreateRect("Label", restartRect);
            restartLabelRect.anchorMin = Vector2.zero;
            restartLabelRect.anchorMax = Vector2.one;
            restartLabelRect.offsetMin = Vector2.zero;
            restartLabelRect.offsetMax = Vector2.zero;
            var restartLabel = restartLabelRect.gameObject.AddComponent<Text>();
            restartLabel.font = _font;
            restartLabel.fontSize = 18;
            restartLabel.alignment = TextAnchor.MiddleCenter;
            restartLabel.color = Color.white;
            restartLabel.text = "다시 시작";

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
