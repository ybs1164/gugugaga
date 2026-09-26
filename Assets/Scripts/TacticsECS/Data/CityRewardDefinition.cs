namespace TacticsECS
{
    /// <summary>
    /// 도시 레벨업 보상 선택지 표(폴리토피아 위키 City 문서 "City Upgrades"). 인덱스 0 = 레벨 2 보상, 1 = 레벨 3,
    /// 2 = 레벨 4, 3 = 레벨 5 이상(이후 레벨은 모두 마지막 줄을 반복). 이름/설명은 UI 표시용.
    /// </summary>
    public static class CityRewardDefinition
    {
        public static readonly CityRewardType[][] OptionsByLevel =
        {
            new[] { CityRewardType.Workshop, CityRewardType.Explorer },
            new[] { CityRewardType.CityWall, CityRewardType.Resources },
            new[] { CityRewardType.PopulationGrowth, CityRewardType.BorderGrowth },
            new[] { CityRewardType.Park, CityRewardType.SuperUnit },
        };

        public const int ResourcesGold = 5;
        public const int ExplorerDevelopment = 3;
        public const int PopulationGrowthAmount = 3;
        public const int BorderGrowthRadius = 2;

        public static readonly (CityRewardType Type, string Name, string Description)[] Info =
        {
            (CityRewardType.Workshop, "공방", "골드 수입 +1/턴"),
            (CityRewardType.Explorer, "탐험가", "발전도 +3 (시야 시스템이 없어 탐험가 대신)"),
            (CityRewardType.CityWall, "성벽", "도시 안 요새화 유닛 방어 +3 (없으면 +1)"),
            (CityRewardType.Resources, "자원", "골드 +5"),
            (CityRewardType.PopulationGrowth, "인구 성장", "인구 +3"),
            (CityRewardType.BorderGrowth, "국경 확장", "영토 반경 1 -> 2"),
            (CityRewardType.Park, "공원", "골드 수입 +1/턴"),
            (CityRewardType.SuperUnit, "슈퍼 유닛", "최대 체력이 가장 높은 유닛을 무료 소환"),
        };
    }
}
