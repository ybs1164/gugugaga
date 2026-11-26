using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using TacticsECS;

namespace TacticsSim
{
    /// <summary>
    /// 학습용 헤드리스 시뮬레이터. 명령:
    ///   bench [games] [seed0] [trace]                         규칙 AI끼리 판을 돌려 결과·상태 해시·시간을 찍는다.
    ///   rollout games seed0 out.bin player enemy record [temp] [blunder]
    ///       player/enemy = 모델 파일 경로 또는 rule, record = player|enemy|both|none. 결정 기록을 out.bin에 쓴다(tools/ai/train.py가 읽는다).
    ///   eval games seed0 a b [tempA] [blunderA] [tempB] [blunderB]
    ///       a와 b를 진영을 번갈아 대국시켜 "a의 승·패·무"를 찍는다.
    /// </summary>
    public static class Program
    {
        /// <summary>판 설정에 쓰는 맵 크기 수 — MapSizes.csv의 앞에서부터(작은 것부터) 이만큼만 쓴다(큰 맵은 학습 시간이 길다).</summary>
        private const int MapSizeChoices = 3;
        public const int MaxTurns = 100;

        public static int Main(string[] args)
        {
            string root = Environment.GetEnvironmentVariable("TACTICS_RESOURCES") ??
                          Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Resources"));
            UnityEngine.Resources.Root = root;
            var errors = GameDataLoader.LoadAll();
            if (errors.Count > 0) { Console.Error.WriteLine($"table errors: {errors.Count}"); foreach (var e in errors) Console.Error.WriteLine(e); }
            string cmd = args.Length > 0 ? args[0] : "bench";
            switch (cmd)
            {
                case "bench": return Bench(Int(args, 1, 8), Int(args, 2, 1), args.Length > 3 && args[3] == "trace");
                case "rollout": return Rollout(args);
                case "eval": return Eval(args);
                default: Console.Error.WriteLine("usage: bench|rollout|eval (see Program.cs)"); return 2;
            }
        }

        private static int Int(string[] a, int i, int d) => a.Length > i && int.TryParse(a[i], out var v) ? v : d;
        private static float Float(string[] a, int i, float d) =>
            a.Length > i && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

        /// <summary>시드 하나로 판 설정을 정한다(종족·신·맵 타입·크기 무작위).</summary>
        public static MatchSetup SetupFor(int seed)
        {
            var rng = new Random(seed * 7919 + 17);
            int tribes = GameTables.Tribes.Length, gods = GameTables.Gods.Length;
            var type = GameTables.MapTypes[rng.Next(GameTables.MapTypes.Length)];
            int pt = rng.Next(tribes);
            return new MatchSetup
            {
                Seed = seed, MapType = type.Id, WaterRatio = type.Wetness,
                MapSize = GameTables.MapSizes[rng.Next(Math.Min(MapSizeChoices, GameTables.MapSizes.Length))].Size,
                PlayerTribe = pt, EnemyTribe = tribes > 1 ? (pt + 1 + rng.Next(tribes - 1)) % tribes : pt,
                PlayerGod = gods > 0 ? rng.Next(gods) : -1, EnemyGod = gods > 0 ? rng.Next(gods) : -1,
            };
        }

        public static string Hash(MatchState s)
        {
            var trace = s.Econ.PolicyTrace; s.Econ.PolicyTrace = null;
            var bytes = GraphSerializer.Serialize(new object[] { s.Grid, s.World, s.Econ });
            s.Econ.PolicyTrace = trace;
            return Convert.ToHexString(MD5.HashData(bytes)).Substring(0, 12);
        }

        /// <summary>"rule"이면 null(규칙 AI), 아니면 모델 파일을 읽어 PolicyModels에 올리고 Id를 돌려준다.</summary>
        private static string LoadModel(string spec)
        {
            if (spec == "rule") return null;
            string id = Path.GetFileNameWithoutExtension(spec);
            if (PolicyModels.All.ContainsKey(id)) return id;
            var errors = new List<string>();
            var m = PolicySystem.Parse(id, File.ReadAllText(spec), errors);
            if (m == null) throw new InvalidDataException(string.Join("; ", errors));
            PolicyModels.All[id] = m;
            return id;
        }

        private static void Apply(MatchState s, Team team, string modelId, float temp, float blunder)
        {
            if (modelId == null) return;
            s.Econ.AiPolicy[team] = new AiPolicySettings { Difficulty = -1, ModelId = modelId, Temperature = temp, BlunderChance = blunder };
        }

