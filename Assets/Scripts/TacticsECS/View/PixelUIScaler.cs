using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// Keeps every pixel-art Image on a canvas at one texel size: <see cref="TexelSize"/> screen pixels per art pixel,
    /// the same as a world pixel at default zoom, whatever the canvas scale. Rules: docs/spec/csv/sprites.md#ui-해상도
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PixelUIScaler : MonoBehaviour
    {
        /// <summary>9-slice border of Panel/Button/Badge, in texels (scripts/author_pixel_ui.py).</summary>
        public const int FrameBorder = 4;
        /// <summary>Screen height that maps to one screen pixel per texel; 1080p gives 2.</summary>
        public const float ReferenceHeight = 540f;
        private Canvas _canvas;
        private float _appliedScale;
        private int _appliedTexel, _appliedImages;

        public static int TexelSize(float screenHeight) => Mathf.Max(1, Mathf.RoundToInt(screenHeight / ReferenceHeight));

        private void LateUpdate()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            int texel = TexelSize(_canvas.pixelRect.height);
            int images = GetComponentsInChildren<Image>(true).Length;
            // Sprites are assigned after Init (Wire*), so also re-check every half second.
            if (Mathf.Approximately(_canvas.scaleFactor, _appliedScale) && texel == _appliedTexel && images == _appliedImages && Time.frameCount % 30 != 0) return;
            Apply(_canvas);
            _appliedScale = _canvas.scaleFactor; _appliedTexel = texel; _appliedImages = images;
        }

        /// <summary>Also called directly in Edit Mode captures, where LateUpdate does not run.</summary>
        public static void Apply(Canvas canvas)
        {
            canvas = canvas.rootCanvas;
            canvas.pixelPerfect = true;
            float scale = canvas.scaleFactor;
            int texel = TexelSize(canvas.pixelRect.height);
            foreach (var image in canvas.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                if (sprite == null || sprite.texture == null || sprite.texture.filterMode != FilterMode.Point) continue;
                if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
                {
                    // Border texels drawn at `texel` screen pixels: UI units per texel = texel / scale.
                    image.pixelsPerUnitMultiplier = scale * canvas.referencePixelsPerUnit / (sprite.pixelsPerUnit * texel);
                    continue;
                }
                // Simple art (icons): native texels × texel, re-centred where it was laid out.
                var rect = image.rectTransform;
                var size = sprite.rect.size * texel / scale;
                var centre = rect.TransformPoint(rect.rect.center);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = size;
                rect.position = centre;
                var element = image.GetComponent<LayoutElement>();
                if (element == null && rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
                    element = image.gameObject.AddComponent<LayoutElement>();
                if (element != null) { element.preferredWidth = element.minWidth = size.x; element.preferredHeight = element.minHeight = size.y; }
            }
        }

        /// <summary>Adds the scaler to every root canvas under <paramref name="root"/> and applies it once.</summary>
        public static void Attach(GameObject root)
        {
            var canvases = new System.Collections.Generic.List<Canvas>(root.GetComponentsInChildren<Canvas>(true));
            var parent = root.GetComponentInParent<Canvas>();
            if (parent != null) canvases.Add(parent.rootCanvas);
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas) continue;
                if (canvas.GetComponent<PixelUIScaler>() == null) canvas.gameObject.AddComponent<PixelUIScaler>();
                Apply(canvas);
            }
        }
    }
}
