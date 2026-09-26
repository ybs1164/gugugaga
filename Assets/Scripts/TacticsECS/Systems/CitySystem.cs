using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 도시(창설/점령/영토/인구/레벨업 보상/수입/수도 연결/유닛 훈련 조건)를 담당하는 순수 함수형 시스템 —
    /// 폴리토피아 위키 City/Population 문서 규칙. 자체 상태는 없고 GridWorld/EntityWorld/EconomyWorld를 인자로
    /// 받아 읽고 쓴다.
    ///
    /// 주요 규칙:
    ///   - 도시는 반경 1(3x3)의 영토를 갖는다. 이미 다른 도시 영토인 칸은 빼앗지 않는다.
    ///   - 레벨 L -> L+1에 인구 L+1이 필요하고, 남는 인구는 이월된다. 레벨업마다 2지선다 보상 하나.
    ///   - 골드 수입 = 도시마다 (레벨 + 공방 + 공원 + 수도 1 + 음수 인구) (0 미만 불가) + 시장. 적 유닛이 도시
    ///     칸에 서 있으면(포위) 그 도시는 수입이 없다.
    ///   - 팀의 유닛 수용량(인구 상한) = 도시마다 (레벨 + 1)의 합.
    ///   - 마을/적 도시는 그 칸에서 턴을 시작한(=이번 턴 아직 이동/행동하지 않은) 유닛이 점령한다. 점령은 그
    ///     유닛의 턴을 끝낸다.
    ///   - 도시를 한 번이라도 가졌던 팀이 도시를 전부 잃으면 패배.
    /// </summary>
    public static class CitySystem
    {
        public const int DefaultBorderRadius = 1;
        public const int MarketGoldCap = 8;
        public const int CityDefenseBonus = 1;
        public const int WallDefenseBonus = 3;

        public static readonly Team[] Teams = { Team.Player, Team.Enemy };

        // ---------- 조회 ----------

        public static int FindCityAt(EconomyWorld econ, Vector2Int pos)
        {
            if (econ == null) return -1;
            for (int i = 0; i < econ.Cities.Count; i++)
                if (econ.Cities[i].Position == pos) return i;
            return -1;
        }

        public static int CountCities(EconomyWorld econ, Team team)
        {
            int count = 0;
            foreach (var c in econ.Cities)
                if (c.Owner == team) count++;
            return count;
        }

        public static int FindCapital(EconomyWorld econ, Team team)
        {
            for (int i = 0; i < econ.Cities.Count; i++)
                if (econ.Cities[i].Owner == team && econ.Cities[i].IsCapital) return i;
            return -1;
        }

        /// <summary>수도/마을 구조물이 있는 칸(주인 유무와 무관).</summary>
        public static bool IsSettlementTile(GridWorld grid, Vector2Int p)
        {
            var s = grid.GetStructure(p);
            return s == StructureGenerationSystem.CapitalStructureId || s == StructureGenerationSystem.VillageStructureId;
        }

        public static bool IsOwnTerritory(GridWorld grid, Team team, Vector2Int p) =>
            grid.InBounds(p) && grid.GetTile(p).OwnerTeam == (int)team;

        public static bool HasLost(EconomyWorld econ, Team team) =>
            econ != null && econ.HadCity.Contains(team) && CountCities(econ, team) == 0;

        public static int UnitCapacity(EconomyWorld econ, Team team)
        {
            int cap = 0;
            foreach (var c in econ.Cities)
                if (c.Owner == team) cap += c.Level + 1;
            return cap;
        }

        /// <summary>econ.RandomCounter를 하나 올리며 [0, maxExclusive) 정수를 만든다(상태 없는 결정적 난수).</summary>
        public static int NextRandom(EconomyWorld econ, int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            unchecked
            {
                uint x = (uint)(econ.RandomCounter++ * 747796405 + 2891336453);
                x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (int)(x % (uint)maxExclusive);
            }
        }

        // ---------- 창설/영토/점령 ----------

        public static int FoundCity(GridWorld grid, EconomyWorld econ, Vector2Int pos, Team owner, bool isCapital, string name)
        {
            econ.Cities.Add(new CityData
            {
                Name = name,
                Position = pos,
                Owner = owner,
                IsCapital = isCapital,
                Level = 1,
                Population = 0,
                BorderRadius = DefaultBorderRadius,
            });
            econ.HadCity.Add(owner);
            int index = econ.Cities.Count - 1;

            // 도시 칸 자체는 항상 이 도시 소유(다른 도시 영토 안에 있던 마을도 점령하면 자기 칸은 가져온다).
            var t = grid.GetTile(pos);
            t.OwnerCity = index;
            t.OwnerTeam = (int)owner;
            t.BuildingId = string.Empty;
            grid.SetTile(pos, t);

            ClaimTerritory(grid, econ, index);
            return index;
        }

        /// <summary>도시 반경 안의 중립 칸을 영토로 편입한다(국경 확장 때 다시 불러 넓힌다).</summary>
        public static void ClaimTerritory(GridWorld grid, EconomyWorld econ, int cityIndex)
        {
            var city = econ.Cities[cityIndex];
            int r = city.BorderRadius;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                var p = city.Position + new Vector2Int(dx, dy);
                if (!grid.InBounds(p)) continue;
                var t = grid.GetTile(p);
                if (t.OwnerCity != TileData.NoOwner) continue;
                t.OwnerCity = cityIndex;
                t.OwnerTeam = (int)city.Owner;
                grid.SetTile(p, t);
            }
        }

        /// <summary>수도가 없는 팀에 수도를 정해준다(전투 시작 시 1회). 팀 유닛 무게중심에서 가장 가까운 아직
        /// 주인 없는 "Capital" 구조물을 쓰고, 지형 생성을 안 해서 수도 구조물이 없으면 무게중심에서 가장 가까운
        /// 빈 육지 칸에 수도 구조물을 새로 놓는다. 주인이 정해지지 않은 나머지 수도 구조물은 마을처럼
        /// 점령 가능한 중립 정착지로 남는다.</summary>
        public static void InitializeCapitals(GridWorld grid, EntityWorld world, EconomyWorld econ)
        {
            foreach (var team in Teams)
            {
                if (FindCapital(econ, team) >= 0) continue;

                var sum = Vector2.zero;
                int n = 0;
                for (int i = 0; i < world.EntityCount; i++)
                {
                    if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                    sum += (Vector2)world.Get<GridPosition>(i).Value;
                    n++;
                }
                if (n == 0) continue;
                var centroid = sum / n;

                Vector2Int? best = null;
                float bestDist = float.MaxValue;
                for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (grid.GetStructure(p) != StructureGenerationSystem.CapitalStructureId || FindCityAt(econ, p) >= 0) continue;
                    float d = (centroid - (Vector2)p).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = p; }
                }

                if (best == null)
                {
                    for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var p = new Vector2Int(x, y);
                        if (grid.GetTerrain(p) != TerrainType.Land || !string.IsNullOrEmpty(grid.GetStructure(p))) continue;
                        if (grid.GetTileType(p) == TerrainGenerationSystem.MountainTileId || FindCityAt(econ, p) >= 0) continue;
                        if (grid.GetTile(p).OwnerCity != TileData.NoOwner) continue;
                        float d = (centroid - (Vector2)p).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; best = p; }
                    }
                    if (best == null) continue;
                    grid.SetStructure(best.Value, StructureGenerationSystem.CapitalStructureId);
                }

                FoundCity(grid, econ, best.Value, team, true, team == Team.Player ? "아군 수도" : "적 수도");
            }
            RefreshConnections(grid, econ, null);
        }

        /// <summary>unitId가 지금 서 있는 칸을 점령할 수 있는지: 살아있고, 이번 턴 아직 이동/행동하지 않았고
        /// (= 그 칸에서 턴을 시작함), 칸이 마을/수도 구조물이며 자기 팀 도시가 아니다.</summary>
        public static bool CanCapture(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId)
        {
            if (econ == null || !UnitQueries.IsAlive(world, unitId)) return false;
            if (world.Get<HasMoved>(unitId).Value || world.Get<HasActed>(unitId).Value) return false;
            var pos = world.Get<GridPosition>(unitId).Value;
            if (!IsSettlementTile(grid, pos)) return false;
            int city = FindCityAt(econ, pos);
            return city < 0 || econ.Cities[city].Owner != world.Get<Team>(unitId);
        }

        public static int Capture(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, List<EconomyLogEntry> log)
        {
            if (!CanCapture(grid, world, econ, unitId)) return -1;
            var team = world.Get<Team>(unitId);
            var pos = world.Get<GridPosition>(unitId).Value;

            int index = FindCityAt(econ, pos);
            if (index < 0)
            {
                index = FoundCity(grid, econ, pos, team, false, $"도시 {econ.Cities.Count + 1}");
            }
            else
            {
                var city = econ.Cities[index];
                city.Owner = team;
                city.IsCapital = false; // 피점령 수도는 일반 도시 취급
                city.ConnectedToCapital = false;
                econ.Cities[index] = city;
                econ.HadCity.Add(team);
                for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new Vector2Int(x, y);
                    var t = grid.GetTile(p);
                    if (t.OwnerCity != index) continue;
                    t.OwnerTeam = (int)team;
                    grid.SetTile(p, t);
                }
            }

            world.Set(unitId, new HasMoved { Value = true });
            world.Set(unitId, new HasActed { Value = true });
            log?.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Capture, Subject = econ.Cities[index].Name, Position = pos, CityIndex = index });
            RefreshConnections(grid, econ, log);
            return index;
        }

        // ---------- 인구/레벨업/보상 ----------

        public static void AddPopulation(EconomyWorld econ, int cityIndex, int amount, List<EconomyLogEntry> log)
        {
            if (cityIndex < 0 || cityIndex >= econ.Cities.Count || amount == 0) return;
            var city = econ.Cities[cityIndex];
            city.Population += amount;
            while (city.Population >= city.Level + 1)
            {
                city.Population -= city.Level + 1;
                city.Level++;
                city.PendingRewards++;
                log?.Add(new EconomyLogEntry { Team = city.Owner, Kind = EconomyLogKind.LevelUp, Subject = city.Name, Position = city.Position, CityIndex = cityIndex });
            }
            econ.Cities[cityIndex] = city;
        }

        /// <summary>지금 고를 차례인 보상이 어느 레벨의 보상인지(가장 오래된 미선택 레벨부터).</summary>
        public static int PendingRewardLevel(CityData city) => city.Level - city.PendingRewards + 1;

        public static CityRewardType[] RewardOptions(int level)
        {
            var table = CityRewardDefinition.OptionsByLevel;
            int i = Mathf.Clamp(level - 2, 0, table.Length - 1);
            return table[i];
        }

        public static string RewardName(CityRewardType type)
        {
            foreach (var info in CityRewardDefinition.Info)
                if (info.Type == type) return info.Name;
            return type.ToString();
        }

        public static string RewardDescription(CityRewardType type)
        {
            foreach (var info in CityRewardDefinition.Info)
                if (info.Type == type) return info.Description;
            return string.Empty;
        }

        public static bool ApplyReward(GridWorld grid, EconomyWorld econ, int cityIndex, CityRewardType reward, List<EconomyLogEntry> log)
        {
            var city = econ.Cities[cityIndex];
            if (city.PendingRewards <= 0) return false;
            if (System.Array.IndexOf(RewardOptions(PendingRewardLevel(city)), reward) < 0) return false;

            city.PendingRewards--;
            var res = econ.Resources[city.Owner];
            var entry = new EconomyLogEntry { Team = city.Owner, Kind = EconomyLogKind.Reward, Subject = RewardName(reward), Position = city.Position, CityIndex = cityIndex };

            switch (reward)
            {
                case CityRewardType.Workshop: city.HasWorkshop = true; break;
                case CityRewardType.Explorer: res.Development += CityRewardDefinition.ExplorerDevelopment; break;
                case CityRewardType.CityWall: city.HasWall = true; break;
                case CityRewardType.Resources: res.Gold += CityRewardDefinition.ResourcesGold; break;
                case CityRewardType.Park: city.ParkCount++; break;
                case CityRewardType.BorderGrowth: city.BorderRadius = CityRewardDefinition.BorderGrowthRadius; break;
                case CityRewardType.SuperUnit: entry.SpawnUnitId = StrongestUnitId(econ); break;
            }
            econ.Cities[cityIndex] = city;
            econ.Resources[city.Owner] = res;
            log?.Add(entry);

            if (reward == CityRewardType.BorderGrowth) ClaimTerritory(grid, econ, cityIndex);
            if (reward == CityRewardType.PopulationGrowth) AddPopulation(econ, cityIndex, CityRewardDefinition.PopulationGrowthAmount, log);
            return true;
        }

        /// <summary>훈련 가능한 유닛 CSV 중 최대 체력이 가장 높은 유닛(폴리토피아 "거인" 대체).</summary>
        public static string StrongestUnitId(EconomyWorld econ)
        {
            UnitCsvRow best = null;
            foreach (var row in econ.UnitRows)
                if (best == null || row.MaxHp > best.MaxHp) best = row;
            return best?.Id ?? string.Empty;
        }

        // ---------- 수입 ----------

        public static bool IsBesieged(GridWorld grid, EntityWorld world, CityData city)
        {
            int occ = grid.GetOccupant(city.Position);
            return occ != TileData.NoOccupant && UnitQueries.IsAlive(world, occ) && world.Get<Team>(occ) != city.Owner;
        }

        public static int CityGoldIncome(GridWorld grid, EntityWorld world, CityData city)
        {
            if (IsBesieged(grid, world, city)) return 0;
            int income = city.Level + (city.HasWorkshop ? 1 : 0) + city.ParkCount + (city.IsCapital ? 1 : 0);
            if (city.Population < 0) income += city.Population;
            return Mathf.Max(0, income);
        }

        public static int GoldIncome(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            int total = 0;
            foreach (var city in econ.Cities)
                if (city.Owner == team) total += CityGoldIncome(grid, world, city);
            return total + TileImprovementSystem.MarketIncome(grid, team);
        }

        /// <summary>발전도(기술 연구 자원) 수입 = 도시 수 + 수도와 연결된 도시 수 + 수도 1.</summary>
        public static int DevelopmentIncome(EconomyWorld econ, Team team)
        {
            int total = 0;
            foreach (var city in econ.Cities)
            {
                if (city.Owner != team) continue;
                total += 1 + (city.ConnectedToCapital ? 1 : 0) + (city.IsCapital ? 1 : 0);
            }
            return total;
        }

        // ---------- 수도 연결 ----------

        /// <summary>각 팀 수도에서 도로/도시/항구(+항구 사이 바다)를 따라 8방향으로 퍼져나가 닿는 자기 팀 도시를
        /// "연결됨"으로 표시한다. 적 영토의 도로는 지나가지 못한다. 연결 상태가 바뀐 도시마다 그 도시와 수도에
        /// 인구 +1/-1(폴리토피아 City Connections).</summary>
        public static void RefreshConnections(GridWorld grid, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            foreach (var team in Teams)
            {
                int capital = FindCapital(econ, team);
                var reached = new HashSet<Vector2Int>();
                if (capital >= 0)
                {
                    var start = econ.Cities[capital].Position;
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(start);
                    reached.Add(start);
                    while (queue.Count > 0)
                    {
                        var cur = queue.Dequeue();
                        bool curIsPort = IsOwnPort(grid, team, cur);
                        bool curIsOpenWater = !curIsPort && grid.GetTerrain(cur) == TerrainType.Water;
                        foreach (var next in grid.GetNeighbors(cur, true))
                        {
                            if (reached.Contains(next)) continue;
                            if (!IsConnectionNode(grid, econ, team, next, curIsPort, curIsOpenWater)) continue;
                            reached.Add(next);
                            queue.Enqueue(next);
                        }
                    }
                }

                for (int i = 0; i < econ.Cities.Count; i++)
                {
                    var city = econ.Cities[i];
                    if (city.Owner != team) continue;
                    bool connected = !city.IsCapital && capital >= 0 && reached.Contains(city.Position);
                    if (connected == city.ConnectedToCapital) continue;
                    city.ConnectedToCapital = connected;
                    econ.Cities[i] = city;
                    int delta = connected ? 1 : -1;
                    AddPopulation(econ, i, delta, log);
                    if (capital >= 0) AddPopulation(econ, capital, delta, log);
                }
            }
        }

        private static bool IsOwnPort(GridWorld grid, Team team, Vector2Int p) =>
            grid.GetTile(p).BuildingId == BuildingDefinition.Port && grid.GetTile(p).OwnerTeam == (int)team;

        /// <summary>육로(도시/도로)에서는 도시·도로·자기 항구로, 항구에서는 그에 더해 바다로, 바다에서는 바다와
        /// 자기 항구로만 이어진다 — 항구 없이 해안 도로로 바로 올라오지 못하게.</summary>
        private static bool IsConnectionNode(GridWorld grid, EconomyWorld econ, Team team, Vector2Int p, bool fromPort, bool fromOpenWater)
        {
            var t = grid.GetTile(p);
            if (IsOwnPort(grid, team, p)) return true;
            if (t.Terrain == TerrainType.Water) return (fromPort || fromOpenWater) && string.IsNullOrEmpty(t.BuildingId);
            if (fromOpenWater) return false;

            int city = FindCityAt(econ, p);
            if (city >= 0) return econ.Cities[city].Owner == team;
            bool enemyLand = t.OwnerTeam != TileData.NoOwner && t.OwnerTeam != (int)team;
            return t.HasRoad && !enemyLand;
        }

        // ---------- 유닛 훈련 ----------

        /// <summary>이 도시에서 row 유닛을 훈련할 수 있는지와, 못 한다면 그 이유.</summary>
        public static bool CanTrain(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int cityIndex, UnitCsvRow row, out string reason)
        {
            reason = string.Empty;
            var city = econ.Cities[cityIndex];
            if (city.Owner != team) { reason = "우리 도시가 아님"; return false; }
            if (!TechSystem.CanTrainUnitType(econ.TechNodes, econ.Tech[team], row.Id)) { reason = "기술 필요"; return false; }
            if (grid.IsOccupied(city.Position)) { reason = "도시 칸이 비어있지 않음"; return false; }
            if (CityResourceSystem.CountPopulation(world, team) >= UnitCapacity(econ, team)) { reason = "인구 상한"; return false; }
            if (econ.Resources[team].Gold < row.Cost) { reason = $"골드 부족 ({econ.Resources[team].Gold}/{row.Cost})"; return false; }
            return true;
        }

        /// <summary>CanTrain이 참일 때 골드만 차감한다 — 실제 스폰(프리팹/View 필요)은 호출자(BattleController)가
        /// 이어서 하고, 스폰된 유닛은 폴리토피아처럼 그 턴에는 움직일 수 없게 HasMoved/HasActed를 세운다.</summary>
        public static bool PayForTraining(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int cityIndex, UnitCsvRow row)
        {
            if (!CanTrain(grid, world, econ, team, cityIndex, row, out _)) return false;
            var res = econ.Resources[team];
            res.Gold -= row.Cost;
            econ.Resources[team] = res;
            return true;
        }

        public static UnitCsvRow FindUnitRow(EconomyWorld econ, string unitId)
        {
            foreach (var row in econ.UnitRows)
                if (row.Id == unitId) return row;
            return null;
        }
    }
}
