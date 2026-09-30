using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 지도 위 도시 배너 — Polytopia의 가장 핵심적인 UI(docs/UxIconizationPlan.md 2.6). 도시 칸 위에 [수도 왕관][이름][레벨 배지]와
    /// 그 아래 인구 칸 게이지(채운 칸 = 인구, 칸 수 = 다음 레벨까지 필요한 인구)를 띄워, 도시를 누르지 않고도 레벨/인구/수도를 읽을 수 있게 한다.
    /// 받을 보상이 남은 우리 도시에는 보상 아이콘을 붙인다.
    ///
    /// 화면 공간 오버레이 캔버스에 그리고 매 프레임(LateUpdate) 도시 칸의 화면 좌표를 따라간다 — 월드 공간 캔버스는 카메라 줌에 따라
    /// 글자가 작아져 휴대폰에서 읽기 어려워서다. 클릭을 가로채지 않도록(GraphicRaycaster 없음, raycastTarget 끔) 지도 입력은 그대로 통과한다.
    /// 값은 판단하지 않고 BattleController가 넘긴 도시 목록/시야를 그리기만 한다.
    /// </summary>
    public class CityBannerHud : MonoBehaviour
    {
        private const float BannerHeight = 24f;
        private const float GaugeHeight = 6f;
        /// <summary>도시 칸 중심에서 배너를 띄울 높이(월드 단위) — 도시 모델 위.</summary>
        private const float WorldLift = 1.1f;

        private struct Banner
        {
            public RectTransform Rect;
            public Vector3 WorldPos;
        }

        private Font _font;
        private RectTransform _root;
        private Camera _cam;
        private readonly List<Banner> _banners = new List<Banner>();

        public void Init(Font font)
        {
            _font = font;
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5; // 다른 모든 HUD 아래(지도 바로 위).
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            ResponsiveCanvas.Attach(canvasGo.transform);
            _root = (RectTransform)canvasGo.transform;
        }

        /// <summary>도시 목록이 바뀔 때마다(RefreshEconomyViews) 배너를 다시 만든다. 위치는 LateUpdate가 매 프레임 따라간다.</summary>
        public void SetCities(GridWorld grid, IReadOnlyList<CityData> cities, Camera cam)
        {
            _cam = cam;
            foreach (var b in _banners) if (b.Rect != null) Destroy(b.Rect.gameObject);
            _banners.Clear();
            if (grid == null || cities == null) return;

            foreach (var city in cities)
            {
                if (!VisionSystem.IsExplored(grid, Team.Player, city.Position)) continue;
                _banners.Add(new Banner
                {
                    Rect = BuildBanner(city),
                    WorldPos = grid.GridToWorld(city.Position) + Vector3.up * WorldLift,
                });
            }
            LateUpdate();
        }

        private RectTransform BuildBanner(CityData city)
        {
            var accent = city.Owner == Team.Player ? BattleHud.PlayerAccent : BattleHud.EnemyAccent;
            var banner = UiKit.Rect("Banner", _root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
            banner.pivot = new Vector2(0.5f, 0f);

            var bg = UiKit.Image(banner, "Bg", null, new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 0.92f));
            float x = 6f;
            var row = UiKit.Rect("Row", bg.transform);
            if (city.IsCapital)
            {
                var crown = UiKit.Icon(row, "crown", UiKit.GoldColor, BannerHeight - 6f);
                crown.rectTransform.anchoredPosition = new Vector2(x, -3f);
                x += BannerHeight - 3f;
            }
            var name = UiKit.Label(row, _font, city.Name, 14, Color.white);
            UiKit.Outline(name);
            UiKit.TopLeft(name.rectTransform, new Vector2(0f, BannerHeight));
            float nw = UiKit.TextWidth(name);
            name.rectTransform.sizeDelta = new Vector2(nw, BannerHeight);
            name.rectTransform.anchoredPosition = new Vector2(x, 0f);
            x += nw + 4f;
            var level = UiKit.NumberBadge(row, _font, city.Level, accent, BannerHeight - 4f);
            level.anchoredPosition = new Vector2(x, -2f);
            x += BannerHeight;
            if (city.Owner == Team.Player && city.PendingRewards > 0)
            {
                var reward = UiKit.Icon(row, "reward", UiKit.GoldColor, BannerHeight - 4f);
                reward.rectTransform.anchoredPosition = new Vector2(x, -2f);
                x += BannerHeight;
            }
            float width = x + 2f;

            UiKit.TopLeft(bg.rectTransform, new Vector2(width, BannerHeight));
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bg.rectTransform.pivot = new Vector2(0.5f, 1f);
            bg.rectTransform.anchoredPosition = Vector2.zero;
            UiKit.TopLeft(row, new Vector2(width, BannerHeight));
            row.anchoredPosition = Vector2.zero;

            var gauge = UiKit.SegmentGauge(banner, city.Population, city.Level + 1, UiKit.PopulationColor, width, GaugeHeight);
            gauge.anchorMin = gauge.anchorMax = new Vector2(0.5f, 1f);
            gauge.pivot = new Vector2(0.5f, 1f);
            gauge.anchoredPosition = new Vector2(0f, -BannerHeight - 2f);

            banner.sizeDelta = new Vector2(width, BannerHeight + GaugeHeight + 2f);
            return banner;
        }

        private void LateUpdate()
        {
            if (_cam == null || _root == null) return;
            foreach (var b in _banners)
            {
                var screen = _cam.WorldToScreenPoint(b.WorldPos);
                bool visible = screen.z > 0f;
                b.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local))
                    b.Rect.anchoredPosition = local;
            }
        }
    }
}
