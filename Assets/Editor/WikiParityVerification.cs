using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    public static class WikiParityVerification
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WikiParityVerification] " + message);
        }

        private static GridWorld Grid()
        {
            var grid = new GridWorld(12, 12);
            for (int y = 0; y < grid.Height; y++) for (int x = 0; x < grid.Width; x++)
                grid.SetTileType(new Vector2Int(x, y), "Grass");
            return grid;
        }

        private static EconomyWorld Economy()
        {
            var econ = new EconomyWorld { TechNodes = GameDataLoader.LoadTechNodes(), UnitRows = GameTables.Units.ToList() };
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Stars = 100 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            TaskSystem.Init(econ);
            return econ;
        }

        private static int Unit(GridWorld grid, EntityWorld world, Team team, string id, int x, int y) =>
            UnitFactorySystem.CreateFromCsv(grid, world, team, GameTables.Units.First(u => u.Id == id), new Vector2Int(x, y));
        private static void Ready(EntityWorld world, int id)
        {
            world.Set(id, new HasMoved { Value = false });
            world.Set(id, new HasActed { Value = false });
        }

        public static void Run()
        {
            Verify();
            Debug.Log("[WikiParityVerification] ALL PASS: capital start, Juggernaut embark/move/disembark, embassy lifecycle, temple score");
        }

        public static void VerifyAndBuild()
        {
            bool failed = false;
            Application.LogCallback track = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) failed = true;
            };
            Application.logMessageReceived += track;
            try
            {
                VerificationSuite.Run();
                Require(!failed, "full verification suite failed; build cancelled");
                Pixel2DVerification.Run();
                Require(!failed, "render verification failed; build cancelled");
                Pixel2DBuild.Run();
            }
            finally { Application.logMessageReceived -= track; }
        }

        public static void Verify()
        {
            Require(GameDataLoader.LoadAll().Count == 0, "CSV load errors");
            VerifyCapital();
            VerifyJuggernaut();
            VerifyEmbassy();
            VerifyTemple();
        }

        private static void VerifyCapital()
        {
            var grid = Grid(); var world = new EntityWorld(); var econ = Economy();
            TribeSystem.Apply(econ, Team.Player, GameTables.Tribes.First(t => t.Id == "Luxidoor"), econ.UnitRows);
            int i = CitySystem.FoundCity(grid, econ, new Vector2Int(2, 2), Team.Player, true, "Capital");
            var city = econ.Cities[i];
            Require(city.Level == 3 && !city.HasWall && !city.HasWorkshop && city.PendingRewards == 0, "Luxidoor starting rewards");
            Require(econ.Resources[Team.Player].Stars == 2 && CitySystem.StarsIncome(grid, world, econ, Team.Player) == 4, "Luxidoor resources/income");
            int village = CitySystem.FoundCity(grid, econ, new Vector2Int(7, 7), Team.Player, false, "Village");
            Require(econ.Cities[village].Level == 1, "start level must not affect later captures");
            var text = ArrayTableCsvSerializer.WriteTribes(GameTables.Tribes);
            Require(ArrayTableCsvSerializer.ParseTribes(text).First(t => t.Id == "Luxidoor").StartCapitalLevel == 3, "tribe round trip");
        }

        private static void VerifyJuggernaut()
        {
            var grid = Grid(); var world = new EntityWorld(); var econ = Economy(); grid.Economy = econ;
            var port = new Vector2Int(3, 3);
            grid.SetTerrain(port, TerrainType.Water); grid.SetTileType(port, "Water");
            var tile = grid.GetTile(port); tile.OwnerTeam = (int)Team.Player; tile.BuildingId = BuildingDefinition.Port; grid.SetTile(port, tile);
            int giant = Unit(grid, world, Team.Player, "giant", 2, 3);
            int enemy = Unit(grid, world, Team.Enemy, "infantry", 4, 4); // diagonal must be included
            int ally = Unit(grid, world, Team.Player, "infantry", 3, 4);
            Require(MovementSystem.TryMove(grid, world, giant, port), "giant embark");
            Require(EmbarkSystem.NavalUnitId(world, giant) == "juggernaut" && world.Get<Attack>(giant).Value == 4 && world.Get<Defense>(giant).Value == 4, "juggernaut form");
            Require(world.Get<Hp>(giant).Value == 40 && world.Get<Hp>(enemy).Value == 4 && world.Get<Hp>(ally).Value == 10, "embark stomp, no retaliation, no friendly fire");
            Require(!EmbarkSystem.CanUpgrade(grid, world, econ, giant, "bomber", out _), "special boats cannot upgrade");
            Ready(world, giant);
            var water = new Vector2Int(4, 3); grid.SetTerrain(water, TerrainType.Water); grid.SetTileType(water, "Water");
            Require(MovementSystem.TryMove(grid, world, giant, water), "juggernaut move");
            Require(!UnitQueries.IsAlive(world, enemy) && grid.GetOccupant(new Vector2Int(4, 4)) == TileData.NoOccupant && econ.Tasks[Team.Player].Kills == 1, "stomp death/capacity/task cleanup");
            int other = Unit(grid, world, Team.Enemy, "infantry", 6, 4);
            Ready(world, giant);
            Require(MovementSystem.TryMove(grid, world, giant, new Vector2Int(5, 3)), "juggernaut disembark");
            Require(!EmbarkSystem.IsEmbarked(world, giant) && world.Get<Attack>(giant).Value == 5 && world.Get<Hp>(other).Value == 4, "disembark uses boat damage then restores giant");
            Ready(world, giant);
            Require(MovementSystem.TryMove(grid, world, giant, new Vector2Int(5, 4)) && world.Get<Hp>(other).Value == 4, "land giant has no stomp");
            Require(PixelSpriteCatalog.Has("Boat.juggernaut"), "juggernaut visual");
        }

        private static void VerifyEmbassy()
        {
            var grid = Grid(); var world = new EntityWorld(); var econ = Economy();
            int own = CitySystem.FoundCity(grid, econ, new Vector2Int(2, 2), Team.Player, true, "Own");
            int other = CitySystem.FoundCity(grid, econ, new Vector2Int(8, 8), Team.Enemy, true, "Other");
            var pos = econ.Cities[other].Position;
            Require(!TileImprovementSystem.GetOptions(grid, econ, Team.Player, pos).Any(o => o.Id == "Embassy"), "embassy requires diplomacy");
            econ.Tech[Team.Player].Unlocked.Add("Diplomacy"); econ.Tech[Team.Enemy].Unlocked.Add("Diplomacy");
            econ.Resources[Team.Player] = new CityResourceData { Stars = 4 };
            Require(TileImprovementSystem.GetOptions(grid, econ, Team.Player, pos).Any(o => o.Id == "Embassy" && !o.Enabled), "embassy insufficient stars");
            Require(!TileImprovementSystem.Execute(grid, econ, Team.Player, pos, "Embassy", null), "disabled embassy cannot execute");
            econ.Resources[Team.Player] = new CityResourceData { Stars = 10 };
            grid.FogEnabled = true; VisionSystem.Reveal(grid, Team.Player, pos, 0);
            Require(TileImprovementSystem.Execute(grid, econ, Team.Player, pos, "Embassy", null), "embassy build through menu path");
            Require(econ.Resources[Team.Player].Stars == 5 && econ.Cities[other].HasEmbassy && string.IsNullOrEmpty(grid.GetTile(pos).BuildingId), "embassy cost and city coexistence");
            Require(VisionSystem.IsExplored(grid, Team.Player, pos + Vector2Int.one), "embassy reveals 3x3");
            Require(!TileImprovementSystem.Execute(grid, econ, Team.Player, pos, "Embassy", null), "one embassy per foreign tribe");
            Require(DiplomacySystem.Income(econ, Team.Player) == 2 && DiplomacySystem.Income(econ, Team.Enemy) == 2, "bilateral income");
            var resource = CityResourceSystem.ApplyTurnStart(econ.Resources[Team.Player], grid, world, econ, Team.Player);
            Require(resource.Stars == 9, "turn starts pay city and embassy income");
            VisionSystem.Reveal(grid, Team.Enemy, econ.Cities[own].Position, 0);
            Require(TileImprovementSystem.Execute(grid, econ, Team.Enemy, econ.Cities[own].Position, "Embassy", null), "reciprocal embassy");
            Require(DiplomacySystem.Income(econ, Team.Player) == 4, "reciprocal embassy income stacks");
            DiplomacySystem.SetPeaceTreaty(econ, Team.Player, Team.Enemy, true);
            Require(DiplomacySystem.Income(econ, Team.Player) == 8, "peace treaty doubles embassy income");
            int attacker = Unit(grid, world, Team.Player, "infantry", 6, 8);
            int defender = Unit(grid, world, Team.Enemy, "infantry", 7, 8);
            Require(!CombatSystem.TryAttack(grid, world, attacker, defender, out _, out _) && econ.Embassies.Count == 2, "treaty forbids attack without deleting embassies");
            DiplomacySystem.SetPeaceTreaty(econ, Team.Player, Team.Enemy, false);
            Require(CombatSystem.TryAttack(grid, world, attacker, defender, out _, out _), "attack before war");
            Require(econ.Embassies.Count == 0 && !econ.Cities[own].HasEmbassy && !econ.Cities[other].HasEmbassy && DiplomacySystem.AtWar(econ, Team.Player, Team.Enemy), "attack removes both embassies");
            Require(!DiplomacySystem.CanBuild(econ, Team.Player, other), "cannot build while at war");
            DiplomacySystem.SetPeaceTreaty(econ, Team.Player, Team.Enemy, true);
            var city = econ.Cities[other]; city.Owner = Team.Player; city.IsCapital = false; econ.Cities[other] = city;
            Require(!DiplomacySystem.CanBuild(econ, Team.Enemy, other), "captured capital cannot host embassy");
        }

        private static void VerifyTemple()
        {
            var grid = Grid(); var world = new EntityWorld(); var econ = Economy();
            CitySystem.FoundCity(grid, econ, new Vector2Int(2, 2), Team.Player, true, "City");
            econ.Tech[Team.Player].Unlocked.Add("FreeSpirit");
            var pos = new Vector2Int(3, 2); int before = ScoreSystem.Compute(grid, world, econ, Team.Player);
            Require(TileImprovementSystem.Execute(grid, econ, Team.Player, pos, "Temple", null), "temple construction");
            int built = ScoreSystem.Compute(grid, world, econ, Team.Player);
            Require(built > before, "temple enters actual score");
            econ.Turn += 3;
            Require(ScoreSystem.Compute(grid, world, econ, Team.Player) - built == 50, "temple gains 50 after 3 turns");
            econ.Turn += 9;
            Require(ScoreSystem.Compute(grid, world, econ, Team.Player) - built == 200 && ScoreSystem.TemplePoints(5) == 300, "temple caps at 300");
        }
    }
}
