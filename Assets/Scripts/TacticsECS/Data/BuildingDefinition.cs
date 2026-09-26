namespace TacticsECS
{
    /// <summary>
    /// 건물 고정 표 — 폴리토피아 위키 Buildings 문서의 Resource Buildings / Temples / Other improvements 표를
    /// 옮겼다(비용 = 별 → 이 프로젝트의 골드). StructureDefinition/TechTreeDefinition과 같은 패턴(Core의 순수
    /// 데이터 struct + Data의 고정 표). 어떤 기술이 어떤 건물을 여는지는 이 표의 UnlockKey와
    /// TechTree.csv의 Unlocks 칸이 같은 키("Build.Farm" 등)를 공유하는 것으로만 연결된다.
    ///
    /// 원문과 다른 점: 대장간(Forge)은 위키 표에 "Field, Forest adjacent to a Lumber Hut"로 적혀 있지만 효과
    /// 설명("인접 광산 하나당 인구 2")과 맞지 않는 오기라 광산 인접으로 옮겼다. 다리/대사관/기념물은 이번 범위에
    /// 넣지 않았다(다리는 육지 유닛의 물 이동, 대사관은 외교, 기념물은 과업 시스템이 필요).
    /// </summary>
    public static class BuildingDefinition
    {
        public const string Farm = "Farm";
        public const string Mine = "Mine";
        public const string LumberHut = "LumberHut";
        public const string Windmill = "Windmill";
        public const string Forge = "Forge";
        public const string Sawmill = "Sawmill";
        public const string Market = "Market";
        public const string Port = "Port";
        public const string Road = "Road";

        private static readonly string[] None = new string[0];

        public static readonly BuildingInfo[] All =
        {
            new BuildingInfo { Id = LumberHut, Name = "벌목장", UnlockKey = "Build.LumberHut", Cost = 3, Population = 1, Terrain = TileClass.Forest,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1." },
            new BuildingInfo { Id = Farm, Name = "농장", UnlockKey = "Build.Farm", Cost = 5, Population = 2, Terrain = TileClass.Field,
                RequiredStructures = new[] { "Resource_Crop" }, AdjacentBuildings = None, Description = "작물 위. 인구 +2." },
            new BuildingInfo { Id = Mine, Name = "광산", UnlockKey = "Build.Mine", Cost = 5, Population = 2, Terrain = TileClass.Mountain,
                RequiredStructures = new[] { "Resource_Metal", "Resource_Ore" }, AdjacentBuildings = None, Description = "광물 위. 인구 +2." },
            new BuildingInfo { Id = Port, Name = "항구", UnlockKey = "Build.Port", Cost = 7, Population = 1, Terrain = TileClass.ShallowWater,
                RequiredStructures = None, AdjacentBuildings = None, Description = "얕은 물. 인구 +1, 해로로 수도 연결." },
            new BuildingInfo { Id = Sawmill, Name = "제재소", UnlockKey = "Build.Sawmill", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { LumberHut }, PopulationPerAdjacent = 1, Description = "인접 벌목장 하나당 인구 +1." },
            new BuildingInfo { Id = Windmill, Name = "풍차", UnlockKey = "Build.Windmill", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Farm }, PopulationPerAdjacent = 1, Description = "인접 농장 하나당 인구 +1." },
            new BuildingInfo { Id = Forge, Name = "대장간", UnlockKey = "Build.Forge", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Mine }, PopulationPerAdjacent = 2, Description = "인접 광산 하나당 인구 +2." },
            new BuildingInfo { Id = Market, Name = "시장", UnlockKey = "Build.Market", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Sawmill, Windmill, Forge }, ProducesGoldFromAdjacent = true,
                Description = "인접 제재소/풍차/대장간 인구 합만큼 매 턴 골드 (최대 8)." },
            new BuildingInfo { Id = "Temple", Name = "신전", UnlockKey = "Build.Temple", Cost = 20, Population = 1, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1, 신앙 최대치 +5." },
            new BuildingInfo { Id = "ForestTemple", Name = "숲 신전", UnlockKey = "Build.ForestTemple", Cost = 15, Population = 1, Terrain = TileClass.Forest,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1, 신앙 최대치 +5." },
            new BuildingInfo { Id = "MountainTemple", Name = "산악 신전", UnlockKey = "Build.MountainTemple", Cost = 20, Population = 1, Terrain = TileClass.Mountain,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1, 신앙 최대치 +5." },
            new BuildingInfo { Id = "WaterTemple", Name = "해양 신전", UnlockKey = "Build.WaterTemple", Cost = 20, Population = 1, Terrain = TileClass.ShallowWater | TileClass.Ocean,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1, 신앙 최대치 +5." },
            new BuildingInfo { Id = Road, Name = "도로", UnlockKey = "Build.Road", Cost = 3, Terrain = TileClass.Field | TileClass.Forest,
                RequiredStructures = None, AdjacentBuildings = None, IsRoad = true, Description = "수도 연결(도로망). 중립 땅에도 건설 가능." },
        };

        /// <summary>신전류가 올려주는 신앙 최대치(폴리토피아 신전은 점수만 주지만, 이 프로젝트의 신앙 자원과 연결).</summary>
        public const int TempleMaxFaithBonus = 5;
    }
}
