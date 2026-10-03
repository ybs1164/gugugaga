namespace TacticsECS
{
    /// <summary>One sprite layer; CSV cells each contain one value. Loading and rendering live in View.</summary>
    public struct SpritePartInfo
    {
        public string VisualId;
        public string Sheet;
        public int Column, Row, Width, Height, Layer, MinLevel, MaxLevel;
        public int X, Y, Scale;
        public bool FlipX;
        public string Color;
        public string Note;
    }

    /// <summary>One visual drawn into a tile composite, offset in detail pixels from the tile centre.</summary>
    public struct SpritePlacement
    {
        public string VisualId;
        public int X, Y;
        public bool FlipX;

        public SpritePlacement(string visualId, int x = 0, int y = 0, bool flipX = false)
        {
            VisualId = visualId; X = x; Y = y; FlipX = flipX;
        }
    }
}
