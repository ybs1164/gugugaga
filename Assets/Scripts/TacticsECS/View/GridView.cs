using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld 데이터를 보고 타일 GameObject들을 한 번 생성해주는 뷰.
    /// 생성 이후에는 GridWorld를 다시 들여다보지 않고, 하이라이트 갱신만 담당한다.
    /// </summary>
    public class GridView : MonoBehaviour
    {
        [SerializeField] private float tileGap = 0.05f;

        private TileView[] _tileViews;
        private GridWorld _grid;

        public void Build(GridWorld grid)
        {
            _grid = grid;
            _tileViews = new TileView[grid.Width * grid.Height];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Tile_{x}_{y}";
                    go.transform.SetParent(transform, false);
                    go.transform.position = grid.GridToWorld(pos);
                    go.transform.localScale = new Vector3(grid.TileSize - tileGap, 0.1f, grid.TileSize - tileGap);
                    go.GetComponent<Renderer>().sharedMaterial = RuntimeMaterial.CreateColored(Color.white);

                    var view = go.AddComponent<TileView>();
                    view.Init(pos);
                    _tileViews[grid.Index(pos)] = view;
                }
            }
        }

        public void ClearHighlights()
        {
            foreach (var t in _tileViews)
                t.SetHighlight(TileView.TileHighlight.None);
        }

        public void HighlightMove(IEnumerable<Vector2Int> tiles)
        {
            foreach (var p in tiles)
                _tileViews[_grid.Index(p)].SetHighlight(TileView.TileHighlight.Move);
        }

        public void HighlightAttack(IEnumerable<Vector2Int> tiles)
        {
            foreach (var p in tiles)
                _tileViews[_grid.Index(p)].SetHighlight(TileView.TileHighlight.Attack);
        }
    }
}
