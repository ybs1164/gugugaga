using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 신앙 포함 AI 대전 하네스 — 화면 없이 양 팀 모두 AI(EconomyAI + FaithAI + EnemyAI)로 여러 맵·신 조합에서 한 판씩 끝까지 돌린다.
    /// 전투 화면의 턴 흐름(BattleController.HandleTurnStart)과 같은 System 호출 순서를 재현한다:
    /// 별 수입 → 신앙 수입 → 신전 패시브 → 신화 유지 → 대신전 → 시야/과업 → 경제·신앙 AI → 군사 AI → 상태 갱신.
    /// 매 턴 끝에 불변식(신앙 범위, 신화 유닛 하나, 원본 보존, 점유 일관성, 대신전 상태)을 검사하고, 판마다 관측치를 CSV로 남긴다.
    /// 결과: Logs/FaithSimulation/&lt;판&gt;.csv, summary.csv + Console 요약.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.FaithSimulation.Run
    /// </summary>
    public static class FaithSimulation
    {
        private const int MaxTurns = 100;
        private const string BiomeCsv = "Assets/Resources/Tables/Biomes.csv";

        /// <summary>판마다 맵 시드의 시작값. 환경 변수 FAITH_SIM_SEED로 바꿔 다른 시드 묶음을 돌릴 수 있다(기본 11).</summary>
        private static int SeedOffset => int.TryParse(System.Environment.GetEnvironmentVariable("FAITH_SIM_SEED"), out var v) ? v : 11;

        private static bool _ok;
        private static string PunchLogPrefix => LocalizationSystem.T("UI.FaithPunch.Log").Split('{')[0];
        private static readonly List<string> Violations = new List<string>();

        private struct Game
        {
            public string Name;
            public TerrainGenerationSystem.MapShapeMode Mode;
            public float Wetness;
            public int Size;
            public int Seed;
            public int PlayerGod;
            public int EnemyGod;
        }

        private class Stats
        {
            public string Csv;
            public int Turns;
            public string Winner = "-";
            public string Victory = "conquest/none";
            public readonly Dictionary<Team, int> MaxTemples = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> MaxPraying = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> PunchesUsed = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> Descents = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> MythicReturns = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> GreatTempleTurn = new Dictionary<Team, int>();
            public readonly Dictionary<Team, int> PeakFaith = new Dictionary<Team, int>();
            public readonly Dictionary<Team, bool> TaskDone = new Dictionary<Team, bool>();
        }

        private static List<Game> Games()
        {
            var list = new List<Game>();
            var shapes = new[]
            {
                (TerrainGenerationSystem.MapShapeMode.Pangea, 0.50f, 14),
                (TerrainGenerationSystem.MapShapeMode.Continents, 0.55f, 16),
                (TerrainGenerationSystem.MapShapeMode.Lakes, 0.275f, 14),
                (TerrainGenerationSystem.MapShapeMode.Freeform, 0.05f, 14),
            };
            int godCount = GameTables.Gods.Length;
            for (int i = 0; i < 24; i++)
            {
                var (mode, wet, size) = shapes[i % shapes.Length];
                list.Add(new Game
                {
                    Name = $"G{i:00}_{mode}_{size}_s{i + SeedOffset}",
                    Mode = mode, Wetness = wet, Size = size, Seed = i + SeedOffset,
                    PlayerGod = i % godCount, EnemyGod = (i * 5 + 2) % godCount,
                });
            }
            return list;
        }

        public static void Run()
        {
            GameDataLoader.LoadAll();
            _ok = true;
            Violations.Clear();
            var biomes = BiomeCsvSerializer.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", BiomeCsv)));
            var units = new List<UnitCsvRow>(GameTables.Units);
            var techNodes = GameDataLoader.LoadTechNodes();
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "FaithSimulation");
            Directory.CreateDirectory(outDir);

            var summary = new StringBuilder();
            summary.AppendLine("game,gods P/E,turns,winner,victory,temples P/E,peakFaith P/E,praying P/E,punches P/E,descents P/E,mythicReturns P/E,greatTempleTurn P/E,task P/E");
            var totals = new Stats();
            int completed = 0, faithWins = 0;

            foreach (var game in Games())
            {
                Stats s;
                try { s = Simulate(game, biomes, units, techNodes); }
                catch (System.Exception e)
                {
                    Check(false, $"{game.Name}: exception {e}");
                    continue;
                }
                completed++;
                File.WriteAllText(Path.Combine(outDir, game.Name + ".csv"), s.Csv, new UTF8Encoding(true));
                if (s.Victory == "faith") faithWins++;

                string Pe(Dictionary<Team, int> d) => $"{Get(d, Team.Player)}/{Get(d, Team.Enemy)}";
                string tasks = $"{(s.TaskDone.TryGetValue(Team.Player, out var tp) && tp ? 1 : 0)}/{(s.TaskDone.TryGetValue(Team.Enemy, out var te) && te ? 1 : 0)}";
                string line = $"{game.Name},{GameTables.Gods[game.PlayerGod].Id}/{GameTables.Gods[game.EnemyGod].Id},{s.Turns},{s.Winner},{s.Victory},{Pe(s.MaxTemples)},{Pe(s.PeakFaith)},{Pe(s.MaxPraying)},{Pe(s.PunchesUsed)},{Pe(s.Descents)},{Pe(s.MythicReturns)},{Pe(s.GreatTempleTurn)},{tasks}";
                summary.AppendLine(line);
                Debug.Log("[FaithSimulation] " + line);
                foreach (var team in CitySystem.Teams)
                {
                    Add(totals.MaxTemples, team, Get(s.MaxTemples, team));
                    Add(totals.MaxPraying, team, Get(s.MaxPraying, team));
                    Add(totals.PunchesUsed, team, Get(s.PunchesUsed, team));
                    Add(totals.Descents, team, Get(s.Descents, team));
                    Add(totals.MythicReturns, team, Get(s.MythicReturns, team));
                    if (Get(s.GreatTempleTurn, team) > 0) Add(totals.GreatTempleTurn, team, 1);
                }
            }
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary.ToString(), new UTF8Encoding(true));

            int Sum(Dictionary<Team, int> d) => d.Values.Sum();
            Debug.Log($"[FaithSimulation] TOTALS games={completed} temples={Sum(totals.MaxTemples)} praying={Sum(totals.MaxPraying)} punches={Sum(totals.PunchesUsed)} descents={Sum(totals.Descents)} " +
                      $"mythicReturns={Sum(totals.MythicReturns)} greatTemples={Sum(totals.GreatTempleTurn)} faithVictories={faithWins} violations={Violations.Count}");

            Check(completed == Games().Count, "every game completed without an exception");
            Check(Violations.Count == 0, "no invariant violations: " + string.Join(" | ", Violations.Take(8)));
            Check(Sum(totals.MaxTemples) > 0, "AI built temples");
            Check(Sum(totals.MaxPraying) > 0, "AI units prayed");
            Check(Sum(totals.PunchesUsed) > 0, "AI used faith punches");

            // 재현성: 같은 판을 한 번 더 돌리면 같은 기록이어야 한다.
            try
            {
                var g0 = Games()[0];
                var again = Simulate(g0, biomes, units, techNodes);
                var first = File.ReadAllText(Path.Combine(outDir, g0.Name + ".csv")).TrimStart('﻿');
                Check(again.Csv == first, "same seed reproduces the same game");
            }
            catch (System.Exception e) { Check(false, "determinism rerun exception " + e); }

            Debug.Log(_ok ? "[FaithSimulation] ALL PASS" : "[FaithSimulation] SOME CHECKS FAILED - see errors above");
        }

        private static int Get(Dictionary<Team, int> d, Team t) => d.TryGetValue(t, out var v) ? v : 0;
        private static void Add(Dictionary<Team, int> d, Team t, int n) => d[t] = Get(d, t) + n;
        private static void Max(Dictionary<Team, int> d, Team t, int n) { if (n > Get(d, t)) d[t] = n; }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[FaithSimulation] FAIL: " + message);
        }

        private static void Violation(string game, int turn, string message)
        {
            if (Violations.Count < 50) Violations.Add($"{game} t{turn}: {message}");
        }

        // ---------- 한 판 ----------

        private static Stats Simulate(Game game, List<BiomeCsvRow> biomes, List<UnitCsvRow> units, List<TechNodeData> techNodes)
        {
            var stats = new Stats();
            var grid = new GridWorld(game.Size, game.Size);
            float wetMult = game.Wetness / 0.55f;
            var anchors = TerrainGenerationSystem.Generate(grid, biomes, game.Seed, wetMult, game.Mode, game.Wetness, out var suburbs, out var planned);
            StructureGenerationSystem.Generate(grid, biomes, anchors, game.Seed, suburbs, planned, game.Mode);

            var world = new EntityWorld();
            var econ = new EconomyWorld { TechNodes = techNodes, UnitRows = new List<UnitCsvRow>(units) };
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = new CityResourceData { Stars = 5 };
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            econ.GodIds[Team.Player] = game.PlayerGod;
            econ.GodIds[Team.Enemy] = game.EnemyGod;
            TaskSystem.Init(econ);
            grid.FogEnabled = true;

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
            foreach (var team in CitySystem.Teams)
                econ.Resources[team] = FaithSystem.RefreshMax(econ.Resources[team], grid, world, econ, team);

            var csv = new StringBuilder();
            csv.AppendLine("turn,team,stars,faith,maxFaith,temples,praying,units,mythic,greatTemple,punchesTotal,descentsTotal");

            var lastMythic = new Dictionary<Team, int> { [Team.Player] = -1, [Team.Enemy] = -1 };
            for (int turn = 1; turn <= MaxTurns && stats.Winner == "-"; turn++)
            {
                stats.Turns = turn;
                foreach (var team in CitySystem.Teams)
                {
                    if (CitySystem.HasLost(econ, team)) continue;
                    RunTeamTurn(game, grid, world, econ, team, turn, stats, lastMythic);
                    csv.AppendLine(Row(grid, world, econ, team, turn, stats));
                    CheckInvariants(game.Name, grid, world, econ, turn);

                    if (econ.FaithWinner >= 0) { stats.Winner = ((Team)econ.FaithWinner).ToString(); stats.Victory = "faith"; break; }
                    foreach (var t in CitySystem.Teams)
                        if (CitySystem.HasLost(econ, t)) { stats.Winner = t == Team.Player ? "Enemy" : "Player"; stats.Victory = "conquest"; }
                    if (stats.Winner != "-") break;
                }
            }
            foreach (var team in CitySystem.Teams)
            {
                stats.TaskDone[team] = econ.Tasks[team].Completed.Contains(GreatTempleSystem.TaskId);
                if (stats.TaskDone[team] && GreatTempleSystem.IndexOf(econ, team) < 0) Diagnose(game.Name, grid, econ, team, stats);
            }
            stats.Csv = csv.ToString();
            return stats;
        }

        /// <summary>과업을 채웠는데 대신전이 없는 팀: 영토 칸이 왜 후보가 못 되는지 이유별로 센다(Debug.Log).</summary>
        private static void Diagnose(string game, GridWorld grid, EconomyWorld econ, Team team, Stats stats)
        {
            int own = 0, free = 0, nonField = 0, building = 0, structure = 0, road = 0, city = 0, occupied = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y); var t = grid.GetTile(p);
                if (t.OwnerTeam != (int)team) continue;
                own++;
                if (GreatTempleSystem.IsFree(grid, econ, team, p)) { free++; continue; }
                if (TileImprovementSystem.Classify(grid, p) != TileClass.Field) nonField++;
                else if (!string.IsNullOrEmpty(t.BuildingId)) building++;
                else if (!string.IsNullOrEmpty(t.StructureId)) structure++;
                else if (t.HasRoad) road++;
                else if (CitySystem.FindCityAt(econ, p) >= 0) city++;
                else occupied++;
            }
            Debug.Log($"[FaithSimulation] NO-GREAT-TEMPLE {game} {team}: own={own} free={free} nonField={nonField} building={building} structure={structure} road={road} city={city} occupied={occupied} stars={econ.Resources[team].Stars}");
        }

        /// <summary>BattleController.HandleTurnStart + 적 턴 루틴과 같은 순서.</summary>
        private static void RunTeamTurn(Game game, GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turn, Stats stats, Dictionary<Team, int> lastMythic)
        {
            TurnSystem.StartTurn(world, team, turn);
            econ.Turn = turn;
            if (turn > 1)
            {
                econ.Resources[team] = CityResourceSystem.ApplyTurnStart(econ.Resources[team], grid, world, econ, team);
                econ.Resources[team] = FaithSystem.ApplyTurnStart(econ.Resources[team], grid, world, econ, team, turn);
                TemplePassiveSystem.OnTurnStart(grid, world, econ, team, turn);
            }
            GreatTempleSystem.OnTurnStart(grid, world, econ, team, turn);
            int mythicBefore = MythicSystem.FindMythic(world, team);
            MythicSystem.OnTurnStart(grid, world, team);
            if (mythicBefore >= 0 && MythicSystem.FindMythic(world, team) < 0) Add(stats.MythicReturns, team, 1);
            if (econ.FaithWinner >= 0) return;

            var log = new List<EconomyLogEntry>();
            Refresh(grid, world, econ, log);
            int mythicStart = MythicSystem.FindMythic(world, team);
            log.AddRange(EconomyAI.RunTurn(grid, world, econ, team));
            foreach (var e in log)
            {
                if (!string.IsNullOrEmpty(e.SpawnUnitId)) UnitFactorySystem.SpawnEconomyUnit(grid, world, econ, e.Team, e.SpawnUnitId, e.Position, e.CityIndex, e.SpawnVeteran, e.SpawnBoatId);
                if (e.Kind == EconomyLogKind.Action && e.Subject != null && e.Subject.StartsWith(PunchLogPrefix)) Add(stats.PunchesUsed, team, 1);
            }
            int mythicNow = MythicSystem.FindMythic(world, team);
            if (mythicNow >= 0 && mythicNow != mythicStart) Add(stats.Descents, team, 1);
            Refresh(grid, world, econ, null);

            EnemyAI.RunTurn(grid, world, econ, team, CaptureTargets(grid, econ, team));
            Refresh(grid, world, econ, null);
            AbilitySystem.ApplyTurnEndWait(grid, world, team);
            TaskSystem.EndTurn(econ, team);
            Refresh(grid, world, econ, null);
        }

        /// <summary>BattleController.RefreshEconomyViews의 상태 갱신 부분(시야/과업/대신전/신화/오라/생산/기술 효과).</summary>
        private static void Refresh(GridWorld grid, EntityWorld world, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            var l = log ?? new List<EconomyLogEntry>();
            VisionSystem.Refresh(grid, world, econ, l);
            TaskSystem.Refresh(grid, econ, l);
            GreatTempleSystem.Refresh(grid, world, econ, econ.Turn);
            MythicSystem.Sweep(grid, world);
            TemplePassiveSystem.RefreshAuras(grid, world, econ);
            foreach (var t in CitySystem.Teams)
                econ.Resources[t] = CityResourceSystem.RefreshProduction(econ.Resources[t], grid, world, econ, t);
            TechEffectSystem.RefreshUnits(grid, world, econ);
        }

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

        private static string Row(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turn, Stats stats)
        {
            int temples = TemplePassiveSystem.Temples(grid, team).Count;
            int praying = 0, units = 0;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                units++;
                if (world.GetOrDefault<Praying>(i).Value) praying++;
            }
            int gt = GreatTempleSystem.IndexOf(econ, team);
            string gtState = gt < 0 ? "-" : econ.GreatTemples[gt].State.ToString();
            if (gt >= 0 && Get(stats.GreatTempleTurn, team) == 0) stats.GreatTempleTurn[team] = turn;
            Max(stats.MaxTemples, team, temples);
            Max(stats.MaxPraying, team, praying);
            var res = econ.Resources[team];
            Max(stats.PeakFaith, team, res.Faith);
            int mythic = MythicSystem.FindMythic(world, team);
            return $"{turn},{team},{res.Stars},{res.Faith},{res.MaxFaith},{temples},{praying},{units},{(mythic >= 0 ? world.Get<Mythic>(mythic).TurnsLeft : 0)},{gtState},{Get(stats.PunchesUsed, team)},{Get(stats.Descents, team)}";
        }

        // ---------- 불변식 ----------

        private static void CheckInvariants(string game, GridWorld grid, EntityWorld world, EconomyWorld econ, int turn)
        {
            foreach (var team in CitySystem.Teams)
            {
                var r = econ.Resources[team];
                if (r.Faith < 0 || r.Faith > r.MaxFaith) Violation(game, turn, $"{team} faith {r.Faith} outside 0..{r.MaxFaith}");
                int mythics = 0;
                for (int i = 0; i < world.EntityCount; i++)
                    if (world.Get<Team>(i) == team && MythicSystem.IsMythic(world, i)) mythics++;
                if (mythics > 1) Violation(game, turn, $"{team} has {mythics} mythic units");
                int greatTemples = econ.GreatTemples.Count(t => t.Owner == team);
                if (greatTemples > 1) Violation(game, turn, $"{team} has {greatTemples} great temples");
            }

            for (int i = 0; i < world.EntityCount; i++)
            {
                var m = world.GetOrDefault<Mythic>(i);
                if (m.Active && UnitQueries.IsAlive(world, i))
                {
                    if (!world.GetOrDefault<Dormant>(m.Origin).Value) Violation(game, turn, $"mythic {i}: origin {m.Origin} is not dormant");
                    if (m.TurnsLeft <= 0) Violation(game, turn, $"mythic {i}: upkeep {m.TurnsLeft} not positive");
                }
                if (world.GetOrDefault<Dormant>(i).Value)
                {
                    bool owned = false;
                    for (int j = 0; j < world.EntityCount; j++)
                        if (world.GetOrDefault<Mythic>(j).Active && world.GetOrDefault<Mythic>(j).Origin == i && UnitQueries.IsAlive(world, j)) owned = true;
                    if (!owned) Violation(game, turn, $"dormant unit {i} has no live mythic");
                    if (grid.GetOccupant(world.Get<GridPosition>(i).Value) == i) Violation(game, turn, $"dormant unit {i} still occupies a tile");
                }
                if (UnitQueries.IsAlive(world, i) && grid.GetOccupant(world.Get<GridPosition>(i).Value) != i)
                    Violation(game, turn, $"alive unit {i} ({world.Get<Team>(i)}) not registered on its tile");
            }

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                int occ = grid.GetOccupant(p);
                if (occ == TileData.NoOccupant) continue;
                if (!UnitQueries.IsAlive(world, occ) || world.Get<GridPosition>(occ).Value != p) Violation(game, turn, $"tile {p} occupant {occ} is dead or elsewhere");
            }

            foreach (var t in econ.GreatTemples)
                foreach (var pos in t.Tiles)
                    if (grid.GetTile(pos).BuildingId != GreatTempleSystem.BuildingId) Violation(game, turn, $"great temple tile {pos} lost its building");
            if (econ.FaithWinner >= 0)
            {
                int gt = GreatTempleSystem.IndexOf(econ, (Team)econ.FaithWinner);
                if (gt < 0 || econ.GreatTemples[gt].State != GreatTempleState.Waiting) Violation(game, turn, "faith winner without a completed great temple");
            }
        }
    }
}
