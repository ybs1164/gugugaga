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
                "tech CSV is header-based too");
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
