using UnityEngine;
using UnityEngine.Tilemaps;
namespace TacticsECS
{
    /// <summary>Shared ground colour and the tile's range/selection frame; no per-frame update.</summary>
    public class TileView : MonoBehaviour
    {
        public enum TileHighlight { None, Move, Attack, Selected }
        private static readonly Color SelectedColor = new Color(1f,.86f,.3f);
        private static readonly Color MoveColor = new Color(.24f,.55f,1f);
        private static readonly Color AttackColor = new Color(1f,.3f,.37f);
        private GameObject _frame; // 칸 테두리: 선택 노랑 · 이동 파랑 · 공격 빨강
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
            _overlay.enabled = false; // 범위 표시는 테두리만 — 칸 그림 색을 덮지 않는다(docs/spec/game/session.md#화면-배치)
            if (_highlight != TileHighlight.None && _frame == null)
            {
                _frame = new GameObject("Frame"); _frame.transform.SetParent(transform,false);
                const float t = .0625f, edge = .5f-t/2;
                PixelSpriteCatalog.Rectangle(_frame.transform,"Top",new Vector2(0,edge),new Vector2(1,t),SelectedColor,PixelSpriteCatalog.OverlayOrder+1);
                PixelSpriteCatalog.Rectangle(_frame.transform,"Bottom",new Vector2(0,-edge),new Vector2(1,t),SelectedColor,PixelSpriteCatalog.OverlayOrder+1);
                PixelSpriteCatalog.Rectangle(_frame.transform,"Left",new Vector2(-edge,0),new Vector2(t,1),SelectedColor,PixelSpriteCatalog.OverlayOrder+1);
                PixelSpriteCatalog.Rectangle(_frame.transform,"Right",new Vector2(edge,0),new Vector2(t,1),SelectedColor,PixelSpriteCatalog.OverlayOrder+1);
            }
            if (_frame != null && _highlight != TileHighlight.None)
            {
                var color = _highlight == TileHighlight.Move ? MoveColor : _highlight == TileHighlight.Attack ? AttackColor : SelectedColor;
                foreach (var edgeRenderer in _frame.GetComponentsInChildren<SpriteRenderer>(true)) edgeRenderer.color = color;
            }
            if (_frame != null) _frame.SetActive(!_fogged && _highlight != TileHighlight.None);
        }
    }
}
