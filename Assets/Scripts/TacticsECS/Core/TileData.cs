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

        /// <summary>이 타일의 지형(육지/물). 유닛의 MoveDomain(Core/UnitComponents.cs)과 일치해야
        /// (IgnoreTerrainAction 패시브가 없는 한) 그 유닛이 들어갈 수 있다 — Walkable(장애물 여부)과는
        /// 별개 판정이다.</summary>
        public TerrainType Terrain;

        /// <summary>바이옴 절차 생성이 채워 넣는 세부 타일 타입 키(예: "Grass"/"Forest"/"Sand" — Systems/
        /// TerrainGenerationSystem.cs 참고). 비어있으면(기본값) 이동 판정에는 영향이 없고, View(TileView)도
        /// 기존 Terrain 2색 표시로 폴백한다 — Terrain(이동 판정)과 독립된 순수 표시/생성용 값이다.</summary>
        public string TileTypeId;

        public static TileData Default => new TileData
        {
            Walkable = true,
            OccupantId = NoOccupant,
            Terrain = TerrainType.Land,
            TileTypeId = string.Empty
        };
    }
}
