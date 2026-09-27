namespace TacticsECS
{
    /// <summary>
    /// 점수 규칙 상수 — 폴리토피아 위키 Score 문서. 계산은 ScoreSystem.
    ///
    /// 원문과 다른 점/보충: 위키 Temple 문서와 Score 문서의 신전 점수가 서로 다르다(Temple: 100 + 레벨당 50, 최대 300 /
    /// Score: 100 + 레벨당 100, 최대 500). 점수 계산 전용 문서인 Score 쪽을 따르고, 레벨이 오르는 간격은 Temple 문서의
    /// 표(건설 후 0-2턴 Lv1, 3-5턴 Lv2 ... 12턴+ Lv5 = 3턴마다)를 따른다.
    /// </summary>
    public static class ScoreDefinition
    {
        public const int PerUnitCostStar = 5;
        public const int SuperUnit = 50;
        public const int PerTerritoryTile = 20;
        public const int PerExploredTile = 5;
        public const int CityBase = 100;
        public const int PerCityLevelAbove1 = 50;
        public const int PerPopulation = 5;
        public const int Park = 250;
        public const int Monument = 400;
        public const int TempleBase = 100;
        public const int PerTempleLevelAbove1 = 100;
        public const int TempleMaxLevel = 5;
        public const int TempleTurnsPerLevel = 3;
        public const int PerTechTier = 100;
    }
}
