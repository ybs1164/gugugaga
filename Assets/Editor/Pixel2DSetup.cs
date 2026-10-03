using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace TacticsECS.EditorTools
{
    /// <summary>Run through Unity CLI. Preserve stats, actions, prefab GUIDs, fonts and scene wiring.</summary>
    public static class Pixel2DSetup
    {
        public static void Run()
        {
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles("Assets/Resources/Pixel2D/Sheets", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.isReadable = true; // Native 16px composition samples packed CC0 parts once, then caches.
                string sheet = Path.GetFileNameWithoutExtension(path);
                if (sheet == "Panel" || sheet == "Button" || sheet == "Badge")
                    importer.spriteBorder = Vector4.one * PixelUIScaler.FrameBorder; // scripts/author_pixel_ui.py
                importer.SaveAndReimport();
            }
            string materialPath = "Assets/Resources/Pixel2D/PixelUnlit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")) { name = "PixelUnlit" }, materialPath);

            // Persist generated sprites used by authored prefabs. Runtime tilemaps still share packed sheets.
            Directory.CreateDirectory("Assets/Art/Pixel2D/Generated");
            AssetDatabase.Refresh();
            foreach (string id in PixelSpriteCatalog.VisualIds)
            foreach (var part in PixelSpriteCatalog.Layers(id))
            {
                var sprite = PixelSpriteCatalog.SpriteFor(part);
                if (AssetDatabase.Contains(sprite)) continue;
                string path = "Assets/Art/Pixel2D/Generated/" + sprite.name.Replace('/', '_').Replace(':', '_') + ".asset";
                AssetDatabase.CreateAsset(sprite, path);
                if (!AssetDatabase.Contains(sprite.texture)) AssetDatabase.AddObjectToAsset(sprite.texture, sprite);
            }
            // Composites and 32px ground tiles referenced by the structure/tile prefabs saved below.
            foreach (string id in PixelSpriteCatalog.VisualIds)
            {
                if (id.StartsWith("UI.") || id == "Terrain.Cloud") continue;
                var sprite = PixelSpriteCatalog.Get(id);
                string path = "Assets/Art/Pixel2D/Generated/" + sprite.name.Replace('.', '_') + ".asset";
                AssetDatabase.CreateAsset(sprite,path);
                AssetDatabase.AddObjectToAsset(sprite.texture,sprite);
            }
            ConvertHpDisplay();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Units" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ClearChildren(root.transform);
                    var definition = root.GetComponent<UnitDefinition>();
                    var serialized = new SerializedObject(definition);
                    serialized.FindProperty("bodyTexture").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Structures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).Replace("Structure_", "");
                string id = name.StartsWith("Resource") ? "Resource_" + name.Substring("Resource".Length).TrimStart('_') : name;
                SaveVisualPrefab("Structure." + id, path);
            }
            SaveVisualPrefab("Ground.Grass", "Assets/Prefabs/Tiles/Tile_Land.prefab");
            SaveVisualPrefab("Ground.Water", "Assets/Prefabs/Tiles/Tile_Water.prefab");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { PixelUISkin.Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Sandbox.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    camera.orthographic = true;
                    camera.transform.SetPositionAndRotation(new Vector3(4,4,-20), Quaternion.identity);
                    camera.allowHDR = false; camera.allowMSAA = false;
                    var data = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null) data.renderPostProcessing = false;
                }
                EditorSceneManager.SaveScene(scene);
            }
            // Resources would otherwise package unused 512px legacy icons into every 2D player.
            const string legacy = "Assets/Art/GameIcons/Resources/Icons";
            if (AssetDatabase.IsValidFolder(legacy))
            {
                string error = AssetDatabase.MoveAsset(legacy, "Assets/Art/GameIcons/LegacyIcons");
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Pixel2DSetup] DONE: CC0 sheets, sprites, prefabs, UI and both scenes migrated");
        }
        private static void ClearChildren(Transform root)
        {
            while (root.childCount > 0) UnityEngine.Object.DestroyImmediate(root.GetChild(0).gameObject);
        }
        private static void SaveVisualPrefab(string id, string path)
        {
            if (!PixelSpriteCatalog.Has(id)) throw new InvalidOperationException("Unmapped prefab: " + id);
            var root = PixelSpriteCatalog.Build(id, null);
            root.name = Path.GetFileNameWithoutExtension(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }
        private static void ConvertHpDisplay()
        {
            const string path = "Assets/Prefabs/UI/HpDisplay.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (string name in new[] { "Bar_Bg", "Bar_Fill" })
                {
                    var bar = root.transform.Find(name);
                    var oldRenderer = bar.GetComponent<MeshRenderer>();
                    if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);
                    var mesh = bar.GetComponent<MeshFilter>();
                    if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                    var renderer = bar.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = bar.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = PixelSpriteCatalog.Get("UI.Solid");
                    renderer.sharedMaterial = PixelSpriteCatalog.Material;
                    renderer.color = name == "Bar_Bg" ? new Color(.12f,.17f,.23f) : Color.green;
                    renderer.sortingOrder = name == "Bar_Bg" ? 20 : 21;
                    bar.localPosition = new Vector3(0,UnitView.HpBarLocalY,0);
                    bar.localScale = new Vector3(UnitView.HpBarSize.x,UnitView.HpBarSize.y,1);
                }
                var number = root.transform.Find("Number");
                number.localPosition = new Vector3(0,UnitView.HpNumberLocalY,0);
                number.localScale = Vector3.one*.11f;
                number.GetComponent<Renderer>().sortingOrder = 22;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
