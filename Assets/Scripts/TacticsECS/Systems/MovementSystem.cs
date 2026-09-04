using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 판정/적용을 담당하는 시스템. UnitData/GridWorld를 읽고 쓸 뿐, 자체 상태는 없다.
    /// </summary>
    public static class MovementSystem
    {
        public static bool TryMove(GridWorld grid, UnitWorld units, int unitId, Vector2Int destination)
        {
            var unit = units.Get(unitId);
            if (!unit.IsAlive || unit.HasMoved) return false;
            if (!grid.InBounds(destination) || !grid.IsWalkable(destination)) return false;

            int occ = grid.GetOccupant(destination);
            if (occ != TileData.NoOccupant && occ != unitId) return false;

            grid.RemoveOccupant(unit.GridPos);
            unit.GridPos = destination;
            unit.HasMoved = true;
            grid.PlaceOccupant(destination, unitId);

            units.Set(unitId, unit);
            return true;
        }
    }
}
