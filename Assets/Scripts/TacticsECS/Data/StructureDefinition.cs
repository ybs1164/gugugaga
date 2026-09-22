namespace TacticsECS
{
    /// <summary>
    /// 타일 구조물(StructureGenerationSystem이 배치하는 수도/마을/유적/자원/불가사리) 6종의 고정
    /// 이름/설명 테이블 — TechTreeDefinition과 같은 패턴(Core의 순수 데이터 struct + Data의 고정 표).
    /// BattleHud.ShowStructurePanel이 TileData.StructureId로 이 표를 선형 탐색해 보여준다.
    /// </summary>
    public static class StructureDefinition
    {
        public static readonly StructureInfo[] All =
        {
            new StructureInfo { Id = "Capital", Name = "수도", Description = "이 지역을 다스리는 도시의 중심입니다." },
            new StructureInfo { Id = "Village", Name = "마을", Description = "사람들이 모여 사는 작은 정착지입니다." },
            new StructureInfo { Id = "Ruin", Name = "유적", Description = "오래전 무너진 문명의 흔적이 남아 있습니다." },
            new StructureInfo { Id = "Resource_Food", Name = "식량 자원", Description = "식용 버섯이 자생하는 채집지입니다." },
            new StructureInfo { Id = "Resource_Ore", Name = "광물 자원", Description = "채굴할 수 있는 광석이 묻혀 있는 노두입니다." },
            new StructureInfo { Id = "Starfish", Name = "불가사리", Description = "얕은 바다에 사는 불가사리입니다." },
        };
    }
}
