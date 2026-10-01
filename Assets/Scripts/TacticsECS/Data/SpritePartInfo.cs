namespace TacticsECS
{
    /// <summary>One sprite layer; CSV cells each contain one value. Loading and rendering live in View.</summary>
    public struct SpritePartInfo
    {
        public string VisualId;
        public string Sheet;
        public int Column, Row, Width, Height, Layer, MinLevel, MaxLevel;
        public float X, Y, ScaleX, ScaleY;
        public string Color;
        public string Note;
    }
}
