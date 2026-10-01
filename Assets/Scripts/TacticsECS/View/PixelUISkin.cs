using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    public static class PixelUISkin
    {
        public static void Apply(GameObject root)
        {
            var panel = Resources.Load<Sprite>("Pixel2D/Sheets/Panel");
            var button = Resources.Load<Sprite>("Pixel2D/Sheets/Button");
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
        }
    }
}
