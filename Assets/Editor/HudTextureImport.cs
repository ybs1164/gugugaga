using UnityEditor;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>Assets/Resources/Hud 아래 그림은 도트 그대로 쓴다: 크기 유지, 점 필터, 압축·밉맵 없음.</summary>
    public class HudTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Hud/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
