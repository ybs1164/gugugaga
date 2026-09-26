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
        public int Cost;
        public bool Enabled;

        /// <summary>Enabled=false일 때 그 이유(골드 부족/인접 조건 등), true일 때는 효과 요약.</summary>
        public string Detail;
    }
}
