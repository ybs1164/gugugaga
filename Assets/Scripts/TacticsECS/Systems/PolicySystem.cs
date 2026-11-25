using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 학습한 AI(난이도)의 결정: 규칙 기반 AI가 만든 합법 후보 목록에 모델 점수를 매겨 하나를 고른다. 모델이 없는 팀은 규칙 AI가 고른
    /// 후보(ruleIndex)를 그대로 쓴다. 학습용 기록(EconomyWorld.PolicyTrace)이 켜져 있으면 결정마다 특징·선택을 남긴다.
    /// 자체 상태 없음 — 모델은 PolicyModels(Data), 팀 설정은 EconomyWorld.AiPolicy, 난수는 CitySystem.NextRandom.
    /// 규칙: docs/spec/game/ai.md#난이도
    /// </summary>
    public static class PolicySystem
    {
        /// <summary>전역 특징 수 · 결정 종류 수 · 후보 특징 수(모델 입력 = 셋의 합). 바꾸면 모델을 다시 학습해야 한다.</summary>
        public const int GlobalSize = 20, KindCount = 5, CandidateSize = 20;
        public const int InputSize = GlobalSize + KindCount + CandidateSize;

        /// <summary>팀에 난이도(AIDifficulty.csv Index)를 정한다. 범위 밖이면 규칙 AI(설정 없음).</summary>
        public static void ApplyDifficulty(EconomyWorld econ, Team team, int difficulty)
        {
            if (difficulty < 0 || difficulty >= GameTables.AIDifficulties.Length) { econ.AiPolicy.Remove(team); return; }
            var row = GameTables.AIDifficulties[difficulty];
            econ.AiPolicy[team] = new AiPolicySettings { Difficulty = difficulty, ModelId = row.Model, Temperature = row.Temperature, BlunderChance = row.BlunderChance };
        }

        /// <summary>팀이 모델을 쓰는지(쓰면 model).</summary>
        public static bool TryModel(EconomyWorld econ, Team team, out AiPolicySettings settings, out PolicyModel model)
        {
            model = null;
            if (econ == null || !econ.AiPolicy.TryGetValue(team, out settings)) { settings = default; return false; }
            return !string.IsNullOrEmpty(settings.ModelId) && PolicyModels.All.TryGetValue(settings.ModelId, out model);
        }

        /// <summary>특징을 계산할 필요가 있는지(모델을 쓰거나 학습 기록 중). 아니면 규칙 AI는 특징 없이 원래대로 돈다.</summary>
        public static bool Wants(EconomyWorld econ, Team team) =>
            econ != null && (econ.PolicyTrace != null || TryModel(econ, team, out _, out _));

        /// <summary>후보 하나를 고른다. 후보가 없으면 -1. ruleIndex는 규칙 AI의 선택(모델이 없을 때의 답).</summary>
        public static int Choose(EconomyWorld econ, Team team, PolicyDecisionKind kind, float[] global, List<float[]> candidates, int ruleIndex)
        {
            if (candidates.Count == 0) return -1;
            if (!TryModel(econ, team, out var settings, out var model))
            {
                if (candidates.Count > 1) Record(econ, team, kind, global, candidates, ruleIndex, 0f);
                return ruleIndex;
            }
            if (candidates.Count == 1) return 0;

            var scores = new float[candidates.Count];
            var input = new float[InputSize];
            for (int i = 0; i < candidates.Count; i++) scores[i] = Score(model, BuildInput(input, global, kind, candidates[i]));

            int best = 0, second = -1;
            for (int i = 1; i < scores.Length; i++)
                if (scores[i] > scores[best]) { second = best; best = i; }
                else if (second < 0 || scores[i] > scores[second]) second = i;

            float t = settings.Temperature;
            int chosen = best;
            if (t > 0f)
            {
                float max = scores[best], sum = 0f;
                var p = new float[scores.Length];
                for (int i = 0; i < p.Length; i++) { p[i] = Mathf.Exp((scores[i] - max) / t); sum += p[i]; }
                float r = Uniform(econ) * sum;
                for (chosen = 0; chosen < p.Length - 1; chosen++) { r -= p[chosen]; if (r < 0f) break; }
            }
            if (settings.BlunderChance > 0f && second >= 0 && chosen == best && Uniform(econ) < settings.BlunderChance) chosen = second;

            if (econ.PolicyTrace != null) Record(econ, team, kind, global, candidates, chosen, LogSoftmax(scores, chosen, t > 0f ? t : 1f));
            return chosen;
        }

        private static float Uniform(EconomyWorld econ) => CitySystem.NextRandom(econ, 1 << 24) / (float)(1 << 24);

        private static float LogSoftmax(float[] scores, int index, float t)
        {
            float max = float.MinValue;
            foreach (var s in scores) max = Mathf.Max(max, s);
            double sum = 0;
            foreach (var s in scores) sum += Math.Exp((s - max) / t);
            return (float)((scores[index] - max) / t - Math.Log(sum));
        }

        private static void Record(EconomyWorld econ, Team team, PolicyDecisionKind kind, float[] global, List<float[]> candidates, int chosen, float logProb)
        {
            if (econ.PolicyTrace == null) return;
            econ.PolicyTrace.Add(new PolicyDecision { Team = team, Turn = econ.Turn, Kind = kind, Global = global, Candidates = new List<float[]>(candidates), Chosen = chosen, LogProb = logProb });
        }

        private static float[] BuildInput(float[] input, float[] global, PolicyDecisionKind kind, float[] candidate)
        {
            Array.Clear(input, 0, input.Length);
            Array.Copy(global, 0, input, 0, GlobalSize);
            input[GlobalSize + (int)kind] = 1f;
            Array.Copy(candidate, 0, input, GlobalSize + KindCount, Math.Min(candidate.Length, CandidateSize));
            return input;
        }

        /// <summary>MLP 순전파(정규화 → ReLU 은닉층 → 선형 출력 1).</summary>
        public static float Score(PolicyModel m, float[] input)
        {
            var x = new float[input.Length];
            for (int i = 0; i < x.Length; i++) x[i] = (input[i] - m.Mean[i]) / m.Std[i];
            for (int l = 0; l < m.Weights.Length; l++)
            {
                int outN = m.Layers[l + 1], inN = m.Layers[l];
                var y = new float[outN];
                var w = m.Weights[l];
                for (int o = 0; o < outN; o++)
                {
                    float s = m.Biases[l][o];
                    int row = o * inN;
                    for (int i = 0; i < inN; i++) s += w[row + i] * x[i];
                    y[o] = l < m.Weights.Length - 1 && s < 0f ? 0f : s;
                }
                x = y;
            }
            return x[0];
        }

        /// <summary>모델 파일(텍스트) → PolicyModel. 형식: docs/spec/csv/ai-difficulty.md#모델-파일. 틀리면 null과 오류.</summary>
        public static PolicyModel Parse(string id, string text, List<string> errors)
        {
            try
            {
                var lines = text.Replace("\r", "").Split('\n');
                float[] Floats(string line)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var v = new float[parts.Length - 1];
                    for (int i = 1; i < parts.Length; i++) v[i - 1] = float.Parse(parts[i], CultureInfo.InvariantCulture);
                    return v;
                }
                int li = 0;
                if (!lines[li++].StartsWith("PolicyModel 1")) throw new FormatException("header");
                var layerParts = lines[li++].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var layers = new int[layerParts.Length - 1];
                for (int i = 1; i < layerParts.Length; i++) layers[i - 1] = int.Parse(layerParts[i], CultureInfo.InvariantCulture);
                if (layers[0] != InputSize || layers[layers.Length - 1] != 1) throw new FormatException($"layers {layers[0]}..{layers[layers.Length - 1]} (input must be {InputSize})");
                var m = new PolicyModel { Id = id, Layers = layers, Mean = Floats(lines[li++]), Std = Floats(lines[li++]), Weights = new float[layers.Length - 1][], Biases = new float[layers.Length - 1][] };
                for (int l = 0; l < layers.Length - 1; l++)
                {
                    m.Weights[l] = Floats(lines[li++]);
                    m.Biases[l] = Floats(lines[li++]);
                    if (m.Weights[l].Length != layers[l] * layers[l + 1] || m.Biases[l].Length != layers[l + 1]) throw new FormatException($"layer {l} size");
                }
                for (int i = 0; i < m.Std.Length; i++) if (m.Std[i] <= 0f) m.Std[i] = 1f;
                return m;
            }
            catch (Exception e)
            {
                errors?.Add($"AI/{id}: 모델 파일을 읽지 못함({e.Message}) — 그 난이도는 규칙 AI로 돈다.");
                return null;
            }
        }

        // ---------- 특징 ----------

        /// <summary>팀 시점의 전역 특징(구름 속 적은 모른다).</summary>
        public static float[] Global(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var g = new float[GlobalSize];
            var res = econ.Resources[team];
            int cities = 0, levels = 0, enemyCities = 0;
            foreach (var c in econ.Cities)
            {
                if (c.Owner == team) { cities++; levels += c.Level; }
                else if (VisionSystem.IsExplored(grid, team, c.Position)) enemyCities++;
            }
            int units = 0, enemies = 0;
            float hp = 0, maxHp = 0, atk = 0, enemyAtk = 0;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i)) continue;
                if (world.Get<Team>(i) == team) { units++; hp += world.Get<Hp>(i).Value; maxHp += world.Get<MaxHp>(i).Value; atk += world.Get<Attack>(i).Value; }
                else if (VisionSystem.IsExplored(grid, team, world.Get<GridPosition>(i).Value)) { enemies++; enemyAtk += world.Get<Attack>(i).Value; }
            }
            int explored = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (VisionSystem.IsExplored(grid, team, new Vector2Int(x, y))) explored++;
            var other = team == Team.Player ? Team.Enemy : Team.Player;

            g[0] = econ.Turn / 30f;
            g[1] = res.Stars / 20f;
            g[2] = res.StarsProduction / 10f;
            g[3] = cities / 5f;
            g[4] = levels / 15f;
            g[5] = units / 10f;
            g[6] = CitySystem.UnitCapacity(econ, team) / 10f;
            g[7] = econ.Tech[team].Unlocked.Count / 20f;
            g[8] = res.MaxFaith > 0 ? res.Faith / (float)res.MaxFaith : 0f;
            g[9] = ScoreSystem.Compute(grid, world, econ, team) / 2000f;
            g[10] = ScoreSystem.Compute(grid, world, econ, other) / 2000f;
            g[11] = enemies / 10f;
            g[12] = enemyCities / 5f;
            g[13] = EconomyAI.IsThreatened(grid, world, econ, team) ? 1f : 0f;
            g[14] = explored / (float)(grid.Width * grid.Height);
            g[15] = maxHp > 0 ? hp / maxHp : 0f;
            g[16] = atk / 20f;
            g[17] = enemyAtk / 20f;
            g[18] = econ.GodIds.TryGetValue(team, out int god) && god >= 0 ? 1f : 0f;
            g[19] = 1f;
            return g;
        }
    }
}
