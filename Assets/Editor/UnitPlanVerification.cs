using System.Linq;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 유닛 기획서(docs/spec/game/unit.md, movement.md, combat.md, vision.md, tile.md)의 규칙을 헤드리스로 검사한다 —
    /// 기본 유닛 패시브 표, 은신 발동, 잠입·지형무시·비행·수륙양용·물 이동 타입·헤엄, 중독, 성장, 침수·폭우·얼음·스케이트, 약탈, 독립, 파괴, 상륙.
    /// 사용법: VerificationSuite에 포함. 결과는 "[UnitPlanVerification] ALL PASS" / "SOME CHECKS FAILED".
    /// </summary>
    public static class UnitPlanVerification
    {
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            GameDataLoader.LoadAll();
            VerifyTable();
            VerifyHide();
            VerifyMovementPassives();
            VerifyDomains();
            VerifyPoison();
            VerifyGrow();
            VerifyTerrainChanges();
            VerifyPlunderAndWild();
            Debug.Log(_ok ? "[UnitPlanVerification] ALL PASS" : "[UnitPlanVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[UnitPlanVerification] FAIL: " + message);
        }

        private static UnitCsvRow Row(string id) => GameTables.Units.First(u => u.Id == id);
        private static bool Has(UnitCsvRow row, ActionType flag) => (row.Actions & flag) != 0;

        private static GridWorld Field(int w, int h)
        {
            var grid = new GridWorld(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            return grid;
        }

        private static void Water(GridWorld grid, Vector2Int p, bool ocean = false)
        {
            grid.SetTerrain(p, TerrainType.Water);
            grid.SetTileType(p, ocean ? TerrainGenerationSystem.OceanTileId : "Water");
        }

        private static UnitCsvRow Custom(ActionType actions, TerrainType domain = TerrainType.Land, int move = 2) => new UnitCsvRow
        {
            Id = "test", Name = "test", MaxHp = 10, Defense = 1, Actions = ActionType.Move | ActionType.Attack | actions, MoveRange = move,
            AttackAttack = 2, AttackRange = 1, Domain = domain,
        };

        private static void VerifyTable()
        {
            Check(!Has(Row("cavalry"), ActionType.Fortify) && !Has(Row("knight"), ActionType.Fortify), "plan: rider/knight have no Fortify");
            Check(Has(Row("cleric"), ActionType.Static), "plan: cleric is Static");
            var spy = Row("spy");
            Check(Has(spy, ActionType.Hide) && Has(spy, ActionType.Charge) && Has(spy, ActionType.Creep) && Has(spy, ActionType.Sneak) &&
                  Has(spy, ActionType.Plunder) && Has(spy, ActionType.Static) && !Has(spy, ActionType.Infiltrate) && !Has(spy, ActionType.Stiff),
                "plan: spy passives (hide, charge, creep, sneak, plunder, static)");
            Check(!Has(NavalUnitDefinition.Raft, ActionType.Stiff), "plan: raft has no Stiff");
            var scout = NavalUnitDefinition.Upgrades.First(u => u.Row.Id == "scout").Row;
            Check(!Has(scout, ActionType.Scout), "plan: scout boat has no Scout");
        }

        private static void VerifyHide()
        {
            var grid = Field(6, 6);
            var world = new EntityWorld();
            int spy = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("spy"), new Vector2Int(1, 1));
            int foe = UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("infantry"), new Vector2Int(2, 1));
            Check(!StealthSystem.IsHidden(world, spy), "hide: visible right after training");
            MovementSystem.TryMove(grid, world, spy, new Vector2Int(1, 3));
            Check(!StealthSystem.IsHidden(world, spy), "hide: moving alone doesn't hide");
            TurnSystem.StartTurn(world, Team.Player, 2);
            Check(StealthSystem.IsHidden(world, spy), "hide: a round without being hit hides the unit");
            StealthSystem.OnHit(world, spy);
            Check(!StealthSystem.IsHidden(world, spy), "hide: being hit reveals");
            TurnSystem.StartTurn(world, Team.Player, 3);
            Check(!StealthSystem.IsHidden(world, spy), "hide: no hiding after a round in which it was hit");
            TurnSystem.StartTurn(world, Team.Player, 4);
            Check(StealthSystem.IsHidden(world, spy), "hide: hides again after a quiet round");
            grid.RemoveOccupant(world.Get<GridPosition>(foe).Value);
            world.Set(foe, new GridPosition { Value = new Vector2Int(2, 3) });
            grid.PlaceOccupant(new Vector2Int(2, 3), foe);
            Check(StealthSystem.AdjacentHiddenEnemy(grid, world, foe), "hide: adjacent enemy senses a hidden unit (!)");
        }

        private static void VerifyMovementPassives()
        {
            // 잠입: 적 옆 칸에서도 멈추지 않는다. 지형무시: 숲·산에서 멈추지 않는다.
            // 2줄 맵, 적은 (2,0): 어느 줄로 가도 적 옆 칸((1,0) 또는 (2,1))을 지나야 (3,1)에 닿는다.
            var grid = Field(7, 2);
            var world = new EntityWorld();
            int foe = UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("infantry"), new Vector2Int(2, 0));
            int plain = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Custom(ActionType.None, move: 4), new Vector2Int(0, 1));
            int sneak = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Custom(ActionType.Sneak, move: 4), new Vector2Int(0, 0));
            PathfindingSystem.GetReachable(grid, world, new Vector2Int(0, 1), plain, out var plainReach);
            PathfindingSystem.GetReachable(grid, world, new Vector2Int(0, 0), sneak, out var sneakReach);
            Check(!plainReach.Contains(new Vector2Int(3, 1)) && plainReach.Contains(new Vector2Int(2, 1)), "ZoC stops a plain unit next to an enemy");
            Check(sneakReach.Contains(new Vector2Int(3, 1)), "sneak ignores zone of control");
            Check(!sneakReach.Contains(new Vector2Int(2, 0)), "sneak cannot stand on enemy units");

            var g2 = Field(5, 1);
            g2.SetTileType(new Vector2Int(1, 0), TerrainGenerationSystem.ForestTileId);
            g2.SetTileType(new Vector2Int(2, 0), TerrainGenerationSystem.MountainTileId);
            var w2 = new EntityWorld();
            int creep = UnitFactorySystem.CreateFromCsv(g2, w2, Team.Player, Custom(ActionType.Creep, move: 3), new Vector2Int(0, 0));
            PathfindingSystem.GetReachable(g2, w2, new Vector2Int(0, 0), creep, out var creepReach);
            Check(creepReach.Contains(new Vector2Int(3, 0)), "creep passes forest and mountain without stopping");
        }

        private static void VerifyDomains()
        {
            // 0..1 육지, 2 얕은 물, 3 깊은 바다, 4 육지(침수), 5 육지
            var grid = Field(6, 1);
            Water(grid, new Vector2Int(2, 0));
            Water(grid, new Vector2Int(3, 0), ocean: true);
            var t = grid.GetTile(new Vector2Int(4, 0)); t.Flooded = true; grid.SetTile(new Vector2Int(4, 0), t);
            var world = new EntityWorld();

            int amph = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("amphibian"), new Vector2Int(1, 0));
            Check(PathfindingSystem.CanEnter(grid, world, amph, new Vector2Int(2, 0), out _) && !PathfindingSystem.CanEnter(grid, world, amph, new Vector2Int(3, 0), out _),
                "amphibious: shallow water yes, ocean no");
            int swim = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Custom(ActionType.Swim), new Vector2Int(0, 0));
            Check(PathfindingSystem.CanEnter(grid, world, swim, new Vector2Int(2, 0), out _) && !EmbarkSystem.CanBoard(world, swim), "swim: shallow water, never boards");

            int dragon = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("babydragon"), new Vector2Int(5, 0));
            Check(PathfindingSystem.CanEnter(grid, world, dragon, new Vector2Int(3, 0), out bool stop) && !stop && !EmbarkSystem.CanBoard(world, dragon),
                "air: crosses the ocean, never boards");

            grid.RemoveOccupant(new Vector2Int(0, 0)); world.Set(swim, new Hp { Value = 0 });
            int puffer = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("puffer"), new Vector2Int(2, 0));
            Check(!PathfindingSystem.CanEnter(grid, world, puffer, new Vector2Int(1, 0), out _), "water domain: dry land is closed");
            Check(PathfindingSystem.CanEnter(grid, world, puffer, new Vector2Int(4, 0), out bool flooded) && flooded, "water domain: flooded land is open but stops");
        }

        private static void VerifyPoison()
        {
            var grid = Field(4, 1);
            var world = new EntityWorld();
            int kiton = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("kiton"), new Vector2Int(0, 0));
            int target = UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("shield"), new Vector2Int(1, 0));
            world.Set(target, new PositionalDefenseBonus { Value = 1 });
            float before = CombatSystem.EffectiveDefense(world, target);
            CombatSystem.TryAttack(grid, world, kiton, target, out _, out _);
            Check(CombatSystem.IsPoisoned(world, target), "poison: surviving target is poisoned");
            Check(Mathf.Approximately(CombatSystem.DefenseMultiplier(world, target), 1f) &&
                  Mathf.Approximately(CombatSystem.EffectiveDefense(world, target), world.Get<Defense>(target).Value * GameRules.Poison.DefenseMultiplier) &&
                  CombatSystem.EffectiveDefense(world, target) < before, "poison: no defense bonus, defense x multiplier");
            int hp = world.Get<Hp>(target).Value;
            world.Set(target, new HasMoved { Value = false }); world.Set(target, new HasActed { Value = false });
            AbilitySystem.TryWait(grid, world, target);
            Check(!CombatSystem.IsPoisoned(world, target) && world.Get<Hp>(target).Value == hp, "poison: recovering cures it without healing");
        }

        private static void VerifyGrow()
        {
            var grid = Field(3, 1);
            var world = new EntityWorld();
            int egg = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("dragonegg"), new Vector2Int(1, 0));
            world.Set(egg, new Hp { Value = 7 });
            int turns = Row("dragonegg").GrowTurns;
            Check(world.Get<Growth>(egg).TurnsLeft == turns && turns > 0, "grow: egg counts down Grow.Turns");
            for (int i = 0; i < turns - 1; i++) GrowSystem.OnTurnStart(grid, world, null, Team.Player);
            Check(UnitQueries.IsAlive(world, egg), "grow: still an egg before the last turn");
            var grown = GrowSystem.OnTurnStart(grid, world, null, Team.Player);
            Check(grown.Count == 1 && !UnitQueries.IsAlive(world, egg), "grow: egg becomes a new unit");
            if (grown.Count != 1) return;
            int baby = grown[0];
            Check(world.GetOrDefault<UnitTypeId>(baby).Value == "babydragon" && world.Get<MoveDomain>(baby).Value == TerrainType.Air &&
                  grid.GetOccupant(new Vector2Int(1, 0)) == baby, "grow: baby dragon (air) on the same tile");
            Check(world.Get<Hp>(baby).Value == world.Get<MaxHp>(baby).Value - 3, "grow: damage is inherited");
        }

        private static void VerifyTerrainChanges()
        {
            // 침수 행동 · 폭우
            var grid = Field(5, 2);
            Water(grid, new Vector2Int(0, 0));
            var world = new EntityWorld();
            int flooder = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Custom(ActionType.Flood), new Vector2Int(3, 1));
            Check(TerrainChangeSystem.FloodHere(grid, world, flooder) && grid.GetTile(new Vector2Int(3, 1)).Flooded && world.Get<HasActed>(flooder).Value,
                "flood: unit floods its own field tile and acts");
            grid.SetTileType(new Vector2Int(4, 0), TerrainGenerationSystem.MountainTileId);
            Check(!TerrainChangeSystem.CanFloodTile(grid, new Vector2Int(4, 0)) && !TerrainChangeSystem.CanFloodTile(grid, new Vector2Int(0, 0)), "flood: never mountains or water");
            int puffer = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("puffer"), new Vector2Int(0, 0));
            var drench = TerrainChangeSystem.DrenchTargets(grid, world, puffer);
            Check(drench.Contains(new Vector2Int(1, 0)) && TerrainChangeSystem.DrenchTile(grid, world, puffer, new Vector2Int(1, 0)) &&
                  grid.GetTile(new Vector2Int(1, 0)).Flooded, "drench: attacking an empty tile floods it");

            // 상륙: 침수 칸에 들어간 배는 배 그대로이고, 상륙 행동으로 내린다.
            var g3 = Field(3, 1);
            Water(g3, new Vector2Int(0, 0));
            var ft = g3.GetTile(new Vector2Int(1, 0)); ft.Flooded = true; g3.SetTile(new Vector2Int(1, 0), ft);
            var w3 = new EntityWorld();
            int sailor = UnitFactorySystem.CreateFromCsv(g3, w3, Team.Player, Row("infantry"), new Vector2Int(0, 0));
            EmbarkSystem.EmbarkAs(w3, sailor, NavalUnitDefinition.RaftId);
            MovementSystem.TryMove(g3, w3, sailor, new Vector2Int(1, 0));
            Check(EmbarkSystem.IsEmbarked(w3, sailor), "flooded tile: a boat stays a boat");
            w3.Set(sailor, new HasActed { Value = false });
            Check(EmbarkSystem.Disembark(g3, w3, sailor) && !EmbarkSystem.IsEmbarked(w3, sailor), "disembark on a flooded tile");

            // 얼음: 자동 빙결 → 육지 유닛이 건넘 → 얼음 깨기. 스케이트 +1.
            var g2 = Field(4, 3);
            for (int y = 0; y < 3; y++) Water(g2, new Vector2Int(2, y));
            var w2 = new EntityWorld();
            int mooni = UnitFactorySystem.CreateFromCsv(g2, w2, Team.Player, Row("mooni"), new Vector2Int(0, 1));
            MovementSystem.TryMove(g2, w2, mooni, new Vector2Int(1, 1));
            Check(g2.GetTile(new Vector2Int(2, 1)).Iced && g2.GetTile(new Vector2Int(2, 0)).Iced, "auto freeze: adjacent water turns to ice");
            int walker = UnitFactorySystem.CreateFromCsv(g2, w2, Team.Player, Row("infantry"), new Vector2Int(1, 0));
            Check(PathfindingSystem.CanEnter(g2, w2, walker, new Vector2Int(2, 0), out _), "ice: land units walk on it");
            int skater = UnitFactorySystem.CreateFromCsv(g2, w2, Team.Player, Custom(ActionType.Skate, move: 1), new Vector2Int(2, 2));
            Check(MovementSystem.EffectiveMoveRange(w2, skater, g2) == 2, "skate: +1 movement starting on ice");
            Check(TerrainChangeSystem.BreakIce(g2, w2, walker) > 0 && !g2.GetTile(new Vector2Int(2, 1)).Iced, "break ice turns adjacent ice back into water");

            // 파괴: 지나간 칸의 건물·유적이 사라진다.
            var g4 = Field(4, 1);
            var bt = g4.GetTile(new Vector2Int(1, 0)); bt.BuildingId = "Farm"; g4.SetTile(new Vector2Int(1, 0), bt);
            g4.SetStructure(new Vector2Int(2, 0), StructureGenerationSystem.RuinStructureId);
            var w4 = new EntityWorld();
            int razer = UnitFactorySystem.CreateFromCsv(g4, w4, Team.Player, Custom(ActionType.Raze, move: 3), new Vector2Int(0, 0));
            MovementSystem.TryMove(g4, w4, razer, new Vector2Int(3, 0));
            Check(string.IsNullOrEmpty(g4.GetTile(new Vector2Int(1, 0)).BuildingId) && g4.GetStructure(new Vector2Int(2, 0)) != StructureGenerationSystem.RuinStructureId,
                "raze: buildings and ruins on the path are gone");
        }

        private static void VerifyPlunderAndWild()
        {
            var grid = Field(8, 8);
            var world = new EntityWorld();
            var econ = new EconomyWorld { TechNodes = GameDataLoader.LoadTechNodes(TableSource.Resources) };
            econ.UnitRows.AddRange(GameTables.Units);
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Stars = 5 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, Row("infantry"), new Vector2Int(1, 1));
            UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("infantry"), new Vector2Int(6, 6));
            CitySystem.InitializeCapitals(grid, world, econ);
            var cap = econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position;
            int spy = UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("spy"), cap + Vector2Int.right * 2);
            MovementSystem.TryMove(grid, world, spy, cap + Vector2Int.right);
            int stars = econ.Resources[Team.Enemy].Stars;
            int income = CitySystem.CityStarsIncome(grid, world, econ.Cities[CitySystem.FindCityAt(econ, cap)]);
            Check(PlunderSystem.Targets(grid, world, econ, spy).Contains(cap), "plunder: dash lets the spy plunder after moving");
            Check(PlunderSystem.Plunder(grid, world, econ, spy, cap, null) == income && econ.Resources[Team.Enemy].Stars == stars + income &&
                  econ.Cities[CitySystem.FindCityAt(econ, cap)].Infiltrated && UnitQueries.IsAlive(world, spy), "plunder: steals the city's income, spy stays");
            Check(PlunderSystem.Targets(grid, world, econ, spy).Count == 0, "plunder: once per turn / per city round");

            int bunny = UnitFactorySystem.CreateFromCsv(grid, world, Team.Enemy, Row("bunny"), new Vector2Int(4, 6));
            var bunnyPos = world.Get<GridPosition>(bunny).Value;
            EnemyAI.RunTurn(grid, world, econ, Team.Enemy);
            Check(world.Get<GridPosition>(bunny).Value == bunnyPos && !world.Get<HasActed>(bunny).Value, "wild: AI never commands it");
            world.Set(bunny, new Kills { Value = 10 });
            Check(!VeteranSystem.CanPromote(world, bunny), "wild: cannot be promoted");
        }
    }
}
