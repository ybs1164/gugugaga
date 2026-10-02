namespace TacticsECS
{
    /// <summary>
    /// 건물 표 — 폴리토피아 위키 Buildings 문서의 Resource Buildings / Monuments / Temples / Other improvements
    /// 표를 옮긴 값(비용 = 별 → 이 프로젝트의 별). 값 자체는 Assets/Resources/Tables/Buildings.csv에 있고, 여기에는 코드가
    /// 특별 취급하는 건물 Id 상수와 CSV를 담을 필드만 있다. 어떤 기술이 어떤 건물을 여는지는 TechUnlocks.csv의
    /// BuildingIndex가 가리키고, 로드 후 TableLinkSystem이 BuildingInfo.UnlockKey("Build.Farm" 등)를 채운다.
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

        /// <summary>건물 표 — Tables/Buildings.csv(GameTables.BuildingsPath)를 GameDataLoader.LoadAll이 파싱해 채운다(코드에 기본 표를 두지
        /// 않는다: CSV가 유일한 원본). 배열 위치 = 행의 Index.
        /// 기념물 행(인구 +3, 평지/얕은 물, 비용 0)도 CSV에 있다.</summary>
        public static BuildingInfo[] All = new BuildingInfo[0];
    }
}
