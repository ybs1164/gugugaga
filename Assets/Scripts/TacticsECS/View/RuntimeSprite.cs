using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 텍스처 임포트 설정을 건드리지 않고(에디터 직접 조작 금지 — CLAUDE.md 규칙 1) 코드로 그때그때
    /// 만들어 캐싱하는 런타임 전용 Sprite 헬퍼. IconLibrary(디스크의 PNG를 불러오는 것)와 달리 이 안의
    /// 것들은 디스크 에셋이 아니라 메모리에서 생성된다 — 그래서 UIPrefabSetup이 만드는 프리팹에는 이
    /// 결과를 구워둘 수 없고(참조가 유지되지 않음), 각 View의 Init()이 인스턴스화 직후 채워야 한다
    /// (IconLibrary와 같은 이유, CityResourceHud/BattleHud 주석 참고).
    /// </summary>
    public static class RuntimeSprite
    {
        private static readonly Dictionary<int, Sprite> CircleCache = new Dictionary<int, Sprite>();

        /// <summary>가장자리 1px만 부드럽게(anti-alias) 처리한 흰색 원형 스프라이트. 해상도별로 한 번만
        /// 만들고 캐싱한다 — BattleHud의 패시브 배지, TechTreeHud의 원형 노드 배경처럼 원형 UI 요소가
        /// 여러 군데서 공유해서 쓴다.</summary>
        public static Sprite CreateCircle(int resolution = 64)
        {
            if (CircleCache.TryGetValue(resolution, out var cached)) return cached;

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { name = "CircleSprite" };
            var center = new Vector2((resolution - 1) * 0.5f, (resolution - 1) * 0.5f);
            float radius = resolution * 0.5f;
            var pixels = new Color32[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
            CircleCache[resolution] = sprite;
            return sprite;
        }
    }
}
