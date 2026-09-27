namespace TacticsECS
{
    /// <summary>
    /// 도시 레벨업 보상 표(폴리토피아 위키 City 문서 "City Upgrades") — 값은 Assets/Resources/CityRewards.csv이고
    /// GameDataLoader.LoadAll이 채운다. 레벨별 선택지 조회/보상 크기 조회는 CitySystem(RewardOptions/RewardAmount).
    /// </summary>
    public static class CityRewardDefinition
    {
        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/CityRewards.csv.</summary>
        public const string CsvResourcePath = "CityRewards";

        public static CityRewardInfo[] All = new CityRewardInfo[0];
    }
}
