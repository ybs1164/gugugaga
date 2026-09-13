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

        /// <summary>landTilePrefab/waterTilePrefab은 GridView 자신이 아니라 BattleController가 들고 있는
        /// 값을 그대로 넘겨받는다(GridView는 씬에 저장된 오브젝트가 아니라 SetupBattle이 매번 새로
        /// AddComponent로 만드는 오브젝트라 자기 자신의 SerializeField는 저장/설정될 수 없다). 둘 다
        /// null이면 예전처럼 프리미티브 큐브로 대체된다(Assets/Prefabs/Tiles/Tile_Land·Water.prefab —
        /// TileAssetSetup.GenerateAll 참고).</summary>
        public void Build(GridWorld grid, GameObject landTilePrefab = null, GameObject waterTilePrefab = null)
        {
            _grid = grid;
            _tileViews = new TileView[grid.Width * grid.Height];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var terrain = grid.GetTerrain(pos);
                    var go = CreateTileObject(terrain, landTilePrefab, waterTilePrefab);
                    go.name = $"Tile_{x}_{y}";
                    go.transform.SetParent(transform, false);
                    go.transform.position = grid.GridToWorld(pos);
                    go.transform.localScale = new Vector3(grid.TileSize - tileGap, go.transform.localScale.y, grid.TileSize - tileGap);
                    // 하이라이트 색을 타일마다 독립적으로 바꿔야 하니, 프리팹이 갖고 있던 머티리얼을 그대로
                    // 쓰지 않고(다른 타일과 sharedMaterial을 공유하게 됨) 매번 새로 만들어 갈아끼운다.
                    go.GetComponentInChildren<Renderer>().sharedMaterial = RuntimeMaterial.CreateColored(Color.white);

                    var view = go.AddComponent<TileView>();
                    view.Init(pos, terrain);
                    _tileViews[grid.Index(pos)] = view;
                }
            }
        }

        /// <summary>terrain에 맞는 타일 프리팹이 주어졌으면 그것을 인스턴스화하고(가로/세로만 타일 크기에
        /// 맞춰 스케일하고 높이는 그대로 둔다), 없으면 예전처럼 얇은 큐브를 만든다.</summary>
        private static GameObject CreateTileObject(TerrainType terrain, GameObject landTilePrefab, GameObject waterTilePrefab)
        {
            var prefab = terrain == TerrainType.Water ? waterTilePrefab : landTilePrefab;
            if (prefab != null) return Object.Instantiate(prefab);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = new Vector3(1f, 0.1f, 1f);
            return cube;
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
