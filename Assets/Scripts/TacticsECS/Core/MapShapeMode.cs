namespace TacticsECS
{
    /// <summary>docs/reference/PolytopiaMapGeneration.md 2절의 6종 맵 타입 중, 개별 타일 확률(+MinDistance)에
    /// 맡기지 않고 전용 "랜드마스 마스크"로 모양 자체를 만드는 타입들. Drylands는 원래도 물이 거의
    /// 없어(목표 0~10%) 기존 방식으로 충분해 여기 포함하지 않는다 — 마스크가 필요 없다(Freeform).
    /// 생성: TerrainGenerationSystem.</summary>
    public enum MapShapeMode { Freeform, Pangea, Lakes, Continents, Archipelago, Waterworld }
}
