namespace TacticsECS
{
    /// <summary>
    /// 기술트리 관련 고정 상수. 노드 목록 자체는 더 이상 코드 표가 아니라 Assets/Resources/TechTree.csv에 있다
    /// (기획자가 스프레드시트로 편집 — docs/TechTreeCsv.md). 런타임에는 BattleController가
    /// Resources.Load로 읽어 TechCsvSerializer.Parse로 파싱하고, EconomyWorld.TechNodes에 담아 쓴다.
    /// </summary>
    public static class TechTreeDefinition
    {
        /// <summary>Resources.Load&lt;TextAsset&gt;에 넘기는 경로(확장자 제외) — Assets/Resources/TechTree.csv.</summary>
        public const string CsvResourcePath = "TechTree";

        /// <summary>중앙 허브(시작 노드, 어떤 기술도 아님)에 쓰는 아이콘 이름.</summary>
        public const string HubIcon = "tech_hub";
    }
}
