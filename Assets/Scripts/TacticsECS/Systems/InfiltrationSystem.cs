using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 침투(위키 Cloak "Infiltrate") — 순수 함수형, 자체 상태 없음.
    ///   조건: 침투(InfiltrateAction) 유닛이 살아 있고 이번 턴 행동 전이며, 이동했다면 돌격(Charge)이 있고 이번 턴을 숨은 채 시작했어야 한다
    ///   (위키: "Hide와 Dash가 충돌 — 턴을 이미 숨은 채 시작했을 때만 이동 후 침투"). 대상은 상하좌우로 인접한 다른 팀 도시이고, 포위된
    ///   도시(도시 칸에 도시 주인이 아닌 유닛)나 이미 침투당한 도시(주인의 다음 턴까지)는 안 된다.
    ///   효과(위키 순서):
    ///     1. 침투 유닛은 소모된다.
    ///     2. 도시 칸에 있던 적 유닛은 침투 유닛의 공격력만큼 피해(침투 유닛의 체력과 무관 — 원문 "at full health").
    ///     3. 도시 레벨만큼(최대 5) Dagger가 침투한 팀 소속으로 그 도시 영토에 나타난다 — 도시 칸 → 방어 보너스 칸(침투 팀 기술 기준:
    ///        산=Defense.Mountain, 숲=Defense.Forest) → 나머지 육지 → 물(Pirate) 순, 같은 순위면 도시에 가까운 칸. 침투 팀이 들어갈 수 없는
    ///        지형(산/깊은 바다 기술)에는 안 나온다. 다음 턴까지 행동할 수 없다. Dagger는 독립(소속 도시 없음).
    ///     4. 침투한 팀이 그 도시의 별 수입만큼 즉시 받고, 도시는 주인의 다음 턴에 별을 만들지 않는다(CityData.Infiltrated).
    ///   침투는 공격이 아니다(평화주의 과업에 영향 없음). Dagger 행(GameRules.Infiltration.DaggerUnitId)이 유닛 CSV에 없으면 소환은 건너뛴다.
    /// </summary>
    public static class InfiltrationSystem
    {
        public static bool CanInfiltrate(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, Vector2Int cityPos)
        {
            if (econ == null || !UnitQueries.IsAlive(world, unitId)) return false;
            if (UnitActionQueries.Find<InfiltrateAction>(world, unitId) == null) return false;
            if (world.Get<HasActed>(unitId).Value) return false;
            if (world.Get<HasMoved>(unitId).Value &&
                (UnitActionQueries.Find<ChargeAction>(world, unitId) == null || !world.GetOrDefault<Hidden>(unitId).AtTurnStart)) return false;
            if (PathfindingSystem.Distance(world.Get<GridPosition>(unitId).Value, cityPos) != 1) return false;
            int city = CitySystem.FindCityAt(econ, cityPos);
            if (city < 0) return false;
            var c = econ.Cities[city];
            if (c.Owner == world.Get<Team>(unitId) || c.Infiltrated) return false;
            if (DiplomacySystem.HasPeaceTreaty(econ, c.Owner, world.Get<Team>(unitId))) return false;
            if (CitySystem.IsBesieged(grid, world, c)) return false;
            return true;
        }

        /// <summary>unitId가 지금 침투할 수 있는 도시 칸들.</summary>
        public static List<Vector2Int> Targets(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId)
        {
            var list = new List<Vector2Int>();
            if (econ == null) return list;
            foreach (var c in econ.Cities)
                if (CanInfiltrate(grid, world, econ, unitId, c.Position)) list.Add(c.Position);
            return list;
        }

        /// <summary>침투한다. spawned에 새로 나타난 Dagger/Pirate 엔티티 id를 담는다(View는 호출자가 붙인다).</summary>
        public static bool Infiltrate(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, Vector2Int cityPos, List<int> spawned, List<EconomyLogEntry> log)
        {
            if (!CanInfiltrate(grid, world, econ, unitId, cityPos)) return false;
            var team = world.Get<Team>(unitId);
            int cityIndex = CitySystem.FindCityAt(econ, cityPos);
            var city = econ.Cities[cityIndex];
            float strength = world.Get<Attack>(unitId).Value;
            int stolen = CitySystem.CityStarsIncome(grid, world, city);

            // 1. 침투 유닛 소모.
            grid.RemoveOccupant(world.Get<GridPosition>(unitId).Value);
            world.Set(unitId, new Hp { Value = 0 });

            // 2. 도시 칸의 적 유닛 피해.
            int defender = grid.GetOccupant(cityPos);
            if (defender != TileData.NoOccupant && UnitQueries.IsAlive(world, defender) && world.Get<Team>(defender) != team)
            {
                var hp = world.Get<Hp>(defender);
                hp.Value = Mathf.Max(0, hp.Value - Mathf.RoundToInt(strength));
                world.Set(defender, hp);
                if (!UnitQueries.IsAlive(world, defender)) grid.RemoveOccupant(cityPos);
            }

            // 3. Dagger 소환.
            int count = Mathf.Min(city.Level, GameRules.Infiltration.MaxDaggers);
            var row = CitySystem.FindUnitRow(econ, GameRules.Infiltration.DaggerUnitId);
            int made = 0;
            if (row != null)
            {
                foreach (var spot in SpawnSpots(grid, econ, team, cityIndex))
                {
                    if (made >= count) break;
                    if (grid.IsOccupied(spot)) continue;
                    int id = UnitFactorySystem.CreateFromCsv(grid, world, team, row, spot);
                    world.Set(id, new HasMoved { Value = true });
                    world.Set(id, new HasActed { Value = true });
                    CitySystem.AssignHome(world, econ, id, HomeCity.None); // 위키 Dagger: Independent
                    if (grid.GetTerrain(spot) == TerrainType.Water)
                        EmbarkSystem.EmbarkAs(world, id, world.GetOrDefault<PortBoat>(id).NavalUnitId); // 물이면 Pirate
                    spawned?.Add(id);
                    made++;
                }
            }

            // 4. 수입 탈취 + 다음 턴 수입 없음.
            var res = econ.Resources[team];
            res.Stars += stolen;
            econ.Resources[team] = res;
            city.Infiltrated = true;
            econ.Cities[cityIndex] = city;

            log?.Add(new EconomyLogEntry
            {
                Team = team, Kind = EconomyLogKind.Action, Position = cityPos, CityIndex = cityIndex,
                Subject = LocalizationSystem.F("UI.Event.Infiltration", city.Name, LocalizationSystem.Name(UnitCsvSerializer.UnitStringTable, GameRules.Infiltration.DaggerUnitId), made, stolen)
            });
            return true;
        }

        /// <summary>Dagger가 나타날 칸 후보(위키 우선순위): 도시 칸, 방어 보너스 육지, 나머지 육지, 물. 침투 팀이 못 들어가는 지형은 뺀다.</summary>
        private static List<Vector2Int> SpawnSpots(GridWorld grid, EconomyWorld econ, Team team, int cityIndex)
        {
            var city = econ.Cities[cityIndex];
            var tech = econ.Tech[team];
            var nodes = econ.TechNodes;
            bool mountainOk = !TechSystem.IsKeyGated(nodes, "Move.Mountain") || TechSystem.HasUnlock(nodes, tech, "Move.Mountain");
            bool oceanOk = !TechSystem.IsKeyGated(nodes, "Move.Ocean") || TechSystem.HasUnlock(nodes, tech, "Move.Ocean");
            var ranked = new List<(int Rank, int Dist, int Y, int X, Vector2Int Pos)>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (grid.GetTile(p).OwnerCity != cityIndex || !grid.IsWalkable(p)) continue;
                var cls = TileImprovementSystem.Classify(grid, p);
                if (cls == TileClass.Mountain && !mountainOk) continue;
                if (cls == TileClass.Ocean && !oceanOk) continue;
                int rank;
                if (p == city.Position) rank = 0;
                else if (cls == TileClass.ShallowWater || cls == TileClass.Ocean) rank = 3;
                else if ((cls == TileClass.Mountain && TechSystem.HasUnlock(nodes, tech, "Defense.Mountain")) ||
                         (cls == TileClass.Forest && TechSystem.HasUnlock(nodes, tech, "Defense.Forest"))) rank = 1;
                else rank = 2;
                int dist = Mathf.Max(Mathf.Abs(x - city.Position.x), Mathf.Abs(y - city.Position.y));
                ranked.Add((rank, dist, y, x, p));
            }
            ranked.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return ranked.ConvertAll(r => r.Pos);
        }
    }
}
