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
    /// 계층은 프리팹이 아니라 Init이 코드로 만든다(BattleHud.prefab은 이 컴포넌트와 폰트·EventSystem 참조만 가진다).
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

        [Tooltip("모든 HUD 글자에 쓰는 한글 폰트. Assets/Fonts/Jua-Regular.ttf. UIPrefabSetup.GenerateAll이 채운다.")]
        [SerializeField] private Font uiFont;

        /// <summary>코드로 계층을 만드는 다른 View(TechTreeHud, FaithPunchHud, TitleHud)가 같은 한글 폰트를 쓰도록 공유한다.</summary>
        public Font UiFont => uiFont;

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
        };

        /// <summary>구조물 정보의 강조색(StructureAssetSetup이 모델을 칠하는 색과 맞춤). 표시 전용이라 View가 갖는다.</summary>
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

        private RectTransform _safe;
        private GameObject _topBar;
        private Image _turnBg;
        private Text _turnText;
        private readonly List<GameObject> _economyItems = new List<GameObject>();
        private Text _starsText, _unitsText, _scoreText, _faithText;
        private RectTransform _faithFill;
        private Image _faithFillImage;
        private RectTransform _faithChip;
        private int _shownFaith = -1;
        private GameObject _statusBox;
        private Text _statusText;

        private RectTransform _toastRoot;
        private readonly List<(RectTransform Rect, Text Text, float Born)> _toasts = new List<(RectTransform, Text, float)>();

        private RectTransform _inspectorArea;
        private RectTransform _inspector;
        private RectTransform _inspectorContent;
        private ScrollRect _inspectorScroll;
        private float _inspectorHeight;

        private Button _endTurnButton;
        private GameObject _tooltip;
        private Text _tooltipText;

        private GameObject _dialog;
        private RectTransform _dialogBox;

        public bool IsDialogOpen => _dialog != null && _dialog.activeSelf;
        public bool IsInspectorOpen => _inspector != null && _inspector.gameObject.activeSelf;

        public void Init()
        {
            EnsureEventSystem();
            // 예전 프리팹(Canvas 계층을 구워 둔 것)으로 만들어졌어도 새 계층만 쓴다.
            var legacy = transform.Find("Canvas");
            if (legacy != null) UiKit.Destroy(legacy.gameObject);

            _safe = UiKit.CreateCanvas(transform, 0, out _);
            BuildTopBar();
            BuildToasts();
            BuildInspector();
            BuildEndTurn();
            BuildTooltip();
            BuildDialog();

            HideInspector();
            PixelUISkin.Apply(gameObject);
            Canvas.willRenderCanvases += FitInspector;
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= FitInspector;

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

        private void BuildTopBar()
        {
            var bar = UiKit.Row(UiKit.Rect("TopBar", _safe), 0f, UiKit.TopBarHeight);
            _topBar = bar.gameObject;
            UiKit.Fill(bar, UiKit.BarColor, raycast: true);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)UiKit.Gap, (int)UiKit.Gap, 5, 5);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var menu = UiKit.Button(bar, "MenuButton", uiFont, LocalizationSystem.T("UI.Hud.Menu"), UiKit.ButtonColor, () => OnMenuClicked?.Invoke());
            Width(menu.gameObject, 72f);

            var turn = UiKit.Rect("TurnBadge", bar);
            _turnBg = UiKit.Panel(turn, PlayerAccent);
            _turnText = UiKit.Text(turn, "Number", uiFont, UiKit.BodyText, TextAnchor.MiddleCenter);
            UiKit.Stretch(_turnText.rectTransform);
            Width(turn.gameObject, 84f);

            _starsText = Chip(bar, "Stars", 120f, UiKit.StarColor);
            _unitsText = Chip(bar, "Units", 104f, Color.white);

            _scoreText = UiKit.Text(bar, "Score", uiFont, UiKit.BodyText, TextAnchor.MiddleCenter, UiKit.MutedText);
            _scoreText.resizeTextMaxSize = UiKit.BodyText;
            _scoreText.resizeTextMinSize = UiKit.SmallText - 2;
            _scoreText.resizeTextForBestFit = true;
            _scoreText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _economyItems.Add(_scoreText.gameObject);

            var faith = UiKit.Button(bar, "FaithButton", uiFont, string.Empty, UiKit.ButtonColor, () => OnFaithClicked?.Invoke());
            _faithChip = (RectTransform)faith.transform;
            Width(faith.gameObject, 164f);
            _faithText = UiKit.Label(faith);
            _faithText.color = UiKit.FaithColor;
            UiKit.Stretch(_faithText.rectTransform, 8f, 8f, 8f, 2f);
            var gauge = UiKit.Rect("GaugeFill", faith.transform);
            gauge.anchorMin = new Vector2(0f, 0f);
            gauge.anchorMax = new Vector2(1f, 0f);
            gauge.pivot = new Vector2(0.5f, 0f);
            gauge.offsetMin = new Vector2(10f, 6f);
            gauge.offsetMax = new Vector2(-10f, 11f);
            UiKit.Fill(gauge, new Color(0.20f, 0.22f, 0.27f));
            _faithFill = UiKit.Rect("BarFill", gauge);
            _faithFill.anchorMin = Vector2.zero;
            _faithFill.anchorMax = new Vector2(0f, 1f);
            _faithFill.offsetMin = _faithFill.offsetMax = Vector2.zero;
            _faithFillImage = UiKit.Fill(_faithFill, UiKit.FaithColor);
            _economyItems.Add(faith.gameObject);

            var tech = UiKit.Button(bar, "TechButton", uiFont, LocalizationSystem.T("UI.TechTree.Toggle"), UiKit.PrimaryColor, () => OnTechClicked?.Invoke());
            Width(tech.gameObject, 96f);
            _economyItems.Add(tech.gameObject);

            // 대신전 상태 줄: 막대 아래 오른쪽(없으면 숨김).
            var status = UiKit.Place(UiKit.Rect("GreatTempleStatus", _safe), new Vector2(1f, 1f), new Vector2(-UiKit.Gap, -UiKit.TopBarHeight - 4f), new Vector2(UiKit.SidePanelWidth, 40f));
            UiKit.Panel(status, UiKit.BarColor);
            _statusText = UiKit.Text(status, "Text", uiFont, UiKit.SmallText, TextAnchor.MiddleCenter, UiKit.FaithColor);
            UiKit.Stretch(_statusText.rectTransform, 8f, 2f, 8f, 2f);
            _statusBox = status.gameObject;
            _statusBox.SetActive(false);
        }

        private Text Chip(RectTransform bar, string name, float width, Color color)
        {
            var chip = UiKit.Rect(name, bar);
            UiKit.Panel(chip, UiKit.ButtonColor);
            var text = UiKit.Text(chip, "Text", uiFont, UiKit.BodyText, TextAnchor.MiddleCenter, color);
            UiKit.Stretch(text.rectTransform, 4f, 0f, 4f, 0f);
            Width(chip.gameObject, width);
            _economyItems.Add(chip.gameObject);
            return text;
        }

        private static void Width(GameObject go, float width)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
        }

        public void SetTurn(Team team, int turnNumber)
        {
            _turnBg.color = team == Team.Player ? PlayerAccent : EnemyAccent;
            _turnText.text = LocalizationSystem.F("UI.Hud.Turn", turnNumber);
        }

        /// <summary>경제가 없는 전투(데모 씬)에서는 별·유닛 수·점수·신앙·기술을 숨긴다.</summary>
        public void SetEconomyVisible(bool visible)
        {
            foreach (var item in _economyItems) item.SetActive(visible);
            if (!visible) _statusBox.SetActive(false);
        }

        public void SetResources(CityResourceData city, int unitsUsed)
        {
            _starsText.text = city.StarsProduction > 0 ? LocalizationSystem.F("UI.Hud.StarsIncome", city.Stars, city.StarsProduction) : LocalizationSystem.F("UI.Hud.Stars", city.Stars);
            _unitsText.text = LocalizationSystem.F("UI.Hud.Units", unitsUsed, city.PopulationCap);
        }

        public void SetScoreLine(string line) => _scoreText.text = line ?? string.Empty;

        /// <summary>신앙 칩: 현재/최대(다음 턴 수입), 게이지, 대신전 상태 줄. 값이 바뀌면 칩 아래에 증감을 잠깐 띄운다.</summary>
        public void SetFaith(int current, int max, int income, string status)
        {
            _faithText.text = income > 0 ? LocalizationSystem.F("UI.Hud.FaithIncome", current, max, income) : LocalizationSystem.F("UI.Hud.Faith", current, max);
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            _faithFill.anchorMax = new Vector2(ratio, 1f);
            _faithFillImage.color = max > 0 && current >= max ? Color.Lerp(UiKit.FaithColor, Color.white, 0.5f) : UiKit.FaithColor;
            if (_shownFaith >= 0 && current != _shownFaith && Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(FaithDelta(current - _shownFaith));
            _shownFaith = current;

            _statusText.text = status ?? string.Empty;
            _statusBox.SetActive(!string.IsNullOrEmpty(status) && _faithChip.gameObject.activeSelf);
            if (!string.IsNullOrEmpty(status))
                ((RectTransform)_statusBox.transform).sizeDelta = new Vector2(UiKit.SidePanelWidth, 12f + 17f * status.Split('\n').Length);
        }

        private System.Collections.IEnumerator FaithDelta(int delta)
        {
            var text = UiKit.Text(_faithChip, "Delta", uiFont, UiKit.TitleText, TextAnchor.MiddleCenter, delta > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.5f));
            text.text = delta > 0 ? "+" + delta : delta.ToString();
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var rect = UiKit.Place(text.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(80f, 26f));
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

        public void SetEndTurnVisible(bool visible) => _endTurnButton.gameObject.SetActive(visible);

        /// <summary>전투에서만 쓰는 부분(상단 막대·알림) — 샌드박스 배치 단계에서는 샌드박스 툴바와 자리가 겹치므로 숨긴다.</summary>
        public void SetBattlePanelsVisible(bool visible)
        {
            _topBar.SetActive(visible);
            _toastRoot.gameObject.SetActive(visible);
            if (!visible) { _statusBox.SetActive(false); HideInspector(); }
        }

        // ---------- 알림(행동 기록) ----------

        private void BuildToasts()
        {
            _toastRoot = UiKit.Place(UiKit.Rect("Toasts", _safe), new Vector2(0f, 1f), new Vector2(UiKit.Gap, -UiKit.TopBarHeight - UiKit.Gap), new Vector2(ToastWidth, 200f));
        }

        /// <summary>행동 한 줄을 알림 맨 위에 띄운다. 몇 초 뒤 흐려지며 사라지고, MaxToasts를 넘으면 오래된 줄부터 지운다.
        /// richText 색 태그(BattleController.FormatLogEntry)로 팀을 구분한다.</summary>
        public void AddLogEntry(string richText)
        {
            var rect = UiKit.Rect("Toast", _toastRoot);
            UiKit.Panel(rect, UiKit.BarColor);
            var text = UiKit.Text(rect, "Text", uiFont, UiKit.SmallText + 1, TextAnchor.MiddleLeft);
            text.text = richText;
            UiKit.Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            float height = Mathf.Max(26f, UiKit.PreferredHeight(text, ToastWidth - 16f) + 8f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(ToastWidth, height);
            _toasts.Insert(0, (rect, text, Time.unscaledTime));
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
                float alpha = age < ToastSeconds ? 1f : 1f - (age - ToastSeconds) / ToastFadeSeconds;
                var group = _toasts[i].Rect.GetComponent<CanvasGroup>();
                if (group == null) group = _toasts[i].Rect.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alpha;
                group.blocksRaycasts = false;
            }
            if (removed) LayoutToasts();
        }

        // ---------- 정보 패널(오른쪽) ----------

        private void BuildInspector()
        {
            // 패널이 차지할 수 있는 칸: 상단 막대 아래 ~ 턴 종료 버튼 위. 패널은 내용 높이만큼만(FitInspector).
            _inspectorArea = UiKit.Rect("InspectorArea", _safe);
            _inspectorArea.anchorMin = new Vector2(1f, 0f);
            _inspectorArea.anchorMax = new Vector2(1f, 1f);
            _inspectorArea.pivot = new Vector2(1f, 1f);
            _inspectorArea.offsetMin = new Vector2(-UiKit.Gap - UiKit.SidePanelWidth, UiKit.Gap * 2f + UiKit.EndTurnSize.y);
            _inspectorArea.offsetMax = new Vector2(-UiKit.Gap, -UiKit.TopBarHeight - UiKit.Gap);

            _inspector = UiKit.Rect("Inspector", _inspectorArea);
            _inspector.anchorMin = new Vector2(0f, 1f);
            _inspector.anchorMax = new Vector2(1f, 1f);
            _inspector.pivot = new Vector2(0.5f, 1f);
            _inspector.offsetMin = new Vector2(0f, -100f);
            _inspector.offsetMax = Vector2.zero;
            UiKit.Panel(_inspector, UiKit.PanelColor);

            var scrollArea = UiKit.Stretch(UiKit.Rect("Scroll", _inspector), 4f, 4f, 4f, 4f);
            _inspectorContent = UiKit.ScrollList(scrollArea, out _inspectorScroll);

            var close = UiKit.Button(_inspector, "CloseButton", uiFont, "X", UiKit.ButtonColor, () => OnDeselectClicked?.Invoke());
            UiKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(UiKit.TouchSize - 4f, UiKit.TouchSize - 4f));
        }

        /// <summary>패널 높이 = 내용 높이, 단 쓸 수 있는 칸을 넘지 않게(넘으면 스크롤). 캔버스를 그리기 직전마다(에디트 모드 캡처 포함) 맞춘다.</summary>
        private void FitInspector()
        {
            if (_inspector == null || _inspectorArea == null) return;
            float height = Mathf.Min(_inspectorHeight + 8f, _inspectorArea.rect.height);
            if (Mathf.Abs(-_inspector.offsetMin.y - height) > 0.5f)
                _inspector.offsetMin = new Vector2(0f, -height);
        }

        public void HideInspector()
        {
            if (_inspector != null) _inspector.gameObject.SetActive(false);
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
            UiKit.ClearChildren(_inspectorContent);
            HideTooltip();
            _inspector.gameObject.SetActive(true);
            _inspectorScroll.verticalNormalizedPosition = 1f;

            var stripe = UiKit.Rect("AccentFill", _inspectorContent);
            UiKit.Place(stripe, new Vector2(0f, 1f), new Vector2(2f, -InspectorPadding), new Vector2(4f, 24f));
            UiKit.Fill(stripe, accent);

            float y = InspectorPadding;
            var titleText = UiKit.Text(_inspectorContent, "Title", uiFont, UiKit.TitleText, TextAnchor.UpperLeft);
            titleText.text = title;
            float textWidth = UiKit.SidePanelWidth - InspectorPadding * 2f - UiKit.TouchSize - 8f;
            float h = Mathf.Max(26f, UiKit.PreferredHeight(titleText, textWidth));
            UiKit.Row(titleText.rectTransform, y, h, InspectorPadding + 4f, UiKit.TouchSize + 4f);
            y += h + 2f;
            if (!string.IsNullOrEmpty(subtitle)) y = AddParagraph(subtitle, y, UiKit.SmallText, UiKit.MutedText, UiKit.TouchSize + 4f);
            return y + 4f;
        }

        private float AddParagraph(string text, float y, int size, Color color, float rightInset = 0f)
        {
            var t = UiKit.Text(_inspectorContent, "Body", uiFont, size, TextAnchor.UpperLeft, color);
            t.text = text;
            float width = UiKit.SidePanelWidth - 8f - InspectorPadding * 2f - rightInset;
            float h = UiKit.PreferredHeight(t, width);
            UiKit.Row(t.rectTransform, y, h, InspectorPadding, InspectorPadding + rightInset);
            return y + h + 6f;
        }

        private float AddUnitStats(EntityWorld world, int unitId, float y)
        {
            int hp = world.Get<Hp>(unitId).Value;
            int maxHp = world.Get<MaxHp>(unitId).Value;
            float frac = maxHp > 0 ? (float)hp / maxHp : 0f;

            var row = UiKit.Row(UiKit.Rect("HpRow", _inspectorContent), y, StatRowHeight, InspectorPadding, InspectorPadding);
            UiKit.Icon(row, "Hp", "hp", new Vector2(0f, 0.5f), Vector2.zero, 24f);
            var hpText = UiKit.Text(row, "HpText", uiFont, UiKit.BodyText, TextAnchor.MiddleLeft);
            hpText.text = $"{hp}/{maxHp}";
            UiKit.Place(hpText.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(64f, StatRowHeight));
            var barBg = UiKit.Rect("HpBarFill", row);
            barBg.anchorMin = new Vector2(0f, 0.5f);
            barBg.anchorMax = new Vector2(1f, 0.5f);
            barBg.pivot = new Vector2(0f, 0.5f);
            barBg.offsetMin = new Vector2(98f, -4f);
            barBg.offsetMax = new Vector2(0f, 4f);
            UiKit.Fill(barBg, new Color(0.20f, 0.22f, 0.27f));
            var fill = UiKit.Rect("ValueFill", barBg);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            UiKit.Fill(fill, HpColorScale.ForFraction(frac));
            y += StatRowHeight + 2f;

            bool guarding = world.Get<IsGuarding>(unitId).Value;
            var stats = new (string Icon, string Value, Color Color)[]
            {
                ("attack", CombatSystem.EffectiveAttack(world, unitId).ToString(), Color.white),
                ("defense", CombatSystem.EffectiveDefense(world, unitId).ToString(), guarding ? GuardHighlight : Color.white),
                ("move", MovementSystem.EffectiveMoveRange(world, unitId).ToString(), Color.white),
                ("range", world.Get<AttackRange>(unitId).Value.ToString(), Color.white),
            };
            var statRow = UiKit.Row(UiKit.Rect("StatRow", _inspectorContent), y, StatRowHeight, InspectorPadding, InspectorPadding);
            float cell = (UiKit.SidePanelWidth - 8f - InspectorPadding * 2f) / stats.Length;
            for (int i = 0; i < stats.Length; i++)
            {
                UiKit.Icon(statRow, stats[i].Icon, stats[i].Icon, new Vector2(0f, 0.5f), new Vector2(cell * i, 0f), 24f);
                var value = UiKit.Text(statRow, stats[i].Icon + "Text", uiFont, UiKit.BodyText, TextAnchor.MiddleLeft, stats[i].Color);
                value.text = stats[i].Value;
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
                if (row == null) row = UiKit.Row(UiKit.Rect("PassiveRow", _inspectorContent), y, size, InspectorPadding, InspectorPadding);
                var badge = UiKit.Place(UiKit.Rect("Passive_" + def.Icon, row), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(size, size));
                var frame = badge.gameObject.AddComponent<Image>();
                frame.sprite = PixelUISkin.Badge; frame.type = Image.Type.Sliced;
                frame.color = new Color(0.42f, 0.24f, 0.55f, 0.95f);
                UiKit.Icon(badge, "Passive", def.Icon, new Vector2(0.5f, 0.5f), Vector2.zero, size - 8f);
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
                float labelWidth = UiKit.SidePanelWidth - 8f - (InspectorPadding - 4f) * 2f - 18f;
                foreach (var option in options)
                {
                    var button = UiKit.Button(_inspectorContent, "Option", uiFont, string.Empty, option.Primary ? UiKit.PrimaryColor : UiKit.ButtonColor, option.OnClick, UiKit.BodyText - 1);
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
            UiKit.SetContentHeight(_inspectorContent, y);
            _inspectorHeight = y;
            FitInspector();
            PixelUIScaler.Attach(gameObject);
        }

        // ---------- 턴 종료 ----------

        private void BuildEndTurn()
        {
            _endTurnButton = UiKit.Button(_safe, "EndTurnButton", uiFont, LocalizationSystem.T("UI.Battle.EndTurn"), UiKit.PrimaryColor, () => OnEndTurnClicked?.Invoke(), UiKit.TitleText);
            UiKit.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 0f), new Vector2(-UiKit.Gap, UiKit.Gap), UiKit.EndTurnSize);
            AddTooltip(_endTurnButton.gameObject, LocalizationSystem.T("UI.Battle.EndTurnTooltip"));
        }

        // ---------- 설명 말풍선 ----------

        private void BuildTooltip()
        {
            var panel = UiKit.Place(UiKit.Rect("Tooltip", _safe), new Vector2(0f, 0f), new Vector2(UiKit.Gap, UiKit.Gap), new Vector2(360f, 60f));
            UiKit.Panel(panel, UiKit.PanelColor);
            _tooltipText = UiKit.Text(panel, "Text", uiFont, UiKit.SmallText + 1, TextAnchor.MiddleLeft);
            UiKit.Stretch(_tooltipText.rectTransform, 10f, 6f, 10f, 6f);
            panel.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            _tooltip = panel.gameObject;
            _tooltip.SetActive(false);
        }

        /// <summary>마우스를 올리거나(PC) 누르고 있는 동안(터치) 왼쪽 아래 말풍선에 설명을 띄운다.</summary>
        private void AddTooltip(GameObject go, string text)
        {
            var trigger = go.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = go.AddComponent<TooltipTrigger>();
            trigger.Text = text;
            trigger.OnEnter = ShowTooltip;
            trigger.OnExit = HideTooltip;
        }

        private void ShowTooltip(string text)
        {
            _tooltipText.text = text;
            ((RectTransform)_tooltip.transform).sizeDelta = new Vector2(360f, UiKit.PreferredHeight(_tooltipText, 340f) + 14f);
            _tooltip.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }

        // ---------- 대화 상자(메뉴·결과·확인) ----------

        private void BuildDialog()
        {
            var safe = UiKit.CreateCanvas(transform, DialogSortingOrder, out var canvas);
            canvas.name = "DialogCanvas";
            var dim = UiKit.Stretch(UiKit.Rect("DimFill", canvas.transform));
            dim.SetAsFirstSibling();
            UiKit.Fill(dim, UiKit.DimColor, raycast: true);
            _dialogBox = UiKit.Place(UiKit.Rect("Box", safe), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 200f));
            UiKit.Panel(_dialogBox, UiKit.PanelColor);
            _dialog = canvas.gameObject;
            _dialog.SetActive(false);
        }

        /// <summary>가운데 대화 상자: 제목, 본문(없으면 생략), 버튼을 세로로. 버튼을 눌러도 저절로 닫히지 않는다(HideDialog).</summary>
        public void ShowDialog(string title, string body, IReadOnlyList<ActionMenuOption> buttons, Color? titleColor = null)
        {
            UiKit.ClearChildren(_dialogBox);
            const float width = 420f, pad = 18f, buttonHeight = 48f;
            float y = pad;
            var titleText = UiKit.Text(_dialogBox, "Title", uiFont, titleColor.HasValue ? UiKit.HeadlineText : UiKit.TitleText + 4, TextAnchor.MiddleCenter, titleColor ?? Color.white);
            titleText.text = title;
            float h = Mathf.Max(30f, UiKit.PreferredHeight(titleText, width - pad * 2f));
            UiKit.Row(titleText.rectTransform, y, h, pad, pad);
            y += h + 8f;
            if (!string.IsNullOrEmpty(body))
            {
                var bodyText = UiKit.Text(_dialogBox, "Body", uiFont, UiKit.BodyText, TextAnchor.UpperCenter, UiKit.MutedText);
                bodyText.text = body;
                float bh = UiKit.PreferredHeight(bodyText, width - pad * 2f);
                UiKit.Row(bodyText.rectTransform, y, bh, pad, pad);
                y += bh + 12f;
            }
            foreach (var option in buttons)
            {
                var color = option.Primary ? UiKit.PrimaryColor : UiKit.ButtonColor;
                var button = UiKit.Button(_dialogBox, "Button", uiFont, option.Label, color, option.OnClick, UiKit.BodyText + 1);
                UiKit.Row((RectTransform)button.transform, y, buttonHeight, pad, pad);
                UiKit.SetEnabled(button, option.Enabled, color);
                y += buttonHeight + 8f;
            }
            _dialogBox.sizeDelta = new Vector2(width, y - 8f + pad);
            _dialog.SetActive(true);
            PixelUIScaler.Attach(_dialog);
        }

        public void HideDialog()
        {
            if (_dialog != null) _dialog.SetActive(false);
        }
    }
}
