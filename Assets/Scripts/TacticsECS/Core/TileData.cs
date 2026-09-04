namespace TacticsECS
{
    /// <summary>
    /// 타일 하나의 "컴포넌트 데이터". GameObject가 아니라 GridWorld 내부 배열의 한 칸.
    /// </summary>
    [System.Serializable]
    public struct TileData
    {
        public const int NoOccupant = -1;

        public bool Walkable;
        public int OccupantId;

        public static TileData Default => new TileData
        {
            Walkable = true,
            OccupantId = NoOccupant
        };
    }
}
