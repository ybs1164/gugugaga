using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛이 지형을 바꾸는 효과 — 침수(Flood 행동·폭우 Drench), 얼음(자동 빙결 AutoFreeze), 얼음 깨기(모든 유닛), 파괴(Raze 이동).
    /// 순수 함수형, 자체 상태 없음(값은 TileData.Flooded/Iced). 규칙: docs/spec/game/tile.md#침수와-얼음, docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    public static class TerrainChangeSystem
    {
        /// <summary>p를 침수시킬 수 있는지: 아직 잠기지 않은 평지·숲(산·물은 안 된다 — 위키 Flood Tile).</summary>
        public static bool CanFloodTile(GridWorld grid, Vector2Int p)
        {
            if (!grid.InBounds(p)) return false;
            var t = grid.GetTile(p);
            if (t.Terrain != TerrainType.Land || t.Flooded) return false;
            var cls = TileImprovementSystem.Classify(grid, p);
            return cls == TileClass.Field || cls == TileClass.Forest;
        }

        /// <summary>p를 침수시킨다. 바뀌었으면 true.</summary>
        public static bool Flood(GridWorld grid, Vector2Int p)
        {
            if (!CanFloodTile(grid, p)) return false;
            var t = grid.GetTile(p);
            t.Flooded = true;
            grid.SetTile(p, t);
            return true;
        }

        /// <summary>침수 행동: 침수(FloodAction) 유닛이 이번 턴 행동 전이고 서 있는 칸을 침수시킬 수 있으면.</summary>
        public static bool CanFloodHere(GridWorld grid, EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && UnitActionQueries.Find<FloodAction>(world, unitId) != null &&
            !world.Get<HasActed>(unitId).Value && !EmbarkSystem.IsEmbarked(world, unitId) &&
            CanFloodTile(grid, world.Get<GridPosition>(unitId).Value);

        /// <summary>서 있는 칸을 침수시키고 행동을 끝낸다.</summary>
        public static bool FloodHere(GridWorld grid, EntityWorld world, int unitId)
        {
            if (!CanFloodHere(grid, world, unitId)) return false;
            Flood(grid, world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }

        /// <summary>폭우(Drench) 유닛이 지금 공격으로 침수시킬 수 있는 빈 칸들(공격 사거리 안, 유닛 없음, 침수 가능).</summary>
        public static List<Vector2Int> DrenchTargets(GridWorld grid, EntityWorld world, int unitId)
        {
            var list = new List<Vector2Int>();
            var attack = UnitActionQueries.Find<AttackAction>(world, unitId);
            if (UnitActionQueries.Find<DrenchAction>(world, unitId) == null || attack == null || !attack.CanExecute(world, unitId)) return list;
            var pos = world.Get<GridPosition>(unitId).Value;
            int range = Mathf.Max(1, world.Get<AttackRange>(unitId).Value);
            for (int dy = -range; dy <= range; dy++)
            for (int dx = -range; dx <= range; dx++)
            {
                var p = pos + new Vector2Int(dx, dy);
                int d = PathfindingSystem.Distance(pos, p);
                if (d < 1 || d > range || !grid.InBounds(p) || grid.IsOccupied(p) || !CanFloodTile(grid, p)) continue;
                list.Add(p);
            }
            return list;
        }

        /// <summary>폭우로 빈 칸을 공격한다: 그 칸이 잠기고 공격 행동을 쓴다.</summary>
        public static bool DrenchTile(GridWorld grid, EntityWorld world, int unitId, Vector2Int p)
        {
            if (!DrenchTargets(grid, world, unitId).Contains(p)) return false;
            StealthSystem.Reveal(world, unitId);
            Flood(grid, p);
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }

        /// <summary>자동 빙결: unitId 주변 8칸의 빈 물 칸을 얼리고, 그 범위의 적 유닛을 빙결시킨다(평화조약 상대 제외).</summary>
        public static void FreezeAround(GridWorld grid, EntityWorld world, int unitId)
        {
            var team = world.Get<Team>(unitId);
            var center = world.Get<GridPosition>(unitId).Value;
            foreach (var p in grid.GetNeighbors(center, true))
            {
                var t = grid.GetTile(p);
                int occ = grid.GetOccupant(p);
                bool occupied = occ != TileData.NoOccupant && UnitQueries.IsAlive(world, occ);
                if (occupied && world.Get<Team>(occ) != team && !DiplomacySystem.HasPeaceTreaty(grid.Economy, team, world.Get<Team>(occ)))
                    world.Set(occ, new Frozen { Value = true });
                if (t.Terrain == TerrainType.Water && !t.Iced && !occupied)
                {
                    t.Iced = true;
                    grid.SetTile(p, t);
                }
            }
        }

        /// <summary>얼음 깨기(위키 Break Ice): 살아 있고 이번 턴 행동 전(이동했다면 돌격 필요)이며 주변 8칸에 빈 얼음 칸이 있으면.</summary>
        public static bool CanBreakIce(GridWorld grid, EntityWorld world, int unitId)
        {
            if (!UnitQueries.IsAlive(world, unitId) || world.Get<HasActed>(unitId).Value) return false;
            if (world.Get<HasMoved>(unitId).Value && UnitActionQueries.Find<ChargeAction>(world, unitId) == null) return false;
            if (UnitActionQueries.Find<AutoFreezeAction>(world, unitId) != null) return false; // 얼리는 유닛은 깨지 않는다(위키)
            foreach (var p in grid.GetNeighbors(world.Get<GridPosition>(unitId).Value, true))
                if (IsBreakableIce(grid, p)) return true;
            return false;
        }

        /// <summary>주변 8칸의 빈 얼음 칸을 모두 물로 되돌리고 행동을 끝낸다. 깬 칸 수.</summary>
        public static int BreakIce(GridWorld grid, EntityWorld world, int unitId)
        {
            if (!CanBreakIce(grid, world, unitId)) return 0;
            int count = 0;
            foreach (var p in grid.GetNeighbors(world.Get<GridPosition>(unitId).Value, true))
            {
                if (!IsBreakableIce(grid, p)) continue;
                var t = grid.GetTile(p);
                t.Iced = false;
                grid.SetTile(p, t);
                count++;
            }
            world.Set(unitId, new HasActed { Value = true });
            return count;
        }

        // 유닛이 서 있는 얼음은 깨지 않는다(육지 유닛이 물 위에 남게 되므로).
        private static bool IsBreakableIce(GridWorld grid, Vector2Int p) => grid.GetTile(p).Iced && !grid.IsOccupied(p);

        /// <summary>파괴(Raze): path 칸들의 건물(다리 제외 — 그 위의 유닛이 물에 빠지므로)과 유적을 없앤다. 도시는 그대로 둔다.</summary>
        public static void Raze(GridWorld grid, EntityWorld world, int unitId, List<Vector2Int> path)
        {
            var team = world.Get<Team>(unitId);
            foreach (var p in path)
            {
                var t = grid.GetTile(p);
                if (!string.IsNullOrEmpty(t.BuildingId) && t.BuildingId != BuildingDefinition.Bridge)
                {
                    if (grid.Economy != null)
                        TileImprovementSystem.DestroyBuilding(grid, grid.Economy, t.OwnerTeam == TileData.NoOwner ? team : (Team)t.OwnerTeam, p, null);
                    else { t.BuildingId = string.Empty; grid.SetTile(p, t); }
                }
                if (grid.GetStructure(p) == StructureGenerationSystem.RuinStructureId) grid.SetStructure(p, string.Empty);
            }
        }
    }
}
