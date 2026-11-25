using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>학습한 AI가 고르는 결정 지점의 종류. 규칙: docs/spec/game/ai.md#난이도</summary>
    public enum PolicyDecisionKind { Research, Priority, Train, Improve, Unit }

    /// <summary>팀 하나의 AI 정책 설정 — 난이도 표(AIDifficulty.csv)의 한 행에서 온 값. ModelId가 비어 있으면 규칙 기반 AI.</summary>
    [System.Serializable]
    public struct AiPolicySettings
    {
        /// <summary>AIDifficulty.csv Index(없으면 -1).</summary>
        public int Difficulty;
        public string ModelId;
        public float Temperature;
        public float BlunderChance;
    }

    /// <summary>후보 점수를 매기는 작은 MLP의 가중치(값만). 입력 = 전역 특징 + 결정 종류 원핫 + 후보 특징, 은닉층은 ReLU, 출력 1.
    /// 파일 형식: docs/spec/csv/ai-difficulty.md#모델-파일</summary>
    public class PolicyModel
    {
        public string Id;
        /// <summary>층 크기(입력, 은닉…, 1).</summary>
        public int[] Layers;
        /// <summary>층마다 [출력][입력] 행 우선 가중치.</summary>
        public float[][] Weights;
        public float[][] Biases;
        /// <summary>입력 정규화(학습 데이터의 평균·표준편차).</summary>
        public float[] Mean;
        public float[] Std;
    }

    /// <summary>불러온 모델(Id → 모델). GameDataLoader가 채운다.</summary>
    public static class PolicyModels
    {
        public static Dictionary<string, PolicyModel> All = new Dictionary<string, PolicyModel>();
    }

    /// <summary>결정 하나의 기록(학습용 — EconomyWorld.PolicyTrace가 null이 아닐 때만 쌓인다).</summary>
    public class PolicyDecision
    {
        public Team Team;
        public int Turn;
        public PolicyDecisionKind Kind;
        public float[] Global;
        public List<float[]> Candidates;
        public int Chosen;
        public float LogProb;
    }
}
