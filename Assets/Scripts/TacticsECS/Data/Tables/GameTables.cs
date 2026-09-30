namespace TacticsECS
{
    /// <summary>
    /// 배열형 게임 테이블(Assets/Resources/Tables/*.csv)을 담는 정적 저장소 — BuildingDefinition.All과 같은 패턴으로,
    /// GameDataLoader가 채우고 각 System이 Index로 바로 꺼내 쓴다(배열 위치 = 행의 Index). 값만 있다(CLAUDE.md 규칙 2).
    /// 형식 규칙은 docs/ArrayTables.md.
    /// </summary>
    public static class GameTables
    {
        public const string Folder = "Tables/";
        public const string TechUnlocksPath = Folder + "TechUnlocks";
        public const string TechsPath = Folder + "Techs";
        public const string TechGroupsPath = Folder + "TechGroups";
        public const string TribesPath = Folder + "Tribes";
        public const string StartConditionsPath = Folder + "StartConditions";
        public const string StartConditionRulesPath = Folder + "StartConditionRules";

        /// <summary>샌드박스에서 바이옴 CSV를 따로 불러오지 않았을 때 쓰는 기본 바이옴 표(docs/sample_biomes.csv와 같은 내용).
        /// 종족의 BiomeIndex는 이 표(또는 불러온 표)의 Biome 행 순서를 가리킨다.</summary>
        public const string DefaultBiomesPath = Folder + "Biomes";

        public static TechUnlockRow[] TechUnlocks = new TechUnlockRow[0];
        public static TechRow[] Techs = new TechRow[0];
        public static TechGroupRow[] TechGroups = new TechGroupRow[0];
        public static TribeRow[] Tribes = new TribeRow[0];
        public static StartConditionRow[] StartConditions = new StartConditionRow[0];
        public static StartConditionRuleRow[] StartConditionRules = new StartConditionRuleRow[0];
    }
}
