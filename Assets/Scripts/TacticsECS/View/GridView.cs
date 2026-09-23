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
        [SerializeField] private float tileGap = 0f;

        /// <summary>구조물(수도/유적/자원/불가사리) 프리팹을 타일 위에 얼마나 축소해서 놓을지 —
        /// 타일 가장자리에 살짝 여백만 남기고 대부분을 채운 건물처럼 보이도록 하는 값(각 프리팹은
        /// StructureAssetSetup에서 이미 1x1 타일 기준 발자국에 맞춰 만들어져 있다).</summary>
        private const float StructureLocalScale = 0.85f;
        private const float StructureLocalHeight = 0.05f;

        private TileView[] _tileViews;
        private GameObject[] _structureObjects;
        private GameObject[] _featureObjects;
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
                    view.Init(pos, terrain, grid.GetTileType(pos));
                    _tileViews[grid.Index(pos)] = view;
                }
            }
            RefreshFeatures(grid);
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

        /// <summary>지형(Terrain/TileTypeId)이 바뀐 뒤(TerrainGenerationSystem.Generate) 기존 타일
        /// GameObject를 파괴/재생성하지 않고 색만 다시 입힌다 — Build와 달리 오브젝트 개수/메시는 그대로다.</summary>
        public void RefreshTerrain(GridWorld grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    _tileViews[grid.Index(pos)].Init(pos, grid.GetTerrain(pos), grid.GetTileType(pos));
                }
            }
            RefreshFeatures(grid);
        }

        /// <summary>숲/산 타일 위의 장식 모델(TerrainFeatureView)을 다시 만든다 — 구조물과 같은 방식으로 타일의
        /// 자식으로 붙고, 숲/산이 아닌 칸은 비워둔다.</summary>
        private void RefreshFeatures(GridWorld grid)
        {
            if (_featureObjects == null) _featureObjects = new GameObject[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);
                    if (_featureObjects[index] != null)
                    {
                        DestroySafe(_featureObjects[index]);
                        _featureObjects[index] = null;
                    }
                    _featureObjects[index] = TerrainFeatureView.Create(grid.GetTileType(pos), _tileViews[index].transform, pos, StructureLocalHeight);
                }
            }
        }

        /// <summary>구조물(수도/유적/자원/불가사리, StructureGenerationSystem 참고) 오브젝트를 타일마다
        /// 다시 배치한다 — 이전 구조물을 지우고, grid.GetStructure(pos)가 있으면 그 프리팹을 타일의
        /// 자식으로 축소 인스턴스화한다. TileView(색상)와 완전히 별개 오브젝트라 하이라이트/색 로직에는
        /// 영향이 없다. structurePrefabsByType에 없는 StructureId는 조용히 건너뛴다(에셋 미배정 상태에서도
        /// 예외 없이 동작).</summary>
        public void RefreshStructures(GridWorld grid, IReadOnlyDictionary<string, GameObject> structurePrefabsByType)
        {
            if (_structureObjects == null) _structureObjects = new GameObject[grid.Width * grid.Height];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);

                    if (_structureObjects[index] != null)
                    {
                        DestroySafe(_structureObjects[index]);
                        _structureObjects[index] = null;
                    }

                    var structureId = grid.GetStructure(pos);
                    if (string.IsNullOrEmpty(structureId)) continue;
                    if (structurePrefabsByType == null || !structurePrefabsByType.TryGetValue(structureId, out var prefab) || prefab == null) continue;

                    var structureGo = Object.Instantiate(prefab, _tileViews[index].transform);
                    structureGo.name = "Structure_" + structureId;
                    structureGo.transform.localPosition = new Vector3(0f, StructureLocalHeight, 0f);
                    structureGo.transform.localScale = Vector3.one * StructureLocalScale;
                    _structureObjects[index] = structureGo;
                }
            }
        }

        /// <summary>Play 모드에서는 Destroy, Edit 모드(배치모드 검증 스크립트 등)에서는 DestroyImmediate —
        /// BattleController.DestroySafe와 같은 이유(Edit 모드에서 Destroy를 부르면 무시되고 에러만 남는다).</summary>
        private static void DestroySafe(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
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
