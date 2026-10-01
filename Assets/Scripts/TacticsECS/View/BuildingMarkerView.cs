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
            if (level > 1) AddLevelPips(go.transform,level,teamColor ?? new Color(.94f,.8f,.42f));
            return go;
        }
        public static GameObject CreateCity(CityData city, Color teamColor, Transform parent, float baseHeight)
        {
            var root = CreateCityBase(teamColor,city.Level,parent,baseHeight);
            PixelSpriteCatalog.Build("City.Houses",root.transform,order:0);
            if (city.Level >= 3)
            {
                var extra = PixelSpriteCatalog.Build("City.Houses",root.transform,order:1);
                extra.transform.localPosition = new Vector3(-.26f,.08f,0); extra.transform.localScale = Vector3.one*.65f;
            }
            if (city.IsCapital) PixelSpriteCatalog.Build("City.Capital",root.transform,order:2);
            if (city.HasWall) PixelSpriteCatalog.Build("City.Wall",root.transform,order:3);
            if (city.HasWorkshop) PixelSpriteCatalog.Build("City.Workshop",root.transform,order:4);
            if (city.ParkCount > 0) PixelSpriteCatalog.Build("City.Park",root.transform,order:5);
            PixelSpriteCatalog.Build("City.Flag",root.transform,teamColor:teamColor,order:6);
            root.name = "City"; return root;
        }
        public static GameObject CreateCityBase(Color teamColor, int level, Transform parent, float baseHeight)
        {
            var root = new GameObject("CityBase"); root.transform.SetParent(parent,false);
            root.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = PixelSpriteCatalog.SortOrder(root.transform.position.y);
            AddLevelPips(root.transform,level,teamColor); return root;
        }
        private static void AddLevelPips(Transform parent, int level, Color color)
        {
            for (int i = 0; i < Mathf.Min(level,8); i++) PixelSpriteCatalog.Rectangle(parent,"Level",new Vector2(-.4375f+i*.125f,-.40625f),
                new Vector2(.0625f,.0625f),color,PixelSpriteCatalog.OverlayOrder-10);
        }
        public static void AddBorders(Transform parent, IEnumerable<Vector2Int> edges, Color teamColor)
        {
            foreach (var e in edges) PixelSpriteCatalog.Rectangle(parent,"Border",new Vector2(e.x,e.y)*.46875f,
                e.y != 0 ? new Vector2(1,.0625f) : new Vector2(.0625f,1),teamColor,PixelSpriteCatalog.OverlayOrder-20);
        }
    }
}
