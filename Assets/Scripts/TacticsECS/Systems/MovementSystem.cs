using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 판정/적용을 담당하는 시스템. EntityWorld/GridWorld를 읽고 쓸 뿐, 자체 상태는 없다.
    /// 목적지 통과 가능 여부는 이동하는 엔티티의 IgnoreTerrain/IgnoreUnitBlocking 컴포넌트 값을 따른다.
    /// </summary>
    public static class MovementSystem
    {
        public static bool TryMove(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination)
        {
            if (!UnitQueries.IsAlive(world, unitId) || world.Get<HasMoved>(unitId).Value) return false;
            if (!grid.InBounds(destination)) return false;

            if (!world.Get<IgnoreTerrain>(unitId).Value && !grid.IsWalkable(destination)) return false;

            int occ = grid.GetOccupant(destination);
            if (!world.Get<IgnoreUnitBlocking>(unitId).Value && occ != TileData.NoOccupant && occ != unitId) return false;

            grid.RemoveOccupant(world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new GridPosition { Value = destination });
            world.Set(unitId, new HasMoved { Value = true });
            grid.PlaceOccupant(destination, unitId);

            return true;
        }
    }
}