        /// <summary>판을 끝까지(또는 MaxTurns까지) 돌린다. 승자: 0=Player, 1=Enemy, -1=턴 제한(점수 비교는 호출자).</summary>
        private static int PlayOut(MatchState s)
        {
            while (!MatchSystem.IsOver(s) && s.Turn.TurnNumber <= MaxTurns) MatchSystem.PlayAiTurn(s);
            return MatchSystem.IsOver(s) ? (int)s.Outcome.Winner : -1;
        }

        private static int Bench(int games, int seed0, bool trace)
        {
            var total = Stopwatch.StartNew();
            for (int g = 0; g < games; g++)
            {
                var sw = Stopwatch.StartNew();
                var s = MatchSystem.Create(SetupFor(seed0 + g));
                if (trace) s.Econ.PolicyTrace = new List<PolicyDecision>();
                while (!MatchSystem.IsOver(s) && s.Turn.TurnNumber <= 150) MatchSystem.PlayAiTurn(s);
                Console.WriteLine($"seed={seed0 + g} {s.Setup.MapType}/{s.Setup.MapSize} turns={s.Turn.TurnNumber} winner={(MatchSystem.IsOver(s) ? s.Outcome.Winner.ToString() : "-")} {s.Outcome.Reason} hash={Hash(s)} ms={sw.ElapsedMilliseconds}");
            }
            Console.WriteLine($"total ms={total.ElapsedMilliseconds}");
            return 0;
        }

        private static int Rollout(string[] a)
        {
            int games = Int(a, 1, 1), seed0 = Int(a, 2, 1);
            string outPath = a[3];
            string player = LoadModel(a[4]), enemy = LoadModel(a[5]);
            string record = a[6];
            float temp = Float(a, 7, 1f), blunder = Float(a, 8, 0f);
            using var w = new BinaryWriter(File.Create(outPath));
            w.Write(0x50544731); // "PTG1"
            w.Write(games);
            w.Write(PolicySystem.GlobalSize);
            w.Write(PolicySystem.CandidateSize);
            for (int g = 0; g < games; g++)
            {
                var s = MatchSystem.Create(SetupFor(seed0 + g));
                Apply(s, Team.Player, player, temp, blunder);
                Apply(s, Team.Enemy, enemy, temp, blunder);
                s.Econ.PolicyTrace = new List<PolicyDecision>();
                int winner = PlayOut(s);
                int scoreP = ScoreSystem.Compute(s.Grid, s.World, s.Econ, Team.Player), scoreE = ScoreSystem.Compute(s.Grid, s.World, s.Econ, Team.Enemy);
                var kept = s.Econ.PolicyTrace.FindAll(d => record == "both" || (record == "player" && d.Team == Team.Player) || (record == "enemy" && d.Team == Team.Enemy));
                w.Write(seed0 + g); w.Write(winner); w.Write(s.Turn.TurnNumber); w.Write(scoreP); w.Write(scoreE);
                w.Write(kept.Count);
                foreach (var d in kept)
                {
                    w.Write((byte)d.Team); w.Write((short)d.Turn); w.Write((byte)d.Kind); w.Write((short)d.Candidates.Count);
                    w.Write(d.Chosen); w.Write(d.LogProb);
                    foreach (var v in d.Global) w.Write(v);
                    foreach (var c in d.Candidates) for (int i = 0; i < PolicySystem.CandidateSize; i++) w.Write(i < c.Length ? c[i] : 0f);
                }
            }
            return 0;
        }

        private static int Eval(string[] a)
        {
            int games = Int(a, 1, 10), seed0 = Int(a, 2, 1);
            string ma = LoadModel(a[3]), mb = LoadModel(a[4]);
            float ta = Float(a, 5, 0f), ba = Float(a, 6, 0f), tb = Float(a, 7, 0f), bb = Float(a, 8, 0f);
            int win = 0, loss = 0, draw = 0;
            for (int g = 0; g < games; g++)
            {
                var s = MatchSystem.Create(SetupFor(seed0 + g / 2)); // 같은 맵을 진영만 바꿔 두 번
                var aTeam = g % 2 == 0 ? Team.Player : Team.Enemy;
                var bTeam = aTeam == Team.Player ? Team.Enemy : Team.Player;
                Apply(s, aTeam, ma, ta, ba);
                Apply(s, bTeam, mb, tb, bb);
                int winner = PlayOut(s);
                if (winner < 0)
                {
                    int sa = ScoreSystem.Compute(s.Grid, s.World, s.Econ, aTeam), sb = ScoreSystem.Compute(s.Grid, s.World, s.Econ, bTeam);
                    if (sa == sb) draw++; else if (sa > sb) win++; else loss++;
                }
                else if (winner == (int)aTeam) win++;
                else loss++;
            }
            Console.WriteLine($"{win} {loss} {draw}");
            return 0;
        }
    }
}
