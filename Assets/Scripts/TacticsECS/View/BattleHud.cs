using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 전투 화면 HUD(PC·모바일 공용). BattleController가 넘겨주는 값을 그리기만 하고, 누름은 이벤트·ActionMenuOption.OnClick으로만 알린다.
    /// 배치(UI 단위, UiScale이 화면 픽셀로 바꾼다 — 가장 좁은 휴대폰 가로 약 900x400에서도 맞게):
    /// - 위: 상단 막대 — 메뉴 · 턴 · 별 · 유닛 수 · 점수 · (빈칸) · 신앙 · 기술. 대신전 상태 줄은 그 아래 오른쪽.
    /// - 오른쪽: 정보 패널 — 고른 유닛·칸·도시의 정보와 할 수 있는 행동을 한 목록으로(ShowUnit/ShowTile). 내용이 길면 스크롤.
    /// - 오른쪽 아래: 턴 종료. 왼쪽 위: 알림(행동 기록이 잠깐 떴다 사라진다). 왼쪽 아래: 설명 말풍선(누르고 있거나 마우스를 올리면).
    /// - 대화 상자(메뉴·결과·확인)는 다른 화면보다 위 캔버스에.
    /// 계층은 BattleHud.prefab에 있고, 정보 패널·대화 상자·알림의 줄은 부품 프리팹(Prefabs/UI/Elements·Items)을 꺼내 쌓는다.
    /// 넓은/좁은 배치에 따른 자리 계산만 코드가 한다.
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        public event Action OnEndTurnClicked;
        public event Action OnMenuClicked;
        public event Action OnTechClicked;
        public event Action OnFaithClicked;
        /// <summary>정보 패널의 닫기 버튼(선택 해제).</summary>
        public event Action OnDeselectClicked;

        [Tooltip("uGUI 클릭 입력에 필요한 EventSystem 프리팹(EventSystem + InputSystemUIInputModule). " +
            "씬에 EventSystem이 이미 있으면 쓰이지 않는다. Assets/Prefabs/UI/EventSystem.prefab.")]
        [SerializeField] private GameObject eventSystemPrefab;

        /// <summary>상단 막대 칸 하나: 왼쪽 무리(메뉴·턴·별·유닛) / 오른쪽 무리(신앙·기술) / 나머지를 채우는 점수.</summary>
        public enum BarGroup { Left, Right, Fill }

        [Serializable]
        public struct BarItem
        {
            public RectTransform Rect;
            public float Width;
            public BarGroup Group;
        }

        [Header("화면(프리팹 안)")]
        [SerializeField] private RectTransform safe;
        [SerializeField] private RectTransform topBar;
        [Tooltip("상단 막대 칸 — 만든 순서대로 왼쪽→오른쪽.")]
        [SerializeField] private BarItem[] barItems;
        [Tooltip("경제가 없는 전투(데모 씬)에서 숨기는 칸.")]
        [SerializeField] private GameObject[] economyItems;
        [SerializeField] private Button menuButton;
        [SerializeField] private Image turnBg;
        [SerializeField] private Text turnText;
        [SerializeField] private Text starsText;
        [SerializeField] private Text unitsText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Button faithButton;
        [SerializeField] private RectTransform faithFill;
        [SerializeField] private Image faithFillImage;
        [SerializeField] private Button techButton;
        [SerializeField] private GameObject statusBox;
        [SerializeField] private Text statusText;
        [SerializeField] private RectTransform toastRoot;
        [SerializeField] private RectTransform inspectorArea;
        [SerializeField] private RectTransform inspector;
        [SerializeField] private ScrollRect inspectorScroll;
        [SerializeField] private RectTransform inspectorContent;
        [SerializeField] private Button inspectorCloseButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private GameObject tooltip;
        [SerializeField] private Text tooltipText;
        [SerializeField] private GameObject dialog;
        [SerializeField] private RectTransform dialogBox;

        [Header("부품 프리팹")]
        [SerializeField] private Text textPrefab;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private Image fillPrefab;
        [SerializeField] private RectTransform groupPrefab;
        [SerializeField] private RectTransform toastPrefab;
        [SerializeField] private Text faithDeltaPrefab;
        [SerializeField] private RectTransform hpRowPrefab;
        [SerializeField] private RectTransform statRowPrefab;
        [SerializeField] private RectTransform passiveBadgePrefab;

        /// <summary>팀 색. BattleController가 기록 문구를 팀 색으로 칠할 때도 이 값을 쓴다(FormatLogEntry).</summary>
        public static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        public static readonly Color EnemyAccent = new Color(0.90f, 0.35f, 0.30f);
        private static readonly Color GuardHighlight = Color.yellow;

        /// <summary>대화 상자 캔버스 순서 — 기술 트리(TechTreeHud)·신앙 펀치(FaithPunchHud)보다 위.</summary>
        public const int DialogSortingOrder = 50;

        /// <summary>유닛이 가질 수 있는 패시브와 그 아이콘. 새 패시브는 여기 한 줄만 더한다(설명은 번역 표 Action.&lt;행동&gt;.Tooltip).</summary>
        private static readonly (ActionType Flag, string Icon)[] PassiveDefs =
        {
            (ActionType.Counter, "counter"),
            (ActionType.Charge, "charge"),
            (ActionType.Retreat, "retreat"),
            (ActionType.Ambush, "ambush"),
            (ActionType.Infiltrate, "infiltrate"),
            (ActionType.Herd, "herd"),
            (ActionType.Convert, "convert"),
            (ActionType.Combo, "combo"),
            (ActionType.Scout, "scout"),
            (ActionType.Splash, "splash"),
            (ActionType.Stomp, "splash"),
            (ActionType.Stiff, "stiff"),
            (ActionType.Freeze, "freeze"),
            (ActionType.Fortify, "shields"),
            (ActionType.Hide, "infiltrate"),
            (ActionType.Static, "defense"),
            (ActionType.Independent, "freespirit"),
            (ActionType.Creep, "forestry"),
            (ActionType.Sneak, "infiltrate"),
            (ActionType.Fly, "move"),
            (ActionType.Plunder, "trade"),
            (ActionType.Poison, "defeat"),
            (ActionType.Swim, "aquatics"),
            (ActionType.Navigate, "navigation"),
            (ActionType.Grow, "farming"),
            (ActionType.Raze, "selfdestruct"),
            (ActionType.Wild, "freespirit"),
            (ActionType.Skate, "sailing"),
            (ActionType.Flood, "aquatics"),
            (ActionType.Drench, "aquatics"),
            (ActionType.AutoFreeze, "freeze"),
        };

        /// <summary>능력치 줄(StatRow.prefab)의 칸 — 프리팹 안 "&lt;이름&gt;Slot"·"&lt;이름&gt;Text"와 아이콘 이름이 같다.</summary>
        public static readonly string[] StatIcons = { "attack", "defense", "move", "range" };

        /// <summary>구조물 정보의 강조색. 표시 전용이라 View가 갖는다.</summary>
        private static readonly Dictionary<string, Color> StructureAccentColors = new Dictionary<string, Color>
        {
            ["Capital"] = new Color(0.85f, 0.7f, 0.15f),
            ["Village"] = new Color(0.75f, 0.62f, 0.42f),
            ["Ruin"] = new Color(0.6f, 0.6f, 0.6f),
            ["Resource_Food"] = new Color(0.8f, 0.25f, 0.3f),
            ["Resource_Ore"] = new Color(0.3f, 0.75f, 0.75f),
            ["Resource_Fruit"] = new Color(0.8f, 0.25f, 0.3f),
            ["Resource_Crop"] = new Color(0.85f, 0.75f, 0.3f),
            ["Resource_Animal"] = new Color(0.6f, 0.45f, 0.3f),
            ["Resource_Metal"] = new Color(0.3f, 0.75f, 0.75f),
            ["Resource_Fish"] = new Color(1f, 0.85f, 0.3f),
            ["Lighthouse"] = new Color(0.95f, 0.95f, 0.9f),
            ["Starfish"] = new Color(0.9f, 0.5f, 0.2f),
        };

        /// <summary>행동·패시브 설명 — 번역 표 "Action.&lt;행동&gt;.Tooltip"(수치는 문구 속 {Rule.*} 자리표시자).</summary>
        public static string ActionTooltip(ActionType flag) => LocalizationSystem.T("Action." + flag + ".Tooltip");
        public static string ActionName(ActionType flag) => LocalizationSystem.T("Action." + flag + ".Name", flag.ToString());

        private const float InspectorPadding = 10f;
        private const float OptionHeight = 40f;
        private const float OptionDetailHeight = 52f;
        private const float OptionGap = 4f;
        private const float StatRowHeight = 30f;
        private const int MaxToasts = 4;
        private const float ToastWidth = 400f;
        private const float ToastSeconds = 4f;
        private const float ToastFadeSeconds = 0.6f;

        private Text _faithText;
        private int _shownFaith = -1;
        private readonly List<(RectTransform Rect, CanvasGroup Group, float Born)> _toasts = new List<(RectTransform, CanvasGroup, float)>();
        private float _inspectorHeight;
        private bool? _compact;

        public bool IsDialogOpen => dialog != null && dialog.activeSelf;
        public bool IsInspectorOpen => inspector != null && inspector.gameObject.activeSelf;

        public void Init()
        {
            EnsureEventSystem();
            UiKit.Label(menuButton).text = LocalizationSystem.T("UI.Hud.Menu");
            UiKit.Label(techButton).text = LocalizationSystem.T("UI.TechTree.Toggle");
            UiKit.Label(endTurnButton).text = LocalizationSystem.T("UI.Battle.EndTurn");
            _faithText = UiKit.Label(faithButton);
            menuButton.onClick.AddListener(() => OnMenuClicked?.Invoke());
            faithButton.onClick.AddListener(() => OnFaithClicked?.Invoke());
            techButton.onClick.AddListener(() => OnTechClicked?.Invoke());
            inspectorCloseButton.onClick.AddListener(() => OnDeselectClicked?.Invoke());
            endTurnButton.onClick.AddListener(() => OnEndTurnClicked?.Invoke());
            AddTooltip(endTurnButton.gameObject, LocalizationSystem.T("UI.Battle.EndTurnTooltip"));

            statusBox.SetActive(false);
            tooltip.SetActive(false);
            dialog.SetActive(false);
            HideInspector();
            PixelUIScaler.Attach(gameObject);
            Canvas.willRenderCanvases += OnWillRender;
            OnWillRender();
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        /// <summary>새 Input System 기준(activeInputHandler=Input System Package)이라 uGUI 클릭을 받으려면 InputSystemUIInputModule이 필요하다.</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (eventSystemPrefab == null)
            {
                Debug.LogError($"[BattleHud] {name}: eventSystemPrefab이 비어있습니다. Assets/Prefabs/UI/EventSystem.prefab을 연결하세요.");
                return;
            }
            Instantiate(eventSystemPrefab).name = "EventSystem";
        }

        // ---------- 상단 막대 ----------

        /// <summary>넓은 배치: 한 줄 [왼쪽 무리 · 점수 · 오른쪽 무리]. 좁은 배치: 두 줄 — 위 [왼쪽 무리], 아래 [점수 · 오른쪽 무리]. 왼쪽 무리는 좁은 배치에서 조금 줄인다.</summary>
        private void LayoutTopBar(bool compact)
        {
            const float pad = 5f, spacing = 6f;
            float row = UiKit.TopBarHeight, itemHeight = row - pad * 2f;
            topBar.offsetMin = new Vector2(0f, -UiKit.TopBarHeightFor(compact));
            topBar.offsetMax = Vector2.zero;

            float left = UiKit.Gap, right = UiKit.Gap;
            int rightRow = compact ? 1 : 0;
            // 오른쪽 무리는 오른쪽 끝부터 채우므로 거꾸로 돈다(만든 순서대로 왼쪽→오른쪽에 보이게).
            for (int i = barItems.Length - 1; i >= 0; i--)
            {
                var item = barItems[i];
                if (item.Group != BarGroup.Right) continue;
                var r = item.Rect;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
                r.anchoredPosition = new Vector2(-right, -pad - rightRow * row);
                r.sizeDelta = new Vector2(item.Width, itemHeight);
                right += item.Width + spacing;
            }
            foreach (var item in barItems)
            {
                var r = item.Rect;
                if (item.Group == BarGroup.Left)
                {
                    float w = compact ? item.Width * 0.86f : item.Width;
                    r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
                    r.anchoredPosition = new Vector2(left, -pad);
                    r.sizeDelta = new Vector2(w, itemHeight);
                    left += w + spacing;
                }
            }
            foreach (var item in barItems)
            {
                if (item.Group != BarGroup.Fill) continue;
                var r = item.Rect;
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.offsetMin = new Vector2(compact ? UiKit.Gap : left, -pad - rightRow * row - itemHeight);
                r.offsetMax = new Vector2(-right, -pad - rightRow * row);
            }

            var status = (RectTransform)statusBox.transform;
            UiKit.Place(status, new Vector2(1f, 1f), new Vector2(-UiKit.Gap, -UiKit.TopBarHeightFor(compact) - 4f),
                new Vector2(Mathf.Min(UiKit.SidePanelWidth, SafeWidth() - UiKit.Gap * 2f), status.sizeDelta.y > 0f ? status.sizeDelta.y : 40f));
        }

        private float SafeWidth() => safe.rect.width > 1f ? safe.rect.width : UiScale.Reference.x;

        /// <summary>캔버스를 그리기 직전마다(에디트 모드 캡처 포함): 폭이 넓은/좁은 배치 경계를 넘었으면 전체를 다시 배치하고, 정보 패널 높이를 내용에 맞춘다.</summary>
        private void OnWillRender()
        {
            if (safe == null) return;
            bool compact = UiKit.IsCompact(safe);
            if (_compact != compact)
            {
                _compact = compact;
                LayoutTopBar(compact);
                toastRoot.anchoredPosition = new Vector2(UiKit.Gap, -UiKit.TopBarHeightFor(compact) - UiKit.Gap);
                UiKit.LayoutSheetArea(inspectorArea, compact);
                LayoutTooltip(compact);
            }
            UiKit.FitSheet(inspectorArea, inspector, _inspectorHeight + 8f, compact);
        }

        public void SetTurn(Team team, int turnNumber)
        {
            turnBg.color = team == Team.Player ? PlayerAccent : EnemyAccent;
            turnText.text = LocalizationSystem.F("UI.Hud.Turn", turnNumber);
        }

        /// <summary>경제가 없는 전투(데모 씬)에서는 별·유닛 수·점수·신앙·기술을 숨긴다.</summary>
        public void SetEconomyVisible(bool visible)
        {
            foreach (var item in economyItems) item.SetActive(visible);
            if (!visible) statusBox.SetActive(false);
        }

        public void SetResources(CityResourceData city, int unitsUsed)
        {
            starsText.text = city.StarsProduction > 0 ? LocalizationSystem.F("UI.Hud.StarsIncome", city.Stars, city.StarsProduction) : LocalizationSystem.F("UI.Hud.Stars", city.Stars);
            unitsText.text = LocalizationSystem.F("UI.Hud.Units", unitsUsed, city.PopulationCap);
        }

        public void SetScoreLine(string line) => scoreText.text = line ?? string.Empty;

        /// <summary>신앙 칩: 현재/최대(다음 턴 수입), 게이지, 대신전 상태 줄. 값이 바뀌면 칩 아래에 증감을 잠깐 띄운다.</summary>
        public void SetFaith(int current, int max, int income, string status)
        {
            _faithText.text = income > 0 ? LocalizationSystem.F("UI.Hud.FaithIncome", current, max, income) : LocalizationSystem.F("UI.Hud.Faith", current, max);
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            faithFill.anchorMax = new Vector2(ratio, 1f);
            faithFillImage.color = max > 0 && current >= max ? Color.Lerp(UiKit.FaithColor, Color.white, 0.5f) : UiKit.FaithColor;
            if (_shownFaith >= 0 && current != _shownFaith && Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(FaithDelta(current - _shownFaith));
            _shownFaith = current;

            statusText.text = status ?? string.Empty;
            statusBox.SetActive(!string.IsNullOrEmpty(status) && faithButton.gameObject.activeSelf);
            if (!string.IsNullOrEmpty(status))
                ((RectTransform)statusBox.transform).sizeDelta = new Vector2(Mathf.Min(UiKit.SidePanelWidth, SafeWidth() - UiKit.Gap * 2f), 12f + 17f * status.Split('\n').Length);
        }

        private System.Collections.IEnumerator FaithDelta(int delta)
        {
            var text = UiKit.Spawn(faithDeltaPrefab, faithButton.transform, "Delta");
            text.color = delta > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.5f);
            text.text = delta > 0 ? "+" + delta : delta.ToString();
            var rect = text.rectTransform;
            const float duration = 1.1f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.anchoredPosition = new Vector2(0f, -28f - 10f * k);
                var c = text.color; c.a = 1f - k * k; text.color = c;
                yield return null;
            }
            UiKit.Destroy(text.gameObject);
        }

        public void SetEndTurnVisible(bool visible) => endTurnButton.gameObject.SetActive(visible);

        /// <summary>전투에서만 쓰는 부분(상단 막대·알림) — 샌드박스 배치 단계에서는 샌드박스 툴바와 자리가 겹치므로 숨긴다.</summary>
        public void SetBattlePanelsVisible(bool visible)
        {
            topBar.gameObject.SetActive(visible);
            toastRoot.gameObject.SetActive(visible);
            if (!visible) { statusBox.SetActive(false); HideInspector(); }
        }

        // ---------- 알림(행동 기록) ----------

        /// <summary>행동 한 줄을 알림 맨 위에 띄운다. 몇 초 뒤 흐려지며 사라지고, MaxToasts를 넘으면 오래된 줄부터 지운다.
        /// richText 색 태그(BattleController.FormatLogEntry)로 팀을 구분한다.</summary>
        public void AddLogEntry(string richText)
        {
            var rect = UiKit.Spawn(toastPrefab, toastRoot, "Toast");
            var text = rect.GetComponentInChildren<Text>();
            text.text = richText;
            float width = Mathf.Min(ToastWidth, SafeWidth() - UiKit.Gap * 2f);
            float height = Mathf.Max(26f, UiKit.PreferredHeight(text, width - 16f) + 8f);
            rect.sizeDelta = new Vector2(width, height);
            _toasts.Insert(0, (rect, rect.GetComponent<CanvasGroup>(), Time.unscaledTime));
            while (_toasts.Count > MaxToasts)
            {
                UiKit.Destroy(_toasts[_toasts.Count - 1].Rect.gameObject);
                _toasts.RemoveAt(_toasts.Count - 1);
            }
            LayoutToasts();
        }

        private void LayoutToasts()
        {
            float y = 0f;
            foreach (var t in _toasts)
            {
                t.Rect.anchoredPosition = new Vector2(0f, -y);
                y += t.Rect.sizeDelta.y + 4f;
            }
        }

        private void Update()
        {
            bool removed = false;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _toasts[i].Born;
                if (age > ToastSeconds + ToastFadeSeconds)
                {
                    UiKit.Destroy(_toasts[i].Rect.gameObject);
                    _toasts.RemoveAt(i);
                    removed = true;
                    continue;
                }
                _toasts[i].Group.alpha = age < ToastSeconds ? 1f : 1f - (age - ToastSeconds) / ToastFadeSeconds;
            }
            if (removed) LayoutToasts();
        }

        // ---------- 정보 패널(넓은 배치: 오른쪽 열, 좁은 배치: 아래 시트) ----------

        /// <summary>정보 패널 폭(배치에 따라 오른쪽 열 폭 또는 화면 폭) — 글자 줄바꿈 높이를 잴 때 쓴다.</summary>
        private float PanelWidth => UiKit.SheetWidth(inspectorArea);

        public void HideInspector()
        {
            if (inspector != null) inspector.gameObject.SetActive(false);
            HideTooltip();
        }

        /// <summary>유닛 정보: 이름·소속 줄, 체력 막대와 능력치(공격·방어·이동·사거리), 가진 패시브, 그리고 할 수 있는 행동 목록.</summary>
        public void ShowUnit(EntityWorld world, int unitId, string title, string subtitle, IReadOnlyList<ActionMenuOption> options)
        {
            var team = world.Get<Team>(unitId);
            float y = BeginInspector(title, subtitle, team == Team.Player ? PlayerAccent : EnemyAccent);
            y = AddUnitStats(world, unitId, y);
            y = AddPassives(world.Get<AvailableActions>(unitId).Value, y);
            EndInspector(y, options);
        }

        /// <summary>칸·도시 정보: 제목, 설명(구조물 이름·설명 + 도시/칸 정보), 행동 목록. 강조색은 구조물 색(없으면 흰색).</summary>
        public void ShowTile(string title, string body, string structureId, IReadOnlyList<ActionMenuOption> options)
        {
            Color accent = Color.white;
            string structureText = null;
            if (!string.IsNullOrEmpty(structureId))
            {
                foreach (var info in StructureDefinition.All)
                {
                    if (info.Id != structureId) continue;
                    if (StructureAccentColors.TryGetValue(structureId, out var c)) accent = c;
                    structureText = $"<color=#{ColorUtility.ToHtmlStringRGB(accent)}>{info.Name}</color> — {info.Description}";
                }
            }
            float y = BeginInspector(title, structureText, accent);
            if (!string.IsNullOrEmpty(body)) y = AddParagraph(body, y, UiKit.SmallText + 1, UiKit.MutedText);
            EndInspector(y, options);
        }

        private float BeginInspector(string title, string subtitle, Color accent)
        {
            UiKit.ClearChildren(inspectorContent);
            HideTooltip();
            inspector.gameObject.SetActive(true);
            inspectorScroll.verticalNormalizedPosition = 1f;

            var stripe = UiKit.Fill(fillPrefab, inspectorContent, "AccentFill", accent);
            UiKit.Place(stripe.rectTransform, new Vector2(0f, 1f), new Vector2(2f, -InspectorPadding), new Vector2(4f, 24f));

            float y = InspectorPadding;
            var titleText = UiKit.Text(textPrefab, inspectorContent, "Title", UiKit.TitleText, TextAnchor.UpperLeft);
            titleText.text = title;
            float textWidth = PanelWidth - InspectorPadding * 2f - UiKit.TouchSize - 8f;
            float h = Mathf.Max(26f, UiKit.PreferredHeight(titleText, textWidth));
            UiKit.Row(titleText.rectTransform, y, h, InspectorPadding + 4f, UiKit.TouchSize + 4f);
            y += h + 2f;
            if (!string.IsNullOrEmpty(subtitle)) y = AddParagraph(subtitle, y, UiKit.SmallText, UiKit.MutedText, UiKit.TouchSize + 4f);
            return y + 4f;
        }

        private float AddParagraph(string text, float y, int size, Color color, float rightInset = 0f)
        {
            var t = UiKit.Text(textPrefab, inspectorContent, "Body", size, TextAnchor.UpperLeft, color);
            t.text = text;
            float width = PanelWidth - 8f - InspectorPadding * 2f - rightInset;
            float h = UiKit.PreferredHeight(t, width);
            UiKit.Row(t.rectTransform, y, h, InspectorPadding, InspectorPadding + rightInset);
            return y + h + 6f;
        }

        private float AddUnitStats(EntityWorld world, int unitId, float y)
        {
            int hp = world.Get<Hp>(unitId).Value;
            int maxHp = world.Get<MaxHp>(unitId).Value;
            float frac = maxHp > 0 ? (float)hp / maxHp : 0f;

            // HpRow.prefab: 체력 아이콘 · 숫자 · 막대(HpBarFill/ValueFill).
            var row = UiKit.Row(UiKit.Spawn(hpRowPrefab, inspectorContent, "HpRow"), y, StatRowHeight, InspectorPadding, InspectorPadding);
            UiKit.SetIcon(row.Find("HpSlot/HpIcon").GetComponent<Image>(), "hp");
            row.Find("HpText").GetComponent<Text>().text = $"{hp}/{maxHp}";
            var fill = (RectTransform)row.Find("HpBarFill/ValueFill");
            fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            fill.GetComponent<Image>().color = HpColorScale.ForFraction(frac);
            y += StatRowHeight + 2f;

            bool guarding = world.Get<IsGuarding>(unitId).Value;
            var stats = new (string Value, Color Color)[]
            {
                (CombatSystem.EffectiveAttack(world, unitId).ToString(), Color.white),
                (CombatSystem.EffectiveDefense(world, unitId).ToString(), guarding ? GuardHighlight : Color.white),
                (MovementSystem.EffectiveMoveRange(world, unitId).ToString(), Color.white),
                (world.Get<AttackRange>(unitId).Value.ToString(), Color.white),
            };
            // StatRow.prefab: 칸마다 아이콘(<이름>Slot) + 값(<이름>Text). 칸 폭은 패널 폭에 따라 나눈다.
            var statRow = UiKit.Row(UiKit.Spawn(statRowPrefab, inspectorContent, "StatRow"), y, StatRowHeight, InspectorPadding, InspectorPadding);
            float cell = (Mathf.Min(PanelWidth, 420f) - 8f - InspectorPadding * 2f) / StatIcons.Length;
            for (int i = 0; i < StatIcons.Length; i++)
            {
                var slot = UiKit.Place((RectTransform)statRow.Find(StatIcons[i] + "Slot"), new Vector2(0f, 0.5f), new Vector2(cell * i, 0f), new Vector2(24f, 24f));
                UiKit.SetIcon(slot.GetChild(0).GetComponent<Image>(), StatIcons[i]);
                var value = statRow.Find(StatIcons[i] + "Text").GetComponent<Text>();
                value.text = stats[i].Value;
                value.color = stats[i].Color;
                UiKit.Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(cell * i + 28f, 0f), new Vector2(cell - 28f, StatRowHeight));
            }
            return y + StatRowHeight + 4f;
        }

        private float AddPassives(ActionType available, float y)
        {
            const float size = 30f;
            float x = 0f;
            RectTransform row = null;
            foreach (var def in PassiveDefs)
            {
                if ((available & def.Flag) == 0) continue;
                if (row == null) row = UiKit.Row(UiKit.Spawn(groupPrefab, inspectorContent, "PassiveRow"), y, size, InspectorPadding, InspectorPadding);
                // PassiveBadge.prefab: 둥근 틀 + 가운데 아이콘(PassiveSlot/PassiveIcon) + 설명 말풍선 트리거.
                var badge = UiKit.Place(UiKit.Spawn(passiveBadgePrefab, row, "Passive_" + def.Icon), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(size, size));
                UiKit.SetIcon(badge.Find("PassiveSlot/PassiveIcon").GetComponent<Image>(), def.Icon);
                AddTooltip(badge.gameObject, ActionTooltip(def.Flag));
                x += size + 6f;
            }
            return row == null ? y : y + size + 6f;
        }

        private void EndInspector(float y, IReadOnlyList<ActionMenuOption> options)
        {
            if (options != null && options.Count > 0)
            {
                y += 2f;
                float labelWidth = PanelWidth - 8f - (InspectorPadding - 4f) * 2f - 18f;
                foreach (var option in options)
                {
                    var button = UiKit.Button(buttonPrefab, inspectorContent, "Option", string.Empty, option.Primary ? UiKit.PrimaryColor : UiKit.ButtonColor, option.OnClick, UiKit.BodyText - 1);
                    var label = UiKit.Label(button);
                    label.alignment = TextAnchor.MiddleLeft;
                    UiKit.Stretch(label.rectTransform, 10f, 2f, 8f, 2f);
                    label.text = string.IsNullOrEmpty(option.Detail) ? option.Label
                        : $"{option.Label}\n<size={UiKit.SmallText - 1}><color=#{ColorUtility.ToHtmlStringRGB(option.Enabled ? UiKit.MutedText : UiKit.DisabledText)}>{option.Detail}</color></size>";
                    // 설명이 여러 줄이면 그만큼 줄을 키운다(손가락 크기보다 작아지지는 않는다).
                    float h = Mathf.Max(string.IsNullOrEmpty(option.Detail) ? OptionHeight : OptionDetailHeight, UiKit.PreferredHeight(label, labelWidth) + 14f);
                    UiKit.Row((RectTransform)button.transform, y, h, InspectorPadding - 4f, InspectorPadding - 4f);
                    UiKit.SetEnabled(button, option.Enabled, option.Primary ? UiKit.PrimaryColor : UiKit.ButtonColor);
                    if (!option.Enabled) label.color = UiKit.DisabledText;
                    y += h + OptionGap;
                }
            }
            y += InspectorPadding - OptionGap;
            UiKit.SetContentHeight(inspectorContent, y);
            _inspectorHeight = y;
            OnWillRender();
            PixelUIScaler.Attach(gameObject);
        }

        // ---------- 설명 말풍선 ----------

        /// <summary>넓은 배치: 왼쪽 아래. 좁은 배치: 아래 시트가 차지하므로 상단 막대 바로 아래, 폭 전체.</summary>
        private void LayoutTooltip(bool compact)
        {
            var rect = (RectTransform)tooltip.transform;
            float height = rect.sizeDelta.y > 0f ? rect.sizeDelta.y : 60f;
            if (compact) UiKit.Place(rect, new Vector2(0f, 1f), new Vector2(UiKit.Gap, -UiKit.TopBarHeightFor(true) - UiKit.Gap), new Vector2(TooltipWidth, height));
            else UiKit.Place(rect, new Vector2(0f, 0f), new Vector2(UiKit.Gap, UiKit.Gap), new Vector2(TooltipWidth, height));
        }

        private float TooltipWidth => Mathf.Min(360f, SafeWidth() - UiKit.Gap * 2f);

        /// <summary>마우스를 올리거나(PC) 누르고 있는 동안(터치) 말풍선에 설명을 띄운다. 트리거(TooltipTrigger)는 프리팹에 붙어 있다.</summary>
        private void AddTooltip(GameObject go, string text)
        {
            var trigger = go.GetComponent<TooltipTrigger>();
            trigger.Text = text;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;
        }

        private void ShowTooltip(string text)
        {
            tooltipText.text = text;
            ((RectTransform)tooltip.transform).sizeDelta = new Vector2(TooltipWidth, UiKit.PreferredHeight(tooltipText, TooltipWidth - 20f) + 14f);
            tooltip.SetActive(true);
        }

        private void HideTooltip()
        {
            if (tooltip != null) tooltip.SetActive(false);
        }

        // ---------- 대화 상자(메뉴·결과·확인) ----------

        /// <summary>가운데 대화 상자: 제목, 본문(없으면 생략), 버튼을 세로로. 버튼을 눌러도 저절로 닫히지 않는다(HideDialog).</summary>
        public void ShowDialog(string title, string body, IReadOnlyList<ActionMenuOption> buttons, Color? titleColor = null)
        {
            UiKit.ClearChildren(dialogBox);
            const float pad = 18f, buttonHeight = 48f;
            float width = Mathf.Min(420f, SafeWidth() - UiKit.Gap * 4f); // 좁은 배치(세로 휴대폰)에서도 화면 안에
            float y = pad;
            var titleText = UiKit.Text(textPrefab, dialogBox, "Title", titleColor.HasValue ? UiKit.HeadlineText : UiKit.TitleText + 4, TextAnchor.MiddleCenter, titleColor ?? Color.white);
            titleText.text = title;
            float h = Mathf.Max(30f, UiKit.PreferredHeight(titleText, width - pad * 2f));
            UiKit.Row(titleText.rectTransform, y, h, pad, pad);
            y += h + 8f;
            if (!string.IsNullOrEmpty(body))
            {
                var bodyText = UiKit.Text(textPrefab, dialogBox, "Body", UiKit.BodyText, TextAnchor.UpperCenter, UiKit.MutedText);
                bodyText.text = body;
                float bh = UiKit.PreferredHeight(bodyText, width - pad * 2f);
                UiKit.Row(bodyText.rectTransform, y, bh, pad, pad);
                y += bh + 12f;
            }
            foreach (var option in buttons)
            {
                var color = option.Primary ? UiKit.PrimaryColor : UiKit.ButtonColor;
                var button = UiKit.Button(buttonPrefab, dialogBox, "Button", option.Label, color, option.OnClick, UiKit.BodyText + 1);
                UiKit.Row((RectTransform)button.transform, y, buttonHeight, pad, pad);
                UiKit.SetEnabled(button, option.Enabled, color);
                y += buttonHeight + 8f;
            }
            dialogBox.sizeDelta = new Vector2(width, y - 8f + pad);
            dialog.SetActive(true);
            PixelUIScaler.Attach(dialog);
        }

        public void HideDialog()
        {
            if (dialog != null) dialog.SetActive(false);
        }
    }
}
