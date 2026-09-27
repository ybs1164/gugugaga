namespace TacticsECS
{
    /// <summary>점수 항목별 값(위키 Score 문서의 분류). ScoreSystem.ComputeBreakdown이 채우는 순수 값.</summary>
    public struct ScoreBreakdown
    {
        public int Units;
        public int Territory;
        public int Exploration;
        public int Cities;
        public int Parks;
        public int Monuments;
        public int Temples;
        public int Tech;
    }
}
