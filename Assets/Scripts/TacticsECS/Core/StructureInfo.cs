namespace TacticsECS
{
    /// <summary>
    /// 타일 구조물 하나(수도/마을/유적/자원/불가사리)의 고정 정의(이름/설명). 순수 데이터이며, 이 값들을
    /// 채우는 테이블은 Data/StructureDefinition.cs가 맡는다. TileData.StructureId 문자열 키로 조회한다.
    /// </summary>
    public struct StructureInfo
    {
        /// <summary>TileData.StructureId/StructureGenerationSystem이 쓰는 것과 같은 키(예: "Capital").</summary>
        public string Id;

        public string Name;

        /// <summary>정보 패널에 보여줄 한 줄 설명.</summary>
        public string Description;
    }
}
