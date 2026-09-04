using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 판정/적용을 담당하는 시스템. UnitWorld/GridWorld를 읽고 쓸 뿐, 자체 상태는 없다.
    /// 목적지 통과 가능 여부는 이동하는 유닛의 IgnoreTerrain/IgnoreUnitBlocking 값을 따른다.
    /// </summary>
    public static class MovementSystem
    {
        public static bool TryMove(GridWorld grid, UnitWorld units, int unitId, Vector2Int destination)
        {
            if (!units.IsAlive(unitId) || units.GetHasMoved(unitId)) return false;
            if (!grid.InBounds(destination)) return false;

            if (!units.GetIgnoreTerrain(unitId) && !grid.IsWalkable(destination)) return false;

            int occ = grid.GetOccupant(destination);
            if (!units.GetIgnoreUnitBlocking(unitId) && occ != TileData.NoOccupant && occ != unitId) return false;

            grid.RemoveOccupant(units.GetGridPos(unitId));
            units.SetGridPos(unitId, destination);
            units.SetHasMoved(unitId, true);
            grid.PlaceOccupant(destination, unitId);

            return true;
        }
    }
}
