using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>ActionMenuHud에 넘기는 버튼 하나(표시 문구 + 활성 여부 + 클릭 시 할 일).</summary>
    public struct ActionMenuOption
    {
        public string Label;
        public string Detail;
        public bool Enabled;
        public Action OnClick;
    }

    /// <summary>
    /// 화면 오른쪽 가운데에 뜨는 "상황별 메뉴" — 도시(유닛 훈련/레벨업 보상), 타일(채집/건설), 유닛(점령/유적
    /// 탐험/해산)처럼 무엇을 선택했느냐에 따라 버튼 개수와 내용이 매번 바뀌는 목록을 보여준다. 다른 HUD와 달리
    /// 프리팹 없이 코드로 계층을 만든다 — 내용 전체가 가변 목록이라 BattleHud의 유닛 로스터/행동 로그처럼 "행
    /// 수가 계속 바뀌는 목록은 코드로 그때그때 만든다"는 기존 예외에 해당하고, 고정 뼈대라고 할 부분이 제목/본문
    /// 두 줄뿐이라 별도 프리팹을 두는 이득이 없다. 값은 판단하지 않고 BattleController가 넘긴 옵션을 그리기만 한다.
    /// </summary>
    public class ActionMenuHud : MonoBehaviour
    {
        private const float Width = 280f;
        private const float RowHeight = 34f;
        private const float RowGap = 4f;
        private const float Padding = 10f;
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.88f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.36f, 0.62f, 1f);
        private static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.28f, 1f);

        private Font _font;
        private RectTransform _panel;
        private Text _title;
        private Text _body;
        private readonly List<GameObject> _rows = new List<GameObject>();

        public bool IsVisible => _panel != null && _panel.gameObject.activeSelf;

        public void Init(Font font)
        {
            _font = font;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = NewRect("Panel", canvasGo.transform);
            _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 0.5f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.anchoredPosition = new Vector2(-12f, -20f);
            _panel.gameObject.AddComponent<Image>().color = PanelColor;

            _title = NewText("Title", _panel, 17, TextAnchor.UpperLeft);
            _body = NewText("Body", _panel, 13, TextAnchor.UpperLeft);
            _body.color = new Color(0.85f, 0.87f, 0.9f);
            _panel.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        public void Show(string title, string body, IReadOnlyList<ActionMenuOption> options)
        {
            foreach (var r in _rows) Destroy(r);
            _rows.Clear();

            float y = -Padding;
            _title.text = title;
            Place(_title.rectTransform, ref y, 24f);
            _body.text = body ?? string.Empty;
            int bodyLines = string.IsNullOrEmpty(body) ? 0 : body.Split('\n').Length;
            Place(_body.rectTransform, ref y, bodyLines * 17f);

            if (options != null)
            {
                foreach (var option in options)
                {
                    var row = NewRect("Option", _panel);
                    Place(row, ref y, RowHeight);
                    var img = row.gameObject.AddComponent<Image>();
                    img.color = option.Enabled ? ButtonColor : DisabledColor;
                    var button = row.gameObject.AddComponent<Button>();
                    button.targetGraphic = img;
                    button.interactable = option.Enabled;
                    var onClick = option.OnClick;
                    if (onClick != null) button.onClick.AddListener(() => onClick());

                    var label = NewText("Label", row, 13, TextAnchor.MiddleLeft);
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(8f, 0f);
                    label.rectTransform.offsetMax = new Vector2(-6f, 0f);
                    label.text = string.IsNullOrEmpty(option.Detail) ? option.Label
                        : $"{option.Label}\n<size=10><color=#C8CCD4>{option.Detail}</color></size>";
                    label.color = option.Enabled ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                    _rows.Add(row.gameObject);
                }
            }

            _panel.sizeDelta = new Vector2(Width, -y + Padding - RowGap);
            _panel.gameObject.SetActive(true);
        }

        private static void Place(RectTransform rect, ref float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-Padding * 2f, height);
            rect.anchoredPosition = new Vector2(0f, y);
            y -= height + RowGap;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
