using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// Bakes catalog parts into one sprite at native detail resolution (32 px per tile, parts only at integer scale).
    /// Every part loses its own dark edge ring and gets the same thick stroke, so all art shares one outline style.
    /// Rules: docs/spec/csv/sprites.md
    /// </summary>
    public static class PixelSpriteComposer
    {
        public const int Tile = 32;
        public const int Headroom = 8;
        public const int Stroke = 2;
        public static readonly Color32 Outline = new Color32(63, 38, 49, 255);
        /// <summary>Visual ids whose art did not fit the canvas even after nudging (verification reads this).</summary>
        public static readonly HashSet<string> Clipped = new HashSet<string>();
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<Texture2D, Color32[]> SourcePixels = new Dictionary<Texture2D, Color32[]>();

        private struct Layer { public int X, Y, W, H; public Color32[] Pixels; public bool[] Stroke; }

        public static Sprite Compose(string id, int level = 1, Color? team = null, bool half = false) =>
            Compose(new[] { new SpritePlacement(id) }, level, team, half);

        /// <summary>Placements are drawn in list order; parts inside one placement by their CSV Layer.</summary>
        public static Sprite Compose(IReadOnlyList<SpritePlacement> placements, int level = 1, Color? team = null, bool half = false)
        {
            var color = team ?? Color.white;
            string key = string.Join("|", placements.Select(p => $"{p.VisualId}@{p.X},{p.Y},{p.FlipX}")) + $":{level}:{ColorUtility.ToHtmlStringRGBA(color)}:{half}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            bool icon = placements.All(p => p.VisualId != null && p.VisualId.StartsWith("Icon.", StringComparison.Ordinal));
            var groups = new List<List<Layer>>();
            int maxScale = 1;
            foreach (var placement in placements)
            {
                string id = PixelSpriteCatalog.Has(placement.VisualId) ? placement.VisualId : "UI.Missing";
                var group = new List<Layer>();
                foreach (var part in PixelSpriteCatalog.Layers(id).Where(p => level >= p.MinLevel && level <= p.MaxLevel).OrderBy(p => p.Layer))
                {
                    int scale = half ? Mathf.Max(1, part.Scale / 2) : part.Scale;
                    maxScale = Mathf.Max(maxScale, scale);
                    int x = half ? part.X / 2 : part.X, y = half ? part.Y / 2 : part.Y;
                    group.Add(Rasterize(part, scale, placement.FlipX ? -x : x, y, part.FlipX != placement.FlipX, color));
                }
                groups.Add(group);
            }

            int width = icon ? 16 * maxScale + 2 * Stroke : Tile;
            int height = icon ? width : Tile + Headroom;
            int centreX = width / 2, centreY = icon ? height / 2 : Tile / 2;
            var canvas = new Color32[width * height];
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                if (group.Count == 0) continue;
                // Nudge the whole visual back inside the canvas instead of cutting it at the edge.
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var l in group)
                    Bounds(l, centreX + placements[g].X, centreY + placements[g].Y, ref minX, ref minY, ref maxX, ref maxY);
                if (minX > maxX) continue;
                int dx = Shift(minX, maxX, width), dy = Shift(minY, maxY, height);
                if (maxX - minX >= width || maxY - minY >= height) Clipped.Add(placements[g].VisualId);
                foreach (var l in group)
                    Draw(canvas, width, height, l, centreX + placements[g].X + dx, centreY + placements[g].Y + dy);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Composite_" + placements[0].VisualId, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(canvas);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(centreX / (float)width, centreY / (float)height),
                PixelSpriteCatalog.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "Composite_" + placements[0].VisualId.Replace('.', '_') + "_" + level + (placements.Count > 1 ? "_" + (uint)key.GetHashCode() : "");
            Cache[key] = sprite;
            return sprite;
        }

        private static int Shift(int min, int max, int size)
        {
            if (max - min >= size) return (size - 1 - max - min) / 2;
            if (min < 0) return -min;
            if (max >= size) return size - 1 - max;
            return 0;
        }

        /// <summary>Part pixels at integer scale with the source's own dark edge ring removed, plus the stroke mask.</summary>
        private static Layer Rasterize(SpritePartInfo part, int scale, int x, int y, bool flip, Color team)
        {
            var tint = team;
            if (part.Color != "Team" && !ColorUtility.TryParseHtmlString("#" + part.Color, out tint))
                throw new InvalidOperationException("Invalid pixel tint: " + part.Color);
            Color32[] art;
            int w, h;
            if (part.Sheet == "Pattern/Solid")
            {
                // Solid rectangles are sized directly in detail pixels.
                w = part.Width; h = part.Height; scale = 1;
                art = Enumerable.Repeat((Color32)new Color(tint.r, tint.g, tint.b, 1), w * h).ToArray();
            }
            else
            {
                var sprite = PixelSpriteCatalog.SpriteFor(part);
                var rect = sprite.rect;
                var source = Read(sprite.texture);
                int sw = (int)rect.width, sh = (int)rect.height;
                var opaque = new bool[sw * sh];
                for (int j = 0; j < sh; j++) for (int i = 0; i < sw; i++)
                    opaque[j * sw + i] = source[((int)rect.y + j) * sprite.texture.width + (int)rect.x + i].a >= 128;
                w = sw * scale; h = sh * scale;
                art = new Color32[w * h];
                for (int j = 0; j < sh; j++) for (int i = 0; i < sw; i++)
                {
                    if (!opaque[j * sw + i]) continue;
                    Color32 c = source[((int)rect.y + j) * sprite.texture.width + (int)rect.x + i];
                    bool edge = i == 0 || j == 0 || i == sw - 1 || j == sh - 1 || !opaque[j * sw + i - 1] || !opaque[j * sw + i + 1]
                        || !opaque[(j - 1) * sw + i] || !opaque[(j + 1) * sw + i];
                    if (edge && (.299f * c.r + .587f * c.g + .114f * c.b) / 255f < .26f) continue;
                    var tinted = new Color32((byte)(c.r * tint.r), (byte)(c.g * tint.g), (byte)(c.b * tint.b), 255);
                    for (int sy = 0; sy < scale; sy++) for (int sx = 0; sx < scale; sx++)
                        art[(j * scale + sy) * w + (flip ? w - 1 - (i * scale + sx) : i * scale + sx)] = tinted;
                }
            }
            // Pad by the stroke width and mark every empty pixel within the rounded brush of an art pixel.
            int pw = w + 2 * Stroke, ph = h + 2 * Stroke;
            var pixels = new Color32[pw * ph];
            var stroke = new bool[pw * ph];
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) pixels[(j + Stroke) * pw + i + Stroke] = art[j * w + i];
            for (int j = 0; j < ph; j++) for (int i = 0; i < pw; i++)
            {
                if (pixels[j * pw + i].a == 0) continue;
                for (int oy = -Stroke; oy <= Stroke; oy++) for (int ox = -Stroke; ox <= Stroke; ox++)
                {
                    if (Mathf.Abs(ox) + Mathf.Abs(oy) > Stroke + 1) continue;
                    int n = (j + oy) * pw + i + ox;
                    if (pixels[n].a == 0) stroke[n] = true;
                }
            }
            return new Layer { X = x - w / 2 - Stroke, Y = y - h / 2 - Stroke, W = pw, H = ph, Pixels = pixels, Stroke = stroke };
        }

        private static void Bounds(Layer l, int originX, int originY, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            for (int j = 0; j < l.H; j++) for (int i = 0; i < l.W; i++)
            {
                int k = j * l.W + i;
                if (l.Pixels[k].a == 0 && !l.Stroke[k]) continue;
                int x = originX + l.X + i, y = originY + l.Y + j;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
        }

        private static void Draw(Color32[] canvas, int width, int height, Layer l, int originX, int originY)
        {
            for (int j = 0; j < l.H; j++) for (int i = 0; i < l.W; i++)
            {
                int k = j * l.W + i, x = originX + l.X + i, y = originY + l.Y + j;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                if (l.Pixels[k].a != 0) canvas[y * width + x] = l.Pixels[k];
                else if (l.Stroke[k]) canvas[y * width + x] = Outline;
            }
        }

        private static Color32[] Read(Texture2D texture)
        {
            if (!SourcePixels.TryGetValue(texture, out var pixels)) SourcePixels[texture] = pixels = texture.GetPixels32();
            return pixels;
        }
    }
}
