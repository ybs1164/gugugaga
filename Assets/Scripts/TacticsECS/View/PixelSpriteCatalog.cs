using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TacticsECS
{
    /// <summary>CC0 sheets and code-native pixel patterns, shared by all 2D views. No gameplay state.</summary>
    public static class PixelSpriteCatalog
    {
        /// <summary>Detail pixels per tile; composites and ground tiles share this grid.</summary>
        public const float PixelsPerUnit = 32f;
        /// <summary>Raw sheet cells (16 px) as one-unit sprites, used for rectangles and UI shapes.</summary>
        public const float SourcePixelsPerUnit = 16f;
        public const int ObjectOrder = 1000;
        public const int UnitOrder = 12000;
        public const int OverlayOrder = 24000;
        public const int FogOrder = 25000;
        private static readonly Dictionary<string, List<SpritePartInfo>> Parts = new Dictionary<string, List<SpritePartInfo>>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> Grounds = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static Material _material;
        private static bool _loaded;

        public static Material Material
        {
            get
            {
                if (_material == null)
                    _material = Resources.Load<Material>("Pixel2D/PixelUnlit") ?? new Material(Shader.Find("Sprites/Default")) { name = "Pixel2D_Unlit" };
                return _material;
            }
        }

        public static IEnumerable<string> VisualIds { get { Load(); return Parts.Keys; } }
        public static bool Has(string id) { Load(); return id != null && Parts.ContainsKey(id); }
        public static IReadOnlyList<SpritePartInfo> Layers(string id)
        {
            Load();
            return Parts.TryGetValue(id, out var parts) ? parts : Array.Empty<SpritePartInfo>();
        }

        private static void Load()
        {
            if (_loaded) return;
            var asset = Resources.Load<TextAsset>("Pixel2D/SpriteCatalog");
            if (asset == null) throw new InvalidOperationException("Pixel2D/SpriteCatalog.csv is missing");
            var table = CsvTableReader.Parse("SpriteCatalog.csv", asset.text);
            var errors = new List<string>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                var part = new SpritePartInfo
                {
                    VisualId = CsvTableReader.Get(table, i, "VisualId"),
                    Sheet = CsvTableReader.Get(table, i, "Sheet"),
                    Column = CsvTableReader.GetInt(table, i, "Column", 0, errors),
                    Row = CsvTableReader.GetInt(table, i, "Row", 0, errors),
                    Width = CsvTableReader.GetInt(table, i, "Width", 16, errors),
                    Height = CsvTableReader.GetInt(table, i, "Height", 16, errors),
                    X = CsvTableReader.GetInt(table, i, "X", 0, errors),
                    Y = CsvTableReader.GetInt(table, i, "Y", 0, errors),
                    Scale = CsvTableReader.GetInt(table, i, "Scale", 1, errors),
                    FlipX = CsvTableReader.GetInt(table, i, "FlipX", 0, errors) != 0,
                    Layer = CsvTableReader.GetInt(table, i, "Layer", 0, errors),
                    MinLevel = CsvTableReader.GetInt(table, i, "MinLevel", 0, errors),
                    MaxLevel = CsvTableReader.GetInt(table, i, "MaxLevel", int.MaxValue, errors),
                    Color = CsvTableReader.Get(table, i, "Color", "FFFFFF"),
                    Note = CsvTableReader.Get(table, i, "Note")
                };
                if (string.IsNullOrEmpty(part.VisualId) || string.IsNullOrEmpty(part.Sheet))
                    throw new InvalidOperationException($"SpriteCatalog.csv line {i + 2}: visual/sheet required");
                if (part.Scale < 1) throw new InvalidOperationException($"SpriteCatalog.csv line {i + 2}: Scale must be a positive integer");
                if (!Parts.TryGetValue(part.VisualId, out var list)) Parts[part.VisualId] = list = new List<SpritePartInfo>();
                list.Add(part);
            }
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            _loaded = true;
        }

        public static Sprite SpriteFor(SpritePartInfo part)
        {
            string key = $"{part.Sheet}:{part.Column}:{part.Row}:{part.Width}:{part.Height}";
            if (Sprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            Texture2D texture;
            Rect rect;
            if (part.Sheet.StartsWith("Pattern/", StringComparison.Ordinal))
            {
                texture = Pattern(part.Sheet.Substring(8));
                rect = new Rect(0, 0, texture.width, texture.height);
            }
            else
            {
                texture = Resources.Load<Texture2D>("Pixel2D/Sheets/" + part.Sheet);
                if (texture == null) throw new InvalidOperationException("Missing pixel sheet: " + part.Sheet);
                rect = new Rect(part.Column * 16, texture.height - part.Row * 16 - part.Height, part.Width, part.Height);
                if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > texture.width || rect.yMax > texture.height)
                    throw new InvalidOperationException("Sprite is outside sheet: " + key);
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), SourcePixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Sprites[key] = sprite;
            return sprite;
        }

        public static Sprite Get(string id)
        {
            Load();
            if (Parts.TryGetValue(id, out var parts))
            {
                if (IsGround(id)) return Ground(id, parts[0]);
                return id.StartsWith("UI.") && id != "UI.Missing" ? SpriteFor(parts[0]) : PixelSpriteComposer.Compose(id);
            }
            Diagnose(id);
            return PixelSpriteComposer.Compose("UI.Missing");
        }

        public static void Diagnose(string id)
        {
            if (Warned.Add(id ?? "<null>")) Debug.LogWarning("[Pixel2D] Unmapped custom visual: " + id);
        }

        private static bool IsGround(string id) => id.StartsWith("Ground.") || id == "Terrain.Cloud";

        /// <summary>A 16 px ground cell repeated 2x2, so ground shares the composites' 32 px detail grid.</summary>
        private static Sprite Ground(string id, SpritePartInfo part)
        {
            if (Grounds.TryGetValue(id, out var sprite) && sprite != null) return sprite;
            var source = SpriteFor(part);
            int w = (int)source.rect.width, h = (int)source.rect.height;
            var pixels = source.texture.GetPixels((int)source.rect.x, (int)source.rect.y, w, h);
            var tint = ParseColor(part.Color);
            var tiled = new Color[w * h * 4];
            for (int y = 0; y < h * 2; y++)
                for (int x = 0; x < w * 2; x++) tiled[y * w * 2 + x] = pixels[(y % h) * w + x % w] * tint;
            var texture = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, false)
                { name = "Ground_" + id, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(tiled);
            texture.Apply(false, false);
            sprite = Sprite.Create(texture, new Rect(0, 0, w * 2, h * 2), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = id;
            Grounds[id] = sprite;
            return sprite;
        }

        public static GameObject Build(string id, Transform parent, int level = 1, Color? teamColor = null, int order = ObjectOrder)
        {
            Load();
            if (string.IsNullOrEmpty(id)) return null;
            if (IsGround(id)) return Build(id, Get(id), parent, order);
            if (!Parts.ContainsKey(id)) Diagnose(id); // custom CSV IDs remain usable with a visible 2D placeholder
            return Build(id, PixelSpriteComposer.Compose(id, level, teamColor), parent, order);
        }

        /// <summary>One tile composite (see TileVisuals) as a single sorted object.</summary>
        public static GameObject Build(IReadOnlyList<SpritePlacement> placements, Transform parent, int level, Color? teamColor, int order)
        {
            Load();
            if (placements == null || placements.Count == 0) return null;
            foreach (var p in placements) if (!Parts.ContainsKey(p.VisualId)) Diagnose(p.VisualId);
            return Build(placements[0].VisualId, PixelSpriteComposer.Compose(placements, level, teamColor), parent, order);
        }

        private static GameObject Build(string name, Sprite sprite, Transform parent, int order)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<SortingGroup>().sortingOrder = order;
            Add(root.transform, "Pixels", sprite, Vector2.zero, Vector2.one, Color.white, 0).spriteSortPoint = SpriteSortPoint.Pivot;
            return root;
        }

        public static SpriteRenderer Add(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = Material;
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static SpriteRenderer Rectangle(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order) =>
            Add(parent, name, Get("UI.Solid"), position, size, color, order);

        /// <summary>Lower rows draw in front; units use their own band so no tile object covers them.</summary>
        public static int SortOrder(float worldY, bool unit = false) =>
            (unit ? UnitOrder : ObjectOrder) + Mathf.Clamp(Mathf.RoundToInt(-worldY * 16f), -5000, 5000);

        private static Color ParseColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out var color))
                throw new InvalidOperationException("Invalid SpriteCatalog color: " + hex);
            return color;
        }

        private static Texture2D Pattern(string name)
        {
            var text = Resources.Load<TextAsset>("Pixel2D/Patterns/" + name);
            if (text == null) throw new InvalidOperationException("Missing pixel pattern: " + name);
            var lines = text.text.Replace("\r", "").Trim().Split('\n');
            var palette = new Dictionary<char, Color> { ['.'] = Color.clear };
            int row = 0;
            while (row < lines.Length && lines[row].Contains("="))
            {
                palette[lines[row][0]] = ParseColor(lines[row].Substring(2).Trim());
                row++;
            }
            int width = lines[row].Length, height = lines.Length - row;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                if (lines[row + y].Length != width) throw new InvalidOperationException("Nonrectangular pattern: " + name);
                for (int x = 0; x < width; x++) pixels[(height - y - 1) * width + x] = palette[lines[row + y][x]];
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Pixel_" + name, filterMode = FilterMode.Point };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
