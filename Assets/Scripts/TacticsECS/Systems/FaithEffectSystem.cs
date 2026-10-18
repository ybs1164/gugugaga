using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 효과(FaithEffects.csv) 하나의 대상 찾기와 적용. 신전 패시브(TemplePassiveSystem)와 신앙 펀치(FaithPunchSystem)가 같이 쓴다.
    /// 대상은 중심 칸에서 체비쇼프 radius 안에서 찾는다. 자체 상태는 없다. 규칙: docs/spec/csv/faith-effects.md
    /// </summary>
    public static class FaithEffectSystem
    {
        public const string WaterTileId = "Water";

        public static bool UsableInPunch(FaithEffectKind kind) =>
            kind == FaithEffectKind.Damage || kind == FaithEffectKind.Heal || kind == FaithEffectKind.Structure ||
            kind == FaithEffectKind.Terrain || kind == FaithEffectKind.Descend;

        public static bool UsableInPassive(FaithEffectKind kind) =>
            kind != FaithEffectKind.Terrain && kind != FaithEffectKind.Descend;

        /// <summary>신전 패시브에서 턴 시작에 한 번 발동하는 유형(나머지는 늘 켜져 있다).</summary>
        public static bool IsTurnStart(FaithEffectKind kind) =>
            kind == FaithEffectKind.Damage || kind == FaithEffectKind.Heal || kind == FaithEffectKind.Structure;

        /// <summary>범위 밖 Index는 null.</summary>
        public static FaithEffectRow? Get(int index) =>
            index >= 0 && index < GameTables.FaithEffects.Length ? GameTables.FaithEffects[index] : (FaithEffectRow?)null;

        public static List<FaithEffectRow> Rows(int[] indices)
        {
            var list = new List<FaithEffectRow>();
            foreach (int i in indices ?? new int[0])
                if (Get(i) is FaithEffectRow e) list.Add(e);
            return list;
        }

        /// <summary>효과 크기 = Amount + PerPrayer × prayers, 0 미만이면 0.</summary>
        public static float Strength(FaithEffectRow e, int prayers) => Mathf.Max(0f, e.Amount + e.PerPrayer * prayers);

        public static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        /// <summary>중심 칸에서 체비쇼프 radius 안의 칸들(맵 안만).</summary>
        public static List<Vector2Int> Area(GridWorld grid, Vector2Int center, int radius)
        {
            var tiles = new List<Vector2Int>();
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var p = new Vector2Int(center.x + dx, center.y + dy);
                if (grid.InBounds(p)) tiles.Add(p);
            }
            return tiles;
        }

        // ---------- 대상 찾기 ----------

        /// <summary>유닛 효과(Damage·Heal·Stat·GuardStat)를 받을 유닛. Side는 team 기준. Heal은 다친 유닛만, Damage는 MinHp보다 체력이 많은 유닛만.</summary>
        public static List<int> Units(GridWorld grid, EntityWorld world, Team team, FaithEffectRow e, Vector2Int center, int radius)
        {
            var ids = new List<int>();
            foreach (var p in Area(grid, center, radius))
            {
                int id = grid.GetOccupant(p);
                if (id == TileData.NoOccupant || !UnitQueries.IsAlive(world, id)) continue;
                var owner = world.Get<Team>(id);
                if ((owner == team) != (e.Side == FaithSide.Ally)) continue;
                if (e.SkipHidden && owner != team && StealthSystem.IsHiddenFrom(world, id, team)) continue;
                if (e.SkipPeace && owner != team && DiplomacySystem.HasPeaceTreaty(grid.Economy, team, owner)) continue;
                if (e.Effect == FaithEffectKind.Heal && world.Get<Hp>(id).Value >= world.Get<MaxHp>(id).Value) continue;
                if (e.Effect == FaithEffectKind.Damage && world.Get<Hp>(id).Value <= e.MinHp) continue;
                ids.Add(id);
            }
            return ids;
        }

        /// <summary>Structure 효과를 받을 빈 육지 칸(Territory에 맞는 영토, 탐험한 칸). 중심에서 가까운 순이고, rotate만큼 시작 위치를 돌린다
        /// (신전 패시브가 턴마다 같은 칸만 쓰지 않게 턴 번호를 넘긴다).</summary>
        public static List<Vector2Int> StructureTiles(GridWorld grid, Team team, FaithEffectRow e, Vector2Int center, int radius, int rotate = 0)
        {
            var list = new List<Vector2Int>();
            foreach (var p in Area(grid, center, radius))
            {
                var tile = grid.GetTile(p);
                bool own = tile.OwnerTeam == (int)team, neutral = tile.OwnerTeam == TileData.NoOwner;
                if (e.Territory == FaithTerritory.Own && !own) continue;
                if (e.Territory == FaithTerritory.OwnOrNeutral && !own && !neutral) continue;
                if (tile.Terrain != TerrainType.Land || !tile.Walkable) continue;
                if (!string.IsNullOrEmpty(tile.StructureId) || !string.IsNullOrEmpty(tile.BuildingId) || tile.OccupantId != TileData.NoOccupant) continue;
                if (grid.FogEnabled && !VisionSystem.IsExplored(grid, team, p)) continue;
                list.Add(p);
            }
            if (list.Count > 1 && rotate != 0)
            {
                int k = ((rotate % list.Count) + list.Count) % list.Count;
                list.AddRange(list.GetRange(0, k));
                list.RemoveRange(0, k);
            }
            var order = new List<Vector2Int>(list);
            list.Sort((a, b) =>
            {
                int d = Chebyshev(a, center).CompareTo(Chebyshev(b, center));
                return d != 0 ? d : order.IndexOf(a).CompareTo(order.IndexOf(b)); // 같은 거리는 돌린 순서 유지
            });
            return list;
        }

        public static FaithTerrain TerrainOf(GridWorld grid, Vector2Int p)
        {
            var tile = grid.GetTile(p);
            if (tile.Terrain == TerrainType.Water) return FaithTerrain.Water;
            return tile.TileTypeId == TerrainGenerationSystem.MountainTileId ? FaithTerrain.Mountain : FaithTerrain.Land;
        }

        /// <summary>Terrain 효과: 칸이 FromTerrain이고, 유닛이 없고 도시 칸이 아니어야 한다.</summary>
        public static bool CanChangeTerrain(GridWorld grid, EconomyWorld econ, FaithEffectRow e, Vector2Int p)
        {
            if (grid.GetTile(p).OccupantId != TileData.NoOccupant || CitySystem.FindCityAt(econ, p) >= 0) return false;
            return TerrainOf(grid, p) == e.FromTerrain && e.FromTerrain != e.ToTerrain;
        }

        // ---------- 적용 ----------

        /// <summary>피해: 체력을 MinHp 아래로 깎지 않는다. 0이 되면 칸을 비운다. 기도·은신은 피격으로 풀리고, CountsAsAttack이면 공격으로 친다.
        /// 체력이 실제로 줄어든 유닛을 hit에 더한다.</summary>
        public static void Damage(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithEffectRow e, int amount, List<int> ids, List<int> hit)
        {
            if (amount <= 0) return;
            foreach (int id in ids)
            {
                var hp = world.Get<Hp>(id);
                int next = Mathf.Max(e.MinHp, hp.Value - amount);
                if (next >= hp.Value) continue;
                hp.Value = Mathf.Max(0, next);
                world.Set(id, hp);
                if (hp.Value <= 0) grid.RemoveOccupant(world.Get<GridPosition>(id).Value); // 처치된 유닛이 칸을 막고 있으면 안 된다
                PrayerSystem.OnHit(world, id);
                StealthSystem.OnHit(world, id);
                if (e.CountsAsAttack)
                {
                    world.Set(id, new Accelerated { Value = false });
                    StealthSystem.Reveal(world, id);
                    DiplomacySystem.DeclareWar(econ, team, world.Get<Team>(id));
                }
                hit.Add(id);
            }
        }

        public static void Heal(EntityWorld world, int amount, List<int> ids, List<int> healed)
        {
            if (amount <= 0) return;
            foreach (int id in ids)
            {
                var hp = world.Get<Hp>(id);
                int next = Mathf.Min(world.Get<MaxHp>(id).Value, hp.Value + amount);
                if (next <= hp.Value) continue;
                hp.Value = next;
                world.Set(id, hp);
                healed.Add(id);
            }
        }

        /// <summary>앞에서부터 count칸에 TargetId 구조물을 둔다. 쓴 칸을 placed에 더한다.</summary>
        public static void SpawnStructures(GridWorld grid, FaithEffectRow e, List<Vector2Int> tiles, int count, List<Vector2Int> placed)
        {
            if (string.IsNullOrEmpty(e.TargetId)) return;
            for (int i = 0; i < tiles.Count && i < count; i++)
            {
                var tile = grid.GetTile(tiles[i]);
                tile.StructureId = e.TargetId;
                grid.SetTile(tiles[i], tile);
                placed.Add(tiles[i]);
            }
        }

        /// <summary>칸을 ToTerrain으로 바꾼다. 건물·도로·구조물은 사라진다(건물은 파괴 규칙대로 인구가 줄어든다). 물에서 육지가 되면 맨땅은 이웃 평지를 따른다.</summary>
        public static void ChangeTerrain(GridWorld grid, EconomyWorld econ, Team team, FaithEffectRow e, Vector2Int p)
        {
            var log = new List<EconomyLogEntry>(); // 인구 변화로 생기는 도시 로그는 쓰지 않는다
            var tile = grid.GetTile(p);
            if (!string.IsNullOrEmpty(tile.BuildingId)) TileImprovementSystem.DestroyBuilding(grid, econ, team, p, log);
            tile = grid.GetTile(p);
            tile.StructureId = string.Empty;
            tile.HasRoad = false;
            tile.BuildingId = string.Empty;
            bool wasWater = tile.Terrain == TerrainType.Water;
            if (wasWater) tile.GroundTileId = TileImprovementSystem.NeighborFieldTileType(grid, p);
            switch (e.ToTerrain)
            {
                case FaithTerrain.Water:
                    tile.Terrain = TerrainType.Water;
                    tile.TileTypeId = WaterTileId;
                    break;
                case FaithTerrain.Mountain:
                    tile.Terrain = TerrainType.Land;
                    tile.TileTypeId = TerrainGenerationSystem.MountainTileId;
                    break;
                default:
                    tile.Terrain = TerrainType.Land;
                    tile.TileTypeId = string.IsNullOrEmpty(tile.GroundTileId) ? TileImprovementSystem.NeighborFieldTileType(grid, p) : tile.GroundTileId;
                    break;
            }
            grid.SetTile(p, tile);
            CitySystem.RefreshConnections(grid, econ, log);
        }
    }
}
