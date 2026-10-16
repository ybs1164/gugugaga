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

    /// <summary>
    /// 빠른 한 판(quickPlay)의 시작 화면 두 장(PC·모바일 공용):
    /// 1) 첫 화면 — 이어하기(저장이 있을 때만) · 새로하기 · 언어.
    /// 2) 종족 고르기 — 왼쪽 카드 목록(맨 앞 "무작위"), 오른쪽 고른 종족 설명과 시작 버튼. 상대 종족은 무작위.
    /// 고른 결과는 이벤트로만 알린다 — 판을 만들고 불러오는 일은 BattleController가 한다.
    /// </summary>
    public class TitleHud : MonoBehaviour
    {
        public const int SortingOrder = 40;
        /// <summary>"무작위" 카드의 종족 번호.</summary>
        public const int RandomTribe = -1;

        private static readonly Color BackgroundColor = new Color(0.09f, 0.12f, 0.16f, 1f);
        private const float CardHeight = 64f;

        public event Action OnContinueClicked;
        public event Action<int> OnStartClicked;
        public event Action OnLanguageClicked;

        private Font _font;
        private GameObject _root;
        private RectTransform _safe;
        private RectTransform _page;
        /// <summary>종족 고르기 화면의 목록·설명(넓은 배치: 왼쪽·오른쪽 열, 좁은 배치: 위·아래). 첫 화면이면 null.</summary>
        private RectTransform _tribeList, _tribeDetail;
        private bool? _compact;
        private const float CompactDetailHeight = 230f;
        private IReadOnlyList<TribeChoice> _tribes = new List<TribeChoice>();
        private int _selected = RandomTribe;
        private bool _canContinue;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Init(Font font, IReadOnlyList<TribeChoice> tribes)
        {
            _font = font;
            _tribes = tribes ?? new List<TribeChoice>();
            _safe = UiKit.CreateCanvas(transform, SortingOrder, out var canvas);
            _root = canvas.gameObject;
            var background = UiKit.Stretch(UiKit.Rect("BackgroundFill", canvas.transform));
            background.SetAsFirstSibling();
            UiKit.Fill(background, BackgroundColor, raycast: true);
            _page = UiKit.Stretch(UiKit.Rect("Page", _safe));
            Canvas.willRenderCanvases += OnWillRender;
            _root.SetActive(false);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>첫 화면. canContinue면 이어하기를 켜고, detail(마지막 저장 요약)을 그 아래에 보인다.</summary>
        public void ShowTitle(bool canContinue, string continueDetail)
        {
            _canContinue = canContinue;
            BeginPage();
            var column = UiKit.Place(UiKit.Rect("Column", _page), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 360f));

            var title = UiKit.Text(column, "Title", _font, UiKit.HeadlineText + 14, TextAnchor.MiddleCenter);
            title.text = LocalizationSystem.T("UI.Title.Name");
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            UiKit.Row(title.rectTransform, 0f, 64f);
            var goal = UiKit.Text(column, "Goal", _font, UiKit.SmallText + 1, TextAnchor.UpperCenter, UiKit.MutedText);
            goal.text = LocalizationSystem.T("UI.Title.Goal");
            UiKit.Row(goal.rectTransform, 68f, 40f);

            float y = 120f;
            var cont = UiKit.Button(column, "ContinueButton", _font, string.IsNullOrEmpty(continueDetail) || !canContinue
                    ? LocalizationSystem.T("UI.Title.Continue")
                    : $"{LocalizationSystem.T("UI.Title.Continue")}\n<size={UiKit.SmallText - 1}>{continueDetail}</size>",
                UiKit.PrimaryColor, () => OnContinueClicked?.Invoke(), UiKit.BodyText + 2);
            UiKit.Row((RectTransform)cont.transform, y, 60f);
            UiKit.SetEnabled(cont, canContinue, UiKit.PrimaryColor);
            y += 68f;
            var fresh = UiKit.Button(column, "NewGameButton", _font, LocalizationSystem.T("UI.Title.NewGame"), canContinue ? UiKit.ButtonColor : UiKit.PrimaryColor, ShowTribeSelect, UiKit.BodyText + 2);
            UiKit.Row((RectTransform)fresh.transform, y, 56f);
            y += 64f;
            var language = UiKit.Button(column, "LanguageButton", _font, LocalizationSystem.F("UI.Title.Language", LocalizationSystem.LanguageName(StringTable.Language)), UiKit.ButtonColor, () => OnLanguageClicked?.Invoke());
            UiKit.Row((RectTransform)language.transform, y, UiKit.TouchSize);
            EndPage();
        }

        /// <summary>종족 고르기 화면.</summary>
        public void ShowTribeSelect()
        {
            BeginPage();
            var header = UiKit.Row(UiKit.Rect("Header", _page), 0f, UiKit.TopBarHeight);
            var back = UiKit.Button(header, "BackButton", _font, LocalizationSystem.T("UI.Title.Back"), UiKit.ButtonColor, () => ShowTitle(_canContinue, null));
            UiKit.Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(UiKit.Gap, 0f), new Vector2(96f, UiKit.TouchSize));
            var title = UiKit.Text(header, "Title", _font, UiKit.TitleText + 2, TextAnchor.MiddleLeft);
            title.text = LocalizationSystem.T("UI.Title.PickTribe");
            UiKit.Stretch(title.rectTransform, 96f + UiKit.Gap * 3f, 0f, UiKit.Gap, 0f);

            var listArea = UiKit.Rect("Tribes", _page);
            var content = UiKit.ScrollList(listArea, out _);

            var detail = UiKit.Rect("Detail", _page);
            _tribeList = listArea;
            _tribeDetail = detail;
            _compact = null;
            OnWillRender();
            UiKit.Panel(detail, UiKit.PanelColor);

            // 카드 격자: 폭이 정해지기 전이라 열 수는 비율(앵커)로 나눈다 — 2열이면 휴대폰에서도 카드가 충분히 넓다.
            const int columns = 2;
            int count = _tribes.Count + 1;
            int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                int tribe = i == 0 ? RandomTribe : i - 1;
                string name = tribe == RandomTribe ? LocalizationSystem.T("UI.Title.RandomTribe") : _tribes[tribe].Name;
                var card = UiKit.Button(content, "Tribe" + tribe, _font, name, tribe == _selected ? UiKit.PrimaryColor : UiKit.ButtonColor, () => { _selected = tribe; ShowTribeSelect(); }, UiKit.BodyText + 1);
                var rect = (RectTransform)card.transform;
                int col = i % columns, row = i / columns;
                rect.anchorMin = new Vector2((float)col / columns, 1f);
                rect.anchorMax = new Vector2((float)(col + 1) / columns, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(col == 0 ? 0f : 4f, -(row * (CardHeight + 8f)) - CardHeight);
                rect.offsetMax = new Vector2(col == columns - 1 ? 0f : -4f, -(row * (CardHeight + 8f)));
            }
            UiKit.SetContentHeight(content, rows * (CardHeight + 8f));

            var nameText = UiKit.Text(detail, "Name", _font, UiKit.TitleText + 2, TextAnchor.UpperLeft);
            UiKit.Row(nameText.rectTransform, 12f, 30f, 14f, 14f);
            var body = UiKit.Text(detail, "Body", _font, UiKit.SmallText + 1, TextAnchor.UpperLeft, UiKit.MutedText);
            body.rectTransform.anchorMin = Vector2.zero;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.offsetMin = new Vector2(14f, 76f);
            body.rectTransform.offsetMax = new Vector2(-14f, -48f);
            body.verticalOverflow = VerticalWrapMode.Truncate;
            if (_selected == RandomTribe)
            {
                nameText.text = LocalizationSystem.T("UI.Title.RandomTribe");
                body.text = LocalizationSystem.T("UI.Title.RandomTribeDesc");
            }
            else
            {
                var t = _tribes[_selected];
                nameText.text = t.Name;
                body.text = string.IsNullOrEmpty(t.Description) ? t.Summary : t.Description + "\n\n" + t.Summary;
            }
            var start = UiKit.Button(detail, "StartButton", _font, LocalizationSystem.T("UI.Title.Start"), UiKit.PrimaryColor, () => OnStartClicked?.Invoke(_selected), UiKit.TitleText);
            var startRect = (RectTransform)start.transform;
            startRect.anchorMin = new Vector2(0f, 0f);
            startRect.anchorMax = new Vector2(1f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.offsetMin = new Vector2(12f, 12f);
            startRect.offsetMax = new Vector2(-12f, 12f + 52f);
            EndPage();
        }

        private void OnDestroy() => Canvas.willRenderCanvases -= OnWillRender;

        /// <summary>그리기 직전마다: 종족 고르기 화면이면 넓은/좁은 배치에 맞춰 목록·설명 자리를 고른다(화면을 돌려도 따라간다).</summary>
        private void OnWillRender()
        {
            if (_tribeList == null || _tribeDetail == null) return;
            bool compact = UiKit.IsCompact(_safe);
            if (_compact == compact) return;
            _compact = compact;
            UiKit.LayoutDetail(_tribeDetail, compact, UiKit.TopBarHeight, CompactDetailHeight);
            UiKit.LayoutMain(_tribeList, compact, UiKit.TopBarHeight, CompactDetailHeight);
        }

        private void BeginPage()
        {
            _tribeList = _tribeDetail = null;
            UiKit.ClearChildren(_page);
            _root.SetActive(true);
        }

        private void EndPage() => PixelUISkin.Apply(gameObject);
    }
}
