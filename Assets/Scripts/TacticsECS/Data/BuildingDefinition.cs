namespace TacticsECS
{
    /// <summary>
    /// 건물 고정 표 — 폴리토피아 위키 Buildings 문서의 Resource Buildings / Monuments / Temples / Other improvements
    /// 표를 옮겼다(비용 = 별 → 이 프로젝트의 골드). StructureDefinition/TechTreeDefinition과 같은 패턴(Core의 순수
    /// 데이터 struct + Data의 고정 표). 어떤 기술이 어떤 건물을 여는지는 이 표의 UnlockKey와
    /// TechTree.csv의 Unlocks 칸이 같은 키("Build.Farm" 등)를 공유하는 것으로만 연결된다.
    ///
    /// 원문과 다른 점: 대장간(Forge)은 위키 표에 "Field, Forest adjacent to a Lumber Hut"로 적혀 있지만 효과
    /// 설명("인접 광산 하나당 인구 2")과 맞지 않는 오기라 광산 인접으로 옮겼다. 대사관(Embassy)은 평화 조약이 맺어진
    /// 부족의 수도에만 지을 수 있는데, 이 프로젝트는 두 팀이 항상 전쟁 중이라(외교 시스템 없음) 넣지 않았다.
    /// 얼음 신전/Cymanti·Aquarion 전용 건물은 해당 지형/부족이 없어 제외했다.
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
        public const string Bridge = "Bridge";

        /// <summary>기념물(위키 Monuments): 인구 +3, 점수 400, 평지/얕은 물, 과업 달성 시 무료로 1회.</summary>
        public const int MonumentPopulation = 3;
        public const TileClass MonumentTerrain = TileClass.Field | TileClass.ShallowWater;

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
                RequiredStructures = None, AdjacentBuildings = None,
                Description = "얕은 물. 인구 +1, 들어온 육지 유닛은 뗏목이 된다, 물 5칸 이내 항구와 수도 연결." },
            new BuildingInfo { Id = Sawmill, Name = "제재소", UnlockKey = "Build.Sawmill", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { LumberHut }, PopulationPerAdjacent = 1, Description = "인접 벌목장 하나당 인구 +1." },
            new BuildingInfo { Id = Windmill, Name = "풍차", UnlockKey = "Build.Windmill", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Farm }, PopulationPerAdjacent = 1, Description = "인접 농장 하나당 인구 +1." },
            new BuildingInfo { Id = Forge, Name = "대장간", UnlockKey = "Build.Forge", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Mine }, PopulationPerAdjacent = 2, Description = "인접 광산 하나당 인구 +2." },
            new BuildingInfo { Id = Market, Name = "시장", UnlockKey = "Build.Market", Cost = 5, Terrain = TileClass.Field,
                RequiredStructures = None, AdjacentBuildings = new[] { Sawmill, Windmill, Forge }, ProducesGoldFromAdjacent = true,
                Description = "인접 제재소/풍차/대장간 레벨 합만큼 매 턴 골드 (최대 8)." },
            new BuildingInfo { Id = "Temple", Name = "신전", UnlockKey = "Build.Temple", Cost = 20, Population = 1, Terrain = TileClass.Field, IsTemple = true,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1. 점수 100, 3턴마다 레벨 업(최대 5레벨 500점)." },
            new BuildingInfo { Id = "ForestTemple", Name = "숲 신전", UnlockKey = "Build.ForestTemple", Cost = 15, Population = 1, Terrain = TileClass.Forest, IsTemple = true,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1. 점수 100, 3턴마다 레벨 업(최대 5레벨 500점)." },
            new BuildingInfo { Id = "MountainTemple", Name = "산악 신전", UnlockKey = "Build.MountainTemple", Cost = 20, Population = 1, Terrain = TileClass.Mountain, IsTemple = true,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1. 점수 100, 3턴마다 레벨 업(최대 5레벨 500점)." },
            new BuildingInfo { Id = "WaterTemple", Name = "해양 신전", UnlockKey = "Build.WaterTemple", Cost = 20, Population = 1, Terrain = TileClass.ShallowWater | TileClass.Ocean, IsTemple = true,
                RequiredStructures = None, AdjacentBuildings = None, Description = "인구 +1. 점수 100, 3턴마다 레벨 업(최대 5레벨 500점)." },
            new BuildingInfo { Id = Road, Name = "도로", UnlockKey = "Build.Road", Cost = 3, Terrain = TileClass.Field | TileClass.Forest,
                RequiredStructures = None, AdjacentBuildings = None, IsRoad = true, AllowNeutral = true,
                Description = "도로끼리 이동 비용 0.5, 숲 이동 제한 해제, 수도 연결. 중립 땅에도 건설 가능." },
            new BuildingInfo { Id = Bridge, Name = "다리", UnlockKey = "Build.Road", Cost = 5, Terrain = TileClass.ShallowWater,
                RequiredStructures = None, AdjacentBuildings = None, AllowNeutral = true, RequiresOppositeLand = true, ActsAsRoad = true,
                Description = "양쪽(상하/좌우)이 육지인 물 1칸. 육지 유닛 통행 + 도로 효과 + 수도 연결. 중립 물에도 건설 가능." },

            // ---- 기념물(위키 Monuments). 과업 조건은 TaskDefinition, 달성 판정은 TaskSystem. ----
            Monument("AltarOfPeace", "평화의 제단", TaskDefinition.Pacifist),
            Monument("EmperorsTomb", "황제의 무덤", TaskDefinition.Wealth),
            Monument("EyeOfGod", "신의 눈", TaskDefinition.Explorer),
            Monument("GateOfPower", "힘의 문", TaskDefinition.Killer),
            Monument("GrandBazaar", "대시장", TaskDefinition.Network),
            Monument("ParkOfFortune", "행운의 공원", TaskDefinition.Metropolis),
            Monument("TowerOfWisdom", "지혜의 탑", TaskDefinition.Genius),
        };

        private static BuildingInfo Monument(string id, string name, string taskId) => new BuildingInfo
        {
            Id = id, Name = name, UnlockKey = string.Empty, Cost = 0, Population = MonumentPopulation, Terrain = MonumentTerrain,
            RequiredStructures = None, AdjacentBuildings = None, TaskId = taskId,
            Description = "기념물. 인구 +3, 점수 400. 팀당 한 번.",
        };
    }
}
