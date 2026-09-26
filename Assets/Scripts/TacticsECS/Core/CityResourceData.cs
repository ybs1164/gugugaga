namespace TacticsECS
{
    /// <summary>
    /// 한 팀(제국)이 보유한 발전 자원 값(도시 발전도/인구 상한/골드/신앙). 순수 데이터이며, 매 턴 생산량
    /// 계산이나 소모 판정 같은 로직은 전부 CityResourceSystem이 담당한다(기술트리 해금 소모는 TechSystem).
    /// 팀마다 하나씩 EconomyWorld.Resources에 들어 있다.
    ///
    /// 예전엔 타일/영토 시스템이 없어 GoldProduction/DevelopmentProduction/PopulationCap을 인스펙터 고정값으로
    /// 대신했지만, 이제 도시(CityData)가 생겨서 CityResourceSystem.ApplyTurnStart가 매 턴 도시 목록으로
    /// 다시 계산해 채운다 — 이 세 값은 "이번 턴 계산 결과"를 HUD에 보여주기 위한 캐시다.
    ///   - 골드(🪙) = 폴리토피아의 별. 도시 레벨 합 + 수도 +1 + 공방/공원 + 시장. 채집/건설/유닛 훈련에 쓴다.
    ///   - 발전도(⚙️) = 기술 연구 전용 자원. 도시 수 + 수도와 연결된 도시 수 + 수도 +1.
    ///   - 인구 상한(👤) = 도시마다 (레벨 + 1)의 합 = 폴리토피아의 도시별 유닛 수용량을 팀 전체로 합친 값.
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
