using System.Collections.Generic;
using UnityEngine;
namespace TacticsECS
{
    public static class BuildingMarkerView
    {
        public const string BuildingModelPrefix = "Building.";
        public static GameObject CreateBuilding(string buildingId, Transform parent, float baseHeight, int level = 1, bool rotate90 = false, Color? teamColor = null)
        {
            if (string.IsNullOrEmpty(buildingId)) return null;
            var go = PixelSpriteCatalog.Build(BuildingModelPrefix+buildingId,parent,level,teamColor,PixelSpriteCatalog.SortOrder(parent == null ? 0 : parent.position.y));
            if (rotate90) go.transform.localRotation = Quaternion.Euler(0,0,90);
            return go;
        }
        public static GameObject CreateCity(CityData city, Color teamColor, Transform parent, float baseHeight) =>
            PixelSpriteCatalog.Build(TileVisuals.City(city),parent,city.Level,teamColor,PixelSpriteCatalog.SortOrder(parent == null ? 0 : parent.position.y));
        /// <summary>Level pips along the tile's bottom edge, drawn above objects so tall art never hides them.</summary>
        public static void AddLevelPips(Transform parent, int level, Color color)
        {
            for (int i = 0; i < Mathf.Min(level,8); i++)
            {
                var pip = new Vector2(-.4375f+i*.125f,-.4375f);
                PixelSpriteCatalog.Rectangle(parent,"LevelStroke",pip,new Vector2(.125f,.125f),PixelSpriteComposer.Outline,PixelSpriteCatalog.OverlayOrder-11);
                PixelSpriteCatalog.Rectangle(parent,"Level",pip,new Vector2(.0625f,.0625f),color,PixelSpriteCatalog.OverlayOrder-10);
            }
        }
        public static void AddBorders(Transform parent, IEnumerable<Vector2Int> edges, Color teamColor)
        {
            foreach (var e in edges) PixelSpriteCatalog.Rectangle(parent,"Border",new Vector2(e.x,e.y)*.46875f,
                e.y != 0 ? new Vector2(1,.0625f) : new Vector2(.0625f,1),teamColor,PixelSpriteCatalog.OverlayOrder-20);
        }
    }
}
