using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// What one tile shows, as a single composite in draw order. Rules: docs/spec/csv/sprites.md#타일-표시-우선순위
    /// Offsets are detail pixels (PixelSpriteComposer.Tile per tile).
    /// </summary>
    public static class TileVisuals
    {
        public const int FeatureBackX = -3, FeatureBackY = 3, StructureFrontX = 7, StructureFrontY = -6;

        public static string Feature(string tileTypeId) =>
            tileTypeId == "Forest" || tileTypeId == "Mountain" || tileTypeId == "Rock" ? "Terrain." + tileTypeId : null;

        /// <summary>City &gt; building &gt; (feature behind + structure in front). Null when the tile has no object.</summary>
        public static List<SpritePlacement> For(Vector2Int pos, string tileTypeId, string structureId, string buildingId, CityData? city)
        {
            if (city.HasValue) return City(city.Value);
            if (!string.IsNullOrEmpty(buildingId)) return new List<SpritePlacement> { new SpritePlacement(BuildingMarkerView.BuildingModelPrefix + buildingId) };
            string feature = Feature(tileTypeId);
            bool flip = feature == "Terrain.Forest" && (pos.x * 73 + pos.y * 151) % 2 == 0; // vary repeated forests
            bool hasStructure = !string.IsNullOrEmpty(structureId);
            if (feature != null && hasStructure)
                return new List<SpritePlacement>
                {
                    new SpritePlacement(feature, FeatureBackX, FeatureBackY, flip),
                    new SpritePlacement("Structure." + structureId, StructureFrontX, StructureFrontY)
                };
            if (hasStructure) return new List<SpritePlacement> { new SpritePlacement("Structure." + structureId) };
            if (feature != null) return new List<SpritePlacement> { new SpritePlacement(feature, 0, 0, flip) };
            return null;
        }

        /// <summary>Houses grow with the composite level (MinLevel rows); upgrades are added in front.</summary>
        public static List<SpritePlacement> City(CityData city)
        {
            var list = new List<SpritePlacement> { new SpritePlacement("City.Houses") };
            if (city.IsCapital) list.Add(new SpritePlacement("City.Capital"));
            if (city.HasWall) list.Add(new SpritePlacement("City.Wall"));
            if (city.HasWorkshop) list.Add(new SpritePlacement("City.Workshop"));
            if (city.ParkCount > 0) list.Add(new SpritePlacement("City.Park"));
            if (city.HasEmbassy) list.Add(new SpritePlacement("City.Embassy"));
            list.Add(new SpritePlacement("City.Flag"));
            return list;
        }
    }
}
