namespace TacticsECS
{
    /// <summary>
    /// 건물(타일 개량) 하나의 고정 정의 — 폴리토피아 위키 Buildings 표를 옮긴 값. 순수 데이터이며,
    /// 표는 Data/BuildingDefinition.cs, 배치 판정/건설/파괴는 Systems/TileImprovementSystem.cs가 맡는다.
    /// </summary>
    public struct BuildingInfo
    {
        /// <summary>TileData.BuildingId에 저장되는 키.</summary>
        public string Id;
        public string Name;

        /// <summary>이 건물을 지으려면 팀이 가져야 하는 기술 해금 키(TechNodeData.Unlocks).</summary>
        public string UnlockKey;

        public int Cost;

        /// <summary>지을 때 타일 주인 도시에 더해지는 고정 인구.</summary>
        public int Population;

        /// <summary>지을 수 있는 지형.</summary>
        public TileClass Terrain;

        /// <summary>비어있지 않으면 타일에 이 구조물(자원) 중 하나가 있어야 하고, 지으면 그 자원을 소모한다
        /// (예: 농장 = 작물 위).</summary>
        public string[] RequiredStructures;

        /// <summary>비어있지 않으면 같은 팀 영토의 인접(8방향) 칸에 이 건물 중 하나 이상이 있어야 한다.</summary>
        public string[] AdjacentBuildings;

        /// <summary>인접한 AdjacentBuildings 하나당 더해지는 인구(풍차/대장간/제재소). 나중에 인접 칸에 기반
        /// 건물이 추가로 지어져도 그만큼 더해진다.</summary>
        public int PopulationPerAdjacent;

        /// <summary>시장처럼 인구 대신 매 턴 골드를 만드는 건물이면 true — 인접 AdjacentBuildings가 만드는
        /// 인구 합만큼(최대 CitySystem.MarketGoldCap) 골드 수입이 늘어난다.</summary>
        public bool ProducesGoldFromAdjacent;

        /// <summary>도로처럼 타일 자체를 바꿀 뿐 "건물"로 치지 않는 개량이면 true(TileData.HasRoad에 기록되고
        /// 다른 건물과 공존 가능, 중립 영토에도 지을 수 있다).</summary>
        public bool IsRoad;

        public string Description;
    }
}
