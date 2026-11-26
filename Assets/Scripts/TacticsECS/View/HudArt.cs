using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// HUD 전용 그림과 글꼴(Assets/Resources/Hud, Assets/Resources/Fonts). 아이콘은 흰 그림이라 Image.color로 물들인다.
    /// 가져오기 설정은 Editor/HudTextureImport가 맞춘다. 규칙: docs/spec/csv/sprites.md#hud-그림
    /// </summary>
    public static class HudArt
    {
        /// <summary>IconLibrary 이름 앞에 붙이면 이 폴더의 아이콘을 쓴다(예: "hud.crown").</summary>
        public const string IconPrefix = "hud.";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Font _body, _number;

        /// <summary>본문 글꼴.</summary>
        public static Font Body => _body != null ? _body : _body = Resources.Load<Font>("Fonts/NotoSansKR-Medium");
        /// <summary>숫자 글꼴(도트). 12의 배수 크기에서 또렷하다.</summary>
        public static Font Number => _number != null ? _number : _number = Resources.Load<Font>("Fonts/Galmuri11");

        public static Texture2D Texture(string path) => Resources.Load<Texture2D>("Hud/" + path);

        /// <summary>Resources/Hud 아래 그림 하나를 스프라이트로(없으면 null).</summary>
        public static Sprite Load(string path)
        {
            if (Sprites.TryGetValue(path, out var sprite) && sprite != null) return sprite;
            var texture = Texture(path);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = path;
            Sprites[path] = sprite;
            return sprite;
        }

        public static Sprite Icon(string name) => Load("Icons/" + name);

        /// <summary>종족 아이콘(없는 종족은 "random").</summary>
        public static Sprite Tribe(string id) => Load("Tribes/" + id) ?? Load("Tribes/random");

        /// <summary>신 아이콘(없는 신은 "random").</summary>
        public static Sprite God(string id) => Load("Gods/" + id) ?? Load("Gods/random");

        /// <summary>아이콘 하나를 parent의 한 점(anchor)에서 position만큼 떨어진 곳에 놓는다. 고정 크기 자리(Slot) 안에 넣어, 부모 폭이 바뀌어도
        /// PixelUIScaler가 맞춘 크기 그대로 제자리에 남는다.</summary>
        public static Image PlaceIcon(Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, Color color)
        {
            var slot = new GameObject("IconSlot", typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            UiKit.Place((RectTransform)slot.transform, anchor, position, new Vector2(28f, 28f));
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot.transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            UiKit.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            return image;
        }

        /// <summary>글자를 숫자 글꼴로 바꾼다.</summary>
        public static void UseNumberFont(Text text, int size)
        {
            if (Number != null) text.font = Number;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
        }
    }
}
