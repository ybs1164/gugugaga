using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 타일 개량(건물 건설 + 채집/벌목/화전/숲 조성/건물 파괴)의 배치 판정과 효과 적용을 담당하는 순수 함수형
    /// 시스템. 표는 Data/BuildingDefinition.cs·TileActionDefinition.cs, 해금 여부는 TechSystem.HasUnlock으로
    /// 각 항목의 UnlockKey를 조회한다. 자체 상태는 없다.
    ///
    /// 공통 규칙(폴리토피아 위키 Buildings): 자기 영토 안에서만(도로/다리는 중립 땅·물도 가능), 칸 하나에 건물 하나,
    /// 도시/마을/유적/등대 칸에는 짓지 못한다. 구름(아직 탐험하지 않은 칸)에는 아무것도 못 한다. 인구는 그 칸을 영토로
    /// 가진 도시에 들어가고, 인접 조건은 같은 팀 영토의 칸만 센다. 기념물은 과업(TaskSystem) 달성 시 무료로 한 번,
    /// 신전은 지은 턴을 기록해 레벨(점수)이 오른다(ScoreSystem).
    /// </summary>
    public static class TileImprovementSystem
    {
        // ---------- 조회 ----------

        public static TileClass Classify(GridWorld grid, Vector2Int p)
        {
            var t = grid.GetTile(p);
            if (t.Terrain == TerrainType.Water)
                return t.TileTypeId == TerrainGenerationSystem.OceanTileId ? TileClass.Ocean : TileClass.ShallowWater;
            if (t.TileTypeId == TerrainGenerationSystem.ForestTileId) return TileClass.Forest;
            if (t.TileTypeId == TerrainGenerationSystem.MountainTileId) return TileClass.Mountain;
            return TileClass.Field;
        }

        public static BuildingInfo? FindBuilding(string id)
        {
            foreach (var b in BuildingDefinition.All)
                if (b.Id == id) return b;
            return null;
        }

        public static TileActionInfo? FindAction(string id)
        {
            foreach (var a in TileActionDefinition.All)
                if (a.Id == id) return a;
            return null;
        }

        private static bool Contains(string[] list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value)) return false;
            foreach (var s in list) if (s == value) return true;
            return false;
        }

        /// <summary>건물/유적/등대처럼 개량이 불가능한 구조물이 있는 칸인지(자원은 개량 대상이라 제외).</summary>
        private static bool HasBlockingStructure(GridWorld grid, Vector2Int p)
        {
            var s = grid.GetStructure(p);
            return s == StructureGenerationSystem.CapitalStructureId || s == StructureGenerationSystem.VillageStructureId ||
                   s == StructureGenerationSystem.RuinStructureId || s == StructureGenerationSystem.LighthouseStructureId;
        }

        /// <summary>다리 자리: 상하 또는 좌우 양쪽 이웃이 모두 육지(위키 Bridge — 대각선 불가).</summary>
        public static bool HasOppositeLand(GridWorld grid, Vector2Int p)
        {
            bool Land(Vector2Int q) => grid.InBounds(q) && grid.GetTerrain(q) == TerrainType.Land;
            return (Land(p + Vector2Int.left) && Land(p + Vector2Int.right)) || (Land(p + Vector2Int.up) && Land(p + Vector2Int.down));
        }

        public static bool IsMonument(string buildingId)
        {
            var info = FindBuilding(buildingId);
            return info != null && !string.IsNullOrEmpty(info.Value.TaskId);
        }

        /// <summary>pos 주변(8방향) 같은 팀 영토 칸 중 buildingIds 건물이 있는 칸 수.</summary>
        public static int CountAdjacentBuildings(GridWorld grid, Vector2Int pos, Team team, string[] buildingIds)
        {
            int count = 0;
            foreach (var n in grid.GetNeighbors(pos, true))
            {
                var t = grid.GetTile(n);
                if (t.OwnerTeam == (int)team && Contains(buildingIds, t.BuildingId)) count++;
            }
            return count;
        }

        /// <summary>풍차/대장간/제재소가 지금 만들고 있는 인구(= 인접 기반 건물 수 x 개당 인구).</summary>
        public static int ProcessorPopulation(GridWorld grid, Vector2Int pos, BuildingInfo info, Team team) =>
            info.PopulationPerAdjacent * CountAdjacentBuildings(grid, pos, team, info.AdjacentBuildings);

        /// <summary>모델에 보여줄 건물 레벨(위키: 레벨이 오르면 모양이 바뀐다) — 신전 = 지은 뒤 지난 턴(ScoreSystem.TempleLevel),
        /// 제재소/풍차/대장간 = 인접 기반 건물 수(0이면 위키의 "레벨 0" — 대장간 불 꺼짐), 시장 = 인접 가공 건물 인구 합(최대 8),
        /// 그 밖의 건물 = 1.</summary>
        public static int DisplayLevel(GridWorld grid, Vector2Int pos, int currentTurn)
        {
            var t = grid.GetTile(pos);
            var info = FindBuilding(t.BuildingId);
            if (info == null) return 1;
            var b = info.Value;
            if (b.IsTemple) return ScoreSystem.TempleLevel(currentTurn, t.BuildingTurn);
            if (t.OwnerTeam == TileData.NoOwner) return 1;
            var team = (Team)t.OwnerTeam;
            if (b.PopulationPerAdjacent > 0) return CountAdjacentBuildings(grid, pos, team, b.AdjacentBuildings);
            if (b.ProducesGoldFromAdjacent)
            {
                int level = 0;
                foreach (var n in grid.GetNeighbors(pos, true))
                {
                    var nt = grid.GetTile(n);
                    if (nt.OwnerTeam != (int)team || !Contains(b.AdjacentBuildings, nt.BuildingId)) continue;
                    var ninfo = FindBuilding(nt.BuildingId);
                    if (ninfo != null) level += ProcessorPopulation(grid, n, ninfo.Value, team);
                }
                return Mathf.Min(level, GameRules.City.MarketGoldCap);
            }
            return 1;
        }

        /// <summary>이 칸의 건물이 주인 도시에 주고 있는 인구 총량(고정 + 인접 비례).</summary>
        public static int BuildingPopulation(GridWorld grid, Vector2Int pos)
        {
            var t = grid.GetTile(pos);
            var info = FindBuilding(t.BuildingId);
            if (info == null || t.OwnerTeam == TileData.NoOwner) return 0;
            return info.Value.Population + ProcessorPopulation(grid, pos, info.Value, (Team)t.OwnerTeam);
        }

        /// <summary>팀의 모든 시장이 만드는 골드 합(시장마다 인접 가공 건물 인구 합, 최대 MarketGoldCap).</summary>
        public static int MarketIncome(GridWorld grid, Team team)
        {
            int total = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                var t = grid.GetTile(p);
                if (t.OwnerTeam != (int)team) continue;
                var info = FindBuilding(t.BuildingId);
                if (info == null || !info.Value.ProducesGoldFromAdjacent) continue;

                int gold = 0;
                foreach (var n in grid.GetNeighbors(p, true))
                {
                    var nt = grid.GetTile(n);
                    if (nt.OwnerTeam != (int)team || !Contains(info.Value.AdjacentBuildings, nt.BuildingId)) continue;
                    var ninfo = FindBuilding(nt.BuildingId);
                    if (ninfo != null) gold += ProcessorPopulation(grid, n, ninfo.Value, team);
                }
                total += Mathf.Min(gold, GameRules.City.MarketGoldCap) * GameRules.City.MarketGoldPerLevel; // 위키: 시장 레벨(최대 8)당 별 1
            }
            return total;
        }

        // ---------- 선택지 ----------

        /// <summary>team이 pos에서 할 수 있는 건설/행동 목록. 기술이 없거나 지형/구조물이 안 맞는 항목은 아예
        /// 빼고, 조건은 맞는데 골드/인접 조건이 모자란 항목은 Enabled=false와 이유를 담아 넣는다.</summary>
        public static List<TileOption> GetOptions(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos)
        {
            var options = new List<TileOption>();
            if (econ == null || !grid.InBounds(pos) || !VisionSystem.IsExplored(grid, team, pos)) return options;

            var tile = grid.GetTile(pos);
            bool own = tile.OwnerTeam == (int)team;
            bool neutral = tile.OwnerTeam == TileData.NoOwner;
            var cls = Classify(grid, pos);
            var tech = econ.Tech[team];
            int gold = econ.Resources[team].Gold;
            var hidden = TechSystem.HiddenStructures(econ.TechNodes, tech);
            string structure = hidden.Contains(tile.StructureId) ? string.Empty : tile.StructureId;

            foreach (var b in BuildingDefinition.All)
            {
                if (!string.IsNullOrEmpty(b.UnlockKey) && !TechSystem.HasUnlock(econ.TechNodes, tech, b.UnlockKey)) continue;
                if (!string.IsNullOrEmpty(b.TaskId) && !TaskSystem.CanBuildMonument(econ, team, b.TaskId)) continue;
                if ((b.Terrain & cls) == 0) continue;
                if (!(own || (b.AllowNeutral && neutral)) || HasBlockingStructure(grid, pos)) continue;
                if (b.IsRoad)
                {
                    if (tile.HasRoad) continue;
                }
                else
                {
                    if (!string.IsNullOrEmpty(tile.BuildingId)) continue;
                    if (b.RequiresOppositeLand && !HasOppositeLand(grid, pos)) continue;
                    bool needsResource = b.RequiredStructures != null && b.RequiredStructures.Length > 0;
                    if (needsResource ? !Contains(b.RequiredStructures, structure) : !string.IsNullOrEmpty(structure)) continue;
                }

                string reason = null;
                if (b.AdjacentBuildings != null && b.AdjacentBuildings.Length > 0 &&
                    CountAdjacentBuildings(grid, pos, team, b.AdjacentBuildings) == 0)
                    reason = "인접 조건: " + string.Join("/", NamesOf(b.AdjacentBuildings));
                else if (gold < b.Cost)
                    reason = $"골드 부족 ({gold}/{b.Cost})";

                options.Add(new TileOption { Id = b.Id, IsBuilding = true, Name = b.Name, Cost = b.Cost, Enabled = reason == null, Detail = reason ?? b.Description });
            }

            foreach (var a in TileActionDefinition.All)
            {
                if (!own) continue;
                if (!TechSystem.HasUnlock(econ.TechNodes, tech, a.UnlockKey)) continue;
                if ((a.Terrain & cls) == 0) continue;
                switch (a.Kind)
                {
                    case TileActionKind.Harvest:
                        if (!Contains(a.RequiredStructures, structure) || !string.IsNullOrEmpty(tile.BuildingId)) continue;
                        break;
                    case TileActionKind.ClearForest:
                    case TileActionKind.BurnForest:
                    case TileActionKind.GrowForest:
                        if (!string.IsNullOrEmpty(tile.BuildingId) || HasBlockingStructure(grid, pos)) continue;
                        if (a.Kind == TileActionKind.GrowForest && !string.IsNullOrEmpty(structure)) continue;
                        break;
                    case TileActionKind.Destroy:
                        if (string.IsNullOrEmpty(tile.BuildingId) || IsMonument(tile.BuildingId)) continue;
                        break;
                }

                string reason = gold < a.Cost ? $"골드 부족 ({gold}/{a.Cost})" : null;
                options.Add(new TileOption { Id = a.Id, IsBuilding = false, Name = a.Name, Cost = a.Cost, Enabled = reason == null, Detail = reason ?? a.Description });
            }
            return options;
        }

        private static IEnumerable<string> NamesOf(string[] buildingIds)
        {
            foreach (var id in buildingIds)
            {
                var info = FindBuilding(id);
                yield return info?.Name ?? id;
            }
        }

        // ---------- 실행 ----------

        /// <summary>GetOptions에서 Enabled였던 항목 하나를 실행한다(다시 검증한 뒤 골드 차감 + 효과 적용).
        /// 인구 변화로 도시 레벨이 오르면 log에 LevelUp이 쌓인다. 도로/항구처럼 수도 연결이 바뀔 수 있는 개량은
        /// 곧바로 CitySystem.RefreshConnections까지 돌린다.</summary>
        public static bool Execute(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos, string optionId, List<EconomyLogEntry> log)
        {
            TileOption? chosen = null;
            foreach (var o in GetOptions(grid, econ, team, pos))
                if (o.Id == optionId) chosen = o;
            if (chosen == null || !chosen.Value.Enabled) return false;

            var res = econ.Resources[team];
            res.Gold -= chosen.Value.Cost;
            econ.Resources[team] = res;

            if (chosen.Value.IsBuilding) Build(grid, econ, team, pos, FindBuilding(optionId).Value, log);
            else DoAction(grid, econ, team, pos, FindAction(optionId).Value, log);

            log?.Add(new EconomyLogEntry
            {
                Team = team, Kind = chosen.Value.IsBuilding ? EconomyLogKind.Build : EconomyLogKind.Action,
                Subject = chosen.Value.Name, Position = pos, CityIndex = grid.GetTile(pos).OwnerCity
            });
            return true;
        }

        private static void Build(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos, BuildingInfo b, List<EconomyLogEntry> log)
        {
            var t = grid.GetTile(pos);
            if (b.IsRoad)
            {
                t.HasRoad = true;
                grid.SetTile(pos, t);
                CitySystem.RefreshConnections(grid, econ, log);
                return;
            }

            if (b.RequiredStructures != null && b.RequiredStructures.Length > 0) t.StructureId = string.Empty; // 자원 소모
            t.BuildingId = b.Id;
            t.BuildingTurn = econ.Turn;
            grid.SetTile(pos, t);

            CitySystem.AddPopulation(econ, t.OwnerCity, b.Population + ProcessorPopulation(grid, pos, b, team), log);
            NotifyNeighborProcessors(grid, econ, team, pos, b.Id, +1, log);

            if (!string.IsNullOrEmpty(b.TaskId)) TaskSystem.MarkMonumentBuilt(econ, team, b.TaskId);
            if (b.Id == BuildingDefinition.Port || b.ActsAsRoad) CitySystem.RefreshConnections(grid, econ, log);
        }

        /// <summary>기반 건물(농장/광산/벌목장)이 생기거나 없어질 때, 그 옆의 가공 건물(풍차/대장간/제재소)이
        /// 만드는 인구도 같이 늘거나 준다 — 가공 건물 칸의 주인 도시에 반영한다.</summary>
        private static void NotifyNeighborProcessors(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos, string baseBuildingId, int sign, List<EconomyLogEntry> log)
        {
            foreach (var n in grid.GetNeighbors(pos, true))
            {
                var nt = grid.GetTile(n);
                if (nt.OwnerTeam != (int)team) continue;
                var info = FindBuilding(nt.BuildingId);
                if (info == null || info.Value.PopulationPerAdjacent == 0 || !Contains(info.Value.AdjacentBuildings, baseBuildingId)) continue;
                CitySystem.AddPopulation(econ, nt.OwnerCity, sign * info.Value.PopulationPerAdjacent, log);
            }
        }

        private static void DoAction(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos, TileActionInfo a, List<EconomyLogEntry> log)
        {
            var t = grid.GetTile(pos);
            switch (a.Kind)
            {
                case TileActionKind.Harvest:
                    t.StructureId = string.Empty;
                    grid.SetTile(pos, t);
                    break;
                case TileActionKind.ClearForest:
                    t.TileTypeId = NeighborFieldTileType(grid, pos);
                    grid.SetTile(pos, t);
                    break;
                case TileActionKind.BurnForest:
                    t.TileTypeId = NeighborFieldTileType(grid, pos);
                    t.StructureId = "Resource_Crop";
                    grid.SetTile(pos, t);
                    break;
                case TileActionKind.GrowForest:
                    // 기술이 없어 안 보이는 자원(예: 채집 전의 작물)은 숲이 덮으면서 사라진다 — 숲 + 작물 칸이 생기지 않게.
                    t.TileTypeId = TerrainGenerationSystem.ForestTileId;
                    t.StructureId = string.Empty;
                    grid.SetTile(pos, t);
                    break;
                case TileActionKind.Destroy:
                    DestroyBuilding(grid, econ, team, pos, log);
                    return;
            }

            if (a.Population != 0) CitySystem.AddPopulation(econ, t.OwnerCity, a.Population, log);
            if (a.GoldGain != 0)
            {
                var res = econ.Resources[team];
                res.Gold += a.GoldGain;
                econ.Resources[team] = res;
            }
        }

        private static void DestroyBuilding(GridWorld grid, EconomyWorld econ, Team team, Vector2Int pos, List<EconomyLogEntry> log)
        {
            var t = grid.GetTile(pos);
            var info = FindBuilding(t.BuildingId);
            if (info == null) return;

            int pop = BuildingPopulation(grid, pos);
            t.BuildingId = string.Empty;
            grid.SetTile(pos, t);
            CitySystem.AddPopulation(econ, t.OwnerCity, -pop, log);
            NotifyNeighborProcessors(grid, econ, team, pos, info.Value.Id, -1, log);

            if (info.Value.Id == BuildingDefinition.Port || info.Value.ActsAsRoad) CitySystem.RefreshConnections(grid, econ, log);
        }

        /// <summary>숲을 없앤 뒤 칸에 쓸 평지 타일 Id — 주변 8칸에서 가장 흔한 평지 타입(바이옴에 맞게 초원/사막
        /// 등), 없으면 "Grass".</summary>
        private static string NeighborFieldTileType(GridWorld grid, Vector2Int pos)
        {
            var counts = new Dictionary<string, int>();
            foreach (var n in grid.GetNeighbors(pos, true))
            {
                if (Classify(grid, n) != TileClass.Field) continue;
                var id = grid.GetTileType(n);
                if (string.IsNullOrEmpty(id)) continue;
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
            }
            string best = "Grass";
            int bestCount = 0;
            foreach (var kv in counts)
                if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
            return best;
        }

        /// <summary>팀 영토 안의 모든 칸에 대해 GetOptions를 모은다(AI용).</summary>
        public static List<(Vector2Int Pos, TileOption Option)> GetAllOptions(GridWorld grid, EconomyWorld econ, Team team)
        {
            var all = new List<(Vector2Int, TileOption)>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (grid.GetTile(p).OwnerTeam != (int)team) continue;
                foreach (var o in GetOptions(grid, econ, team, p)) all.Add((p, o));
            }
            return all;
        }
    }
}
