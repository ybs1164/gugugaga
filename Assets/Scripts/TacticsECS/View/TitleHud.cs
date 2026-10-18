using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>시작 화면에서 고를 수 있는 종족 하나(값만).</summary>
    public struct TribeChoice
    {
        public string Name;
        public string Description;
        /// <summary>시작 기술·별·유닛 요약(TribeSystem.Summary).</summary>
        public string Summary;
    }

    /// <summary>시작 화면에서 고를 수 있는 신 하나(값만).</summary>
    public struct GodChoice
    {
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
    }

    /// <summary>
    /// 빠른 한 판(quickPlay)의 시작 화면 두 장(PC·모바일 공용):
    /// 1) 첫 화면 — 이어하기(저장이 있을 때만) · 새로하기 · 언어.
    /// 2) 종족 고르기 — 왼쪽 카드 목록(맨 앞 "무작위"), 오른쪽 고른 종족 설명, 신·맵 크기 순환 버튼과 시작 버튼. 상대 종족·신은 무작위.
    /// 고른 결과는 이벤트로만 알린다 — 판을 만들고 불러오는 일은 BattleController가 한다.
    /// </summary>
    public class TitleHud : MonoBehaviour
    {
        public const int SortingOrder = 40;
        /// <summary>"무작위" 카드의 종족 번호.</summary>
        public const int RandomTribe = -1;
        /// <summary>"무작위" 신 번호.</summary>
        public const int RandomGod = -1;

        private const float CardHeight = 64f;

        public event Action OnContinueClicked;
        public event Action<QuickPlaySetup> OnStartClicked;
        public event Action OnLanguageClicked;

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
        private const float CompactDetailHeight = 290f;
        // 상세 칸 아래: 시작 버튼(프리팹: 아래 12, 높이 52) 위에 신·맵 크기 버튼 한 줄.
        private const float OptionRowBottom = 72f;
        private const float OptionRowHeight = 48f;
        private IReadOnlyList<TribeChoice> _tribes = new List<TribeChoice>();
        private IReadOnlyList<GodChoice> _gods = new List<GodChoice>();
        private IReadOnlyList<string> _mapSizes = new List<string>();
        private int _selected = RandomTribe;
        private int _selectedGod = RandomGod;
        private int _selectedMapSize;
        private Button _godButton;
        private Button _mapSizeButton;
        private bool _canContinue;

        public bool IsOpen => root != null && root.activeSelf;

        /// <param name="mapSizes">맵 크기 버튼에 보일 이름(순환 순서). defaultMapSize는 처음 고른 번호.</param>
        public void Init(IReadOnlyList<TribeChoice> tribes, IReadOnlyList<GodChoice> gods, IReadOnlyList<string> mapSizes, int defaultMapSize)
        {
            _tribes = tribes ?? new List<TribeChoice>();
            _gods = gods ?? new List<GodChoice>();
            _mapSizes = mapSizes ?? new List<string>();
            _selectedMapSize = Mathf.Clamp(defaultMapSize, 0, Mathf.Max(0, _mapSizes.Count - 1));
            titleText.text = LocalizationSystem.T("UI.Title.Name");
            goalText.text = LocalizationSystem.T("UI.Title.Goal");
            UiKit.Label(newGameButton).text = LocalizationSystem.T("UI.Title.NewGame");
            UiKit.Label(backButton).text = LocalizationSystem.T("UI.Title.Back");
            pickTitleText.text = LocalizationSystem.T("UI.Title.PickTribe");
            UiKit.Label(startButton).text = LocalizationSystem.T("UI.Title.Start");
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
            newGameButton.onClick.AddListener(ShowTribeSelect);
            languageButton.onClick.AddListener(() => OnLanguageClicked?.Invoke());
            backButton.onClick.AddListener(() => ShowTitle(_canContinue, null));
            startButton.onClick.AddListener(() => OnStartClicked?.Invoke(new QuickPlaySetup { Tribe = _selected, God = _selectedGod, MapSize = _selectedMapSize }));
            BuildOptionRow();
            Canvas.willRenderCanvases += OnWillRender;
            root.SetActive(false);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>첫 화면. canContinue면 이어하기를 켜고, detail(마지막 저장 요약)을 그 아래에 보인다.</summary>
        public void ShowTitle(bool canContinue, string continueDetail)
        {
            _canContinue = canContinue;
            ShowPage(titlePage);
            UiKit.Label(continueButton).text = string.IsNullOrEmpty(continueDetail) || !canContinue
                ? LocalizationSystem.T("UI.Title.Continue")
                : $"{LocalizationSystem.T("UI.Title.Continue")}\n<size={UiKit.SmallText - 1}>{continueDetail}</size>";
            UiKit.SetEnabled(continueButton, canContinue, UiKit.PrimaryColor);
            UiKit.SetEnabled(newGameButton, true, canContinue ? UiKit.ButtonColor : UiKit.PrimaryColor);
            UiKit.Label(languageButton).text = LocalizationSystem.F("UI.Title.Language", LocalizationSystem.LanguageName(StringTable.Language));
            PixelUIScaler.Attach(gameObject);
        }

        /// <summary>종족 고르기 화면.</summary>
        public void ShowTribeSelect()
        {
            ShowPage(tribePage);
            _compact = null;
            OnWillRender();

            // 카드 격자: 폭이 정해지기 전이라 열 수는 비율(앵커)로 나눈다 — 2열이면 휴대폰에서도 카드가 충분히 넓다.
            UiKit.ClearChildren(tribeContent);
            const int columns = 2;
            int count = _tribes.Count + 1;
            int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                int tribe = i == 0 ? RandomTribe : i - 1;
                string name = tribe == RandomTribe ? LocalizationSystem.T("UI.Title.RandomTribe") : _tribes[tribe].Name;
                var card = UiKit.Button(buttonPrefab, tribeContent, "Tribe" + tribe, name, tribe == _selected ? UiKit.PrimaryColor : UiKit.ButtonColor, () => { _selected = tribe; ShowTribeSelect(); }, UiKit.BodyText + 1);
                var rect = (RectTransform)card.transform;
                int col = i % columns, row = i / columns;
                rect.anchorMin = new Vector2((float)col / columns, 1f);
                rect.anchorMax = new Vector2((float)(col + 1) / columns, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(col == 0 ? 0f : 4f, -(row * (CardHeight + 8f)) - CardHeight);
                rect.offsetMax = new Vector2(col == columns - 1 ? 0f : -4f, -(row * (CardHeight + 8f)));
            }
            UiKit.SetContentHeight(tribeContent, rows * (CardHeight + 8f));

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
            detailBody.text = body;
            RefreshOptionLabels();
            PixelUIScaler.Attach(gameObject);
        }

        /// <summary>상세 칸의 시작 버튼 위에 신·맵 크기 버튼을 반씩 놓고, 설명 글은 그 위까지만 쓴다. 누를 때마다 다음 값으로 넘어간다.</summary>
        private void BuildOptionRow()
        {
            _godButton = UiKit.Button(buttonPrefab, tribeDetail, "GodButton", "", UiKit.ButtonColor, () =>
            {
                _selectedGod = _selectedGod + 1 >= _gods.Count ? RandomGod : _selectedGod + 1;
                ShowTribeSelect();
            }, UiKit.SmallText);
            _mapSizeButton = UiKit.Button(buttonPrefab, tribeDetail, "MapSizeButton", "", UiKit.ButtonColor, () =>
            {
                if (_mapSizes.Count > 0) _selectedMapSize = (_selectedMapSize + 1) % _mapSizes.Count;
                ShowTribeSelect();
            }, UiKit.SmallText);
            PlaceHalf((RectTransform)_godButton.transform, 0f, 0.5f, 12f, -4f);
            PlaceHalf((RectTransform)_mapSizeButton.transform, 0.5f, 1f, 4f, -12f);
            var body = detailBody.rectTransform;
            body.offsetMin = new Vector2(body.offsetMin.x, OptionRowBottom + OptionRowHeight + 12f);
        }

        private static void PlaceHalf(RectTransform rect, float fromX, float toX, float left, float right)
        {
            rect.anchorMin = new Vector2(fromX, 0f);
            rect.anchorMax = new Vector2(toX, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, OptionRowBottom);
            rect.offsetMax = new Vector2(right, OptionRowBottom + OptionRowHeight);
        }

        private void RefreshOptionLabels()
        {
            string god = _selectedGod == RandomGod ? LocalizationSystem.T("UI.Title.RandomGod") : _gods[_selectedGod].Name;
            UiKit.Label(_godButton).text = LocalizationSystem.F("UI.Title.God", god);
            UiKit.Label(_mapSizeButton).text = LocalizationSystem.F("UI.Title.MapSize", _mapSizes.Count > 0 ? _mapSizes[_selectedMapSize] : "");
            UiKit.SetEnabled(_mapSizeButton, _mapSizes.Count > 1, UiKit.ButtonColor);
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        /// <summary>그리기 직전마다: 종족 고르기 화면이면 넓은/좁은 배치에 맞춰 목록·설명 자리를 고른다(화면을 돌려도 따라간다).</summary>
        private void OnWillRender()
        {
            if (tribePage == null || !tribePage.activeInHierarchy) return;
            bool compact = UiKit.IsCompact(safe);
            if (_compact == compact) return;
            _compact = compact;
            UiKit.LayoutDetail(tribeDetail, compact, UiKit.TopBarHeight, CompactDetailHeight);
            UiKit.LayoutMain(tribeList, compact, UiKit.TopBarHeight, CompactDetailHeight);
        }

        private void ShowPage(GameObject page)
        {
            titlePage.SetActive(page == titlePage);
            tribePage.SetActive(page == tribePage);
            root.SetActive(true);
        }
    }
}
