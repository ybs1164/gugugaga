namespace TacticsECS
{
    /// <summary>
    /// 타일 행동(채집/벌목/화전/숲 조성/건물 파괴) 표 — 폴리토피아 위키 Population 문서의 비용/인구 값.
    /// 값은 Assets/Resources/TileActions.csv. 원문과 다른 점: 화전 비용은 위키 Burn Forest 문서 정보상자(3)를 따른다
    /// (Construction 문서 본문은 5, Burn Forest 본문은 2로 서로 달라 가장 최근 패치 값인 정보상자/Population 표를 택함).
    /// 불가사리 인양(항해)만 위키에서 유닛 행동(+별 8)이지만, 여기서는 영토 안 타일 행동으로 단순화했다.
    /// "Resource_Food"는 9차 재정비 이전 CSV의 옛 Id라 과일과 같이 취급한다.
    /// </summary>
    public static class TileActionDefinition
    {
        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/TileActions.csv.</summary>
        public const string CsvResourcePath = "TileActions";

        /// <summary>타일 행동 표 — TileActions.csv를 GameDataLoader.LoadAll이 파싱해 채운다.</summary>
        public static TileActionInfo[] All = new TileActionInfo[0];
    }
}
