using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// Bakes catalog parts into one sprite at native detail resolution (32 px per tile, parts only at integer scale).
    /// Every part loses its own dark edge ring; the final silhouette gets one thick stroke and a part covering earlier
    /// art gets one separating line of the same width, so outlines never stack.
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

        public static void Invalidate()
        {
            Cache.Clear();
            Clipped.Clear();
        }

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
            CloseGaps(canvas, width, height);
            canvas = OuterStroke(canvas, width, height);
            ThickenThinLines(canvas, width, height); // one pass: repeating it would creep into the art

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Composite_" + placements[0].VisualId, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(canvas);
            texture.Apply(false, false);
            var area = new Rect(0, 0, width, height);
            var pivot = new Vector2(centreX / (float)width, centreY / (float)height);
            if (icon)
            {
                // UI shows icons at native texels (PixelUIScaler), so the sprite is cropped to its drawn pixels.
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    if (canvas[y * width + x].a != 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                if (maxX >= 0) area = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                pivot = new Vector2(.5f, .5f);
            }
            var sprite = Sprite.Create(texture, area, pivot, PixelSpriteCatalog.PixelsPerUnit, 0, SpriteMeshType.FullRect);
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
                var stripped = SourceOutline(source, sprite.texture.width, (int)rect.x, (int)rect.y, sw, sh, opaque);
                for (int j = 0; j < sh; j++) for (int i = 0; i < sw; i++)
                {
                    if (!opaque[j * sw + i] || stripped[j * sw + i]) continue;
                    Color32 c = source[((int)rect.y + j) * sprite.texture.width + (int)rect.x + i];
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
                // Only where this part covers earlier art: one separating line, never a second outer stroke.
                else if (l.Stroke[k] && canvas[y * width + x].a != 0) canvas[y * width + x] = Outline;
            }
        }

        /// <summary>
        /// The source's own outline band: dark pixels reachable from outside (8-neighbour) through dark pixels, up to
        /// <see cref="Stroke"/>+1 source pixels deep. Kenney art draws a 2-3px outline; patterns are saved without one; removing the whole
        /// band (not just one ring) is what lets every visual end up with the same stroke width.
        /// </summary>
        private static bool[] SourceOutline(Color32[] source, int texWidth, int rx, int ry, int sw, int sh, bool[] opaque)
        {
            var depth = new int[sw * sh];
            var queue = new Queue<int>();
            bool Dark(int k) => opaque[k] && IsLine(source[(ry + k / sw) * texWidth + rx + k % sw]);
            for (int j = 0; j < sh; j++) for (int i = 0; i < sw; i++)
            {
                int k = j * sw + i;
                if (!Dark(k)) continue;
                bool edge = false;
                for (int oy = -1; oy <= 1 && !edge; oy++) for (int ox = -1; ox <= 1 && !edge; ox++)
                    edge = i + ox < 0 || j + oy < 0 || i + ox >= sw || j + oy >= sh || !opaque[(j + oy) * sw + i + ox];
                if (edge) { depth[k] = 1; queue.Enqueue(k); }
            }
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(), i = k % sw, j = k / sw;
                if (depth[k] > Stroke) continue; // Kenney's 2px outline thickens to 3px in places
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int ni = i + ox, nj = j + oy, n = nj * sw + ni;
                    if (ni < 0 || nj < 0 || ni >= sw || nj >= sh || depth[n] != 0 || !Dark(n)) continue;
                    depth[n] = depth[k] + 1; queue.Enqueue(n);
                }
            }
            var stripped = new bool[sw * sh];
            for (int k = 0; k < stripped.Length; k++) stripped[k] = depth[k] != 0;
            return stripped;
        }

        /// <summary>
        /// A transparent run of up to 2×<see cref="Stroke"/> between two art pixels (row or column) would fill with stroke from
        /// both sides into a 3-4px dark band. Keep a <see cref="Stroke"/>-wide seam and extend the art on each side over the rest.
        /// </summary>
        private static void CloseGaps(Color32[] canvas, int width, int height)
        {
            var source = (Color32[])canvas.Clone();
            for (int pass = 0; pass < 2; pass++)
            {
                bool rows = pass == 0;
                int outer = rows ? height : width, inner = rows ? width : height;
                for (int a = 0; a < outer; a++)
                {
                    int Index(int t) => rows ? a * width + t : t * width + a;
                    for (int b = 0; b < inner;)
                    {
                        if (source[Index(b)].a != 0) { b++; continue; }
                        int e = b;
                        while (e < inner && source[Index(e)].a == 0) e++;
                        int gap = e - b;
                        if (b > 0 && e < inner && gap <= 2 * Stroke)
                        {
                            Color32 left = source[Index(b - 1)], right = source[Index(e)];
                            int extra = Mathf.Max(0, gap - Stroke), leftFill = extra / 2, rightFill = extra - leftFill;
                            for (int t = b; t < e; t++)
                            {
                                if (canvas[Index(t)].a != 0) continue;
                                canvas[Index(t)] = t < b + leftFill ? (IsLine(left) ? Outline : left)
                                    : t >= e - rightFill ? (IsLine(right) ? Outline : right) : Outline;
                            }
                        }
                        b = e;
                    }
                }
            }
        }

        private static bool IsLine(Color32 c) => c.a != 0 && (.299f * c.r + .587f * c.g + .114f * c.b) / 255f < .26f;

        /// <summary>
        /// Source art keeps 1px interior lines (window frames, roof seams, stems) while the stroke is <see cref="Stroke"/> px.
        /// Every 1px-wide run of a line gains its neighbouring art pixel, so all lines share the stroke width.
        /// Line specks under 3 pixels (eyes, dots) stay as drawn. Returns how many pixels were painted.
        /// </summary>
        public static int ThickenThinLines(Color32[] canvas, int width, int height)
        {
            var source = (Color32[])canvas.Clone();
            var size = LineComponentSizes(source, width, height);
            int thin = 0;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (!IsLine(source[i]) || size[i] < 3) continue;
                if (Run(source, width, height, x, y, 1, 0) == 1 && Fill(source, canvas, width, height, x + 1, y, x - 1, y)) thin++;
                if (Run(source, width, height, x, y, 0, 1) == 1 && Fill(source, canvas, width, height, x, y - 1, x, y + 1)) thin++;
            }
            return thin;
        }

        /// <summary>Pixels of 1px-wide line segments (two or more in a row) that could still be thickened; verification expects 0.</summary>
        public static int ThinLineSegments(Color32[] c, int width, int height)
        {
            var size = LineComponentSizes(c, width, height);
            int thin = 0;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (!IsLine(c[y * width + x]) || size[y * width + x] < 3) continue;
                bool vertical = Run(c, width, height, x, y, 1, 0) == 1 && (ThinAt(c, width, height, x, y + 1, 1, 0) || ThinAt(c, width, height, x, y - 1, 1, 0))
                    && (Art(c, width, height, x + 1, y) || Art(c, width, height, x - 1, y));
                bool horizontal = Run(c, width, height, x, y, 0, 1) == 1 && (ThinAt(c, width, height, x + 1, y, 0, 1) || ThinAt(c, width, height, x - 1, y, 0, 1))
                    && (Art(c, width, height, x, y + 1) || Art(c, width, height, x, y - 1));
                if (vertical || horizontal) thin++;
            }
            return thin;
        }

        private static bool ThinAt(Color32[] c, int width, int height, int x, int y, int dx, int dy) =>
            x >= 0 && y >= 0 && x < width && y < height && IsLine(c[y * width + x]) && Run(c, width, height, x, y, dx, dy) == 1;

        private static bool Art(Color32[] c, int width, int height, int x, int y) =>
            x >= 0 && y >= 0 && x < width && y < height && c[y * width + x].a != 0 && !IsLine(c[y * width + x]);

        private static int Run(Color32[] c, int width, int height, int x, int y, int dx, int dy)
        {
            int n = 1;
            for (int s = -1; s <= 1; s += 2)
                for (int i = 1; ; i++)
                {
                    int nx = x + dx * i * s, ny = y + dy * i * s;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || !IsLine(c[ny * width + nx])) break;
                    n++;
                }
            return n;
        }

        /// <summary>Paint the first candidate that is non-line art (never transparent, so the silhouette keeps its size).</summary>
        private static bool Fill(Color32[] source, Color32[] canvas, int width, int height, int ax, int ay, int bx, int by)
        {
            foreach (var (x, y) in new[] { (ax, ay), (bx, by) })
            {
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var c = source[y * width + x];
                if (c.a == 0 || IsLine(c)) continue;
                canvas[y * width + x] = Outline;
                return true;
            }
            return false;
        }

        private static int[] LineComponentSizes(Color32[] c, int width, int height)
        {
            var size = new int[c.Length];
            var seen = new bool[c.Length];
            var stack = new Stack<int>();
            var members = new List<int>();
            for (int start = 0; start < c.Length; start++)
            {
                if (seen[start] || !IsLine(c[start])) continue;
                members.Clear(); stack.Push(start); seen[start] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop(); members.Add(i);
                    int x = i % width, y = i / width;
                    foreach (var n in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                        if (n >= 0 && !seen[n] && IsLine(c[n])) { seen[n] = true; stack.Push(n); }
                }
                foreach (int i in members) size[i] = members.Count;
            }
            return size;
        }

        /// <summary>One stroke around the final silhouette, so overlapping parts never stack their outlines.</summary>
        private static Color32[] OuterStroke(Color32[] canvas, int width, int height)
        {
            var result = (Color32[])canvas.Clone();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (canvas[y * width + x].a == 0) continue;
                for (int oy = -Stroke; oy <= Stroke; oy++) for (int ox = -Stroke; ox <= Stroke; ox++)
                {
                    int nx = x + ox, ny = y + oy;
                    if (Mathf.Abs(ox) + Mathf.Abs(oy) > Stroke + 1 || nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    if (canvas[ny * width + nx].a == 0) result[ny * width + nx] = Outline;
                }
            }
            return result;
        }

        private static Color32[] Read(Texture2D texture)
        {
            if (!SourcePixels.TryGetValue(texture, out var pixels)) SourcePixels[texture] = pixels = texture.GetPixels32();
            return pixels;
        }
    }
}
