namespace TacticsECS
{
    /// <summary>
    /// 타일 구조물(StructureGenerationSystem이 배치하는 수도/마을/유적/자원/등대/불가사리)의 고정
    /// 표 — TechTreeDefinition과 같은 패턴(Core의 순수 데이터 struct + Data의 고정 표). 이름/설명은 번역 표
    /// (Strings.csv "Structure.&lt;Id&gt;.Name/Desc")에 있고 GameDataLoader.LoadStrings가 채운다.
    /// BattleHud.ShowStructurePanel이 TileData.StructureId로 이 표를 선형 탐색해 보여준다.
    /// </summary>
    public static class StructureDefinition
    {
        /// <summary>번역 키 앞부분("Structure.&lt;Id&gt;.Name").</summary>
        public const string StringTable = "Structure";

        public static readonly StructureInfo[] All =
        {
            new StructureInfo { Id = "Capital" },
            new StructureInfo { Id = "Village" },
            new StructureInfo { Id = "Ruin" },
            new StructureInfo { Id = "Resource_Fruit" },
            new StructureInfo { Id = "Resource_Crop" },
            new StructureInfo { Id = "Resource_Animal" },
            new StructureInfo { Id = "Resource_Metal" },
            new StructureInfo { Id = "Resource_Fish" },
            new StructureInfo { Id = "Lighthouse" },
            new StructureInfo { Id = "Starfish" },
            // 옛 CSV 호환용 자원 Id — docs/spec/game/tile.md#구조물
            new StructureInfo { Id = "Resource_Food" },
            new StructureInfo { Id = "Resource_Ore" },
        };
    }
}
