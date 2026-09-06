using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// Assets/Art/GameIcons/Resources/Icons 아래의 아이콘 PNG를 Sprite로 감싸 캐싱해주는 헬퍼(CC BY 3.0,
    /// 출처/라이선스는 Assets/Art/GameIcons/LICENSE.txt).
    /// 텍스처 임포트 설정(Texture Type을 Sprite로 바꾸는 등)을 건드리려면 에디터를 열어야 하므로,
    /// 대신 기본 Texture2D로 그냥 불러온 뒤 Sprite.Create로 직접 감싼다 — "Unity 에디터를 직접
    /// 조작하지 않는다"는 프로젝트 규칙을 따르기 위한 선택이다.
    /// </summary>
    public static class IconLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string iconName)
        {
            if (Cache.TryGetValue(iconName, out var cached)) return cached;

            var texture = Resources.Load<Texture2D>($"Icons/{iconName}");
            if (texture == null)
            {
                Debug.LogWarning($"[IconLibrary] icon not found: Resources/Icons/{iconName}.png");
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            Cache[iconName] = sprite;
            return sprite;
        }
    }
}
