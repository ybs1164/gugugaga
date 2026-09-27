using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 건물 기능(위키 Buildings/Movement/Port/Bridge/Temple/Monuments/Score)과 시야(Cloud/Explorer/Lighthouse)를 GameObject 없이
    /// System 호출만으로 검증하는 배치모드 전용 스크립트. EconomyVerification과 같은 패턴 — Console의 PASS/FAIL만 보면 된다.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.BuildingFeatureVerification.Run
    /// </summary>
    public static class BuildingFeatureVerification
    {
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            VerifyRoadMovement();
            VerifyRoughTerrainAndZoc();
            VerifyBridge();
            VerifyPortEmbarkAndUpgrade();
            VerifyPortConnectionDistance();
            VerifyVision();
            VerifyTempleAndScore();
            VerifyMonuments();
            VerifyAiUsesBuildings();
            Debug.Log(_ok ? "[BuildingFeatureVerification] ALL PASS" : "[BuildingFeatureVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[BuildingFeatureVerification] FAIL: " + message);
        }

        // ---------- 공통 준비 ----------

        private static GridWorld NewGrid(int w, int h)
        {
            var grid = new GridWorld(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            return grid;
        }

        private static void SetWater(GridWorld grid, Vector2Int p, bool ocean = false)
        {
            grid.SetTerrain(p, TerrainType.Water);
            grid.SetTileType(p, ocean ? TerrainGenerationSystem.OceanTileId : "Water");
        }

        private static EconomyWorld NewEconomy()
        {
            var econ = new EconomyWorld { TechNodes = TechCsvSerializer.Parse(Resources.Load<TextAsset>(TechTreeDefinition.CsvResourcePath).text) };
            econ.UnitRows.Add(new UnitCsvRow { Id = "infantry", Name = "보병", MaxHp = 10, Defense = 1, Cost = 2, BaseVisual = "Melee",
                Actions = ActionType.Move | ActionType.Attack, MoveRange = 1, AttackAttack = 4, AttackRange = 1 });
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Gold = 100, Development = 5, MaxFaith = 10 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            TaskSystem.Init(econ);
            return econ;
        }

        private static void UnlockAll(EconomyWorld econ, Team team)
        {
            foreach (var n in econ.TechNodes) econ.Tech[team].Unlocked.Add(n.Id);
        }

        private static int Unit(GridWorld grid, EntityWorld world, Team team, Vector2Int pos, int move = 1, int attack = 4, int range = 1)
        {
            var row = new UnitCsvRow { Id = "infantry", Name = "보병", MaxHp = 10, Defense = 1, Cost = 2,
                Actions = ActionType.Move | ActionType.Attack, MoveRange = move, AttackAttack = attack, AttackRange = range };
            return UnitFactorySystem.CreateFromCsv(grid, world, team, row, pos);
        }

        private static HashSet<Vector2Int> Reach(GridWorld grid, EntityWorld world, int id)
        {
            PathfindingSystem.GetReachable(grid, world, world.Get<GridPosition>(id).Value, id, out var set);
            return set;
        }

        // ---------- 이동 ----------

        private static void VerifyRoadMovement()
        {
            var grid = NewGrid(8, 3);
            var world = new EntityWorld();
            for (int x = 0; x <= 5; x++) { var t = grid.GetTile(new Vector2Int(x, 1)); t.HasRoad = true; grid.SetTile(new Vector2Int(x, 1), t); }
            int id = Unit(grid, world, Team.Player, new Vector2Int(0, 1), move: 1);
            var r = Reach(grid, world, id);
            Check(r.Contains(new Vector2Int(2, 1)) && !r.Contains(new Vector2Int(3, 1)), "move 1 on road reaches 2 tiles (cost 0.5 each)");
            Check(r.Contains(new Vector2Int(0, 0)) && !r.Contains(new Vector2Int(1, 0)), "off-road step still costs 1");

            // 적 영토의 도로는 보너스 없음.
            for (int x = 0; x <= 5; x++) { var t = grid.GetTile(new Vector2Int(x, 1)); t.OwnerTeam = (int)Team.Enemy; grid.SetTile(new Vector2Int(x, 1), t); }
            r = Reach(grid, world, id);
            Check(!r.Contains(new Vector2Int(2, 1)), "no road bonus in enemy territory");
        }

        private static void VerifyRoughTerrainAndZoc()
        {
            var grid = NewGrid(8, 3);
            var world = new EntityWorld();
            grid.SetTileType(new Vector2Int(1, 1), TerrainGenerationSystem.ForestTileId);
            int id = Unit(grid, world, Team.Player, new Vector2Int(0, 1), move: 3);
            var r = Reach(grid, world, id);
            // 숲(1,1)에 들어가면 멈춘다. 숲을 돌아가는 길((0,0)→(1,0)→(2,0))은 이동 3으로 (2,1)까지 닿지 않는다.
            Check(r.Contains(new Vector2Int(1, 1)) && !r.Contains(new Vector2Int(2, 1)), "forest stops movement (can enter, not pass)");
            // 숲 위 도로는 제한 해제.
            var ft = grid.GetTile(new Vector2Int(1, 1)); ft.HasRoad = true; grid.SetTile(new Vector2Int(1, 1), ft);
            grid.SetTileType(new Vector2Int(1, 0), TerrainGenerationSystem.ForestTileId);
            grid.SetTileType(new Vector2Int(1, 2), TerrainGenerationSystem.ForestTileId);
            r = Reach(grid, world, id);
            Check(r.Contains(new Vector2Int(2, 1)), "road on forest removes the movement stop");

            // Zone of Control: 적과 인접한 칸에 들어가면 멈춘다.
            var grid2 = NewGrid(8, 1);
            var world2 = new EntityWorld();
            int mover = Unit(grid2, world2, Team.Player, new Vector2Int(0, 0), move: 5);
            Unit(grid2, world2, Team.Enemy, new Vector2Int(3, 0));
            var r2 = Reach(grid2, world2, mover);
            Check(r2.Contains(new Vector2Int(2, 0)) && !r2.Contains(new Vector2Int(4, 0)), "zone of control stops next to enemy");
        }

        private static void VerifyBridge()
        {
            var grid = NewGrid(5, 3);
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;
            for (int y = 0; y < 3; y++) SetWater(grid, new Vector2Int(2, y));
            int city = CitySystem.FoundCity(grid, econ, new Vector2Int(1, 1), p, true, "수도");
            var c = econ.Cities[city]; c.BorderRadius = 2; econ.Cities[city] = c;
            CitySystem.ClaimTerritory(grid, econ, city);
            UnlockAll(econ, p);

            var bridgeTile = new Vector2Int(2, 1);
            Check(TileImprovementSystem.GetOptions(grid, econ, p, bridgeTile).Exists(o => o.Id == BuildingDefinition.Bridge), "bridge offered between two land tiles");
            var cornerGrid = NewGrid(3, 3);
            SetWater(cornerGrid, new Vector2Int(1, 1)); SetWater(cornerGrid, new Vector2Int(2, 1)); SetWater(cornerGrid, new Vector2Int(1, 2)); SetWater(cornerGrid, new Vector2Int(1, 0));
            Check(!TileImprovementSystem.HasOppositeLand(cornerGrid, new Vector2Int(1, 1)), "no bridge without opposite land");

            int unit = Unit(grid, world, p, new Vector2Int(1, 1), move: 2);
            Check(!Reach(grid, world, unit).Contains(new Vector2Int(3, 1)), "land unit can't cross water without bridge");
            var log = new List<EconomyLogEntry>();
            Check(TileImprovementSystem.Execute(grid, econ, p, bridgeTile, BuildingDefinition.Bridge, log), "build bridge");
            var r = Reach(grid, world, unit);
            Check(r.Contains(bridgeTile) && r.Contains(new Vector2Int(3, 1)), "land unit crosses bridge");

            // 다리로 수도 연결: 건너편 마을을 점령.
            var village = new Vector2Int(3, 1);
            grid.SetStructure(village, StructureGenerationSystem.VillageStructureId);
            int cityB = CitySystem.FoundCity(grid, econ, village, p, false, "마을");
            CitySystem.RefreshConnections(grid, econ, log);
            Check(econ.Cities[cityB].ConnectedToCapital, "bridge forms a city connection");
            Check(TileImprovementSystem.Execute(grid, econ, p, bridgeTile, "DestroyBuilding", log), "bridge can be destroyed");
            Check(!econ.Cities[cityB].ConnectedToCapital, "destroying bridge breaks the connection");
        }

        private static void VerifyPortEmbarkAndUpgrade()
        {
            var grid = NewGrid(8, 3);
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;
            for (int x = 2; x < 8; x++) for (int y = 0; y < 3; y++) SetWater(grid, new Vector2Int(x, y));
            int city = CitySystem.FoundCity(grid, econ, new Vector2Int(1, 1), p, true, "수도");
            UnlockAll(econ, p);
            var port = new Vector2Int(2, 1);
            var log = new List<EconomyLogEntry>();
            Check(TileImprovementSystem.Execute(grid, econ, p, port, BuildingDefinition.Port, log), "build port");

            int unit = Unit(grid, world, p, new Vector2Int(0, 1), move: 2);
            world.Set(unit, new MaxHp { Value = 10 });
            var r = Reach(grid, world, unit);
            Check(r.Contains(port) && !r.Contains(new Vector2Int(3, 1)), "land unit can enter own port but not open water");
            Check(MovementSystem.TryMove(grid, world, unit, port), "move onto port");
            Check(EmbarkSystem.NavalUnitId(world, unit) == NavalUnitDefinition.RaftId && world.Get<MoveDomain>(unit).Value == TerrainType.Water, "unit becomes raft");
            Check(world.Get<HasMoved>(unit).Value && world.Get<HasActed>(unit).Value, "embarking ends the turn");
            Check(world.Get<Hp>(unit).Value == 10 && world.Get<Attack>(unit).Value == 0, "raft keeps hp, has no attack");

            Check(EmbarkSystem.CanUpgrade(grid, world, econ, unit, "scout", out _), "raft upgradable to scout in own territory");
            int gold = econ.Resources[p].Gold;
            Check(EmbarkSystem.Upgrade(grid, world, econ, unit, "scout", log), "upgrade to scout");
            Check(econ.Resources[p].Gold == gold - 5 && world.Get<VisionRange>(unit).Value == 2 && world.Get<AttackRange>(unit).Value == 2, "scout: -5 gold, 5x5 vision, range 2");

            TurnSystem.StartTurn(world, p, 2);
            var far = new Vector2Int(5, 1);
            Check(Reach(grid, world, unit).Contains(far), "scout sails 3 tiles");
            Check(MovementSystem.TryMove(grid, world, unit, far), "sail");
            Check(!EmbarkSystem.CanUpgrade(grid, world, econ, unit, "rammer", out _), "no upgrade outside territory / from a non-raft");

            TurnSystem.StartTurn(world, p, 3);
            // 해안에 내리기: 다시 육지 칸 옆으로.
            grid.SetTerrain(new Vector2Int(6, 2), TerrainType.Land); grid.SetTileType(new Vector2Int(6, 2), "Grass");
            Check(Reach(grid, world, unit).Contains(new Vector2Int(6, 2)), "boat can land on coast");
            Check(MovementSystem.TryMove(grid, world, unit, new Vector2Int(6, 2)), "disembark");
            Check(!EmbarkSystem.IsEmbarked(world, unit) && world.Get<MoveDomain>(unit).Value == TerrainType.Land &&
                  world.Get<Attack>(unit).Value == 4 && world.Get<MoveRange>(unit).Value == 2, "disembark restores land stats (upgrade lost)");

            // 적 항구는 쓸 수 없다.
            int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(1, 0), move: 2);
            Check(!Reach(grid, world, enemy).Contains(port), "enemy can't use our port");
        }

        private static void VerifyPortConnectionDistance()
        {
            // 수도(0,1) - 항구(1,1) - 물 n칸 - 항구 - 도시.
            bool Connected(int gap)
            {
                int w = gap + 4;
                var grid = NewGrid(w, 3);
                for (int x = 1; x < w - 1; x++) for (int y = 0; y < 3; y++) SetWater(grid, new Vector2Int(x, y));
                var econ = NewEconomy();
                var p = Team.Player;
                UnlockAll(econ, p);
                int a = CitySystem.FoundCity(grid, econ, new Vector2Int(0, 1), p, true, "A");
                int b = CitySystem.FoundCity(grid, econ, new Vector2Int(w - 1, 1), p, false, "B");
                foreach (var pt in new[] { new Vector2Int(1, 1), new Vector2Int(w - 2, 1) })
                {
                    var t = grid.GetTile(pt); t.BuildingId = BuildingDefinition.Port; grid.SetTile(pt, t);
                }
                // 가운데 물은 중립(도시 반경 밖) — 연결 판정만 본다.
                CitySystem.RefreshConnections(grid, econ, null);
                return econ.Cities[b].ConnectedToCapital;
            }
            Check(Connected(5), "ports 5 water tiles apart connect");
            Check(!Connected(6), "ports 6 water tiles apart don't connect");
        }

        // ---------- 시야 ----------

        private static void VerifyVision()
        {
            var grid = NewGrid(15, 15);
            grid.FogEnabled = true;
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;
            grid.SetStructure(new Vector2Int(14, 14), StructureGenerationSystem.LighthouseStructureId);
            int unit = Unit(grid, world, p, new Vector2Int(2, 2), move: 3);
            CitySystem.InitializeCapitals(grid, world, econ);
            VisionSystem.Refresh(grid, world, econ, null);
            Check(VisionSystem.IsExplored(grid, p, new Vector2Int(4, 4)) && !VisionSystem.IsExplored(grid, p, new Vector2Int(7, 7)), "start reveals 5x5 around capital only");
            Check(!Reach(grid, world, unit).Contains(new Vector2Int(5, 2)), "can't move into clouds");

            // 탐험한 칸은 계속 보인다.
            MovementSystem.TryMove(grid, world, unit, new Vector2Int(4, 2));
            VisionSystem.Refresh(grid, world, econ, null);
            Check(VisionSystem.IsExplored(grid, p, new Vector2Int(5, 3)), "moving reveals 3x3 around the unit");
            Check(VisionSystem.IsExplored(grid, p, new Vector2Int(0, 0)), "explored tiles stay revealed");

            // 산 위는 5x5.
            grid.SetTileType(new Vector2Int(4, 2), TerrainGenerationSystem.MountainTileId);
            VisionSystem.Refresh(grid, world, econ, null);
            Check(VisionSystem.IsExplored(grid, p, new Vector2Int(6, 4)), "mountain gives 5x5 vision");

            // 탐험가: 12걸음으로 구름을 걷어낸다(결정적 난수).
            int before = VisionSystem.CountExplored(grid, p);
            var log = new List<EconomyLogEntry>();
            VisionSystem.RunExplorer(grid, econ, p, new Vector2Int(2, 2), log);
            Check(VisionSystem.CountExplored(grid, p) > before + 10, $"explorer reveals fog ({before} -> {VisionSystem.CountExplored(grid, p)})");

            // 등대: 처음 밝히면 수도 인구 +1.
            int popBefore = econ.Cities[0].Population + econ.Cities[0].Level * 10;
            VisionSystem.Reveal(grid, p, new Vector2Int(14, 14), 0);
            VisionSystem.ProcessLighthouses(grid, econ, log);
            Check(econ.Tasks[p].LighthousesFound.Count == 1 && econ.Cities[0].Population + econ.Cities[0].Level * 10 == popBefore + 1, "lighthouse discovery gives +1 pop");
            TaskSystem.Refresh(grid, econ, log);
            Check(econ.Tasks[p].Completed.Contains(TaskDefinition.Explorer), "all lighthouses found completes the Explorer task");

            // 탐험가 보상(도시 레벨 2)도 구름을 걷는다.
            var econ2 = NewEconomy();
            var grid2 = NewGrid(15, 15);
            grid2.FogEnabled = true;
            int c2 = CitySystem.FoundCity(grid2, econ2, new Vector2Int(7, 7), p, true, "수도");
            var cd = econ2.Cities[c2]; cd.Level = 2; cd.PendingRewards = 1; econ2.Cities[c2] = cd;
            int b2 = VisionSystem.CountExplored(grid2, p);
            Check(CitySystem.ApplyReward(grid2, econ2, c2, CityRewardType.Explorer, log) && VisionSystem.CountExplored(grid2, p) > b2, "Explorer reward reveals tiles");
        }

        // ---------- 점수/신전/기념물 ----------

        private static void VerifyTempleAndScore()
        {
            Check(ScoreSystem.TempleLevel(1, 1) == 1 && ScoreSystem.TempleLevel(3, 1) == 1 && ScoreSystem.TempleLevel(4, 1) == 2 && ScoreSystem.TempleLevel(13, 1) == 5 && ScoreSystem.TempleLevel(40, 1) == 5,
                "temple level every 3 turns (0-2 L1 ... 12+ L5)");
            Check(ScoreSystem.TemplePoints(1) == 100 && ScoreSystem.TemplePoints(5) == 500, "temple points 100..500");

            var grid = NewGrid(9, 9);
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;
            int city = CitySystem.FoundCity(grid, econ, new Vector2Int(4, 4), p, true, "수도");
            UnlockAll(econ, p);
            // 기술 25개: 티어 1 x5, 2 x10, 3 x10 → 100 * (5 + 20 + 30) = 5500.
            var b = ScoreSystem.ComputeBreakdown(grid, world, econ, p);
            Check(b.Tech == 5500, $"tech score 100 per tier (got {b.Tech})");
            Check(b.Territory == 9 * 20 && b.Cities == 100, $"territory 20/tile, level-1 city 100 (got {b.Territory}, {b.Cities})");
            Unit(grid, world, p, new Vector2Int(4, 4));
            Check(ScoreSystem.ComputeBreakdown(grid, world, econ, p).Units == 10, "unit worth 5 per star (infantry 2)");

            econ.Turn = 1;
            var log = new List<EconomyLogEntry>();
            Check(TileImprovementSystem.Execute(grid, econ, p, new Vector2Int(3, 3), "Temple", log), "build temple");
            Check(ScoreSystem.ComputeBreakdown(grid, world, econ, p).Temples == 100, "new temple 100 points");
            econ.Turn = 13;
            Check(ScoreSystem.ComputeBreakdown(grid, world, econ, p).Temples == 500, "level-5 temple 500 points");
        }

        private static void VerifyMonuments()
        {
            var grid = NewGrid(9, 9);
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;
            int city = CitySystem.FoundCity(grid, econ, new Vector2Int(4, 4), p, true, "수도");
            var field = new Vector2Int(3, 3);
            var log = new List<EconomyLogEntry>();

            Check(!TileImprovementSystem.GetOptions(grid, econ, p, field).Exists(o => o.Id == "GateOfPower"), "monument hidden before task");
            econ.Tasks[p].Kills = 10;
            TaskSystem.Refresh(grid, econ, log);
            Check(econ.Tasks[p].Completed.Contains(TaskDefinition.Killer) && log.Exists(e => e.Kind == EconomyLogKind.Task), "10 kills completes Killer");
            var opt = TileImprovementSystem.GetOptions(grid, econ, p, field).Find(o => o.Id == "GateOfPower");
            Check(opt.Id == "GateOfPower" && opt.Cost == 0 && opt.Enabled, "Gate of Power offered free");
            Check(TileImprovementSystem.Execute(grid, econ, p, field, "GateOfPower", log), "build Gate of Power");
            var cc = econ.Cities[city]; // L1에서 인구 3 → L2(2 필요) + 남은 1
            Check(cc.Level == 2 && cc.Population == 1, $"monument +3 pop (L{cc.Level} pop {cc.Population})");
            Check(!TileImprovementSystem.GetOptions(grid, econ, p, new Vector2Int(5, 5)).Exists(o => o.Id == "GateOfPower"), "monument only once");
            UnlockAll(econ, p);
            Check(!TileImprovementSystem.GetOptions(grid, econ, p, field).Exists(o => o.Id == "DestroyBuilding"), "monuments can't be destroyed");
            Check(ScoreSystem.ComputeBreakdown(grid, world, econ, p).Monuments == 400, "monument 400 points");

            // 과업 기술 게이트: 부(Wealth)는 교역이 있어야.
            var econ2 = NewEconomy();
            CitySystem.FoundCity(grid, econ2, new Vector2Int(1, 1), p, true, "x");
            econ2.Resources[p] = new CityResourceData { Gold = 150 };
            TaskSystem.Refresh(grid, econ2, null);
            Check(!econ2.Tasks[p].Completed.Contains(TaskDefinition.Wealth), "Wealth needs Trade");
            econ2.Tech[p].Unlocked.Add("Riding"); econ2.Tech[p].Unlocked.Add("Roads"); econ2.Tech[p].Unlocked.Add("Trade");
            TaskSystem.Refresh(grid, econ2, null);
            Check(econ2.Tasks[p].Completed.Contains(TaskDefinition.Wealth), "Wealth with Trade + 100 gold");

            // 평화주의: 5턴 연속 비공격.
            var econ3 = NewEconomy();
            econ3.Tech[p].Unlocked.Add("Mountaineering"); econ3.Tech[p].Unlocked.Add("Meditation");
            for (int i = 0; i < 4; i++) TaskSystem.EndTurn(econ3, p);
            TaskSystem.RecordAttack(econ3, p);
            TaskSystem.EndTurn(econ3, p);
            for (int i = 0; i < 4; i++) TaskSystem.EndTurn(econ3, p);
            TaskSystem.Refresh(grid, econ3, null);
            Check(!econ3.Tasks[p].Completed.Contains(TaskDefinition.Pacifist), "attack resets Pacifist");
            TaskSystem.EndTurn(econ3, p);
            TaskSystem.Refresh(grid, econ3, null);
            Check(econ3.Tasks[p].Completed.Contains(TaskDefinition.Pacifist), "5 peaceful turns completes Pacifist");

            // 처치 기록(반격 포함).
            var world4 = new EntityWorld();
            var grid4 = NewGrid(3, 1);
            var econ4 = NewEconomy();
            int a = Unit(grid4, world4, p, new Vector2Int(0, 0), attack: 20);
            int e = Unit(grid4, world4, Team.Enemy, new Vector2Int(1, 0));
            var before = TaskSystem.SnapshotAlive(world4);
            CombatSystem.TryAttack(grid4, world4, a, e, out _, out _);
            TaskSystem.RecordDeaths(econ4, world4, before);
            Check(econ4.Tasks[p].Kills == 1 && econ4.Tasks[Team.Enemy].Kills == 0, "kill credited to the attacker's team");
        }

        // ---------- AI ----------

        private static void VerifyAiUsesBuildings()
        {
            // 경제 AI: 기념물은 바로 짓고, 떨어진 도시는 도로로 잇는다.
            var grid = NewGrid(12, 5);
            var world = new EntityWorld();
            var econ = NewEconomy();
            var t = Team.Enemy;
            int cap = CitySystem.FoundCity(grid, econ, new Vector2Int(2, 2), t, true, "적 수도");
            grid.SetStructure(new Vector2Int(2, 2), StructureGenerationSystem.CapitalStructureId);
            grid.SetStructure(new Vector2Int(8, 2), StructureGenerationSystem.VillageStructureId);
            int other = CitySystem.FoundCity(grid, econ, new Vector2Int(8, 2), t, false, "적 도시");
            econ.Tech[t].Unlocked.Add("Riding"); econ.Tech[t].Unlocked.Add("Roads");
            econ.Tasks[t].Kills = 10;
            var log = EconomyAI.RunTurn(grid, world, econ, t);
            Check(log.Exists(en => en.Kind == EconomyLogKind.Build && en.Subject == "힘의 문"), "AI builds an achieved monument");
            Check(econ.Cities[other].ConnectedToCapital, "AI connects a nearby city with roads");

            // 군사 AI: 구름 속 적은 모른다 → 탐험하러 움직인다.
            var grid2 = NewGrid(12, 12);
            grid2.FogEnabled = true;
            var world2 = new EntityWorld();
            int scout = Unit(grid2, world2, t, new Vector2Int(1, 1), move: 2);
            int far = Unit(grid2, world2, Team.Player, new Vector2Int(10, 10));
            VisionSystem.Refresh(grid2, world2, null, null);
            var entries = EnemyAI.RunTurn(grid2, world2, null, t);
            Check(world2.Get<GridPosition>(scout).Value != new Vector2Int(1, 1), "AI explores when no target is visible");
            Check(VisionSystem.CountExplored(grid2, t) > 9, "exploring reveals new tiles for the AI");

            // 군사 AI: 바다 건너 목표 → 항구로 가서 뗏목.
            var grid3 = NewGrid(10, 5);
            var world3 = new EntityWorld();
            var econ3 = NewEconomy();
            for (int y = 0; y < 5; y++) for (int x = 3; x < 7; x++) SetWater(grid3, new Vector2Int(x, y));
            int cap3 = CitySystem.FoundCity(grid3, econ3, new Vector2Int(1, 2), t, true, "적 수도");
            var port = new Vector2Int(3, 2);
            var pt = grid3.GetTile(port); pt.BuildingId = BuildingDefinition.Port; pt.OwnerCity = cap3; pt.OwnerTeam = (int)t; grid3.SetTile(port, pt);
            int sailor = Unit(grid3, world3, t, new Vector2Int(2, 2), move: 2);
            Unit(grid3, world3, Team.Player, new Vector2Int(8, 2));
            EnemyAI.RunTurn(grid3, world3, econ3, t);
            Check(EmbarkSystem.IsEmbarked(world3, sailor), "AI embarks at its port to reach a target across water");
            for (int turn = 0; turn < 3 && EmbarkSystem.IsEmbarked(world3, sailor); turn++)
            {
                TurnSystem.StartTurn(world3, t, turn + 2);
                EnemyAI.RunTurn(grid3, world3, econ3, t);
            }
            Check(!EmbarkSystem.IsEmbarked(world3, sailor) && world3.Get<GridPosition>(sailor).Value.x >= 7, "AI sails across and lands near the target");
        }
    }
}
