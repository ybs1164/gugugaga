namespace TacticsECS
{
    /// <summary>과업 하나의 진행 상황(TaskSystem.Progress). 순수 데이터 — View가 진행 링/칸 게이지로 그린다.</summary>
    public struct TaskProgress
    {
        public int Current;
        public int Target;

        /// <summary>달성했다(기념물을 지을 수 있다).</summary>
        public bool Done;

        /// <summary>달성하고 기념물까지 지었다.</summary>
        public bool MonumentBuilt;
    }
}
