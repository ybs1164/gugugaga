using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 판정/적용을 담당하는 시스템. UnitData/UnitStats/GridWorld를 읽고 쓸 뿐, 자체 상태는 없다.
    /// 목적지 통과 가능 여부는 유닛의 UnitStats.Movement(UnitMovement) 값을 따른다.
    /// </summary>
    public static class MovementSystem
    {
        public static bool TryMove(GridWorld grid, UnitWorld units, int unitId, Vector2Int destination)
        {
            var unit = units.Get(unitId);
            if (!unit.IsAlive || unit.TurnState.HasMoved) return false;
            if (!grid.InBounds(destination)) return false;

            var movement = units.GetStats(unitId).Movement;
            if (!movement.IgnoreTerrain && !grid.IsWalkable(destination)) return false;

            int occ = grid.GetOccupant(destination);
            if (!movement.IgnoreUnitBlocking && occ != TileData.NoOccupant && occ != unitId) return false;

            grid.RemoveOccupant(unit.GridPos);
            unit.GridPos = destination;
            unit.TurnState.HasMoved = true;
            grid.PlaceOccupant(destination, unitId);

            units.Set(unitId, unit);
            return true;
        }
    }
}
