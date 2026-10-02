using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld(값만 가진 데이터)의 칸 조회·수정 로직. 확장 메서드라 호출은 grid.GetTile(p) 형태 그대로다.
    /// </summary>
    public static class GridQueries
    {
        public static bool InBounds(this GridWorld grid, Vector2Int p) =>
            p.x >= 0 && p.y >= 0 && p.x < grid.Width && p.y < grid.Height;

        public static int Index(this GridWorld grid, Vector2Int p) => p.y * grid.Width + p.x;

        public static TileData GetTile(this GridWorld grid, Vector2Int p) => grid.Tiles[grid.Index(p)];

        public static void SetTile(this GridWorld grid, Vector2Int p, TileData data) => grid.Tiles[grid.Index(p)] = data;

        public static Vector3 GridToWorld(this GridWorld grid, Vector2Int p) =>
            grid.Origin + new Vector3(p.x * grid.TileSize, 0f, p.y * grid.TileSize);

        public static bool IsWalkable(this GridWorld grid, Vector2Int p) => grid.InBounds(p) && grid.GetTile(p).Walkable;

        public static TerrainType GetTerrain(this GridWorld grid, Vector2Int p) => grid.GetTile(p).Terrain;

        public static void SetTerrain(this GridWorld grid, Vector2Int p, TerrainType terrain)
        {
            var t = grid.GetTile(p);
            t.Terrain = terrain;
            grid.SetTile(p, t);
        }

        public static string GetTileType(this GridWorld grid, Vector2Int p) => grid.GetTile(p).TileTypeId;

        public static void SetTileType(this GridWorld grid, Vector2Int p, string tileTypeId)
        {
            var t = grid.GetTile(p);
            t.TileTypeId = tileTypeId;
            grid.SetTile(p, t);
        }

        public static string GetStructure(this GridWorld grid, Vector2Int p) => grid.GetTile(p).StructureId;

        public static void SetStructure(this GridWorld grid, Vector2Int p, string structureId)
        {
            var t = grid.GetTile(p);
            t.StructureId = structureId;
            grid.SetTile(p, t);
        }

        public static bool IsOccupied(this GridWorld grid, Vector2Int p) =>
            grid.InBounds(p) && grid.GetTile(p).OccupantId != TileData.NoOccupant;

        public static int GetOccupant(this GridWorld grid, Vector2Int p) =>
            grid.InBounds(p) ? grid.GetTile(p).OccupantId : TileData.NoOccupant;

        public static void PlaceOccupant(this GridWorld grid, Vector2Int p, int unitId)
        {
            var t = grid.GetTile(p);
            t.OccupantId = unitId;
            grid.SetTile(p, t);
        }

        public static void RemoveOccupant(this GridWorld grid, Vector2Int p)
        {
            var t = grid.GetTile(p);
            t.OccupantId = TileData.NoOccupant;
            grid.SetTile(p, t);
        }

        /// <summary>allowDiagonal이 true면 8방향, false면 상하좌우 4방향 이웃 타일을 반환한다.</summary>
        public static IEnumerable<Vector2Int> GetNeighbors(this GridWorld grid, Vector2Int p, bool allowDiagonal)
        {
            foreach (var d in allowDiagonal ? GridWorld.Dir8 : GridWorld.Dir4)
            {
                var n = p + d;
                if (grid.InBounds(n)) yield return n;
            }
        }
    }
}
