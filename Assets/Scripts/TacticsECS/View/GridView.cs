using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TacticsECS
{
    /// <summary>Pixel tilemaps and markers driven by unchanged simulation data.</summary>
    public class GridView : MonoBehaviour
    {
        public const float TileTopHeight = 0f; // legacy editor callers; no 3D surface
        public const float WaterDrop = 0f;
        private GridWorld _grid;
        private TileView[] _tileViews;
        private GameObject[] _features, _structures, _buildings, _roads, _shores;
        private bool[] _fogged, _hideStructure, _hideFeature;
        private Tilemap _ground, _fog;
        private readonly Dictionary<string, Tile> _tiles = new Dictionary<string, Tile>();
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public Tilemap Ground => _ground;

        public void Build(GridWorld grid, GameObject landTilePrefab = null, GameObject waterTilePrefab = null)
        {
            _grid = grid;
            int count = grid.Width * grid.Height;
            _tileViews = new TileView[count];
            _features = new GameObject[count]; _structures = new GameObject[count];
            _buildings = new GameObject[count]; _roads = new GameObject[count]; _shores = new GameObject[count];
            _fogged = new bool[count]; _hideStructure = new bool[count]; _hideFeature = new bool[count];
            var layout = new GameObject("PixelGrid").AddComponent<Grid>();
            layout.transform.SetParent(transform, false);
            layout.transform.position = PixelCoordinates.FromLogical(grid.Origin) - new Vector3(grid.TileSize / 2, grid.TileSize / 2, 0);
            layout.transform.localScale = new Vector3(grid.TileSize, grid.TileSize, 1);
            _ground = MakeTilemap(layout.transform, "Ground", -30000);
            _fog = MakeTilemap(layout.transform, "Fog", PixelSpriteCatalog.FogOrder);
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                var go = new GameObject($"Tile_{x}_{y}");
                go.transform.SetParent(transform, false);
                go.transform.position = PixelCoordinates.GridToWorld(grid, p);
                go.transform.localScale = new Vector3(grid.TileSize, grid.TileSize, 1);
                var view = go.AddComponent<TileView>();
                _tileViews[grid.Index(p)] = view;
                view.Bind(_ground);
            }
            RefreshTerrain(grid);
        }
        private static Tilemap MakeTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            var map = go.GetComponent<Tilemap>(); map.tileAnchor = new Vector3(.5f, .5f, 0);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = PixelSpriteCatalog.Material; renderer.sortingOrder = order;
            return map;
        }
        private Tile TileFor(string id)
        {
            if (_tiles.TryGetValue(id, out var tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>(); tile.name = id;
            tile.sprite = PixelSpriteCatalog.Get(id); tile.flags = TileFlags.None; tile.colliderType = Tile.ColliderType.None;
            _tiles[id] = tile; return tile;
        }
        public void RefreshTerrain(GridWorld grid)
        {
            _grid = grid;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y); int i = grid.Index(p); string type = grid.GetTileType(p);
                string ground = grid.GetTerrain(p) == TerrainType.Water
                    ? (type == TerrainGenerationSystem.OceanTileId ? "Ocean" : "Water")
                    : (PixelSpriteCatalog.Has("Ground." + type) ? type : "Grass");
                _ground.SetTile(new Vector3Int(x, y, 0), TileFor("Ground." + ground));
                _tileViews[i].Init(p, grid.GetTerrain(p), type);
                Replace(ref _features[i], TerrainFeatureView.Create(type, _tileViews[i].transform, p, 0));
                Replace(ref _shores[i], CreateShore(grid, p, _tileViews[i].transform));
                ApplyFog(i);
            }
        }
        private static GameObject CreateShore(GridWorld grid, Vector2Int p, Transform parent)
        {
            if (grid.GetTerrain(p) == TerrainType.Water) return null;
            GameObject root = null;
            foreach (var d in Directions)
            {
                var n = p + d;
                if (!grid.InBounds(n) || grid.GetTerrain(n) != TerrainType.Water) continue;
                if (root == null) { root = new GameObject("Shore"); root.transform.SetParent(parent, false); }
                PixelSpriteCatalog.Rectangle(root.transform, "SandEdge", new Vector2(d.x,d.y) * .46875f,
                    d.x == 0 ? new Vector2(1,.0625f) : new Vector2(.0625f,1), new Color(.91f,.81f,.59f), -29000);
            }
            return root;
        }
        public void RefreshStructures(GridWorld grid, IReadOnlyDictionary<string, GameObject> structurePrefabsByType, ICollection<string> hiddenStructures = null)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x,y); int i = grid.Index(p); string id = grid.GetStructure(p);
                bool show = !string.IsNullOrEmpty(id) && !(hiddenStructures != null && hiddenStructures.Contains(id));
                Replace(ref _structures[i], show ? PixelSpriteCatalog.Build("Structure." + id, _tileViews[i].transform,
                    order: PixelSpriteCatalog.SortOrder(_tileViews[i].transform.position.y)) : null);
                ApplyFog(i);
            }
        }
        public void RefreshEconomy(GridWorld grid, IReadOnlyList<CityData> cities, Color playerColor, Color enemyColor, int currentTurn = 1)
        {
            var cityAt = new Dictionary<Vector2Int, CityData>();
            if (cities != null) foreach (var c in cities) cityAt[c.Position] = c;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x,y); int i = grid.Index(p); var tile = grid.GetTile(p); var view = _tileViews[i];
                Color tint = tile.OwnerTeam == (int)Team.Player ? playerColor : tile.OwnerTeam == (int)Team.Enemy ? enemyColor : Color.clear;
                view.SetTerritory(tint, tile.HasRoad);
                bool isCity = cityAt.TryGetValue(p, out var city);
                var marker = isCity ? BuildingMarkerView.CreateCity(city, city.Owner == Team.Player ? playerColor : enemyColor, view.transform, 0)
                    : BuildingMarkerView.CreateBuilding(tile.BuildingId, view.transform, 0, TileImprovementSystem.DisplayLevel(grid,p,currentTurn),
                        tile.BuildingId == BuildingDefinition.Bridge && !IsHorizontalBridge(grid,p), tint.a > 0 ? tint : (Color?)null);
                _hideStructure[i] = isCity; _hideFeature[i] = !isCity && marker != null;
                if (tile.OwnerTeam != TileData.NoOwner)
                {
                    var edges = new List<Vector2Int>();
                    foreach (var d in Directions) { var n = p+d; if (!grid.InBounds(n) || grid.GetTile(n).OwnerTeam != tile.OwnerTeam) edges.Add(d); }
                    if (edges.Count > 0)
                    {
                        if (marker == null) { marker = new GameObject("Territory"); marker.transform.SetParent(view.transform, false); }
                        BuildingMarkerView.AddBorders(marker.transform, edges, tint);
                    }
                }
                Replace(ref _buildings[i], marker);
                Replace(ref _roads[i], tile.HasRoad && tile.Terrain == TerrainType.Land && !isCity ? CreateRoad(grid,p,view.transform) : null);
                ApplyFog(i);
            }
        }
        private static GameObject CreateRoad(GridWorld grid, Vector2Int pos, Transform parent)
        {
            var root = new GameObject("Road"); root.transform.SetParent(parent, false); var color = new Color(.70f,.49f,.32f);
            PixelSpriteCatalog.Rectangle(root.transform,"Centre",Vector2.zero,Vector2.one*.25f,color,-28000);
            foreach (var d in Directions)
            {
                var n = pos+d; if (!grid.InBounds(n)) continue; var t = grid.GetTile(n);
                if (!(t.HasRoad || t.BuildingId == BuildingDefinition.Bridge || CitySystem.IsSettlementTile(grid,n))) continue;
                PixelSpriteCatalog.Rectangle(root.transform,"Connection",new Vector2(d.x,d.y)*.3125f,
                    d.x == 0 ? new Vector2(.25f,.375f) : new Vector2(.375f,.25f),color,-28000);
            }
            return root;
        }
        private static bool IsHorizontalBridge(GridWorld grid, Vector2Int p) => grid.InBounds(p+Vector2Int.left) && grid.InBounds(p+Vector2Int.right)
            && grid.GetTerrain(p+Vector2Int.left) == TerrainType.Land && grid.GetTerrain(p+Vector2Int.right) == TerrainType.Land;
        public void RefreshFog(GridWorld grid, Team viewer)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x,y); int i = grid.Index(p); _fogged[i] = !VisionSystem.IsExplored(grid,viewer,p);
                _tileViews[i].SetFogged(_fogged[i]);
                _fog.SetTile(new Vector3Int(x,y,0),_fogged[i] ? TileFor("Terrain.Cloud") : null); ApplyFog(i);
            }
        }
        private void ApplyFog(int i)
        {
            bool visible = !_fogged[i];
            if (_features[i] != null) _features[i].SetActive(visible && !_hideFeature[i]);
            if (_structures[i] != null) _structures[i].SetActive(visible && !_hideStructure[i]);
            if (_buildings[i] != null) _buildings[i].SetActive(visible);
            if (_roads[i] != null) _roads[i].SetActive(visible);
            if (_shores[i] != null) _shores[i].SetActive(visible);
        }
        private static void Replace(ref GameObject previous, GameObject replacement)
        {
            if (previous != null) { previous.SetActive(false); DestroySafe(previous); } previous = replacement;
        }
        private static void DestroySafe(Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        private void OnDestroy() { foreach (var tile in _tiles.Values) if (tile != null) DestroySafe(tile); }
        public void ClearHighlights() { foreach (var tile in _tileViews) tile.SetHighlight(TileView.TileHighlight.None); }
        public void HighlightMove(IEnumerable<Vector2Int> tiles) { foreach (var p in tiles) if (_grid.InBounds(p)) _tileViews[_grid.Index(p)].SetHighlight(TileView.TileHighlight.Move); }
        public void HighlightAttack(IEnumerable<Vector2Int> tiles) { foreach (var p in tiles) if (_grid.InBounds(p)) _tileViews[_grid.Index(p)].SetHighlight(TileView.TileHighlight.Attack); }
    }
}
