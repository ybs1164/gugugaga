using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 전투 화면 전체 HUD. 예전 OnGUI(텍스트 라벨 + 기본 스킨 버튼)를 대체한다.
    /// BattleController가 넘겨주는 값을 그대로 그리기만 하는 View — 능력치를 문장 대신
    /// 아이콘(Assets/Art/GameIcons, IconLibrary 참고, 출처는 GameIcons/LICENSE.txt)으로 표현해서
    /// 한눈에 읽히게 하는 것이 목적이다. 값을 스스로 판단하지 않고, 버튼 클릭은 이벤트로만 밖에 알린다.
    ///
    /// 화면 구조(Canvas 이하 전체 계층 — TurnBadge/UnitPanel/행동 버튼/Tooltip/BattleEnd)는 더 이상 이
    /// 스크립트가 코드로 만들지 않는다. Assets/Prefabs/UI/BattleHud.prefab(UIPrefabSetup.GenerateBattleHud
    /// 참고, Unity CLI -executeMethod로만 생성)에 이미 만들어져 있고, 이 컴포넌트는 그 프리팹의 루트에
    /// 붙어 인스턴스화된다 — Init()은 그 안에서 필요한 자식을 이름으로 찾아(Wire*) 참조를 캐싱하고,
    /// 아이콘 스프라이트 지정/버튼 클릭 이벤트 연결처럼 "코드로만 가능한" 부분만 마저 채운다.
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

        [Tooltip("uGUI 클릭 입력에 필요한 EventSystem 프리팹(EventSystem + InputSystemUIInputModule). " +
            "씬에 EventSystem이 이미 있으면 쓰이지 않는다. Assets/Prefabs/UI/EventSystem.prefab.")]
        [SerializeField] private GameObject eventSystemPrefab;

        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color GuardHighlight = Color.yellow;

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
        /// <summary>패시브 배지 줄의 anchoredPosition.y. UnitPanel 프리팹 레이아웃(능력치 4줄 + 여백)에서
        /// 계산되는 고정값이라 UIPrefabSetup.GenerateBattleHud와 이 값이 서로 어긋나지 않게 상수로 고정한다.</summary>
        private const float PassiveRowTop = -168f;

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

        private static Sprite _circleSprite;

        public void Init()
        {
            EnsureEventSystem();

            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[BattleHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/BattleHud.prefab을 통해 인스턴스화하세요.");
                return;
            }

            WireTurnBadge(canvas);
            WireUnitPanel(canvas);
            WireActionButtons(canvas);
            WireTooltip(canvas);
            WireBattleEndPanel(canvas);

            HideUnitPanel();
            SetUnitActions(ActionType.None, hasActed: true);
            SetDeselectVisible(false);
        }

        /// <summary>새 Input System 기준(activeInputHandler=Input System Package)이라 uGUI 클릭을
        /// 받으려면 레거시 StandaloneInputModule이 아니라 InputSystemUIInputModule이 필요하다.</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (eventSystemPrefab == null)
            {
                Debug.LogError($"[BattleHud] {name}: eventSystemPrefab이 비어있습니다. Assets/Prefabs/UI/EventSystem.prefab을 연결하세요.");
                return;
            }
            var go = Instantiate(eventSystemPrefab);
            go.name = "EventSystem";
        }

        /// <summary>가득 찬 흰 원 스프라이트(1개만 만들어 재사용). 패시브 배지의 "동그라미 배경"에 쓴다.
        /// 텍스처 임포트 설정을 건드리지 않고(IconLibrary와 같은 이유) 런타임에 픽셀을 직접 채워 만든다 —
        /// 프리팹에 미리 구워둘 수 없는 이유도 같다: Sprite.Create 결과는 디스크에 저장된 에셋이 아니라서,
        /// 프리팹 저장 시 별도 서브에셋으로 붙이지 않는 한 참조가 유지되지 않는다.</summary>
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

        // ---------- 턴 배지 (좌상단) ----------

        private void WireTurnBadge(Transform canvas)
        {
            var panel = canvas.Find("TurnBadge");
            _turnBadgeBg = panel.GetComponent<Image>();
            panel.Find("Icon").GetComponent<Image>().sprite = IconLibrary.Get("turn");
            _turnText = panel.Find("Number").GetComponent<Text>();
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
        /// (OptionalActionDefs와 마찬가지로) 이 배열에 한 줄만 추가하고 UIPrefabSetup.GenerateBattleHud로
        /// 프리팹을 다시 생성하면 된다 — BattleController는 몰라도 된다.</summary>
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

        private void WireUnitPanel(Transform canvas)
        {
            var panel = canvas.Find("UnitPanel");
            _unitPanel = panel.gameObject;
            _unitPanelAccent = panel.Find("Accent").GetComponent<Image>();

            panel.Find("HpIcon").GetComponent<Image>().sprite = IconLibrary.Get("hp");
            _hpText = panel.Find("HpText").GetComponent<Text>();
            _hpFill = panel.Find("HpBarBg/HpBarFill").GetComponent<Image>();

            panel.Find("AttackIcon").GetComponent<Image>().sprite = IconLibrary.Get("attack");
            _attackText = panel.Find("AttackText").GetComponent<Text>();

            panel.Find("DefenseIcon").GetComponent<Image>().sprite = IconLibrary.Get("defense");
            _defenseText = panel.Find("DefenseText").GetComponent<Text>();

            panel.Find("MoveIcon").GetComponent<Image>().sprite = IconLibrary.Get("move");
            _moveText = panel.Find("MoveText").GetComponent<Text>();

            panel.Find("RangeIcon").GetComponent<Image>().sprite = IconLibrary.Get("range");
            _rangeText = panel.Find("RangeText").GetComponent<Text>();

            _passiveBadges = new PassiveBadge[PassiveDefs.Length];
            for (int i = 0; i < PassiveDefs.Length; i++)
            {
                var def = PassiveDefs[i];
                var badge = panel.Find("Passive_" + def.Icon);
                WirePassiveBadge(badge, def.Icon, def.Tooltip);
                _passiveBadges[i] = new PassiveBadge { Flag = def.Flag, Rect = (RectTransform)badge };
            }
        }

        private void WirePassiveBadge(Transform badge, string iconName, string tooltip)
        {
            badge.GetComponent<Image>().sprite = CircleSprite;
            badge.Find("Icon").GetComponent<Image>().sprite = IconLibrary.Get(iconName);

            var trigger = badge.GetComponent<TooltipTrigger>();
            trigger.Text = tooltip;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;
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
                badge.Rect.anchoredPosition = new Vector2(px, PassiveRowTop);
                px += 26f + 6f; // PassiveBadgeSize + PassiveBadgeGap (UIPrefabSetup.GenerateBattleHud와 동일)
            }
        }

        public void HideUnitPanel() => _unitPanel.SetActive(false);

        // ---------- 행동 버튼 (우하단, 아이콘만) ----------

        private const float ActionButtonSize = 52f;
        private const float ActionButtonGap = 8f;

        /// <summary>방어/치유/자폭처럼 "이 유닛이 가지고 있을 수도, 없을 수도 있는" 행동 버튼을
        /// 어떤 순서로 나열할지 + 아이콘 + 툴팁 설명을 한곳에 모아둔 표. 새 행동을 추가할 때
        /// (예: 향후 다른 특수 행동) 이 배열에 한 줄만 추가하고 UIPrefabSetup.GenerateBattleHud로 프리팹을
        /// 다시 생성하면 된다 — BattleController는 몰라도 된다.</summary>
        private static readonly (ActionType Flag, string Icon, string Tooltip)[] OptionalActionDefs =
        {
            (ActionType.Defend, "guard", "방어 태세: 받는 피해를 줄입니다. (방어력 +" + CombatSystem.GuardDefenseBonus + ")"),
            (ActionType.Heal, "heal", "치유: 사거리 내의 모든 아군 유닛(자신 제외)의 체력을 회복시킵니다."),
            (ActionType.SelfDestruct, "selfdestruct", "자폭: 스스로를 희생해 주위 1칸의 모든 적에게 남은 체력만큼 피해를 입힙니다."),
        };

        private void WireActionButtons(Transform canvas)
        {
            var endTurn = canvas.Find("EndTurnButton");
            WireIconButton(endTurn, "turn", "턴 종료: 현재 팀의 턴을 마칩니다.");
            _endTurnButton = endTurn.GetComponent<Button>();
            _endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());

            _optionalActions = new OptionalActionButton[OptionalActionDefs.Length];
            for (int i = 0; i < OptionalActionDefs.Length; i++)
            {
                var def = OptionalActionDefs[i];
                var buttonTransform = canvas.Find(def.Flag + "Button");
                WireIconButton(buttonTransform, def.Icon, def.Tooltip);

                var button = buttonTransform.GetComponent<Button>();
                switch (def.Flag)
                {
                    case ActionType.Defend: button.onClick.AddListener(() => OnDefendClicked?.Invoke()); break;
                    case ActionType.Heal: button.onClick.AddListener(() => OnHealClicked?.Invoke()); break;
                    case ActionType.SelfDestruct: button.onClick.AddListener(() => OnSelfDestructClicked?.Invoke()); break;
                }
                _optionalActions[i] = new OptionalActionButton { Flag = def.Flag, Button = button, Rect = (RectTransform)buttonTransform };
            }

            var deselect = canvas.Find("DeselectButton");
            WireIconButton(deselect, "deselect", "선택 해제: 유닛 선택을 취소합니다.");
            _deselectButton = deselect.GetComponent<Button>();
            _deselectRect = (RectTransform)deselect;
            _deselectButton.onClick.AddListener(() => OnDeselectClicked?.Invoke());
        }

        private void WireIconButton(Transform button, string iconName, string tooltip)
        {
            button.Find("Icon").GetComponent<Image>().sprite = IconLibrary.Get(iconName);

            var trigger = button.GetComponent<TooltipTrigger>();
            trigger.Text = tooltip;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;
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

        private void WireTooltip(Transform canvas)
        {
            var panel = canvas.Find("Tooltip");
            _tooltipPanel = panel.gameObject;
            _tooltipText = panel.Find("Text").GetComponent<Text>();
            _tooltipPanel.SetActive(false);
        }

        private void ShowTooltip(string text)
        {
            _tooltipText.text = text;
            _tooltipPanel.SetActive(true);
        }

        private void HideTooltip() => _tooltipPanel.SetActive(false);

        // ---------- 승/패 오버레이 ----------

        private void WireBattleEndPanel(Transform canvas)
        {
            var panel = canvas.Find("BattleEnd");
            _battleEndPanel = panel.gameObject;
            _battleEndIcon = panel.Find("Icon").GetComponent<Image>();
            _battleEndText = panel.Find("Label").GetComponent<Text>();

            panel.Find("RestartButton").GetComponent<Button>().onClick.AddListener(() => OnRestartClicked?.Invoke());

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
