using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// AI 난이도(학습한 정책) 불변식. 규칙: docs/spec/game/ai.md#난이도
    ///   1. 학습 기록을 켜도(정책 후보 경로를 타도) 모델이 없으면 규칙 AI와 비트 단위로 같은 판이 나온다 — EconomyAI·EnemyAI의 후보 목록이
    ///      규칙 AI의 선택을 정확히 담고 있다는 뜻(학습 데이터·난이도 모두 이 전제 위에 있다).
    ///   2. AIDifficulty.csv의 모델이 모두 읽혔고 입력 크기가 PolicySystem과 맞다.
    ///   3. 난이도마다 규칙 AI 상대로 한 판을 끝까지 둘 수 있고, 같은 시드면 같은 판이 나온다(무작위도 판의 결정적 난수만 쓴다).
    /// 사용법: VerificationSuite에 포함. 결과는 "[PolicyVerification] ALL PASS" / "SOME CHECKS FAILED".
    /// </summary>
    public static class PolicyVerification
    {
        private const int MaxTurns = 60;
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            GameDataLoader.LoadAll();

            for (int seed = 1; seed <= 3; seed++)
            {
                string plain = Play(seed, -1, false), traced = Play(seed, -1, true);
                Check(plain == traced, $"seed {seed}: rule AI game differs when the policy path records (candidate lists lost the rule choice)");
            }

            foreach (var row in GameTables.AIDifficulties)
            {
                if (string.IsNullOrEmpty(row.Model)) continue;
                Check(PolicyModels.All.TryGetValue(row.Model, out var m), $"AIDifficulty {row.Id}: model AI/{row.Model}.txt loaded");
                if (m != null) Check(m.Layers[0] == PolicySystem.InputSize, $"AIDifficulty {row.Id}: model input {m.Layers[0]} == {PolicySystem.InputSize}");
            }

            for (int d = 0; d < GameTables.AIDifficulties.Length; d++)
            {
                string a = Play(10 + d, d, false), b = Play(10 + d, d, false);
                Check(a == b, $"difficulty {GameTables.AIDifficulties[d].Id}: same seed reproduces the same game");
            }

            Debug.Log(_ok ? "[PolicyVerification] ALL PASS" : "[PolicyVerification] SOME CHECKS FAILED - see errors above");
        }

        /// <summary>규칙 AI(Player) 대 difficulty(Enemy, -1이면 규칙 AI) 한 판 → 최종 상태 해시.</summary>
        private static string Play(int seed, int difficulty, bool trace)
        {
            var rng = new System.Random(seed * 7919 + 17);
            int tribes = GameTables.Tribes.Length, gods = GameTables.Gods.Length;
            int pt = tribes > 0 ? rng.Next(tribes) : -1;
            var mapType = GameTables.MapTypes[GameRules.Map.DefaultType]; // 기본 맵 타입, 가장 작은 맵
            var s = MatchSystem.Create(new MatchSetup
            {
                Seed = seed, MapType = mapType.Id, WaterRatio = mapType.Wetness, MapSize = GameTables.MapSizes[0].Size,
                PlayerTribe = pt, EnemyTribe = tribes > 1 ? (pt + 1) % tribes : pt,
                PlayerGod = gods > 0 ? rng.Next(gods) : -1, EnemyGod = gods > 0 ? rng.Next(gods) : -1,
            });
            if (difficulty >= 0) PolicySystem.ApplyDifficulty(s.Econ, Team.Enemy, difficulty);
            if (trace) s.Econ.PolicyTrace = new List<PolicyDecision>();
            while (!MatchSystem.IsOver(s) && s.Turn.TurnNumber <= MaxTurns) MatchSystem.PlayAiTurn(s);
            s.Econ.PolicyTrace = null;
            var bytes = GraphSerializer.Serialize(new object[] { s.Grid, s.World, s.Econ });
            return System.Convert.ToBase64String(System.Security.Cryptography.MD5.Create().ComputeHash(bytes)) + $" t{s.Turn.TurnNumber}";
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[PolicyVerification] FAIL: " + message);
        }
    }
}
