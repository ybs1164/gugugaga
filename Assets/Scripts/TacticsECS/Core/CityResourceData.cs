namespace TacticsECS
{
    /// <summary>
    /// 도시 하나가 보유한 발전 자원 값(도시 발전도/인구 상한/골드/신앙). 순수 데이터이며, 매 턴 생산량
    /// 계산이나 소모 판정 같은 로직은 전부 CityResourceSystem이 담당한다(기술트리 해금 소모는
    /// TechSystem).
    /// 타일/영토 시스템이 아직 없어 "일반 자원 채집"이나 "수도 연결 보너스"처럼 타일 소유권에 의존하는
    /// 값은 여기 담지 않는다(CityResourceSystem.IsConnectedToCapital 플레이스홀더 참고). GoldProduction/
    /// DevelopmentProduction도 실제로는 도시 타일/건물 생산량의 합이어야 하지만, 그 타일 데이터가 없어
    /// 지금은 도시 하나의 고정값으로 대신한다.
    /// </summary>
    public struct CityResourceData
    {
        public int Development;
        public int DevelopmentProduction;
        public int PopulationCap;
        public int Gold;
        public int GoldProduction;
        public int Faith;
        public int MaxFaith;
        public bool IsCapital;

        public static CityResourceData Create(int populationCap, int goldProduction, int developmentProduction, int maxFaith, bool isCapital)
        {
            return new CityResourceData
            {
                Development = 0,
                DevelopmentProduction = developmentProduction,
                PopulationCap = populationCap,
                Gold = 0,
                GoldProduction = goldProduction,
                Faith = 0,
                MaxFaith = maxFaith,
                IsCapital = isCapital,
            };
        }
    }
}
