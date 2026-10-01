using UnityEngine;
namespace TacticsECS
{
    public static class TerrainFeatureView
    {
        public const string CloudModel = "Terrain.Cloud";
        public static GameObject Create(string tileTypeId, Transform parent, Vector2Int gridPos, float baseHeight)
        {
            if (tileTypeId != "Forest" && tileTypeId != "Mountain" && tileTypeId != "Rock") return null;
            var go = PixelSpriteCatalog.Build("Terrain."+tileTypeId,parent,order:PixelSpriteCatalog.SortOrder(parent.position.y));
            if (tileTypeId == "Forest" && (gridPos.x*73+gridPos.y*151)%2 == 0) go.transform.localScale = new Vector3(-1,1,1);
            return go;
        }
        public static GameObject CreateCloud(Transform parent, Vector2Int gridPos, float baseHeight) => PixelSpriteCatalog.Build(CloudModel,parent,order:PixelSpriteCatalog.FogOrder);
    }
}
