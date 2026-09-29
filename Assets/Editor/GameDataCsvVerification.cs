using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 게임 규칙 CSV(Assets/Resources/*.csv) 검증 — 파일이 오류 없이 읽히는지, 헤더 기반 읽기(컬럼 순서 무관/모르는
    /// 컬럼 경고/잘못된 태그·지형 오류)가 동작하는지, 그리고 위키와 맞춘 값(해금 키/비용)이 CSV에 그대로 있는지를
    /// GameObject 없이 확인한다. 다른 Verification과 같은 패턴 — Console 로그의 PASS/FAIL만 보면 된다.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.GameDataCsvVerification.Run
    /// </summary>
    public static class GameDataCsvVerification
    {
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            var errors = GameDataLoader.LoadAll();
            Check(errors.Count == 0, "game data CSVs load without errors: " + string.Join(" | ", errors));
            VerifyReader();
            VerifyBuildingsAndActions();
            VerifyTechTreeMatchesWiki();
            VerifyCapitalVision();
            VerifyGameRules();
            VerifyRewardsAndTasks();
            VerifyUnitsAndCombat();
            VerifyModels();
            GameDataLoader.LoadAll(); // 규칙을 바꿔 본 검사 뒤 원래 CSV 값으로 되돌린다.
            Debug.Log(_ok ? "[GameDataCsvVerification] ALL PASS" : "[GameDataCsvVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[GameDataCsvVerification] FAIL: " + message);
        }

        private static void VerifyReader()
        {
            // 컬럼 순서를 바꾸고 모르는 컬럼/주석 행을 넣어도 같은 값으로 읽혀야 한다.
            var errors = new List<string>();
            var a = GameTableCsvSerializer.ParseBuildings("Id,Terrain,Cost,Population\nFarm,Field,5,2\n", errors);
            var b = GameTableCsvSerializer.ParseBuildings("Population,Memo,Cost,Terrain,Id\n#주석,,,,\n2,메모,5,Field,Farm\n", errors);
            Check(a.Length == 1 && b.Length == 1 && a[0].Cost == b[0].Cost && a[0].Population == b[0].Population && a[0].Terrain == b[0].Terrain,
                "header-based parse ignores column order and comment rows");
            Check(errors.Count == 1 && errors[0].Contains("Memo"), "unknown column reported once: " + string.Join(" | ", errors));

            errors.Clear();
            var bad = GameTableCsvSerializer.ParseBuildings("Id,Terrain,Cost,Flags,AdjacentBuildings\nX,Feild,abc,Raod,Nope\n", errors);
            Check(bad.Length == 1 && bad[0].Cost == 0, "bad number falls back to default");
            Check(errors.Exists(e => e.Contains("Feild")) && errors.Exists(e => e.Contains("abc")) && errors.Exists(e => e.Contains("Raod")) && errors.Exists(e => e.Contains("Nope")),
                "bad terrain/number/flag/adjacent id are all reported: " + string.Join(" | ", errors));
            Check(errors.TrueForAll(e => e.StartsWith("Buildings.csv:2 ")), "errors carry file:line");

            errors.Clear();
            GameTableCsvSerializer.ParseBuildings("Id,Terrain\nA,Field\nA,Forest\n", errors);
            Check(errors.Exists(e => e.Contains("중복")), "duplicate id reported");

            var nodes = TechCsvSerializer.Parse("Unlocks,Id,Tier\nBuild.Farm,A,2\n");
            Check(nodes.Count == 1 && nodes[0].Id == "A" && nodes[0].Tier == 2 && nodes[0].CostPerCity == 2 && nodes[0].Unlocks[0] == "Build.Farm",
                "tech CSV is header-based too (legacy Unlocks column)");

            // CLAUDE.md 규칙 6: 여러 값은 번호 붙은 반복 컬럼(한 칸에 값 하나). 빈 칸은 건너뛰고 헤더 순서대로 모은다.
            errors.Clear();
            var multi = GameTableCsvSerializer.ParseBuildings("Id,Terrain1,Terrain2,RequiredStructure1,RequiredStructure2,Flag1,Flag2\nW,Field,ShallowWater,Resource_Metal,,Neutral,Road\n", errors);
            Check(multi.Length == 1 && multi[0].Terrain == (TileClass.Field | TileClass.ShallowWater) && multi[0].RequiredStructures.Length == 1 &&
                  multi[0].AllowNeutral && multi[0].IsRoad && errors.Count == 0, "numbered list columns are read one value per cell: " + string.Join(" | ", errors));
            var numberedTech = TechCsvSerializer.Parse("Id,Unlock1,Unlock2\nA,Build.Farm,Unit.shield\n");
            Check(numberedTech[0].Unlocks.Length == 2 && numberedTech[0].Unlocks[1] == "Unit.shield", "tech Unlock1..N columns");
            var techRound = TechCsvSerializer.Parse(TechCsvSerializer.Write(numberedTech));
            Check(techRound[0].Unlocks.Length == 2 && !TechCsvSerializer.Write(numberedTech).Contains(";"), "tech writer emits numbered columns without ';'");
            var unitText = UnitCsvSerializer.Write(UnitCsvSerializer.Parse("Id,Action1,Action2\nu,Move,Attack\n"));
            Check(!unitText.Contains(";") && unitText.Contains("Action2") && UnitCsvSerializer.Parse(unitText)[0].Actions == (ActionType.Move | ActionType.Attack),
                "unit writer emits Action1..N and round-trips");
            errors.Clear();
            UnitCsvSerializer.Parse("Id,Action1\nu,Atack\n", errors);
            Check(errors.Exists(e => e.Contains("Atack")), "unknown action name is reported");

            // 배포되는 게임 데이터 CSV에는 한 칸에 여러 값(세미콜론 목록)이 없어야 한다(설명/메모 칸 제외).
            foreach (var file in new[] { "Buildings", "TileActions", "TechTree", "NavalUnits", "CityRewards", "Tasks", "GameRules" })
            {
                var text = Resources.Load<TextAsset>(file)?.text ?? string.Empty;
                var table = CsvTableReader.Parse(file + ".csv", text);
                bool packed = false;
                for (int r = 0; r < table.Rows.Count; r++)
                    for (int c = 0; c < table.Header.Length && c < table.Rows[r].Length; c++)
                    {
                        string h = table.Header[c];
                        if (h == "Description" || h == "Note" || h == "Effect" || h == "Wiki") continue;
                        if (table.Rows[r][c].Contains(";")) packed = true;
                    }
                Check(!packed, $"{file}.csv has one value per cell (rule 6)");
            }
        }

        private static void VerifyBuildingsAndActions()
        {
            Check(BuildingDefinition.All.Length == 21, $"21 buildings (14 + 7 monuments), got {BuildingDefinition.All.Length}");
            Check(TileActionDefinition.All.Length == 8, $"8 tile actions, got {TileActionDefinition.All.Length}");
            var bridge = TileImprovementSystem.FindBuilding(BuildingDefinition.Bridge);
            Check(bridge != null && bridge.Value.AllowNeutral && bridge.Value.RequiresOppositeLand && bridge.Value.ActsAsRoad && !bridge.Value.IsRoad, "bridge flags from CSV");
            var market = TileImprovementSystem.FindBuilding(BuildingDefinition.Market);
            Check(market != null && market.Value.ProducesGoldFromAdjacent && market.Value.AdjacentBuildings.Length == 3, "market adjacency from CSV");
            Check(BuildingDefinition.All.Count(b => !string.IsNullOrEmpty(b.TaskId)) == 7 && BuildingDefinition.All.Where(b => !string.IsNullOrEmpty(b.TaskId)).All(b => b.Population == 3 && b.Cost == 0),
                "7 monuments, pop 3, free");
            foreach (var b in BuildingDefinition.All)
                Check(string.IsNullOrEmpty(b.TaskId) || TaskDefinition.All.Any(t => t.Id == b.TaskId), $"{b.Id}: task '{b.TaskId}' exists");
            var burn = TileImprovementSystem.FindAction("BurnForest");
            Check(burn != null && burn.Value.Cost == 3, "burn forest costs 3 (wiki Burn Forest infobox)");
            var mine = TileImprovementSystem.FindBuilding(BuildingDefinition.Mine);
            Check(mine != null && mine.Value.Terrain == TileClass.Mountain && mine.Value.RequiredStructures.Contains("Resource_Metal"), "mine terrain/resource list from CSV");
        }

        private static void VerifyTechTreeMatchesWiki()
        {
            var nodes = GameDataLoader.LoadTechNodes();
            bool Has(string id, string key) => TechSystem.Find(nodes, id)?.Unlocks.Contains(key) == true;
            // 위키 Technology 표(2026 기준): 기사도 = 기사 + 파괴, 건축 = 풍차 + 화전, 전략(구 방패) = 방어병, 도로 = 교역망 과업, 외교 = 수도 시야.
            Check(Has("Chivalry", "Ability.Destroy") && !Has("Chivalry", "Ability.BurnForest"), "Chivalry unlocks Destroy");
            Check(Has("Construction", "Ability.BurnForest") && !Has("Construction", "Ability.Destroy"), "Construction unlocks Burn Forest");
            Check(Has("Strategy", "Unit.shield") && TechSystem.Find(nodes, "Diplomacy")?.ParentId == "Strategy", "Strategy (ex-Shields) -> Diplomacy");
            Check(Has("Roads", "Task.Network"), "Roads unlocks Network task");
            Check(Has("Diplomacy", VisionDefinition.CapitalVisionKey), "Diplomacy unlocks capital vision");
            Check(TechSystem.Find(nodes, "Climbing") != null && TechSystem.Find(nodes, "Organization") != null &&
                  TechSystem.Find(nodes, "Smithery") != null && TechSystem.Find(nodes, "Aquatism") != null, "tech ids use current wiki names");
            Check(TaskDefinition.All.First(t => t.Id == TaskDefinition.Network).UnlockKey == "Task.Network", "Network task gated by Task.Network");
        }

        private static void VerifyGameRules()
        {
            var fields = GameRulesCsvSerializer.AllFields();
            var text = Resources.Load<TextAsset>(GameDataLoader.GameRulesCsvResourcePath).text;
            var errors = new List<string>();
            var rows = GameRulesCsvSerializer.ParseRows(text, errors);
            Check(errors.Count == 0, "GameRules.csv rows valid: " + string.Join(" | ", errors));
            Check(rows.Count == fields.Count && rows.TrueForAll(r => fields.ContainsKey(r.Key)), $"every GameRules field has exactly one CSV row ({rows.Count} rows / {fields.Count} fields)");

            // 위키와 다른 규칙 목록(=원문과 다른 점) — 전부 Note가 있어야 한다(ParseRows가 검사). 로그로 남겨 문서와 대조한다.
            var deviations = rows.FindAll(r => r.Wiki.Length > 0 && r.Wiki != r.Value);
            var projectOnly = rows.FindAll(r => r.Wiki.Length == 0);
            Debug.Log($"[GameDataCsvVerification] rules: {rows.Count}, wiki-equal {rows.Count - deviations.Count - projectOnly.Count}, " +
                      $"deviations {deviations.Count} ({string.Join(", ", deviations.ConvertAll(r => r.Key))}), project-only {projectOnly.Count}");

            // CSV 값이 실제 시스템 동작을 바꾸는지: 도시 레벨당 골드 1 -> 2.
            var grid = new GridWorld(5, 5);
            var world = new EntityWorld();
            var city = new CityData { Owner = Team.Player, Level = 3, Position = new Vector2Int(2, 2), IsCapital = true };
            int before = CitySystem.CityGoldIncome(grid, world, city);
            errors.Clear();
            GameRulesCsvSerializer.Apply("Key,Value,Note\nCity.GoldPerLevel,2,테스트\n", errors);
            Check(GameRules.City.GoldPerLevel == 2 && CitySystem.CityGoldIncome(grid, world, city) == before + 3, "editing a CSV rule changes city income");
            Check(errors.Exists(e => e.Contains("City.MarketGoldCap") && e.Contains("행이 없음")), "missing rule rows are reported");

            errors.Clear();
            GameRulesCsvSerializer.Apply("Key,Value,Wiki\nCity.Nope,1,\nCity.MarketGoldCap,abc,8\n", errors);
            Check(errors.Exists(e => e.Contains("City.Nope")) && errors.Exists(e => e.Contains("abc")), "unknown key / bad value reported");
            Check(errors.Exists(e => e.Contains("위키 값(8)과 달라")), "deviation from wiki without a Note is reported");
            GameDataLoader.LoadAll();
            Check(GameRules.City.GoldPerLevel == 1 && GameRules.City.MarketGoldCap == 8, "reload restores CSV values");
        }

        private static void VerifyRewardsAndTasks()
        {
            Check(CityRewardDefinition.All.Length == 8 && TaskDefinition.All.Length == 7, "8 city rewards, 7 tasks from CSV");
            var lv2 = CitySystem.RewardOptions(2);
            var lv7 = CitySystem.RewardOptions(7);
            Check(lv2.Length == 2 && lv2.Contains(CityRewardType.Workshop) && lv2.Contains(CityRewardType.Explorer), "level 2 rewards");
            Check(lv7.Length == 2 && lv7.Contains(CityRewardType.Park) && lv7.Contains(CityRewardType.SuperUnit), "level 5+ rewards repeat the top row");
            Check(CitySystem.RewardAmount(CityRewardType.Resources) == 5 && CitySystem.RewardAmount(CityRewardType.PopulationGrowth) == 3 &&
                  CitySystem.RewardAmount(CityRewardType.BorderGrowth) == 2 && CitySystem.RewardAmount(CityRewardType.Workshop) == 1, "reward amounts (wiki City)");
            var pacifist = TaskDefinition.All.First(t => t.Id == TaskDefinition.Pacifist);
            Check(pacifist.Kind == TaskKind.TurnsWithoutAttack && pacifist.Threshold == 5 && pacifist.UnlockKey == "Task.Pacifist", "pacifist task from CSV");
        }

        // ---------- 3차: 유닛 CSV(위키 원값) + 위키 전투 공식 ----------

        private static List<UnitCsvRow> LoadSandboxUnits() =>
            UnitCsvSerializer.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "SandboxUnits.csv")));

        private static int Spawn(GridWorld grid, EntityWorld world, List<UnitCsvRow> rows, string id, Team team, Vector2Int pos) =>
            UnitFactorySystem.CreateFromCsv(grid, world, team, rows.First(r => r.Id == id), pos);

        private static GridWorld Field(int w, int h)
        {
            var grid = new GridWorld(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            return grid;
        }

        private static void VerifyUnitsAndCombat()
        {
            var rows = LoadSandboxUnits();
            var knightRow = rows.FirstOrDefault(r => r.Id == "knight");
            var spyRow = rows.FirstOrDefault(r => r.Id == "spy");
            Check(knightRow != null && Mathf.Approximately(knightRow.AttackAttack, 3.5f) && spyRow != null && Mathf.Approximately(spyRow.Defense, 0.5f),
                "unit CSV keeps wiki decimals (knight attack 3.5, cloak defence 0.5)");
            Check(rows.Where(r => (r.Actions & ActionType.Stiff) == 0).All(r => (r.Actions & ActionType.Counter) != 0), "every non-stiff wiki unit retaliates (Counter)");

            // 위키 Combat 예시값 — 체력 가득, 보너스 없음.
            var grid = Field(8, 8);
            var world = new EntityWorld();
            int w1 = Spawn(grid, world, rows, "infantry", Team.Player, new Vector2Int(1, 1));
            int w2 = Spawn(grid, world, rows, "infantry", Team.Enemy, new Vector2Int(2, 1));
            Check(CombatSystem.TryAttack(grid, world, w1, w2, out int d, out int c) && d == 5 && c == 5, $"warrior vs warrior = 5 / 5 retaliation (got {d}/{c})");

            int rider = Spawn(grid, world, rows, "cavalry", Team.Player, new Vector2Int(1, 3));
            int defender = Spawn(grid, world, rows, "shield", Team.Enemy, new Vector2Int(2, 3));
            Check(CombatSystem.PreviewRetaliation(world, rider, defender) == 8, "battle preview predicts the retaliation");
            Check(CombatSystem.TryAttack(grid, world, rider, defender, out d, out c) && d == 4 && c == 8, $"rider vs defender = 4 / 8 (got {d}/{c})");

            // 기사 -> 궁수: 12 피해로 처치 + 근접이라 그 칸으로 전진(위키 Combat).
            int knight = Spawn(grid, world, rows, "knight", Team.Player, new Vector2Int(4, 1));
            int archer = Spawn(grid, world, rows, "archer", Team.Enemy, new Vector2Int(5, 1));
            Check(CombatSystem.TryAttack(grid, world, knight, archer, out d, out c) && d == 12 && c == 0 && !UnitQueries.IsAlive(world, archer), $"knight kills archer with 12 (got {d})");
            Check(world.Get<GridPosition>(knight).Value == new Vector2Int(5, 1) && grid.GetOccupant(new Vector2Int(5, 1)) == knight, "melee unit advances into the killed unit's tile");

            // 원거리 유닛은 근접 거리에서 처치해도 움직이지 않는다.
            int archer2 = Spawn(grid, world, rows, "archer", Team.Player, new Vector2Int(6, 6));
            int victim = Spawn(grid, world, rows, "infantry", Team.Enemy, new Vector2Int(7, 6));
            world.Set(victim, new Hp { Value = 1 });
            Check(CombatSystem.TryAttack(grid, world, archer2, victim, out _, out _) && !UnitQueries.IsAlive(world, victim) &&
                  world.Get<GridPosition>(archer2).Value == new Vector2Int(6, 6), "ranged unit does not advance after a kill");

            // 투석기 -> 검사(거리 3): 10 피해, 검사 사거리 밖이라 반격 없음.
            int cat = Spawn(grid, world, rows, "catapult", Team.Player, new Vector2Int(0, 6));
            int sword = Spawn(grid, world, rows, "gladiator", Team.Enemy, new Vector2Int(3, 6));
            Check(CombatSystem.TryAttack(grid, world, cat, sword, out d, out c) && d == 10 && c == 0, $"catapult vs swordsman = 10, no retaliation out of range (got {d}/{c})");

            // 성벽(x4): 체력 3인 전사는 방어병에게 0 피해, 반격으로 죽는다(위키 Defender 문서).
            var g2 = Field(4, 4);
            var w2d = new EntityWorld();
            int def2 = Spawn(g2, w2d, rows, "shield", Team.Enemy, new Vector2Int(1, 1));
            int weak = Spawn(g2, w2d, rows, "infantry", Team.Player, new Vector2Int(2, 1));
            w2d.Set(def2, new PositionalDefenseBonus { Value = 2 });
            w2d.Set(weak, new Hp { Value = 3 });
            Check(CombatSystem.TryAttack(g2, w2d, weak, def2, out d, out c) && d == 0 && !UnitQueries.IsAlive(w2d, weak), $"damaged warrior deals 0 to a walled defender and dies (got {d}/{c})");

            // 방어 보너스는 겹치지 않는다: 숲(궁술) + 요새화 도시 = x1.5 한 번.
            var g3 = Field(5, 5);
            var w3 = new EntityWorld();
            var econ = new EconomyWorld { TechNodes = GameDataLoader.LoadTechNodes() };
            foreach (var team in CitySystem.Teams) { econ.Resources[team] = new CityResourceData(); econ.Tech[team] = TechTreeData.CreateEmpty(); }
            TaskSystem.Init(econ);
            var forestCity = new Vector2Int(2, 2);
            g3.SetTileType(forestCity, TerrainGenerationSystem.ForestTileId);
            CitySystem.FoundCity(g3, econ, forestCity, Team.Player, true, "숲");
            econ.Tech[Team.Player].Unlocked.Add("Hunting"); econ.Tech[Team.Player].Unlocked.Add("Archery");
            int fortArcher = Spawn(g3, w3, rows, "archer", Team.Player, forestCity);
            TechEffectSystem.RefreshUnits(g3, w3, econ);
            Check(w3.Get<PositionalDefenseBonus>(fortArcher).Value == 1 && Mathf.Approximately(CombatSystem.DefenseMultiplier(w3, fortArcher), 1.5f),
                "forest + fortified city bonus does not stack (x1.5 once)");

            // 스플래시 = 그 대상에게 계산한 피해의 절반(내림).
            var g4 = Field(6, 6);
            var w4 = new EntityWorld();
            var bomberRow = NavalUnitDefinition.Upgrades.First(u => u.Row.Id == "bomber").Row;
            var splasher = new UnitCsvRow { Id = "splash", MaxHp = 10, Defense = 2, Actions = bomberRow.Actions | ActionType.Move, MoveRange = 1,
                AttackAttack = bomberRow.AttackAttack, AttackRange = bomberRow.AttackRange, Domain = TerrainType.Land };
            int bomber = UnitFactorySystem.CreateFromCsv(g4, w4, Team.Player, splasher, new Vector2Int(0, 2));
            int main = Spawn(g4, w4, rows, "infantry", Team.Enemy, new Vector2Int(3, 2));
            int side = Spawn(g4, w4, rows, "infantry", Team.Enemy, new Vector2Int(3, 3));
            int expectedSplash = CombatSystem.CalculateDamage(w4, bomber, side) / 2;
            Check(CombatSystem.TryAttack(g4, w4, bomber, main, out d, out _) && w4.Get<Hp>(side).Value == 10 - expectedSplash && expectedSplash > 0,
                $"splash = half of the computed damage ({expectedSplash})");

            // 슈퍼 유닛: 거인은 훈련 불가, 레벨 5+ 보상으로 소환, 점수 50.
            var econ2 = new EconomyWorld { TechNodes = GameDataLoader.LoadTechNodes(), UnitRows = rows };
            foreach (var team in CitySystem.Teams) { econ2.Resources[team] = new CityResourceData { Gold = 100 }; econ2.Tech[team] = TechTreeData.CreateEmpty(); }
            TaskSystem.Init(econ2);
            var g5 = Field(5, 5);
            int ci = CitySystem.FoundCity(g5, econ2, new Vector2Int(2, 2), Team.Player, true, "수도");
            Check(CitySystem.StrongestUnitId(econ2) == "giant", "super unit reward spawns the giant");
            Check(!CitySystem.CanTrain(g5, new EntityWorld(), econ2, Team.Player, ci, rows.First(r => r.Id == "giant"), out _), "giant can't be trained");
            var w5 = new EntityWorld();
            Spawn(g5, w5, rows, "giant", Team.Player, new Vector2Int(0, 0));
            Check(ScoreSystem.ComputeBreakdown(g5, w5, econ2, Team.Player).Units == GameRules.Score.SuperUnit, "giant is worth 50 points");

            // 배 유닛 CSV(위키 원값).
            var rammer = NavalUnitDefinition.Upgrades.First(u => u.Row.Id == "rammer");
            Check(NavalUnitDefinition.Raft.Defense == 1 && NavalUnitDefinition.Raft.MoveRange == 2 && Mathf.Approximately(rammer.Row.AttackAttack, 3f) &&
                  rammer.UnlockKey == "Unit.rammer" && NavalUnitDefinition.Upgrades.Length == 3, "naval units from CSV with wiki stats");
        }

        // ---------- 모델 파츠 CSV (docs/ModelingPlan.md) ----------

        private static void VerifyModels()
        {
            Check(ModelDefinition.Palette.Count > 0 && ModelDefinition.Models.Count > 0, "model palette/parts loaded");
            foreach (var b in BuildingDefinition.All)
                Check(b.IsRoad || ModelDefinition.Models.ContainsKey(BuildingMarkerView.BuildingModelPrefix + b.Id), $"building '{b.Id}' has a model row");
            foreach (var id in new[] { "City.Houses", "City.Capital", "City.Wall", "City.Workshop", "City.Park", "City.Flag",
                                       "Tile.Land", "Tile.Shallow", "Tile.Ocean", "Terrain.Forest", "Terrain.Mountain", "Terrain.Cloud", "Road.Center", "Road.Arm" })
                Check(ModelDefinition.Models.ContainsKey(id), $"model '{id}'");

            foreach (var row in LoadSandboxUnits())
                Check(ModelDefinition.Models.ContainsKey("Unit." + row.Id), $"unit '{row.Id}' has a block model (UnitModels.csv)");
            Check(ModelDefinition.Models.ContainsKey("Boat." + NavalUnitDefinition.RaftId) && NavalUnitDefinition.Upgrades.All(u => ModelDefinition.Models.ContainsKey("Boat." + u.Row.Id)),
                "every naval unit has a boat model");

            // 이어쓰기 행(Model 칸 비움)과 팔레트/레벨 칸.
            var errors = new List<string>();
            var models = new Dictionary<string, List<ModelPartInfo>>();
            ModelCsvSerializer.ParseInto("Model,Shape,SX,SY,Color,MinLevel\nA,Box,1,1,Stone,\n,Cone,0.5,1,#FF0000,3\n,Box,1,1,Nope,\n", ModelDefinition.Palette, models, errors, "t.csv");
            Check(models.TryGetValue("A", out var a) && a.Count == 3 && a[1].MinLevel == 3 && a[1].Size.z == 0.5f && a[1].Color == Color.red,
                "blank Model cell continues the previous model; SZ defaults to SX; #hex colors");
            Check(errors.Count == 1 && errors[0].Contains("Nope"), "unknown palette color reported: " + string.Join(" | ", errors));

            // 조립: 조각이 많아도 렌더러 1개, 레벨에 따라 조각 수가 바뀐다(대장간 레벨 0 = 불빛 없음).
            var forge0 = ModelBuilder.Build("Building.Forge", null, Vector3.zero, 0);
            var forge4 = ModelBuilder.Build("Building.Forge", null, Vector3.zero, 4);
            Check(forge0 != null && forge0.GetComponentsInChildren<Renderer>().Length == 1, "a model is one renderer");
            Check(forge4.GetComponent<MeshFilter>().sharedMesh.triangles.Length > forge0.GetComponent<MeshFilter>().sharedMesh.triangles.Length,
                "level adds parts (forge lights)");
            Object.DestroyImmediate(forge0); Object.DestroyImmediate(forge4);

            // 표시 레벨: 광산 없는 대장간 = 0, 인접 광산 2 = 2, 신전 = 지난 턴.
            var grid = Field(5, 5);
            void Put(Vector2Int p, string id, int turn = 1) { var t = grid.GetTile(p); t.BuildingId = id; t.OwnerTeam = (int)Team.Player; t.BuildingTurn = turn; grid.SetTile(p, t); }
            Put(new Vector2Int(2, 2), BuildingDefinition.Forge);
            Check(TileImprovementSystem.DisplayLevel(grid, new Vector2Int(2, 2), 1) == 0, "forge with no mines = level 0");
            Put(new Vector2Int(1, 2), BuildingDefinition.Mine); Put(new Vector2Int(3, 3), BuildingDefinition.Mine);
            Check(TileImprovementSystem.DisplayLevel(grid, new Vector2Int(2, 2), 1) == 2, "forge next to 2 mines = level 2");
            Put(new Vector2Int(0, 0), "Temple", 1);
            Check(TileImprovementSystem.DisplayLevel(grid, new Vector2Int(0, 0), 13) == 5, "temple built on turn 1 is level 5 on turn 13");
        }

        private static void VerifyCapitalVision()
        {
            var grid = new GridWorld(12, 12) { FogEnabled = true };
            for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            var econ = new EconomyWorld { TechNodes = GameDataLoader.LoadTechNodes() };
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData();
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            TaskSystem.Init(econ);
            var enemyCap = new Vector2Int(10, 10);
            CitySystem.FoundCity(grid, econ, new Vector2Int(1, 1), Team.Player, true, "P");
            CitySystem.FoundCity(grid, econ, enemyCap, Team.Enemy, true, "E");
            var world = new EntityWorld();
            VisionSystem.Refresh(grid, world, econ, null);
            Check(!VisionSystem.IsExplored(grid, Team.Player, enemyCap), "enemy capital hidden without Diplomacy");
            econ.Tech[Team.Player].Unlocked.Add("Diplomacy");
            VisionSystem.Refresh(grid, world, econ, null);
            Check(VisionSystem.IsExplored(grid, Team.Player, enemyCap), "Diplomacy reveals the enemy capital");
            Check(!VisionSystem.IsExplored(grid, Team.Player, enemyCap + Vector2Int.left * 2), "only the capital tile, not its surroundings");
        }
    }
}
