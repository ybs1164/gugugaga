using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 펀치(신앙 포인트를 쓰는 액티브 스킬). 자체 상태 없이 월드를 읽고 쓴다.
    /// 절차: 목록(Available) → 대상 선택 → 검사(Check) → 적용(Apply, 신앙 소모 후 효과). 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class FaithPunchSystem
    {
        /// <summary>팀이 고른 신이 쓸 수 있는 펀치들.</summary>
        public static List<FaithPunchRow> Available(EconomyWorld econ, Team team)
        {
            var list = new List<FaithPunchRow>();
            var god = TemplePassiveSystem.GodOf(econ, team);
            if (god == null || god.Value.Punches == null) return list;
            foreach (int i in god.Value.Punches)
                if (i >= 0 && i < GameTables.FaithPunches.Length) list.Add(GameTables.FaithPunches[i]);
            return list;
        }

        /// <summary>대상 칸 중심 체비쇼프 Range 안의 칸들(맵 안만).</summary>
        public static List<Vector2Int> Area(GridWorld grid, FaithPunchRow punch, Vector2Int center)
        {
            var tiles = new List<Vector2Int>();
            for (int dy = -punch.Range; dy <= punch.Range; dy++)
            for (int dx = -punch.Range; dx <= punch.Range; dx++)
            {
                var p = new Vector2Int(center.x + dx, center.y + dy);
                if (grid.InBounds(p)) tiles.Add(p);
            }
            return tiles;
        }

        /// <summary>대상으로 쓸 수 없으면 이유 문구 키, 쓸 수 있으면 null. 신앙이 모자란 경우도 포함한다.</summary>
        public static string Check(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithPunchRow punch, Vector2Int target)
        {
            if (econ.Resources[team].Faith < punch.Cost) return "UI.FaithPunch.NeedFaith";
            if (!grid.InBounds(target) || (grid.FogEnabled && !VisionSystem.IsExplored(grid, team, target))) return "UI.FaithPunch.NotExplored";
            switch (punch.Kind)
            {
                case FaithPunchKind.Strike:
                    return Targets(grid, world, team, punch, target, enemies: true).Count > 0 ? null : "UI.FaithPunch.NoEnemy";
                case FaithPunchKind.Boost:
                    return Wounded(grid, world, team, punch, target).Count > 0 ? null : "UI.FaithPunch.NoWounded";
                case FaithPunchKind.Resource:
                    return EmptyLand(grid, team, punch, target).Count > 0 ? null : "UI.FaithPunch.NoSpace";
                case FaithPunchKind.Terrain:
                    return CanChangeTerrain(grid, econ, target) ? null : "UI.FaithPunch.TileBlocked";
                case FaithPunchKind.Descend:
                    return MythicSystem.CheckDescend(grid, world, econ, team, target);
                default:
                    return "UI.FaithPunch.TileBlocked";
            }
        }

        /// <summary>효과를 받을 유닛·칸 수(확정 전 미리보기).</summary>
        public static int AffectedCount(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int target)
        {
            switch (punch.Kind)
            {
                case FaithPunchKind.Strike: return Targets(grid, world, team, punch, target, enemies: true).Count;
                case FaithPunchKind.Boost: return Wounded(grid, world, team, punch, target).Count;
                case FaithPunchKind.Resource: return Mathf.Min(punch.Value, EmptyLand(grid, team, punch, target).Count);
                default: return 1;
            }
        }

        /// <summary>신앙을 쓰고 효과를 적용한다. Check가 null이어야 한다.</summary>
        public static FaithPunchResult Apply(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithPunchRow punch, Vector2Int target)
        {
            var result = new FaithPunchResult { UnitIds = new List<int>(), Tiles = new List<Vector2Int>(), SpawnedIds = new List<int>() };
            if (Check(grid, world, econ, team, punch, target) != null) return result;

            var resources = econ.Resources[team];
            resources = FaithSystem.Add(resources, -punch.Cost);
            econ.Resources[team] = resources;
            result.Spent = punch.Cost;
            result.Success = true;

            switch (punch.Kind)
            {
                case FaithPunchKind.Strike:
                    foreach (int id in Targets(grid, world, team, punch, target, enemies: true))
                    {
                        var hp = world.Get<Hp>(id);
                        hp.Value = Mathf.Max(0, hp.Value - punch.Value);
                        world.Set(id, hp);
                        world.Set(id, new Accelerated { Value = false });
                        PrayerSystem.OnHit(world, id);
                        StealthSystem.Reveal(world, id);
                        DiplomacySystem.DeclareWar(econ, team, world.Get<Team>(id));
                        result.UnitIds.Add(id);
                    }
                    break;
                case FaithPunchKind.Boost:
                    foreach (int id in Wounded(grid, world, team, punch, target))
                    {
                        var hp = world.Get<Hp>(id);
                        hp.Value = Mathf.Min(world.Get<MaxHp>(id).Value, hp.Value + punch.Value);
                        world.Set(id, hp);
                        result.UnitIds.Add(id);
                    }
                    break;
                case FaithPunchKind.Resource:
                    var land = EmptyLand(grid, team, punch, target);
                    for (int i = 0; i < land.Count && i < punch.Value; i++)
                    {
                        var tile = grid.GetTile(land[i]);
                        tile.StructureId = punch.Target;
                        grid.SetTile(land[i], tile);
                        result.Tiles.Add(land[i]);
                    }
                    break;
                case FaithPunchKind.Descend:
                    result.SpawnedIds.Add(MythicSystem.Descend(grid, world, econ, team, target, out int origin));
                    result.UnitIds.Add(origin);
                    break;
                case FaithPunchKind.Terrain:
                    ChangeTerrain(grid, econ, team, target);
                    result.Tiles.Add(target);
                    result.TerrainChanged = true;
                    break;
            }
            return result;
        }

        // ---------- 대상 찾기 ----------

        private static List<int> Targets(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int center, bool enemies)
        {
            var ids = new List<int>();
            foreach (var p in Area(grid, punch, center))
            {
                int id = grid.GetOccupant(p);
                if (id == TileData.NoOccupant || !UnitQueries.IsAlive(world, id)) continue;
                var owner = world.Get<Team>(id);
                if ((owner != team) != enemies) continue;
                if (enemies && (StealthSystem.IsHiddenFrom(world, id, team) || DiplomacySystem.HasPeaceTreaty(grid.Economy, team, owner))) continue;
                ids.Add(id);
            }
            return ids;
        }

        private static List<int> Wounded(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int center)
        {
            var ids = Targets(grid, world, team, punch, center, enemies: false);
            ids.RemoveAll(id => world.Get<Hp>(id).Value >= world.Get<MaxHp>(id).Value);
            return ids;
        }

        /// <summary>범위 안 빈 육지 칸(자기 영토나 중립). 대상 칸에서 가까운 순.</summary>
        private static List<Vector2Int> EmptyLand(GridWorld grid, Team team, FaithPunchRow punch, Vector2Int center)
        {
            var list = new List<Vector2Int>();
            foreach (var p in Area(grid, punch, center))
            {
                var tile = grid.GetTile(p);
                if (tile.OwnerTeam != (int)team && tile.OwnerTeam != TileData.NoOwner) continue;
                if (tile.Terrain != TerrainType.Land || !tile.Walkable) continue;
                if (!string.IsNullOrEmpty(tile.StructureId) || !string.IsNullOrEmpty(tile.BuildingId) || tile.OccupantId != TileData.NoOccupant) continue;
                if (grid.FogEnabled && !VisionSystem.IsExplored(grid, team, p)) continue;
                list.Add(p);
            }
            list.Sort((a, b) => Chebyshev(a, center).CompareTo(Chebyshev(b, center)));
            return list;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        // ---------- 지형 변화 ----------

        /// <summary>유닛이 없고 도시 칸이 아니어야 한다.</summary>
        private static bool CanChangeTerrain(GridWorld grid, EconomyWorld econ, Vector2Int p)
        {
            var tile = grid.GetTile(p);
            if (tile.OccupantId != TileData.NoOccupant) return false;
            return CitySystem.FindCityAt(econ, p) < 0;
        }

        /// <summary>땅 → 물 → 산 → 땅. 건물·도로·구조물은 사라진다(건물은 파괴 규칙대로 인구가 줄어든다).</summary>
        private static void ChangeTerrain(GridWorld grid, EconomyWorld econ, Team team, Vector2Int p)
        {
            var log = new List<EconomyLogEntry>(); // 인구 변화로 생기는 도시 로그는 쓰지 않는다
            var tile = grid.GetTile(p);
            if (!string.IsNullOrEmpty(tile.BuildingId)) TileImprovementSystem.DestroyBuilding(grid, econ, team, p, log);
            tile = grid.GetTile(p);
            tile.StructureId = string.Empty;
            tile.HasRoad = false;
            tile.BuildingId = string.Empty;
            if (tile.Terrain == TerrainType.Water) { tile.Terrain = TerrainType.Land; tile.TileTypeId = TerrainGenerationSystem.MountainTileId; }
            else if (tile.TileTypeId == TerrainGenerationSystem.MountainTileId) { tile.Terrain = TerrainType.Land; tile.TileTypeId = LandTileId; }
            else { tile.Terrain = TerrainType.Water; tile.TileTypeId = WaterTileId; }
            grid.SetTile(p, tile);
            CitySystem.RefreshConnections(grid, econ, log);
        }

        public const string LandTileId = "Grass";
        public const string WaterTileId = "Water";
    }
}
