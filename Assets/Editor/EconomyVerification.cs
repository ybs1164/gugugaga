using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 경제 메인 루프(기술트리 CSV/비용 공식, 도시 창설·영토·채집·건설·레벨업·보상, 마을 점령, 도로 수도 연결,
    /// 지형 이동 제한/방어 보너스, 영토 회복, 적 경제 AI, 도시 전멸 패배)를 GameObject 없이 System 호출만으로
    /// 검증하는 배치모드 전용 스크립트. UIVerification과 같은 패턴 — Console 로그의 PASS/FAIL만 보면 된다.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.EconomyVerification.Run
    /// </summary>
    public static class EconomyVerification
    {
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            VerifyTechCsv();
            VerifyTechCost();
            VerifyCityLoop();
            VerifyEnemyAiAndDefeat();
            Debug.Log(_ok ? "[EconomyVerification] ALL PASS" : "[EconomyVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[EconomyVerification] FAIL: " + message);
        }

        private static List<TechNodeData> LoadNodes() =>
            TechCsvSerializer.Parse(Resources.Load<TextAsset>(TechTreeDefinition.CsvResourcePath).text);

        private static void VerifyTechCsv()
        {
            var nodes = LoadNodes();
            Check(nodes.Count == 25, $"TechTree.csv should have 25 nodes, got {nodes.Count}");
            foreach (var n in nodes)
            {
                Check(string.IsNullOrEmpty(n.ParentId) || TechSystem.Find(nodes, n.ParentId) != null, $"{n.Id}: parent '{n.ParentId}' missing");
                Check(!string.IsNullOrEmpty(n.Branch) && TechSystem.Find(nodes, n.Branch) != null, $"{n.Id}: branch '{n.Branch}' missing");
                Check(n.Unlocks != null && n.Unlocks.Length > 0, $"{n.Id}: no unlock keys");
            }

            // 해금 키가 실제로 무언가를 가리키는지(오타 방지): Build.* = 건물 표, Harvest./Ability. = 타일 행동 표.
            var known = new HashSet<string> { "Move.Mountain", "Move.Ocean", "Defense.Mountain", "Defense.Forest", "Defense.Water", TechSystem.LiteracyKey, RuinSystem.DisbandKey };
            foreach (var b in BuildingDefinition.All) known.Add(b.UnlockKey);
            foreach (var a in TileActionDefinition.All) known.Add(a.UnlockKey);
            foreach (var n in nodes)
                foreach (var k in n.Unlocks)
                    Check(known.Contains(k) || k.StartsWith(TechSystem.UnitKeyPrefix) || k.StartsWith(TechSystem.RevealKeyPrefix), $"{n.Id}: unknown unlock key '{k}'");

            var round = TechCsvSerializer.Parse(TechCsvSerializer.Write(nodes));
            Check(round.Count == nodes.Count, "tech CSV round-trip count");
            for (int i = 0; i < round.Count && i < nodes.Count; i++)
                Check(round[i].Id == nodes[i].Id && round[i].Effect == nodes[i].Effect && string.Join(";", round[i].Unlocks) == string.Join(";", nodes[i].Unlocks)
                      && round[i].CostPerCity == nodes[i].CostPerCity, $"tech CSV round-trip mismatch at {nodes[i].Id}");

            // 인용 부호 안의 쉼표.
            var quoted = TechCsvSerializer.Parse("Id,Name,Branch,Parent,Tier,Slot,Icon,CostBase,CostPerCity,Unlocks,Effect\nA,에이,,,1,0,x,,,Build.Farm,\"쉼표, 포함\"\n");
            Check(quoted.Count == 1 && quoted[0].Effect == "쉼표, 포함" && quoted[0].Branch == "A" && quoted[0].CostBase == 4 && quoted[0].CostPerCity == 1, "quoted CSV field / defaults");
        }

        private static void VerifyTechCost()
        {
            var nodes = LoadNodes();
            var tech = TechTreeData.CreateEmpty();
            int Cost(string id, int cities) => TechSystem.Cost(nodes, tech, TechSystem.Find(nodes, id).Value, cities);
            // 위키 표: 1도시 5/6/7, 3도시 7/10/13.
            Check(Cost("Riding", 1) == 5 && Cost("Roads", 1) == 6 && Cost("Trade", 1) == 7, "cost with 1 city (5/6/7)");
            Check(Cost("Riding", 3) == 7 && Cost("Roads", 3) == 10 && Cost("Trade", 3) == 13, "cost with 3 cities (7/10/13)");
            tech.Unlocked.Add("Mountaineering"); tech.Unlocked.Add("Meditation"); tech.Unlocked.Add("Philosophy");
            // Literacy: 1도시 4/4/5, 2도시 T3 7.
            Check(Cost("Riding", 1) == 4 && Cost("Roads", 1) == 4 && Cost("Trade", 1) == 5 && Cost("Trade", 2) == 7, "literacy discount");
        }

        private static int MakeUnit(EntityWorld world, GridWorld grid, Team team, Vector2Int pos, List<IUnitAction> actions = null)
        {
            int id = world.CreateEntity();
            world.Set(id, team);
            world.Set(id, new GridPosition { Value = pos });
            world.Set(id, new Hp { Value = 10 });
            world.Set(id, new MaxHp { Value = 10 });
            world.Set(id, new Attack { Value = 3 });
            world.Set(id, new Defense { Value = 1 });
            world.Set(id, new AttackRange { Value = 1 });
            world.Set(id, new HasMoved { Value = false });
            world.Set(id, new HasActed { Value = false });
            world.Set(id, new IsGuarding { Value = false });
            world.Set(id, new Accelerated { Value = false });
            world.Set(id, new Frozen { Value = false });
            world.Set(id, new MoveRange { Value = 2 });
            world.Set(id, new MoveDomain { Value = TerrainType.Land });
            world.Set(id, new UnitTypeId { Value = "infantry" });
            world.Set(id, new TerrainAccess { Mountain = true, Ocean = true });
            world.Set(id, new PositionalDefenseBonus { Value = 0 });
            var list = actions ?? new List<IUnitAction> { MoveAction.FromCsv(2) };
            world.Set(id, new UnitActions { Value = list });
            grid.PlaceOccupant(pos, id);
            return id;
        }

        private static EconomyWorld NewEconomy()
        {
            var econ = new EconomyWorld { TechNodes = LoadNodes() };
            econ.UnitRows.Add(new UnitCsvRow { Id = "infantry", Name = "보병", MaxHp = 10, Cost = 2, BaseVisual = "Melee" });
            econ.UnitRows.Add(new UnitCsvRow { Id = "shield", Name = "방패병", MaxHp = 16, Cost = 3, BaseVisual = "Guard" });
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Gold = 5, Development = 5, MaxFaith = 10 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            return econ;
        }

        private static void Give(EconomyWorld econ, Team team, int gold, int dev)
        {
            var r = econ.Resources[team];
            r.Gold += gold;
            r.Development += dev;
            econ.Resources[team] = r;
        }

        private static void VerifyCityLoop()
        {
            var grid = new GridWorld(10, 10);
            for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            var world = new EntityWorld();
            var econ = NewEconomy();
            var p = Team.Player;

            int unit = MakeUnit(world, grid, p, new Vector2Int(2, 2));
            CitySystem.InitializeCapitals(grid, world, econ);
            Check(econ.Cities.Count == 1 && econ.Cities[0].IsCapital && econ.Cities[0].Owner == p, "player capital founded");
            var cap = econ.Cities[0].Position;
            Check(grid.GetStructure(cap) == StructureGenerationSystem.CapitalStructureId, "capital structure placed when map had none");
            int owned = 0;
            for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) if (grid.GetTile(new Vector2Int(x, y)).OwnerTeam == (int)p) owned++;
            Check(owned == 9, $"capital territory 3x3, got {owned}");
            Check(CitySystem.GoldIncome(grid, world, econ, p) == 2 && CitySystem.DevelopmentIncome(econ, p) == 2, "L1 capital income 2 gold / 2 dev");
            Check(CitySystem.UnitCapacity(econ, p) == 2, "L1 unit capacity 2");
            Check(WaitAction.IsInOwnTerritory(grid, world, unit), "unit on own territory heals more");

            // 채집: 기술 전엔 선택지 없음(숨김), 채집(Gathering) 해금 후 인구 +1.
            var fruit1 = cap + new Vector2Int(1, 0);
            var fruit2 = cap + new Vector2Int(-1, 0);
            grid.SetStructure(fruit1, "Resource_Fruit");
            grid.SetStructure(fruit2, "Resource_Fruit");
            Check(!TileImprovementSystem.GetOptions(grid, econ, p, fruit1).Exists(o => o.Id == "HarvestFruit"), "fruit not harvestable before Gathering");
            var res = econ.Resources[p];
            Check(TechSystem.Unlock(econ.TechNodes, econ.Tech[p], ref res, 1, "Gathering"), "unlock Gathering with 5 dev");
            econ.Resources[p] = res;
            Check(econ.Resources[p].Development == 0, "Gathering cost 5 dev");
            var log = new List<EconomyLogEntry>();
            Check(TileImprovementSystem.Execute(grid, econ, p, fruit1, "HarvestFruit", log), "harvest fruit");
            Check(econ.Cities[0].Population == 1 && econ.Resources[p].Gold == 3 && grid.GetStructure(fruit1) == "", "harvest: +1 pop, -2 gold, fruit removed");
            Check(TileImprovementSystem.Execute(grid, econ, p, fruit2, "HarvestFruit", log), "harvest fruit 2");
            Check(econ.Cities[0].Level == 2 && econ.Cities[0].Population == 0 && econ.Cities[0].PendingRewards == 1, "level up to 2 at 2 pop");
            Check(log.Exists(e => e.Kind == EconomyLogKind.LevelUp), "level up logged");
            Check(!CitySystem.ApplyReward(grid, econ, 0, CityRewardType.Park, log), "wrong-level reward rejected");
            Check(CitySystem.ApplyReward(grid, econ, 0, CityRewardType.Workshop, log), "workshop reward");
            Check(CitySystem.GoldIncome(grid, world, econ, p) == 4, "L2 capital + workshop = 4 gold");

            // 농장 + 풍차 인접.
            econ.Tech[p].Unlocked.Add("Farming");
            econ.Tech[p].Unlocked.Add("Construction");
            Give(econ, p, 20, 0);
            var crop = cap + new Vector2Int(0, 1);
            var mill = cap + new Vector2Int(1, 1);
            grid.SetStructure(crop, "Resource_Crop");
            Check(TileImprovementSystem.GetOptions(grid, econ, p, mill).Exists(o => o.Id == "Windmill" && !o.Enabled), "windmill disabled without adjacent farm");
            Check(TileImprovementSystem.Execute(grid, econ, p, crop, "Farm", log), "build farm");
            Check(grid.GetTile(crop).BuildingId == "Farm" && grid.GetStructure(crop) == "", "farm consumes crop");
            Check(econ.Cities[0].Population == 2, $"farm +2 pop (got {econ.Cities[0].Population})");
            Check(TileImprovementSystem.Execute(grid, econ, p, mill, "Windmill", log), "build windmill next to farm");
            Check(econ.Cities[0].Level == 3 && econ.Cities[0].Population == 0, $"windmill +1 pop -> level 3 (L{econ.Cities[0].Level} pop {econ.Cities[0].Population})");
            Check(TileImprovementSystem.Execute(grid, econ, p, mill, "DestroyBuilding", log), "destroy windmill");
            Check(econ.Cities[0].Population == -1 && grid.GetTile(mill).BuildingId == "", "destroy returns population");

            // 마을 점령 -> 도시 2개 -> 기술 비용 상승.
            var village = new Vector2Int(8, 8);
            grid.SetStructure(village, StructureGenerationSystem.VillageStructureId);
            grid.RemoveOccupant(world.Get<GridPosition>(unit).Value);
            world.Set(unit, new GridPosition { Value = village });
            grid.PlaceOccupant(village, unit);
            world.Set(unit, new HasMoved { Value = true });
            Check(!CitySystem.CanCapture(grid, world, econ, unit), "cannot capture right after moving onto village");
            world.Set(unit, new HasMoved { Value = false });
            Check(CitySystem.Capture(grid, world, econ, unit, log) == 1, "capture village");
            Check(CitySystem.CountCities(econ, p) == 2 && world.Get<HasActed>(unit).Value, "2 cities, capture ends turn");
            Check(TechSystem.Cost(econ.TechNodes, econ.Tech[p], TechSystem.Find(econ.TechNodes, "Riding").Value, 2) == 6, "T1 cost with 2 cities = 6");

            // 도로로 수도 연결 -> 연결 도시 +1 인구, 수도 +1 인구, 발전도 +1.
            econ.Tech[p].Unlocked.Add("Roads");
            Give(econ, p, 100, 0);
            int capPopBefore = econ.Cities[0].Population;
            for (int i = 1; i < 8; i++)
            {
                var r = new Vector2Int(Mathf.Min(cap.x + i, 8), Mathf.Min(cap.y + i, 8));
                if (r == village || CitySystem.FindCityAt(econ, r) >= 0 || grid.GetTile(r).HasRoad) continue;
                Check(TileImprovementSystem.Execute(grid, econ, p, r, "Road", log), $"road at {r}");
            }
            Check(econ.Cities[1].ConnectedToCapital, "village connected by road");
            Check(econ.Cities[1].Population == 1 && econ.Cities[0].Population == capPopBefore + 1, "connection gives +1 pop both ends");
            Check(CitySystem.DevelopmentIncome(econ, p) == 2 + 2, $"dev income 2 cities + 1 connection + capital (got {CitySystem.DevelopmentIncome(econ, p)})");

            // 산 이동 제한/방어.
            var mountain = new Vector2Int(5, 0);
            grid.SetTileType(mountain, TerrainGenerationSystem.MountainTileId);
            TechEffectSystem.RefreshUnits(grid, world, econ);
            Check(TechEffectSystem.IsTerrainLocked(grid, world, unit, mountain), "mountain locked without Mountaineering");
            econ.Tech[p].Unlocked.Add("Mountaineering");
            TechEffectSystem.RefreshUnits(grid, world, econ);
            Check(!TechEffectSystem.IsTerrainLocked(grid, world, unit, mountain), "mountain open with Mountaineering");

            // 요새화 유닛의 도시 방어.
            int fort = MakeUnit(world, grid, p, cap, new List<IUnitAction> { MoveAction.FromCsv(1), new FortifyAction() });
            TechEffectSystem.RefreshUnits(grid, world, econ);
            Check(world.Get<PositionalDefenseBonus>(fort).Value == CitySystem.CityDefenseBonus, "fortify in city +1");
            Check(CombatSystem.EffectiveDefense(world, fort) == 1 + CitySystem.CityDefenseBonus, "effective defense includes positional bonus");

            // 훈련.
            Give(econ, p, 10, 0);
            Check(!CitySystem.CanTrain(grid, world, econ, p, 0, econ.UnitRows[0], out _), "can't train on occupied city tile");
            Check(CitySystem.CanTrain(grid, world, econ, p, 1, econ.UnitRows[0], out var why0) || why0 == "도시 칸이 비어있지 않음", "train infantry in village (or blocked by capturer)");
            Check(!TechSystem.CanTrainUnitType(econ.TechNodes, econ.Tech[p], "shield"), "shield locked without Shields");
        }

        private static void VerifyEnemyAiAndDefeat()
        {
            var grid = new GridWorld(12, 12);
            for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            var world = new EntityWorld();
            var econ = NewEconomy();
            MakeUnit(world, grid, Team.Player, new Vector2Int(1, 1));
            int enemy = MakeUnit(world, grid, Team.Enemy, new Vector2Int(10, 10));
            CitySystem.InitializeCapitals(grid, world, econ);
            Check(CitySystem.CountCities(econ, Team.Enemy) == 1 && CitySystem.CountCities(econ, Team.Player) == 1, "both capitals founded");

            var fruit = econ.Cities[CitySystem.FindCapital(econ, Team.Enemy)].Position + new Vector2Int(-1, 0);
            grid.SetStructure(fruit, "Resource_Fruit");
            Give(econ, Team.Enemy, 10, 0);
            var log = EconomyAI.RunTurn(grid, world, econ, Team.Enemy);
            Check(log.Exists(e => e.Kind == EconomyLogKind.Research), "enemy AI researches");
            Check(log.Exists(e => e.Kind == EconomyLogKind.Train && !string.IsNullOrEmpty(e.SpawnUnitId)) || grid.IsOccupied(econ.Cities[CitySystem.FindCapital(econ, Team.Enemy)].Position),
                "enemy AI trains when city tile is free");

            // 적 유닛이 플레이어 수도에서 턴을 시작 -> 점령 -> 플레이어 패배.
            var playerCap = econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position;
            grid.RemoveOccupant(world.Get<GridPosition>(enemy).Value);
            if (grid.IsOccupied(playerCap)) grid.RemoveOccupant(playerCap);
            world.Set(enemy, new GridPosition { Value = playerCap });
            grid.PlaceOccupant(playerCap, enemy);
            world.Set(enemy, new HasMoved { Value = false });
            world.Set(enemy, new HasActed { Value = false });
            Check(CitySystem.IsBesieged(grid, world, econ.Cities[0]) || CitySystem.IsBesieged(grid, world, econ.Cities[1]), "enemy on capital besieges it");
            EconomyAI.RunTurn(grid, world, econ, Team.Enemy);
            Check(CitySystem.HasLost(econ, Team.Player), "player loses after last city captured");
            Check(grid.GetTile(playerCap).OwnerTeam == (int)Team.Enemy, "captured city's territory switches team");

            var targets = new List<Vector2Int> { new Vector2Int(5, 5) };
            var entries = EnemyAI.RunTurn(grid, world, targets);
            Check(entries != null, "EnemyAI runs with capture targets");
        }
    }
}
