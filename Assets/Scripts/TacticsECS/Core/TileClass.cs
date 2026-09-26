namespace TacticsECS
{
    /// <summary>
    /// 건물/타일 행동의 배치 조건에 쓰는 지형 분류(폴리토피아의 Field/Forest/Mountain/Water/Ocean).
    /// TileData.Terrain + TileTypeId에서 TileImprovementSystem.Classify가 계산한다 — 저장되는 값이 아니다.
    /// </summary>
    [System.Flags]
    public enum TileClass
    {
        None = 0,
        Field = 1,
        Forest = 2,
        Mountain = 4,
        ShallowWater = 8,
        Ocean = 16,
    }
}
