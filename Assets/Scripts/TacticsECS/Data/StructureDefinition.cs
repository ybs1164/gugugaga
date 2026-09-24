namespace TacticsECS
{
    /// <summary>
    /// 타일 구조물(StructureGenerationSystem이 배치하는 수도/마을/유적/자원/등대/불가사리)의 고정
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
            new StructureInfo { Id = "Resource_Fruit", Name = "과일", Description = "평지에 열매가 맺힌 과일나무입니다." },
            new StructureInfo { Id = "Resource_Crop", Name = "작물", Description = "평지에서 자라는 야생 작물입니다." },
            new StructureInfo { Id = "Resource_Animal", Name = "사냥감", Description = "숲에 사는 야생 동물입니다." },
            new StructureInfo { Id = "Resource_Metal", Name = "광물", Description = "산에 묻혀 있는 채굴 가능한 광맥입니다." },
            new StructureInfo { Id = "Resource_Fish", Name = "물고기", Description = "얕은 물에 모여 사는 물고기 떼입니다." },
            new StructureInfo { Id = "Lighthouse", Name = "등대", Description = "맵 모서리를 밝히는 등대입니다." },
            new StructureInfo { Id = "Starfish", Name = "불가사리", Description = "바다에 사는 불가사리입니다." },
            // 예전 CSV 호환용(9차 재정비 이전 자원 Id).
            new StructureInfo { Id = "Resource_Food", Name = "식량 자원", Description = "식용 버섯이 자생하는 채집지입니다." },
            new StructureInfo { Id = "Resource_Ore", Name = "광물 자원", Description = "채굴할 수 있는 광석이 묻혀 있는 노두입니다." },
        };
    }
}
