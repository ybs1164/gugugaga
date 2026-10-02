namespace TacticsECS
{
    /// <summary>
    /// 도시 레벨업 보상 선택지 하나(위키 City "City Upgrades"). 순수 데이터 — 표는 Assets/Resources/CityRewards.csv
    /// (Data/CityRewardDefinition.All), 적용은 CitySystem.ApplyReward. 같은 Level의 행들이 그 레벨의 선택지이고, 가장 높은
    /// Level의 행들은 그 이상 레벨에서도 반복된다(위키 "5+").
    /// </summary>
    public struct CityRewardInfo
    {
        public int Level;
        public CityRewardType Type;
        public string Name;

        /// <summary>보상 크기 — 종류마다 뜻이 다르다(공방/공원 = 별/턴, 자원 = 별, 인구 성장 = 인구, 국경 확장 = 새 반경).
        /// 탐험가/성벽/슈퍼 유닛은 쓰지 않는다(각각 GameRules.Vision/Combat, 유닛 CSV가 정한다).</summary>
        public int Amount;

        public string Description;
    }
}
