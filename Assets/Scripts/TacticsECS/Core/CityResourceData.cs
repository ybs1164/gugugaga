namespace TacticsECS
{
    /// <summary>팀의 공용 재화인 별과 유닛 수용량. 연구·건설·채집·훈련은 모두 Stars를 사용한다.
    /// StarsProduction은 도시 레벨·수도·공방·공원·시장 수입의 캐시이며 매 턴 한 번만 지급한다.</summary>
    public struct CityResourceData
    {
        public int PopulationCap;
        public int Stars;
        public int StarsProduction;
        public bool IsCapital;

        public static CityResourceData Create(int populationCap, int starsProduction, bool isCapital)
        {
            return new CityResourceData
            {
                PopulationCap = populationCap,
                Stars = 0,
                StarsProduction = starsProduction,
                IsCapital = isCapital,
            };
        }
    }
}
