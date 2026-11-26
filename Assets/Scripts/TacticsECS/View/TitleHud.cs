using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>시작 화면에서 고를 수 있는 종족 하나(값만).</summary>
    public struct TribeChoice
    {
        /// <summary>Tribes.csv Id — 아이콘 이름.</summary>
        public string Id;
        public string Name;
        public string Description;
        /// <summary>시작 기술·별·유닛 요약(TribeSystem.Summary).</summary>
        public string Summary;
    }

    /// <summary>시작 화면에서 고를 수 있는 신 하나(값만).</summary>
    public struct GodChoice
    {
        /// <summary>Gods.csv Id — 아이콘 이름(난이도에는 없다).</summary>
        public string Id;
        public string Name;
        public string Description;
    }

    /// <summary>종족 고르기 화면에서 "시작"을 누를 때의 선택(값만). Tribe·God은 표 Index, 무작위면 TitleHud.RandomTribe/RandomGod.</summary>
    public struct QuickPlaySetup
    {
        public int Tribe;
        public int God;
        /// <summary>Init에 넘긴 맵 크기 목록의 번호.</summary>
        public int MapSize;
        /// <summary>상대 AI 난이도(AIDifficulty.csv Index).</summary>
        public int Difficulty;
    }

    /// <summary>
    /// 빠른 한 판(quickPlay)의 시작 화면 두 장(PC·모바일 공용):
    /// 1) 첫 화면 — 이어하기(저장이 있을 때만) · 새로하기 · 언어 · 표 가져오기/기본 표(표 팩, 지금 쓰는 표 이름).
    /// 2) 종족 고르기 — 종족 카드(아이콘 + 이름, 맨 앞 "무작위"), 그 아래 신 · 맵 · 난이도를 따로 고르는 세 줄, 고른 것의 설명과 시작 버튼. 상대 종족·신은 무작위.
    /// 고른 결과는 이벤트로만 알린다 — 판을 만들고 불러오는 일은 BattleController가 한다.
    /// </summary>
    public class TitleHud : MonoBehaviour
    {
        public const int SortingOrder = 40;
        /// <summary>"무작위" 카드의 종족 번호.</summary>
        public const int RandomTribe = -1;
        /// <summary>"무작위" 신 번호.</summary>
        public const int RandomGod = -1;

        private const float CardHeight = UiKit.TouchSize;

        public event Action OnContinueClicked;
        public event Action<QuickPlaySetup> OnStartClicked;
        public event Action OnLanguageClicked;
        public event Action OnImportPackClicked;
        public event Action OnResetPackClicked;

        [Header("화면(프리팹 안)")]
        [Tooltip("캔버스 — 열고 닫는 단위.")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform safe;
        [Tooltip("첫 화면(가운데 열).")]
        [SerializeField] private GameObject titlePage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text goalText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button languageButton;
        [Tooltip("종족 고르기 화면.")]
        [SerializeField] private GameObject tribePage;
        [SerializeField] private Button backButton;
        [SerializeField] private Text pickTitleText;
        [Tooltip("카드 목록(넓은 배치: 왼쪽, 좁은 배치: 위)과 그 스크롤 내용.")]
        [SerializeField] private RectTransform tribeList;
        [SerializeField] private RectTransform tribeContent;
        [Tooltip("고른 종족 설명(넓은 배치: 오른쪽, 좁은 배치: 아래).")]
        [SerializeField] private RectTransform tribeDetail;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailBody;
        [SerializeField] private Button startButton;

        [Header("부품 프리팹")]
        [Tooltip("종족 카드. Prefabs/UI/Elements/Button.prefab.")]
        [SerializeField] private Button buttonPrefab;

        private bool? _compact;
        private const float CompactDetailHeight = 200f;
        // 선택 줄(신 · 맵 · 난이도): 흰 카드 안에 세 줄, 줄마다 왼쪽 이름표 + 고를 것들.
        private const float OptionPad = 8f, OptionLabelWidth = 56f;
        private const float OptionsHeight = OptionPad * 2f + UiKit.TouchSize * 3f + UiKit.ItemGap * 2f;
        private IReadOnlyList<TribeChoice> _tribes = new List<TribeChoice>();
        private IReadOnlyList<GodChoice> _gods = new List<GodChoice>();
        private IReadOnlyList<string> _mapSizes = new List<string>();
        private int _selected = RandomTribe;
        private int _selectedGod = RandomGod;
        private int _selectedMapSize;
        private IReadOnlyList<GodChoice> _difficulties = new List<GodChoice>();
        private int _selectedDifficulty;
        private RectTransform _options;
        private Button _importPackButton;
        private Button _resetPackButton;
        // 표 팩 줄: 언어 버튼 바로 아래(프리팹의 첫 화면 열에 남은 자리).
        private const float PackRowGap = UiKit.ItemGap;
        private bool _canContinue;

        public bool IsOpen => root != null && root.activeSelf;

        /// <param name="mapSizes">맵 크기 이름(고르는 순서). defaultMapSize는 처음 고른 번호.</param>
        /// <param name="difficulties">AI 난이도(이름·설명, AIDifficulty.csv 순서). defaultDifficulty는 처음 고른 번호.</param>
        public void Init(IReadOnlyList<TribeChoice> tribes, IReadOnlyList<GodChoice> gods, IReadOnlyList<string> mapSizes, int defaultMapSize,
            IReadOnlyList<GodChoice> difficulties, int defaultDifficulty)
        {
            _difficulties = difficulties ?? new List<GodChoice>();
            _selectedDifficulty = Mathf.Clamp(defaultDifficulty, 0, Mathf.Max(0, _difficulties.Count - 1));
            _tribes = tribes ?? new List<TribeChoice>();
            _gods = gods ?? new List<GodChoice>();
            _mapSizes = mapSizes ?? new List<string>();
            _selectedMapSize = Mathf.Clamp(defaultMapSize, 0, Mathf.Max(0, _mapSizes.Count - 1));
            UiKit.ApplyTheme(transform);
            titleText.text = LocalizationSystem.T("UI.Title.Name");
            goalText.text = LocalizationSystem.T("UI.Title.Goal");
            UiKit.Label(newGameButton).text = LocalizationSystem.T("UI.Title.NewGame");
            UiKit.Label(backButton).text = LocalizationSystem.T("UI.Title.Back");
            pickTitleText.text = LocalizationSystem.T("UI.Title.PickTribe");
            UiKit.Label(startButton).text = LocalizationSystem.T("UI.Title.Start");
            ((Image)startButton.targetGraphic).color = UiKit.PrimaryColor; // 이 화면에서 지금 눌러야 하는 것
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
            newGameButton.onClick.AddListener(ShowTribeSelect);
            languageButton.onClick.AddListener(() => OnLanguageClicked?.Invoke());
            backButton.onClick.AddListener(() => ShowTitle(_canContinue, null));
            startButton.onClick.AddListener(() => OnStartClicked?.Invoke(new QuickPlaySetup { Tribe = _selected, God = _selectedGod, MapSize = _selectedMapSize, Difficulty = _difficulties.Count > 0 ? _selectedDifficulty : -1 }));
            var options = new GameObject("Options", typeof(RectTransform), typeof(Image));
            options.transform.SetParent(tribePage.transform, false);
            options.GetComponent<Image>().color = UiKit.PanelColor;
            _options = (RectTransform)options.transform;
            BuildPackRow();
            Canvas.willRenderCanvases += OnWillRender;
            root.SetActive(false);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>버튼 글자를 왼쪽 이름 + 오른쪽 값(작은 글자)으로 나눈다. 값이 없으면 이름만 가운데.</summary>
        private static void SplitLabel(Button button, string name, string value, Color valueColor)
        {
            var label = UiKit.Label(button);
            bool split = !string.IsNullOrEmpty(value);
            label.alignment = split ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            UiKit.Stretch(label.rectTransform, 12f, 0f, 12f, 0f);
            label.text = name;
            var right = button.transform.Find("Value");
            if (right == null)
            {
                right = Instantiate(label.gameObject, button.transform, false).transform;
                right.name = "Value";
            }
            var text = right.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleRight;
            text.fontSize = UiKit.SmallText;
            text.text = value ?? string.Empty;
            text.color = valueColor;
            UiKit.Stretch(text.rectTransform, 12f, 0f, 12f, 0f);
            right.gameObject.SetActive(split);
        }

        /// <summary>첫 화면. canContinue면 이어하기를 켜고, detail(마지막 저장 요약)을 그 오른쪽에 보인다.</summary>
        public void ShowTitle(bool canContinue, string continueDetail)
        {
            _canContinue = canContinue;
            ShowPage(titlePage);
            UiKit.SetEnabled(continueButton, canContinue, UiKit.PrimaryColor);
            UiKit.SetEnabled(newGameButton, true, canContinue ? UiKit.ButtonColor : UiKit.PrimaryColor);
            SplitLabel(continueButton, LocalizationSystem.T("UI.Title.Continue"), canContinue ? continueDetail : null, UiKit.TextColor);
            SplitLabel(languageButton, LocalizationSystem.T("UI.Title.LanguageLabel"), LocalizationSystem.LanguageName(StringTable.Language), UiKit.MutedText);
            PixelUIScaler.Attach(gameObject);
        }

        /// <summary>종족 고르기 화면.</summary>
        public void ShowTribeSelect()
        {
            ShowPage(tribePage);
            _compact = null;
            OnWillRender();
            bool compact = _compact ?? false;

            // 카드 격자: 폭이 정해지기 전이라 열 수는 비율(앵커)로 나눈다. 카드 = 종족 아이콘 + 이름.
            UiKit.ClearChildren(tribeContent);
            int columns = compact ? 2 : 4;
            int count = _tribes.Count + 1;
            int rows = (count + columns - 1) / columns;
            float step = CardHeight + UiKit.ItemGap;
            for (int i = 0; i < count; i++)
            {
                int tribe = i == 0 ? RandomTribe : i - 1;
                string name = tribe == RandomTribe ? LocalizationSystem.T("UI.Title.RandomTribe") : _tribes[tribe].Name;
                var card = UiKit.Button(buttonPrefab, tribeContent, "Tribe" + tribe, name, tribe == _selected ? UiKit.PrimaryColor : UiKit.PanelColor, () => { _selected = tribe; ShowTribeSelect(); });
                var label = UiKit.Label(card);
                label.alignment = TextAnchor.MiddleLeft;
                UiKit.Stretch(label.rectTransform, 44f, 0f, 8f, 0f);
                Icon(card.transform, tribe == RandomTribe ? HudArt.Tribe("random") : HudArt.Tribe(_tribes[tribe].Id), new Vector2(0f, 0.5f), new Vector2(8f, 0f));
                var rect = (RectTransform)card.transform;
                int col = i % columns, row = i / columns;
                rect.anchorMin = new Vector2((float)col / columns, 1f);
                rect.anchorMax = new Vector2((float)(col + 1) / columns, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(col == 0 ? 0f : UiKit.ItemGap / 2f, -(row * step) - CardHeight);
                rect.offsetMax = new Vector2(col == columns - 1 ? 0f : -UiKit.ItemGap / 2f, -(row * step));
            }
            UiKit.SetContentHeight(tribeContent, rows * step);

            string body;
            if (_selected == RandomTribe)
            {
                detailName.text = LocalizationSystem.T("UI.Title.RandomTribe");
                body = LocalizationSystem.T("UI.Title.RandomTribeDesc");
            }
            else
            {
                var t = _tribes[_selected];
                detailName.text = t.Name;
                body = string.IsNullOrEmpty(t.Description) ? t.Summary : t.Description + "\n\n" + t.Summary;
            }
            if (_selectedGod != RandomGod)
                body += "\n\n" + LocalizationSystem.F("UI.Title.GodDetail", _gods[_selectedGod].Name, _gods[_selectedGod].Description);
            if (_difficulties.Count > 0)
                body += "\n\n" + LocalizationSystem.F("UI.Title.DifficultyDetail", _difficulties[_selectedDifficulty].Name, _difficulties[_selectedDifficulty].Description);
            detailBody.text = body;
            BuildOptions(compact);
            PixelUIScaler.Attach(gameObject);
        }

        private static Image Icon(Transform parent, Sprite sprite, Vector2 anchor, Vector2 position) =>
            HudArt.PlaceIcon(parent, sprite, anchor, position, UiKit.TextColor);

        /// <summary>신 · 맵 · 난이도를 따로 고르는 세 줄. 신은 아이콘(맨 앞 무작위), 맵·난이도는 이름. 고른 것은 포인트 색.</summary>
        private void BuildOptions(bool compact)
        {
            UiKit.ClearChildren(_options);
            float y = OptionPad;
            y = OptionRow(y, LocalizationSystem.T("UI.Title.GodLabel"), _gods.Count + 1, i => i - 1 == _selectedGod,
                i => { _selectedGod = i - 1; ShowTribeSelect(); }, i => null, i => i == 0 ? HudArt.God("random") : HudArt.God(_gods[i - 1].Id));
            y = OptionRow(y, LocalizationSystem.T("UI.Title.MapLabel"), _mapSizes.Count, i => i == _selectedMapSize,
                i => { _selectedMapSize = i; ShowTribeSelect(); }, i => compact ? Short(_mapSizes[i]) : _mapSizes[i], null);
            OptionRow(y, LocalizationSystem.T("UI.Title.DifficultyLabel"), _difficulties.Count, i => i == _selectedDifficulty,
                i => { _selectedDifficulty = i; ShowTribeSelect(); }, i => _difficulties[i].Name, null);
        }

        /// <summary>좁은 칸에 넣을 맵 이름: 마지막 낱말(크기)만.</summary>
        private static string Short(string name)
        {
            int cut = name.LastIndexOf(' ');
            return cut >= 0 ? name.Substring(cut + 1) : name;
        }

        private float OptionRow(float y, string title, int count, Func<int, bool> selected, Action<int> pick, Func<int, string> text, Func<int, Sprite> icon)
        {
            var label = Instantiate(UiKit.Label(startButton), _options, false);
            label.name = "Label";
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = UiKit.SmallText + 1;
            label.color = UiKit.MutedText;
            label.text = title;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(OptionLabelWidth, UiKit.TouchSize);
            label.rectTransform.anchoredPosition = new Vector2(OptionPad, -y);
            float left = OptionPad + OptionLabelWidth, right = OptionPad;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var button = UiKit.Button(buttonPrefab, _options, title + i, text(i) ?? string.Empty, selected(i) ? UiKit.PrimaryColor : UiKit.ButtonColor, () => pick(index), UiKit.SmallText + 1);
                var rect = (RectTransform)button.transform;
                // 이름표 오른쪽 폭을 같은 크기로 나눈다(앵커 비율 + 그 비율만큼의 고정 여백).
                float from = (float)i / count, to = (float)(i + 1) / count;
                rect.anchorMin = new Vector2(from, 1f);
                rect.anchorMax = new Vector2(to, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(left * (1f - from) - right * from + (i == 0 ? 0f : UiKit.ItemGap / 2f), -y - UiKit.TouchSize);
                rect.offsetMax = new Vector2(left * (1f - to) - right * to - (i == count - 1 ? 0f : UiKit.ItemGap / 2f), -y);
                if (icon != null) Icon(button.transform, icon(i), new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            return y + UiKit.TouchSize + UiKit.ItemGap;
        }

        /// <summary>언어 버튼 아래 한 줄에 "표 가져오기"(왼쪽, 넓게)와 "기본 표"(오른쪽)를 놓는다. 글은 SetPack이 채운다.</summary>
        private void BuildPackRow()
        {
            var language = (RectTransform)languageButton.transform;
            _importPackButton = UiKit.Button(buttonPrefab, language.parent, "ImportPackButton", "", UiKit.ButtonColor, () => OnImportPackClicked?.Invoke(), UiKit.BodyText);
            _resetPackButton = UiKit.Button(buttonPrefab, language.parent, "ResetPackButton", "", UiKit.ButtonColor, () => OnResetPackClicked?.Invoke(), UiKit.SmallText);
            float top = language.anchoredPosition.y - language.sizeDelta.y - PackRowGap;
            PlaceBelow((RectTransform)_importPackButton.transform, language, 0f, 0.72f, 0f, -UiKit.ItemGap / 2f, top);
            PlaceBelow((RectTransform)_resetPackButton.transform, language, 0.72f, 1f, UiKit.ItemGap / 2f, 0f, top);
        }

        private static void PlaceBelow(RectTransform rect, RectTransform like, float fromX, float toX, float left, float right, float top)
        {
            rect.anchorMin = new Vector2(fromX, 1f);
            rect.anchorMax = new Vector2(toX, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, top - like.sizeDelta.y);
            rect.offsetMax = new Vector2(right, top);
        }

        /// <summary>표 팩 줄의 글: 지금 쓰는 표(packLabel)와 마지막 가져오기 결과(message, 없으면 null). 팩을 쓰는 중일 때만 "기본 표"가 켜진다.</summary>
        public void SetPack(string packLabel, string message, bool usingPack)
        {
            UiKit.SetEnabled(_importPackButton, true, UiKit.ButtonColor);
            UiKit.SetEnabled(_resetPackButton, usingPack, UiKit.ButtonColor);
            SplitLabel(_importPackButton, LocalizationSystem.T("UI.Title.ImportPack"), string.IsNullOrEmpty(message) ? packLabel : message, UiKit.MutedText);
            UiKit.Label(_resetPackButton).text = LocalizationSystem.T("UI.Title.DefaultTables");
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        /// <summary>그리기 직전마다: 종족 고르기 화면이면 넓은/좁은 배치에 맞춰 목록·선택 줄·설명 자리를 고른다(화면을 돌려도 따라간다).
        /// 넓은 배치: 왼쪽 위 목록, 왼쪽 아래 선택 줄, 오른쪽 설명. 좁은 배치: 위에서부터 목록 · 선택 줄 · 설명.</summary>
        private void OnWillRender()
        {
            if (tribePage == null || !tribePage.activeInHierarchy || _options == null) return;
            bool compact = UiKit.IsCompact(safe);
            if (_compact == compact) return;
            bool rebuild = _compact.HasValue;
            _compact = compact;
            UiKit.LayoutDetail(tribeDetail, compact, UiKit.TopBarHeight, CompactDetailHeight);
            UiKit.LayoutMain(tribeList, compact, UiKit.TopBarHeight, CompactDetailHeight);
            tribeList.offsetMin += new Vector2(0f, OptionsHeight + UiKit.Gap);
            float bottom = compact ? UiKit.Gap * 2f + CompactDetailHeight : UiKit.Gap;
            _options.anchorMin = Vector2.zero; _options.anchorMax = new Vector2(1f, 0f); _options.pivot = new Vector2(0.5f, 0f);
            _options.offsetMin = new Vector2(UiKit.Gap, bottom);
            _options.offsetMax = new Vector2(compact ? -UiKit.Gap : -UiKit.SidePanelWidth - UiKit.Gap * 2f, bottom + OptionsHeight);
            if (rebuild) ShowTribeSelect(); // 열 수·이름 길이가 배치에 따라 달라진다
        }

        private void ShowPage(GameObject page)
        {
            titlePage.SetActive(page == titlePage);
            tribePage.SetActive(page == tribePage);
            root.SetActive(true);
        }
    }
}
