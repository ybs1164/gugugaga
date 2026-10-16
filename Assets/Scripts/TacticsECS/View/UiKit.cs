using System;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 코드로 만드는 화면(BattleHud, TechTreeHud, FaithPunchHud, TitleHud)이 함께 쓰는 색·크기·부품.
    /// 크기는 UI 단위(<see cref="UiScale"/>가 화면 픽셀로 바꾼다)다. 배치는 두 가지: 넓은 배치(PC, 휴대폰 가로 약 900x400 이상 —
    /// 정보 패널이 오른쪽 열)와 좁은 배치(폭 CompactWidth 미만, 휴대폰 세로 약 430x960 — 상단 막대 두 줄, 정보 패널은 아래 시트).
    /// 각 화면은 캔버스를 그리기 직전(Canvas.willRenderCanvases)에 지금 폭을 보고 배치를 다시 고른다 — 화면을 돌려도 따라간다.
    /// </summary>
    public static class UiKit
    {
        // 프로젝트가 선형 색 공간이라 어두운 반투명 상자는 알파 값보다 훨씬 비쳐 보인다 — 글을 읽는 패널은 불투명하게.
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        public static readonly Color BarColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.20f, 0.22f, 0.27f, 1f);
        public static readonly Color PrimaryColor = new Color(0.22f, 0.40f, 0.72f, 1f);
        public static readonly Color DisabledColor = new Color(0.22f, 0.22f, 0.25f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color MutedText = new Color(0.80f, 0.82f, 0.87f);
        public static readonly Color DisabledText = new Color(0.62f, 0.62f, 0.66f);
        public static readonly Color StarColor = new Color(0.98f, 0.84f, 0.32f);
        public static readonly Color FaithColor = new Color(0.93f, 0.80f, 0.35f);

        /// <summary>화면 가장자리와 부품 사이 여백.</summary>
        public const float Gap = 8f;
        /// <summary>손가락으로 누를 수 있는 최소 높이(약 7mm, UiScale 참고).</summary>
        public const float TouchSize = 44f;
        public const float TopBarHeight = 48f;
        public const float SidePanelWidth = 300f;
        public static readonly Vector2 EndTurnSize = new Vector2(150f, 56f);

        /// <summary>안전 영역 폭이 이보다 좁으면 좁은 배치.</summary>
        public const float CompactWidth = 700f;
        /// <summary>좁은 배치에서 아래 시트가 차지할 수 있는 화면 높이 비율.</summary>
        public const float CompactSheetMaxFraction = 0.5f;

        public static bool IsCompact(RectTransform safe) => safe != null && safe.rect.width > 1f && safe.rect.width < CompactWidth;

        /// <summary>상단 막대 높이 — 좁은 배치는 두 줄.</summary>
        public static float TopBarHeightFor(bool compact) => compact ? TopBarHeight * 2f : TopBarHeight;

        /// <summary>정보 패널·신앙 펀치가 쓰는 칸. 넓은 배치: 오른쪽 열(상단 막대 아래 ~ 턴 종료 위). 좁은 배치: 아래(턴 종료 위)부터 화면 절반까지, 폭 전체.</summary>
        public static void LayoutSheetArea(RectTransform area, bool compact)
        {
            float bottom = Gap * 2f + EndTurnSize.y;
            if (compact)
            {
                area.anchorMin = Vector2.zero;
                area.anchorMax = new Vector2(1f, CompactSheetMaxFraction);
                area.pivot = new Vector2(0.5f, 0f);
                area.offsetMin = new Vector2(Gap, bottom);
                area.offsetMax = new Vector2(-Gap, 0f);
            }
            else
            {
                area.anchorMin = new Vector2(1f, 0f);
                area.anchorMax = new Vector2(1f, 1f);
                area.pivot = new Vector2(1f, 1f);
                area.offsetMin = new Vector2(-Gap - SidePanelWidth, bottom);
                area.offsetMax = new Vector2(-Gap, -TopBarHeight - Gap);
            }
        }

        /// <summary>칸 안의 패널: 넓은 배치는 위에 매달고 좁은 배치는 아래에 앉힌다. 높이 = 내용 높이, 단 칸을 넘지 않게(넘으면 스크롤).</summary>
        public static void FitSheet(RectTransform area, RectTransform panel, float contentHeight, bool compact)
        {
            float a = compact ? 0f : 1f;
            panel.anchorMin = new Vector2(0f, a);
            panel.anchorMax = new Vector2(1f, a);
            panel.pivot = new Vector2(0.5f, a);
            panel.anchoredPosition = Vector2.zero;
            float height = area.rect.height > 1f ? Mathf.Min(contentHeight, area.rect.height) : contentHeight;
            if (Mathf.Abs(panel.sizeDelta.y - height) > 0.5f || Mathf.Abs(panel.sizeDelta.x) > 0.5f) panel.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>패널 글자 줄바꿈을 잴 폭(칸 폭을 아직 모르면 넓은 배치의 열 폭).</summary>
        public static float SheetWidth(RectTransform area) => area != null && area.rect.width > 1f ? area.rect.width : SidePanelWidth;

        /// <summary>오른쪽 열(넓은 배치) 또는 아래 띠(좁은 배치)에 붙는 상세 패널(기술 트리·종족 고르기). top은 위쪽 머리줄 높이.</summary>
        public static void LayoutDetail(RectTransform detail, bool compact, float top, float compactHeight)
        {
            if (compact)
            {
                detail.anchorMin = Vector2.zero;
                detail.anchorMax = new Vector2(1f, 0f);
                detail.pivot = new Vector2(0.5f, 0f);
                detail.offsetMin = new Vector2(Gap, Gap);
                detail.offsetMax = new Vector2(-Gap, Gap + compactHeight);
            }
            else
            {
                detail.anchorMin = new Vector2(1f, 0f);
                detail.anchorMax = new Vector2(1f, 1f);
                detail.pivot = new Vector2(1f, 0.5f);
                detail.offsetMin = new Vector2(-Gap - SidePanelWidth, Gap);
                detail.offsetMax = new Vector2(-Gap, -top - Gap);
            }
        }

        /// <summary>상세 패널 옆(넓은 배치) 또는 위(좁은 배치)의 나머지 칸(나무·목록).</summary>
        public static void LayoutMain(RectTransform main, bool compact, float top, float compactDetailHeight) =>
            Stretch(main, Gap, compact ? Gap * 2f + compactDetailHeight : Gap, compact ? Gap : SidePanelWidth + Gap * 2f, top + Gap);

        public const int SmallText = 13;
        public const int BodyText = 16;
        public const int TitleText = 20;
        public const int HeadlineText = 34;

        /// <summary>오버레이 캔버스 + 안전 영역 루트("Safe", PixelUIScaler가 맞춘다). 반환값이 그 안전 영역이다.</summary>
        public static RectTransform CreateCanvas(Transform parent, int sortingOrder, out Canvas canvas)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            var safe = Rect(PixelUIScaler.SafeAreaName, canvasGo.transform);
            Stretch(safe);
            return safe;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>부모를 꽉 채운다(left/bottom/right/top 안쪽 여백).</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>한 점(anchor)에 고정한 고정 크기 상자. anchor와 pivot이 같으면 그 모서리 기준 위치다.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>부모 위쪽에 붙여 가로로 늘린 줄(y는 위에서 아래로).</summary>
        public static RectTransform Row(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image Panel(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PixelUISkin.Panel;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static Sprite _white;

        /// <summary>단색 상자용 흰 그림. 그림이 있으면 PixelUISkin이 틀을 씌우지 않고, 점 필터가 아니라 PixelUIScaler도 크기를 건드리지 않는다.</summary>
        private static Sprite White => _white != null ? _white
            : _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));

        /// <summary>틀 없는 단색 상자(막대·구분선·어두운 배경).</summary>
        public static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = White;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text Text(Transform parent, string name, Font font, int size, TextAnchor anchor, Color? color = null)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color ?? Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>글자 버튼(틀 + 가운데 글자). 크기·위치는 부르는 쪽이 정한다.</summary>
        public static Button Button(Transform parent, string name, Font font, string label, Color color, Action onClick, int fontSize = BodyText)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PixelUISkin.Button;
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var text = Text(rect, "Label", font, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            text.text = label;
            return button;
        }

        public static Text Label(Button button) => button.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();

        public static void SetEnabled(Button button, bool enabled, Color enabledColor)
        {
            button.interactable = enabled;
            ((Image)button.targetGraphic).color = enabled ? enabledColor : DisabledColor;
            var label = button.transform.Find("Label");
            if (label != null) label.GetComponent<UnityEngine.UI.Text>().color = enabled ? Color.white : DisabledText;
        }

        /// <summary>도트 아이콘(PixelUIScaler가 원래 픽셀 크기로 맞춘다). size는 배치용 자리 크기 — 아이콘은 그 자리(Slot) 가운데에 놓여
        /// PixelUIScaler가 기준점을 가운데로 옮겨도 부모 크기와 상관없이 제자리에 남는다.</summary>
        public static Image Icon(Transform parent, string name, string iconName, Vector2 anchor, Vector2 position, float size)
        {
            var slot = Place(Rect(name + "Slot", parent), anchor, position, new Vector2(size, size));
            var rect = Place(Rect(name + "Icon", slot), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = IconLibrary.Get(iconName);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>세로 스크롤 목록. 반환값은 내용(Content) — 자식은 Row로 위에서부터 쌓고 높이를 SetContentHeight로 알린다.</summary>
        public static RectTransform ScrollList(RectTransform area, out ScrollRect scroll)
        {
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            var viewport = Stretch(Rect("Viewport", area));
            viewport.gameObject.AddComponent<RectMask2D>();
            // 빈 자리도 끌어서 스크롤되도록 보이지 않는 배경을 깐다.
            Fill(Stretch(Rect("HitFill", viewport)), Color.clear, raycast: true);
            var content = Rect("Content", viewport);
            Row(content, 0f, 0f);
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void SetContentHeight(RectTransform content, float height) =>
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);

        /// <summary>그 폭에서 글자가 차지하는 높이(줄바꿈 포함).</summary>
        public static float PreferredHeight(UnityEngine.UI.Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(width, 0f));
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / Mathf.Max(0.01f, text.pixelsPerUnit);
        }

        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                // Destroy는 다음 프레임에 지우므로 바로 다시 쌓는 목록에서 섞이지 않게 먼저 떼어 낸다.
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }
    }
}
