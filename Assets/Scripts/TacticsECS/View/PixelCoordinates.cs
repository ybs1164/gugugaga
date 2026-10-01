using UnityEngine;

namespace TacticsECS
{
    /// <summary>Keep simulation XZ coordinates unchanged; project only the view into XY.</summary>
    public static class PixelCoordinates
    {
        public static Vector3 FromLogical(Vector3 position) => new Vector3(position.x, position.z, 0f);

        public static Vector3 GridToWorld(GridWorld grid, Vector2Int position) => FromLogical(grid.GridToWorld(position));

        public static bool TryScreenToGrid(Camera camera, GridWorld grid, Vector2 screen, out Vector2Int position)
        {
            position = default;
            var plane = new Plane(Vector3.forward, FromLogical(grid.Origin));
            var ray = camera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) return false;
            var world = ray.GetPoint(distance);
            // Logical cells are centred on integer coordinates. Half-open edges avoid banker's rounding.
            position = new Vector2Int(
                Mathf.FloorToInt((world.x - grid.Origin.x) / grid.TileSize + 0.5f),
                Mathf.FloorToInt((world.y - grid.Origin.z) / grid.TileSize + 0.5f));
            return grid.InBounds(position);
        }
    }
}
