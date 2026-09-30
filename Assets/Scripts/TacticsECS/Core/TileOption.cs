namespace TacticsECS
{
    /// <summary>
    /// 선택한 타일에서 지금 할 수 있는(또는 조건이 모자라 못 하는) 건설/행동 하나. TileImprovementSystem.
    /// GetOptions가 계산해 돌려주는 순수 값이고, View(ActionMenuHud)는 이걸 버튼으로 그리기만 한다.
    /// </summary>
    public struct TileOption
    {
        /// <summary>BuildingInfo.Id 또는 TileActionInfo.Id.</summary>
        public string Id;
        public bool IsBuilding;
        public string Name;

        /// <summary>BuildingInfo.Icon / TileActionInfo.Icon(메뉴 버튼 아이콘).</summary>
        public string Icon;

        public int Cost;

        /// <summary>Block == None일 때만 true(EconomyAI 등 기존 호출부 호환용으로 함께 채운다).</summary>
        public bool Enabled;

        /// <summary>지금 못 하는 이유. 표시 문장/아이콘은 View가 정한다.</summary>
        public BlockReason Block;

        /// <summary>지으면(하면) 도시에 더해질 인구 — 인접 보너스 건물은 지금 인접한 기반 건물 수까지 반영한 예상값.
        /// 메뉴 버튼의 결과 칩([+2 인구])에 쓴다.</summary>
        public int PopulationGain;

        /// <summary>하면 바로 얻는 골드(벌목 +1 등).</summary>
        public int GoldGain;

        /// <summary>효과 요약 문장(CSV Description). 버튼에는 보이지 않고 툴팁에만 쓴다.</summary>
        public string Detail;
    }
}
