using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 설명문 대신 아이콘/숫자로 정보를 보여주기 위한 공용 uGUI 부품(docs/UxIconizationPlan.md 3절). 모든 HUD(ActionMenuHud,
    /// RewardCardHud, CityBannerHud, TechTreeHud, BattleHud)가 같은 모양의 칩/비용 배지/칸 게이지/원형 버튼을 쓰도록 한곳에 모았다.
    /// 상태가 없는 정적 생성 헬퍼다 — 만든 오브젝트를 기억하지 않고, 부모 RectTransform 아래에 새로 만들어 돌려주기만 한다.
    /// (MonoBehaviour가 아니라서 "파일 하나당 MonoBehaviour 하나" 규칙과 무관하다.)
    ///
    /// Polytopia 레퍼런스: 상단 바처럼 [아이콘][숫자]만 쓰고 라벨 단어를 붙이지 않는다(칩), 구매 버튼에는 별(⭐) 비용 배지를 붙이고
    /// 살 수 없으면 숫자를 빨갛게 한다(CostBadge), 도시 인구는 칸이 나뉜 게이지로 보여준다(SegmentGauge).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.36f, 0.62f, 1f);
        public static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        public static readonly Color GoldColor = new Color(0.98f, 0.80f, 0.22f);
        public static readonly Color PopulationColor = new Color(0.45f, 0.75f, 1f);
        public static readonly Color ResearchColor = new Color(0.55f, 0.95f, 0.75f);
        public static readonly Color GainColor = new Color(0.55f, 0.95f, 0.45f);
        public static readonly Color WarnColor = new Color(1f, 0.42f, 0.38f);
        public static readonly Color MutedColor = new Color(0.72f, 0.74f, 0.78f);

        // ---------- 기본 ----------

        /// <summary>자식을 모두 지운다. 부모에서 먼저 떼어 내므로 같은 프레임에 다시 채우고 Flow로 배치해도 지연 삭제(Destroy)될
        /// 옛 자식이 섞이지 않는다. 에디터 배치모드 검증(Play 모드 아님)에서는 DestroyImmediate를 쓴다.</summary>
        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>왼쪽 위 기준(pivot 0,1) 고정 크기 사각형 — 칩/배지를 한 줄에 흘려 놓을 때 쓴다(Flow).</summary>
        public static RectTransform TopLeft(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rect = Rect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = sprite != null;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Icon(Transform parent, string iconName, Color tint, float size)
        {
            var img = Image(parent, "Icon", string.IsNullOrEmpty(iconName) ? null : IconLibrary.Get(iconName), tint);
            if (img.sprite == null) img.color = Color.clear;
            TopLeft(img.rectTransform, new Vector2(size, size));
            return img;
        }

        public static Text Label(Transform parent, Font font, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rect = Rect("Text", parent);
            var t = rect.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text ?? string.Empty;
            return t;
        }

        /// <summary>폰트 크기 기준 한 줄 텍스트의 실제 폭(칩 폭 계산용).</summary>
        public static float TextWidth(Text t) => Mathf.Ceil(t.preferredWidth);

        /// <summary>검은 외곽선 — 지도 위에 뜨는 배너/숫자가 어느 지형 위에서도 읽히게 한다.</summary>
        public static void Outline(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(1f, -1f);
        }

        // ---------- 칩: [아이콘][숫자] ----------

        /// <summary>[아이콘][텍스트] 한 덩어리. height 높이에 맞춰 폭은 내용만큼 — 돌려주는 RectTransform의 sizeDelta.x가 실제 폭이다.
        /// tooltip이 있으면 TooltipTrigger를 붙이지만 표시 방법(OnEnter/OnExit)은 호출자가 연결한다.</summary>
        public static RectTransform Chip(Transform parent, Font font, string icon, string text, Color tint, float height = 22f, Color? background = null)
        {
            var rect = TopLeft(Rect("Chip", parent), new Vector2(0f, height));
            float pad = background.HasValue ? 6f : 0f;
            if (background.HasValue)
            {
                var bg = rect.gameObject.AddComponent<Image>();
                bg.color = background.Value;
                bg.raycastTarget = false;
            }

            float x = pad;
            float iconSize = height - 4f;
            if (!string.IsNullOrEmpty(icon))
            {
                var img = Icon(rect, icon, tint, iconSize);
                img.rectTransform.anchoredPosition = new Vector2(x, -2f);
                x += iconSize + 3f;
            }
            if (!string.IsNullOrEmpty(text))
            {
                var t = Label(rect, font, text, Mathf.RoundToInt(height * 0.68f), tint);
                TopLeft(t.rectTransform, new Vector2(0f, height));
                float w = TextWidth(t);
                t.rectTransform.sizeDelta = new Vector2(w, height);
                t.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += w;
            }
            rect.sizeDelta = new Vector2(x + pad, height);
            return rect;
        }

        /// <summary>⭐비용 배지(Polytopia 구매 버튼의 별 비용). 살 수 없으면 숫자를 빨갛게.</summary>
        public static RectTransform CostBadge(Transform parent, Font font, int cost, bool affordable, float height = 20f, string icon = "star")
        {
            var chip = Chip(parent, font, icon, cost.ToString(), affordable ? GoldColor : WarnColor, height, new Color(0f, 0f, 0f, 0.72f));
            chip.name = "Cost";
            return chip;
        }

        /// <summary>칸이 나뉜 게이지(도시 인구, 처치 수, 과업 진행). filled가 음수면 빨간 칸으로 그 수만큼 표시한다(위키 Population:
        /// 인구는 음수가 될 수 있다). 칸이 너무 많으면(>12) 칸 대신 비율 막대 한 줄로 그린다.</summary>
        public static RectTransform SegmentGauge(Transform parent, int filled, int total, Color color, float width, float height = 8f)
        {
            var rect = TopLeft(Rect("Gauge", parent), new Vector2(width, height));
            total = Mathf.Max(1, total);
            var empty = new Color(1f, 1f, 1f, 0.18f);
            if (total > 12)
            {
                var bg = Image(rect, "Bg", null, empty);
                Stretch(bg.rectTransform);
                var fill = Image(rect, "Fill", null, filled < 0 ? WarnColor : color);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Abs(filled) / (float)total), 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                return rect;
            }

            const float gap = 2f;
            float seg = (width - gap * (total - 1)) / total;
            for (int i = 0; i < total; i++)
            {
                Color c = filled >= 0 ? (i < filled ? color : empty) : (i < -filled ? WarnColor : empty);
                var s = Image(rect, "Seg", null, c);
                TopLeft(s.rectTransform, new Vector2(seg, height));
                s.rectTransform.anchoredPosition = new Vector2(i * (seg + gap), 0f);
            }
            return rect;
        }

        /// <summary>원 안의 숫자(도시 레벨 배지 등).</summary>
        public static RectTransform NumberBadge(Transform parent, Font font, int number, Color bg, float size = 22f)
        {
            var img = Image(parent, "Badge", RuntimeSprite.CreateCircle(), bg);
            TopLeft(img.rectTransform, new Vector2(size, size));
            var t = Label(img.transform, font, number.ToString(), Mathf.RoundToInt(size * 0.62f), Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return img.rectTransform;
        }

        /// <summary>자식 칩들을 왼쪽부터 한 줄로 흘려 놓고, maxWidth를 넘으면 다음 줄로 넘긴다. 전체 높이를 돌려준다.</summary>
        public static float Flow(RectTransform container, float maxWidth, float gapX = 8f, float gapY = 4f)
        {
            float x = 0f, y = 0f, rowH = 0f;
            for (int i = 0; i < container.childCount; i++)
            {
                var c = (RectTransform)container.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                var size = c.sizeDelta;
                if (x > 0f && x + size.x > maxWidth)
                {
                    x = 0f;
                    y += rowH + gapY;
                    rowH = 0f;
                }
                c.anchoredPosition = new Vector2(x, -y);
                x += size.x + gapX;
                rowH = Mathf.Max(rowH, size.y);
            }
            return container.childCount == 0 ? 0f : y + rowH;
        }

        // ---------- 원형 아이콘 버튼 ----------

        /// <summary>
        /// Polytopia 하단 행동 줄과 같은 원형 아이콘 버튼: 원 안 아이콘 + 아래 한 단어 이름 + 원 아래쪽에 겹친 ⭐비용 배지 +
        /// 쓸 수 없을 때 오른쪽 위 이유 아이콘(자물쇠 등). 셀 크기(cellWidth x 전체 높이)를 차지하는 RectTransform을 돌려준다.
        /// </summary>
        public static RectTransform IconButton(Transform parent, Font font, IconButtonSpec spec, float cellWidth, float circle,
            out Button button)
        {
            float captionH = 16f;
            float totalH = circle + 4f + captionH;
            var cell = TopLeft(Rect("Option", parent), new Vector2(cellWidth, totalH));

            var bg = Image(cell, "Circle", RuntimeSprite.CreateCircle(), spec.Enabled ? spec.Color : DisabledColor, raycast: true);
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bg.rectTransform.pivot = new Vector2(0.5f, 1f);
            bg.rectTransform.sizeDelta = new Vector2(circle, circle);
            bg.rectTransform.anchoredPosition = Vector2.zero;
            button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.interactable = spec.Enabled;

            var icon = Image(bg.transform, "Icon", string.IsNullOrEmpty(spec.Icon) ? null : IconLibrary.Get(spec.Icon),
                spec.Enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            Stretch(icon.rectTransform, circle * 0.2f);

            if (spec.Cost > 0)
            {
                var badge = CostBadge(bg.transform, font, spec.Cost, spec.Affordable, 18f, spec.CostIcon ?? "star");
                badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0f);
                badge.pivot = new Vector2(0.5f, 0.5f);
                badge.anchoredPosition = new Vector2(0f, 0f);
            }

            if (!string.IsNullOrEmpty(spec.StatusIcon))
            {
                var status = Image(bg.transform, "Status", RuntimeSprite.CreateCircle(), new Color(0f, 0f, 0f, 0.85f));
                status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1f, 1f);
                status.rectTransform.pivot = new Vector2(0.7f, 0.7f);
                status.rectTransform.sizeDelta = new Vector2(20f, 20f);
                var si = Image(status.transform, "Icon", IconLibrary.Get(spec.StatusIcon), spec.StatusTint);
                Stretch(si.rectTransform, 3f);
            }

            if (spec.Badge > 0)
            {
                var num = NumberBadge(bg.transform, font, spec.Badge, new Color(0.85f, 0.3f, 0.25f), 18f);
                num.anchorMin = num.anchorMax = new Vector2(0f, 1f);
                num.pivot = new Vector2(0.3f, 0.7f);
                num.anchoredPosition = Vector2.zero;
            }

            var caption = Label(cell, font, spec.Caption, 12, spec.Enabled ? Color.white : MutedColor, TextAnchor.UpperCenter);
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(1f, 0f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0f);
            caption.rectTransform.sizeDelta = new Vector2(0f, captionH);
            caption.rectTransform.anchoredPosition = Vector2.zero;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            return cell;
        }
    }

    /// <summary>UiKit.IconButton 한 개의 모양(순수 값).</summary>
    public struct IconButtonSpec
    {
        public string Icon;
        public string Caption;
        public bool Enabled;
        public Color Color;
        public int Cost;
        public bool Affordable;
        /// <summary>비용 아이콘 — 기본 "star"(골드), 기술은 "research".</summary>
        public string CostIcon;
        /// <summary>오른쪽 위 작은 상태 아이콘(자물쇠/경고 등). 비면 없음.</summary>
        public string StatusIcon;
        public Color StatusTint;
        /// <summary>왼쪽 위 빨간 숫자 배지(대기 중인 보상 수 등). 0이면 없음.</summary>
        public int Badge;
    }
}
