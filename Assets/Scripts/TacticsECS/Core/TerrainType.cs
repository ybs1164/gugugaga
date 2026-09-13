namespace TacticsECS
{
    /// <summary>
    /// 타일이 속한 지형의 종류. TileData(타일 쪽)와 MoveDomain(유닛 쪽, Core/UnitComponents.cs) 양쪽에서
    /// 재사용하는 값 타입 — 유닛의 MoveDomain과 타일의 TerrainType이 일치해야 그 타일에 들어갈 수 있다
    /// (PathfindingSystem.GetReachable/Actions/MoveAction 참고). IgnoreTerrain 유닛은 이 일치 여부 자체를
    /// 건너뛴다.
    /// </summary>
    public enum TerrainType
    {
        Land,
        Water,
    }
}
