using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 적 시뮬레이션 하네스 — 화면 없이(GameObject/View 없음) 양 팀 모두 AI(EconomyAI + EnemyAI)로 여러 맵/시드에서 N턴을 돌린다.
    /// 전투 화면의 턴 흐름(BattleController: 턴 시작 수입 → 시야/과업 → 경제 AI → 스폰 → 군사 AI → 턴 종료 대기/과업)을
    /// 같은 System 호출 순서로 재현하고, 유닛 스폰은 UnitFactorySystem으로 한다.
    ///
    /// 출력: Logs/Simulation/&lt;시나리오&gt;.csv(턴·팀별 별/수입/도시/연결/건물/기술/유닛/탐험/점수) + Console 요약.
    /// 판정(ALL PASS 형식): 예외 없이 완주, 양 팀 모두 탐험/연구/건설 진행, 같은 시드 두 번 실행 결과 동일, 전체 시나리오에서
    /// 핵심 건물(도로/항구/가공 건물/시장/신전)이 한 번 이상 지어지고 물 맵에서 승선이 일어남.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.EconomySimulation.Run
    /// </summary>
    public static class EconomySimulation
    {
        private const int MaxTurns = 40;
        private const string BiomeCsv = "Assets/Resources/Tables/Biomes.csv";

        private static bool _ok;

        private struct Scenario
        {
            public string Name;
            public TerrainGenerationSystem.MapShapeMode Mode;
            public float Wetness;
            public int Size;
            public int Seed;
            public bool WaterMap;
        }

        private static readonly Scenario[] Scenarios =
        {
            new Scenario { Name = "Continents_16_s1", Mode = TerrainGenerationSystem.MapShapeMode.Continents, Wetness = 0.55f, Size = 16, Seed = 1, WaterMap = true },
            new Scenario { Name = "Continents_16_s7", Mode = TerrainGenerationSystem.MapShapeMode.Continents, Wetness = 0.55f, Size = 16, Seed = 7, WaterMap = true },
            new Scenario { Name = "Pangea_14_s3", Mode = TerrainGenerationSystem.MapShapeMode.Pangea, Wetness = 0.50f, Size = 14, Seed = 3 },
            new Scenario { Name = "Lakes_14_s4", Mode = TerrainGenerationSystem.MapShapeMode.Lakes, Wetness = 0.275f, Size = 14, Seed = 4 },
            new Scenario { Name = "Archipelago_16_s5", Mode = TerrainGenerationSystem.MapShapeMode.Archipelago, Wetness = 0.70f, Size = 16, Seed = 5, WaterMap = true },
            new Scenario { Name = "Drylands_14_s6", Mode = TerrainGenerationSystem.MapShapeMode.Freeform, Wetness = 0.05f, Size = 14, Seed = 6 },
        };

        /// <summary>한 판의 누적 관측치.</summary>
        private class Result
        {
            public string Csv;
            public readonly HashSet<string> BuildingsBuilt = new HashSet<string>();
            public int Embarks, Disembarks, Upgrades, Captures, Explorers, Tasks, Monuments, LighthousesFound;
            public int LastTurn;
            public string Winner = "-";
            public readonly Dictionary<Team, int> ExploredStart = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> ExploredEnd = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> Techs = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> Score = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> Cities = new Dictionary<Team, int>();
        }

        public static void Run()
        {
            GameDataLoader.LoadAll();
            _ok = true;
            var biomes = BiomeCsvSerializer.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", BiomeCsv)));
            var units = new List<UnitCsvRow>(GameTables.Units);
            var techNodes = GameDataLoader.LoadTechNodes();
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "Simulation");
            Directory.CreateDirectory(outDir);

            var allBuilt = new HashSet<string>();
            int waterEmbarks = 0;
            var summary = new StringBuilder();
            summary.AppendLine("scenario,turns,winner,cities P/E,techs P/E,score P/E,explored P/E,buildings,embarks,disembarks,upgrades,captures,explorers,tasks,monuments,lighthouses");

            foreach (var sc in Scenarios)
            {
                Result r;
                try
                {
                    r = Simulate(sc, biomes, units, techNodes);
                }
                catch (System.Exception e)
                {
                    Check(false, $"{sc.Name}: exception {e}");
                    continue;
                }
                File.WriteAllText(Path.Combine(outDir, sc.Name + ".csv"), r.Csv, new UTF8Encoding(true));
                allBuilt.UnionWith(r.BuildingsBuilt);
                if (sc.WaterMap) waterEmbarks += r.Embarks;

                string Pe(Dictionary<Team, int> d) => $"{Get(d, Team.Player)}/{Get(d, Team.Enemy)}";
                string line = $"{sc.Name},{r.LastTurn},{r.Winner},{Pe(r.Cities)},{Pe(r.Techs)},{Pe(r.Score)},{Pe(r.ExploredEnd)}," +
                              $"{string.Join("|", r.BuildingsBuilt.OrderBy(b => b))},{r.Embarks},{r.Disembarks},{r.Upgrades},{r.Captures},{r.Explorers},{r.Tasks},{r.Monuments},{r.LighthousesFound}";
                summary.AppendLine(line);
                Debug.Log("[EconomySimulation] " + line);

                foreach (var team in CitySystem.Teams)
                {
                    Check(Get(r.ExploredEnd, team) > Get(r.ExploredStart, team), $"{sc.Name}: {team} explores");
                    if (r.LastTurn >= 20) Check(Get(r.Techs, team) >= 5 || Get(r.Cities, team) == 0, $"{sc.Name}: {team} researched {Get(r.Techs, team)} techs");
                }
                Check(r.BuildingsBuilt.Count >= 3, $"{sc.Name}: AI built only {r.BuildingsBuilt.Count} building types");
            }

            // 재현성: 첫 시나리오를 한 번 더.
            try
            {
                var again = Simulate(Scenarios[0], biomes, units, techNodes);
                var first = File.ReadAllText(Path.Combine(outDir, Scenarios[0].Name + ".csv"));
                Check(again.Csv == first.TrimStart('﻿'), "same seed reproduces the same game");
            }
            catch (System.Exception e) { Check(false, $"determinism rerun exception {e}"); }

            foreach (var required in new[] { BuildingDefinition.Road, BuildingDefinition.Port, BuildingDefinition.Market })
                Check(allBuilt.Contains(required), $"no scenario built {required}");
            Check(allBuilt.Overlaps(new[] { BuildingDefinition.Windmill, BuildingDefinition.Sawmill, BuildingDefinition.Forge }), "no processor building built");
            Check(allBuilt.Overlaps(BuildingDefinition.All.Where(b => b.IsTemple).Select(b => b.Id)), "no temple built");
            Check(waterEmbarks > 0, "no unit embarked on water maps");

            summary.AppendLine($"all buildings: {string.Join("|", allBuilt.OrderBy(b => b))}");
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary.ToString(), new UTF8Encoding(true));
            Debug.Log($"[EconomySimulation] all buildings built across scenarios: {string.Join(", ", allBuilt.OrderBy(b => b))}");
            Debug.Log(_ok ? "[EconomySimulation] ALL PASS" : "[EconomySimulation] SOME CHECKS FAILED - see errors above");
        }

        private static int Get(Dictionary<Team, int> d, Team t) => d.TryGetValue(t, out var v) ? v : 0;

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[EconomySimulation] FAIL: " + message);
        }

        private static Result Simulate(Scenario sc, List<BiomeCsvRow> biomes, List<UnitCsvRow> units, List<TechNodeData> techNodes)
        {
            var result = new Result();
            var grid = new GridWorld(sc.Size, sc.Size);
            float wetMult = sc.Wetness / 0.55f;
            var anchors = TerrainGenerationSystem.Generate(grid, biomes, sc.Seed, wetMult, sc.Mode, sc.Wetness, out var suburbs, out var planned);
            StructureGenerationSystem.Generate(grid, biomes, anchors, sc.Seed, suburbs, planned, sc.Mode);

            var world = new EntityWorld();
            var econ = new EconomyWorld { TechNodes = techNodes, UnitRows = new List<UnitCsvRow>(units) };
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Stars = 5 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            TaskSystem.Init(econ);
            grid.FogEnabled = true;

            // 수도: 생성된 수도 구조물 중 가장 멀리 떨어진 두 곳(1:1 대전).
            var capitals = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (grid.GetStructure(new Vector2Int(x, y)) == StructureGenerationSystem.CapitalStructureId) capitals.Add(new Vector2Int(x, y));
            if (capitals.Count < 2) throw new System.Exception($"map has {capitals.Count} capital structures");
            Vector2Int a = capitals[0], b = capitals[1];
            foreach (var p in capitals) foreach (var q in capitals)
                if ((p - q).sqrMagnitude > (a - b).sqrMagnitude) { a = p; b = q; }

            var starter = units.Find(u => u.Id == "infantry") ?? units[0];
            foreach (var (team, pos) in new[] { (Team.Player, a), (Team.Enemy, b) })
            {
                CitySystem.FoundCity(grid, econ, pos, team, true, team == Team.Player ? "P 수도" : "E 수도");
                VisionSystem.Reveal(grid, team, pos, GameRules.Vision.StartRevealRadius);
                UnitFactorySystem.CreateFromCsv(grid, world, team, starter, pos);
            }
            CitySystem.AssignUnitsToCapital(world, econ);
            CitySystem.RefreshConnections(grid, econ, null);
            TechEffectSystem.RefreshUnits(grid, world, econ);
            VisionSystem.Refresh(grid, world, econ, null);
            foreach (var team in CitySystem.Teams) result.ExploredStart[team] = VisionSystem.CountExplored(grid, team);

            var csv = new StringBuilder();
            csv.AppendLine("turn,team,stars,starsIncome,cities,levels,connected,buildings,roads,techs,units,embarked,explored,kills,score");

            for (int turn = 1; turn <= MaxTurns; turn++)
            {
                result.LastTurn = turn;
                foreach (var team in CitySystem.Teams)
                {
                    if (CitySystem.HasLost(econ, team)) continue;
                    RunTeamTurn(grid, world, econ, team, turn, result);
                    csv.AppendLine(Row(grid, world, econ, team, turn));
                    foreach (var t in CitySystem.Teams)
                        if (CitySystem.HasLost(econ, t)) result.Winner = t == Team.Player ? "Enemy" : "Player";
                }
                if (result.Winner != "-") break;
            }

            foreach (var team in CitySystem.Teams)
            {
                result.ExploredEnd[team] = VisionSystem.CountExplored(grid, team);
                result.Techs[team] = econ.Tech[team].Unlocked.Count;
                result.Score[team] = ScoreSystem.Compute(grid, world, econ, team);
                result.Cities[team] = CitySystem.CountCities(econ, team);
                result.LighthousesFound += econ.Tasks[team].LighthousesFound.Count;
                result.Monuments += econ.Tasks[team].MonumentsBuilt.Count;
            }
            result.Csv = csv.ToString();
            return result;
        }

        /// <summary>BattleController의 한 팀 턴과 같은 순서. 플레이어 쪽도 AI가 둔다.</summary>
        private static void RunTeamTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turn, Result result)
        {
            TurnSystem.StartTurn(world, team, turn);
            econ.Turn = turn;
            if (turn > 1) econ.Resources[team] = CityResourceSystem.ApplyTurnStart(econ.Resources[team], grid, world, econ, team);

            var log = new List<EconomyLogEntry>();
            VisionSystem.Refresh(grid, world, econ, log);
            TaskSystem.Refresh(grid, econ, log);
            log.AddRange(EconomyAI.RunTurn(grid, world, econ, team));
            foreach (var e in log)
            {
                if (!string.IsNullOrEmpty(e.SpawnUnitId)) UnitFactorySystem.SpawnEconomyUnit(grid, world, econ, e.Team, e.SpawnUnitId, e.Position, e.CityIndex, e.SpawnVeteran, e.SpawnBoatId);
                if (e.Kind == EconomyLogKind.Upgrade) result.Upgrades++;
                if (e.Kind == EconomyLogKind.Capture) result.Captures++;
                if (e.Kind == EconomyLogKind.Task) result.Tasks++;
                if ((e.Kind == EconomyLogKind.Reward || e.Kind == EconomyLogKind.Explore) && e.Subject == CitySystem.RewardName(CityRewardType.Explorer)) result.Explorers++;
            }
            TechEffectSystem.RefreshUnits(grid, world, econ);

            var wasEmbarked = new HashSet<int>();
            for (int i = 0; i < world.EntityCount; i++) if (EmbarkSystem.IsEmbarked(world, i)) wasEmbarked.Add(i);

            EnemyAI.RunTurn(grid, world, econ, team, CaptureTargets(grid, econ, team));

            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i)) continue;
                bool now = EmbarkSystem.IsEmbarked(world, i);
                if (now && !wasEmbarked.Contains(i)) result.Embarks++;
                if (!now && wasEmbarked.Contains(i)) result.Disembarks++;
            }

            var after = new List<EconomyLogEntry>();
            VisionSystem.Refresh(grid, world, econ, after);
            TechEffectSystem.RefreshUnits(grid, world, econ);
            AbilitySystem.ApplyTurnEndWait(grid, world, team);
            TaskSystem.EndTurn(econ, team);
            TaskSystem.Refresh(grid, econ, after);
            foreach (var e in after) if (e.Kind == EconomyLogKind.Task) result.Tasks++;

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var t = grid.GetTile(new Vector2Int(x, y));
                if (!string.IsNullOrEmpty(t.BuildingId)) result.BuildingsBuilt.Add(t.BuildingId);
                if (t.HasRoad) result.BuildingsBuilt.Add(BuildingDefinition.Road);
            }
        }

        /// <summary>BattleController.CaptureTargets와 같은 규칙: 중립 마을/수도 구조물 + 다른 팀 도시.</summary>
        private static List<Vector2Int> CaptureTargets(GridWorld grid, EconomyWorld econ, Team team)
        {
            var targets = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (!CitySystem.IsSettlementTile(grid, p)) continue;
                int city = CitySystem.FindCityAt(econ, p);
                if (city < 0 || econ.Cities[city].Owner != team) targets.Add(p);
            }
            return targets;
        }

        private static string Row(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turn)
        {
            int cities = 0, levels = 0, connected = 0, buildings = 0, roads = 0, unitsAlive = 0, embarked = 0;
            foreach (var c in econ.Cities)
                if (c.Owner == team) { cities++; levels += c.Level; if (c.ConnectedToCapital) connected++; }
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var t = grid.GetTile(new Vector2Int(x, y));
                if (t.OwnerTeam == (int)team && !string.IsNullOrEmpty(t.BuildingId)) buildings++;
                if (t.HasRoad && (t.OwnerTeam == (int)team || t.OwnerTeam == TileData.NoOwner)) roads++;
            }
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                unitsAlive++;
                if (EmbarkSystem.IsEmbarked(world, i)) embarked++;
            }
            var res = econ.Resources[team];
            return $"{turn},{team},{res.Stars},{CitySystem.StarsIncome(grid, world, econ, team)},{cities},{levels},{connected}," +
                   $"{buildings},{roads},{econ.Tech[team].Unlocked.Count},{unitsAlive},{embarked},{VisionSystem.CountExplored(grid, team)},{econ.Tasks[team].Kills}," +
                   $"{ScoreSystem.Compute(grid, world, econ, team)}";
        }
    }
}
