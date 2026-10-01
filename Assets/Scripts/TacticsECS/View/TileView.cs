using UnityEngine;
using UnityEngine.Tilemaps;
namespace TacticsECS
{
    /// <summary>Shared ground colour and independent range overlay; no per-frame update.</summary>
    public class TileView : MonoBehaviour
    {
        public enum TileHighlight { None, Move, Attack }
        public Vector2Int GridPos { get; private set; }
        public TerrainType Terrain { get; private set; }
        public string TileTypeId { get; private set; }
        private Tilemap _ground;
        private SpriteRenderer _overlay;
        private Color _tint;
        private bool _fogged;
        private TileHighlight _highlight;
        public void Bind(Tilemap ground) => _ground = ground;
        public void Init(Vector2Int gridPos, TerrainType terrain, string tileTypeId = "")
        {
            GridPos = gridPos; Terrain = terrain; TileTypeId = tileTypeId ?? string.Empty;
            if (_overlay == null) _overlay = PixelSpriteCatalog.Rectangle(transform,"Range",Vector2.zero,Vector2.one,Color.clear,PixelSpriteCatalog.OverlayOrder);
            Apply();
        }
        public void SetHighlight(TileHighlight highlight) { _highlight = highlight; Apply(); }
        public void SetFogged(bool fogged) { _fogged = fogged; Apply(); }
        public void SetTerritory(Color tint, bool hasRoad) { _tint = tint; Apply(); }
        private void Apply()
        {
            var baseColor = Color.white;
            if (TileTypeId == "Rock") baseColor = new Color(.7f,.77f,.85f);
            if (_tint.a > 0) baseColor = Color.Lerp(baseColor,_tint,.12f);
            if (_ground != null) _ground.SetColor(new Vector3Int(GridPos.x,GridPos.y,0),baseColor);
            if (_overlay == null) return;
            _overlay.color = _highlight == TileHighlight.Move ? new Color(.24f,.58f,1f,.30f) : new Color(1f,.3f,.22f,.36f);
            _overlay.enabled = !_fogged && _highlight != TileHighlight.None;
        }
    }
}
