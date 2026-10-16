using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 루트 캔버스 하나의 배율과 도트 그림 크기를 맞춘다. 캔버스 배율은 <see cref="UiScale.ScaleFactor"/>(PC·모바일 공용 규칙),
    /// 모든 도트 그림은 한 텍셀이 <see cref="UiScale.TexelSize"/> 화면 픽셀. 캔버스 바로 아래 "Safe" 자식이 있으면 안전 영역(노치·제스처 바 제외)에 맞춘다.
    /// 규칙: docs/spec/csv/sprites.md#ui-해상도
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PixelUIScaler : MonoBehaviour
    {
        /// <summary>9-slice border of Panel/Button/Badge, in texels (scripts/author_pixel_ui.py).</summary>
        public const int FrameBorder = 4;
        /// <summary>안전 영역에 맞출 자식 이름(BattleHud 등 코드로 만드는 화면).</summary>
        public const string SafeAreaName = "Safe";
        private Canvas _canvas;
        private float _appliedScale;
        private Rect _appliedPixelRect, _appliedSafe;
        private int _appliedImages;

        private void LateUpdate()
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            int images = GetComponentsInChildren<Image>(true).Length;
            var pixelRect = _canvas.pixelRect;
            var safe = UiScale.SafeAreaNormalized();
            // Sprites are assigned after Init (Wire*), so also re-check every half second.
            if (pixelRect == _appliedPixelRect && safe == _appliedSafe && Mathf.Approximately(_canvas.scaleFactor, _appliedScale) && images == _appliedImages && Time.frameCount % 30 != 0) return;
            Apply(_canvas);
            _appliedScale = _canvas.scaleFactor; _appliedPixelRect = pixelRect; _appliedSafe = safe; _appliedImages = images;
        }

        /// <summary>Also called directly in Edit Mode captures, where LateUpdate does not run.</summary>
        public static void Apply(Canvas canvas)
        {
            canvas = canvas.rootCanvas;
            canvas.pixelPerfect = true;
            var pixelRect = canvas.pixelRect;
            float scale = UiScale.ScaleFactor(pixelRect.width, pixelRect.height);
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scale;
            }
            canvas.scaleFactor = scale; // 에디트 모드에서는 CanvasScaler가 돌지 않는다
            ApplySafeArea(canvas);

            int texel = UiScale.TexelSize(scale);
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

        private static void ApplySafeArea(Canvas canvas)
        {
            var safeRoot = canvas.transform.Find(SafeAreaName) as RectTransform;
            if (safeRoot == null) return;
            var safe = UiScale.SafeAreaNormalized();
            safeRoot.anchorMin = safe.min;
            safeRoot.anchorMax = safe.max;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
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
