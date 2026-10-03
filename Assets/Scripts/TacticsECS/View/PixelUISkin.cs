using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>Pixel frames for uGUI; texel size is kept uniform by <see cref="PixelUIScaler"/>.</summary>
    public static class PixelUISkin
    {
        public static Sprite Panel => Resources.Load<Sprite>("Pixel2D/Sheets/Panel");
        public static Sprite Button => Resources.Load<Sprite>("Pixel2D/Sheets/Button");
        /// <summary>Rounded white frame for icon badges (tinted by Image.color); use Image.Type.Sliced.</summary>
        public static Sprite Badge => Resources.Load<Sprite>("Pixel2D/Sheets/Badge");

        public static void Apply(GameObject root)
        {
            var panel = Panel;
            var button = Button;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;
                if (image.sprite != null || name.Contains("Icon") || name.Contains("Viewport") || name.Contains("Fill") || name.Contains("Accent")) continue;
                var rect = image.rectTransform.rect;
                if (image.GetComponent<Button>() != null)
                {
                    image.sprite = button;
                    image.type = Image.Type.Sliced;
                }
                else if (rect.width >= 48 && rect.height >= 24 && image.color.a > .5f)
                {
                    image.sprite = panel;
                    image.type = Image.Type.Sliced;
                }
            }
            PixelUIScaler.Attach(root);
        }
    }
}
