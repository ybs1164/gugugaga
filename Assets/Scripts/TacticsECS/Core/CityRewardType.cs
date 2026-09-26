namespace TacticsECS
{
    /// <summary>도시 레벨업 보상(폴리토피아 위키 City 문서의 레벨별 2지선다). 선택지 표는
    /// Data/CityRewardDefinition.cs, 적용은 CitySystem.ApplyReward.</summary>
    public enum CityRewardType
    {
        Workshop,          // Lv2: 골드 수입 +1
        Explorer,          // Lv2: 시야 시스템이 없어 즉시 발전도 +3으로 대체
        CityWall,          // Lv3: 도시 안 요새화 유닛 방어 보너스 강화
        Resources,         // Lv3: 골드 +5
        PopulationGrowth,  // Lv4: 인구 +3
        BorderGrowth,      // Lv4: 영토 반경 1 -> 2
        Park,              // Lv5+: 골드 수입 +1
        SuperUnit,         // Lv5+: 가장 강한(최대 체력) 유닛을 무료로 소환
    }
}
