namespace TacticsECS
{
    /// <summary>타일 행동(건물이 아닌 1회성 작업)의 종류. 효과 처리는 TileImprovementSystem.Execute.</summary>
    public enum TileActionKind
    {
        /// <summary>자원을 소모하고 인구(+골드)를 얻는다 — 과일 채집/사냥/낚시/불가사리 인양.</summary>
        Harvest,
        /// <summary>숲 -> 평지, 골드 획득(임업).</summary>
        ClearForest,
        /// <summary>숲 -> 작물이 있는 평지(화전).</summary>
        BurnForest,
        /// <summary>평지 -> 숲(강신술).</summary>
        GrowForest,
        /// <summary>자기 건물 파괴 — 그 건물이 만든 인구를 되돌린다(건축).</summary>
        Destroy,
    }

    /// <summary>
    /// 타일 행동 하나의 고정 정의. 순수 데이터 — 표는 Data/TileActionDefinition.cs.
    /// </summary>
    public struct TileActionInfo
    {
        public string Id;
        public string Name;
        public TileActionKind Kind;
        public string UnlockKey;
        public int Cost;
        public TileClass Terrain;

        /// <summary>Harvest는 이 중 하나가 타일에 있어야 한다.</summary>
        public string[] RequiredStructures;

        public int Population;
        public int GoldGain;
        public string Description;
    }
}
