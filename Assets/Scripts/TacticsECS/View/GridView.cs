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
        /// <summary>구조물/숲·산 장식을 타일 윗면에서 이만큼 띄운다(납작한 불가사리·물결이 윗면과 겹쳐 깜빡이지 않게).</summary>
        private const float StructureLift = 0.005f;

        private TileView[] _tileViews;
        /// <summary>타일마다 메시 윗면의 로컬 높이 — 구조물을 타일 오브젝트의 자식으로 이 높이에 얹는다. 예전엔 고정값
        /// 0.05를 썼는데 타일 메시(Tile_Land/Water) 높이가 0.2라 모든 구조물이 0.15씩 타일 속에 묻혀 있었다(납작한
        /// 불가사리는 아예 안 보였다).</summary>
        private float[] _tileTopLocalY;
        private GameObject[] _structureObjects;
        private GameObject[] _featureObjects;
        private GameObject[] _buildingObjects;

        /// <summary>타일 받침(흙/모래/짙은 바닥 — TerrainModels.csv "Tile.*"), 구름 덩어리, 흙길 모델. 2차 모델링(docs/ModelingPlan.md).</summary>
        private GameObject[] _baseObjects;
        private GameObject[] _cloudObjects;
        private GameObject[] _roadObjects;

        /// <summary>물 타일 윗면을 땅보다 이만큼 낮춘다(위키 Terrain: 물은 땅 블록보다 낮다). 유닛은 고정 높이라 배가 살짝 뜨는 정도.</summary>
        public const float WaterDrop = 0.05f;

        private GridWorld _grid;
        /// <summary>칸마다 구름에 가렸는지(RefreshFog). 가린 칸의 장식/구조물/건물 오브젝트는 숨긴다 — 다시 만들어도(Refresh*)
        /// 같은 상태를 유지하도록 값을 들고 있는다(표시 전용 상태).</summary>
        private bool[] _fogged;

        /// <summary>도시 칸(도시 모델이 구조물을 대신함) / 건물이 선 칸(건물 모델이 숲·산 장식을 대신함) — RefreshEconomy가 채운다.</summary>
        private bool[] _hideStructure;
        private bool[] _hideFeature;

        /// <summary>landTilePrefab/waterTilePrefab은 GridView 자신이 아니라 BattleController가 들고 있는
        /// 값을 그대로 넘겨받는다(GridView는 씬에 저장된 오브젝트가 아니라 SetupBattle이 매번 새로
        /// AddComponent로 만드는 오브젝트라 자기 자신의 SerializeField는 저장/설정될 수 없다). 둘 다
        /// null이면 예전처럼 프리미티브 큐브로 대체된다(Assets/Prefabs/Tiles/Tile_Land·Water.prefab —
        /// TileAssetSetup.GenerateAll 참고).</summary>
        public void Build(GridWorld grid, GameObject landTilePrefab = null, GameObject waterTilePrefab = null)
        {
            _grid = grid;
            _tileViews = new TileView[grid.Width * grid.Height];
            _tileTopLocalY = new float[grid.Width * grid.Height];

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var terrain = grid.GetTerrain(pos);
                    var go = CreateTileObject(terrain, landTilePrefab, waterTilePrefab);
                    go.name = $"Tile_{x}_{y}";
                    go.transform.SetParent(transform, false);
                    go.transform.position = grid.GridToWorld(pos) + (terrain == TerrainType.Water ? Vector3.down * WaterDrop : Vector3.zero);
                    go.transform.localScale = new Vector3(grid.TileSize - tileGap, go.transform.localScale.y, grid.TileSize - tileGap);
                    // 하이라이트 색을 타일마다 독립적으로 바꿔야 하니, 프리팹이 갖고 있던 머티리얼을 그대로
                    // 쓰지 않고(다른 타일과 sharedMaterial을 공유하게 됨) 매번 새로 만들어 갈아끼운다.
                    go.GetComponentInChildren<Renderer>().sharedMaterial = TopMaterial(terrain);

                    var view = go.AddComponent<TileView>();
                    view.Init(pos, terrain, grid.GetTileType(pos));
                    _tileViews[grid.Index(pos)] = view;
                    _tileTopLocalY[grid.Index(pos)] = TopLocalY(go);
                }
            }
            RefreshFeatures(grid);
        }

        /// <summary>타일 오브젝트의 렌더러 윗면 높이를 그 오브젝트의 로컬 좌표로 환산한다(프리팹 타일은 0.2, 프리미티브
        /// 큐브 폴백은 0.5).</summary>
        private static float TopLocalY(GameObject tile)
        {
            var renderer = tile.GetComponentInChildren<Renderer>();
            float scaleY = tile.transform.lossyScale.y;
            if (renderer == null || Mathf.Approximately(scaleY, 0f)) return 0f;
            return (renderer.bounds.max.y - tile.transform.position.y) / scaleY;
        }

        /// <summary>타일 윗면 두께(위키 Terrain 블록의 풀 층). 그 아래 흙/모래 받침은 TerrainModels.csv의 Tile.* 모델.</summary>
        public const float TileTopHeight = 0.2f;

        /// <summary>타일 하나: 크기 1인 루트 + 경사 없는 저폴리 상자 윗면(첫 자식 — TileView가 이 렌더러의 색을 칠한다).
        /// 2차 모델링 전엔 Kenney 타일 메시(landTilePrefab/waterTilePrefab)를 썼는데, 모서리 경사면이 반투명 물에 비쳐 칸마다 테두리
        /// 격자가 보였고 위키 타일(평평한 블록)과도 달라 상자로 바꿨다. 프리팹 인자는 호환을 위해 남겨 두지만 쓰지 않는다.</summary>
        private static GameObject CreateTileObject(TerrainType terrain, GameObject landTilePrefab, GameObject waterTilePrefab)
        {
            var root = new GameObject("Tile");
            var top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(1f, TileTopHeight, 1f);
            top.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshes.Get(ModelShape.Box);
            top.AddComponent<MeshRenderer>();
            return root;
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
                    var view = _tileViews[grid.Index(pos)];
                    var terrain = grid.GetTerrain(pos);
                    if (view.Terrain != terrain)
                    {
                        // 땅 <-> 물이 바뀌면 높이와 윗면 머티리얼(불투명/반투명)도 바꾼다.
                        view.transform.position = grid.GridToWorld(pos) + (terrain == TerrainType.Water ? Vector3.down * WaterDrop : Vector3.zero);
                        view.GetComponentInChildren<Renderer>().sharedMaterial = TopMaterial(terrain);
                    }
                    view.Init(pos, terrain, grid.GetTileType(pos));
                }
            }
            RefreshFeatures(grid);
        }

        private static Material TopMaterial(TerrainType terrain) => RuntimeMaterial.CreateColored(Color.white);

        /// <summary>타일 종류별 받침 모델 Id(TerrainModels.csv).</summary>
        private static string BaseModel(GridWorld grid, Vector2Int p)
        {
            if (grid.GetTerrain(p) != TerrainType.Water) return "Tile.Land";
            return grid.GetTileType(p) == TerrainGenerationSystem.OceanTileId ? "Tile.Ocean" : "Tile.Shallow";
        }

        /// <summary>숲/산 타일 위의 장식 모델(TerrainFeatureView)을 다시 만든다 — 구조물과 같은 방식으로 타일의
        /// 자식으로 붙고, 숲/산이 아닌 칸은 비워둔다.</summary>
        private void RefreshFeatures(GridWorld grid)
        {
            if (_featureObjects == null) _featureObjects = new GameObject[grid.Width * grid.Height];
            if (_baseObjects == null) _baseObjects = new GameObject[grid.Width * grid.Height];
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
                    _featureObjects[index] = TerrainFeatureView.Create(grid.GetTileType(pos), _tileViews[index].transform, pos, _tileTopLocalY[index] + StructureLift);
                    if (_baseObjects[index] != null) DestroySafe(_baseObjects[index]);
                    // 받침은 물 칸이 WaterDrop만큼 낮아도 바닥이 땅 칸과 맞도록 그만큼 올려 붙인다.
                    _baseObjects[index] = ModelBuilder.Build(BaseModel(grid, pos), _tileViews[index].transform,
                        new Vector3(0f, grid.GetTerrain(pos) == TerrainType.Water ? WaterDrop : 0f, 0f));
                    ApplyFog(index);
                }
            }
        }

        /// <summary>구조물(수도/유적/자원/불가사리, StructureGenerationSystem 참고) 오브젝트를 타일마다
        /// 다시 배치한다 — 이전 구조물을 지우고, grid.GetStructure(pos)가 있으면 그 프리팹을 타일의
        /// 자식으로 축소 인스턴스화한다. TileView(색상)와 완전히 별개 오브젝트라 하이라이트/색 로직에는
        /// 영향이 없다. structurePrefabsByType에 없는 StructureId는 조용히 건너뛴다(에셋 미배정 상태에서도
        /// 예외 없이 동작).</summary>
        public void RefreshStructures(GridWorld grid, IReadOnlyDictionary<string, GameObject> structurePrefabsByType,
            ICollection<string> hiddenStructures = null)
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
                    // 아직 기술이 없어 "숨겨진" 자원(TechSystem.HiddenStructures, 예: 등산 전의 광물)은 그리지 않는다.
                    if (hiddenStructures != null && hiddenStructures.Contains(structureId)) continue;
                    if (structurePrefabsByType == null || !structurePrefabsByType.TryGetValue(structureId, out var prefab) || prefab == null) continue;

                    var structureGo = Object.Instantiate(prefab, _tileViews[index].transform);
                    structureGo.name = "Structure_" + structureId;
                    structureGo.transform.localPosition = new Vector3(0f, _tileTopLocalY[index] + StructureLift, 0f);
                    structureGo.transform.localScale = Vector3.one * StructureLocalScale;
                    _structureObjects[index] = structureGo;
                    ApplyFog(index);
                }
            }
        }

        /// <summary>경제 상태(영토 주인 색/도로/건물/도시 주인 원판)를 다시 그린다. 영토는 타일 색에 팀 색을 섞고
        /// (TileView.SetTerritory), 건물과 도시 원판은 BuildingMarkerView로 타일의 자식 오브젝트를 새로 만든다.
        /// cities가 null이면(경제 없는 씬) 전부 지운다.</summary>
        public void RefreshEconomy(GridWorld grid, IReadOnlyList<CityData> cities, Color playerColor, Color enemyColor, int currentTurn = 1)
        {
            if (_buildingObjects == null) _buildingObjects = new GameObject[grid.Width * grid.Height];
            var cityAt = new Dictionary<Vector2Int, CityData>();
            if (cities != null) foreach (var c in cities) cityAt[c.Position] = c;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);
                    var tile = grid.GetTile(pos);
                    var view = _tileViews[index];

                    Color tint = tile.OwnerTeam == (int)Team.Player ? playerColor
                        : tile.OwnerTeam == (int)Team.Enemy ? enemyColor : Color.clear;
                    view.SetTerritory(tint, tile.HasRoad);

                    if (_buildingObjects[index] != null)
                    {
                        DestroySafe(_buildingObjects[index]);
                        _buildingObjects[index] = null;
                    }
                    float top = _tileTopLocalY[index] + StructureLift;
                    bool isCity = cityAt.TryGetValue(pos, out var city);
                    GameObject marker = isCity
                        ? BuildingMarkerView.CreateCity(city, city.Owner == Team.Player ? playerColor : enemyColor, view.transform, top)
                        : BuildingMarkerView.CreateBuilding(tile.BuildingId, view.transform, top,
                            TileImprovementSystem.DisplayLevel(grid, pos, currentTurn),
                            tile.BuildingId == BuildingDefinition.Bridge && !IsHorizontalBridge(grid, pos),
                            tint.a > 0f ? tint : (Color?)null);
                    if (_roadObjects == null) _roadObjects = new GameObject[grid.Width * grid.Height];
                    if (_roadObjects[index] != null) { DestroySafe(_roadObjects[index]); _roadObjects[index] = null; }
                    if (tile.HasRoad && tile.Terrain == TerrainType.Land && !isCity) _roadObjects[index] = CreateRoad(grid, pos, view.transform, top);

                    // 도시 칸은 도시 모델이 마을/수도 구조물을 대신하고, 숲/산 위 건물(벌목장/광산/신전)은 모델에 자기 지형 장식이
                    // 들어 있어 나무/산 장식을 숨긴다(위키 도판처럼 건물이 그 칸을 차지).
                    if (_hideStructure == null) { _hideStructure = new bool[grid.Width * grid.Height]; _hideFeature = new bool[grid.Width * grid.Height]; }
                    _hideStructure[index] = isCity;
                    _hideFeature[index] = !isCity && marker != null;

                    // 영토 경계: 상하좌우 이웃의 주인 팀이 다르면(또는 맵 밖이면) 그 변에 팀 색 막대.
                    if (tile.OwnerTeam != TileData.NoOwner)
                    {
                        var edges = new List<Vector2Int>();
                        foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                        {
                            var n = pos + d;
                            if (!grid.InBounds(n) || grid.GetTile(n).OwnerTeam != tile.OwnerTeam) edges.Add(d);
                        }
                        if (edges.Count > 0)
                        {
                            if (marker == null)
                            {
                                marker = new GameObject("Territory");
                                marker.transform.SetParent(view.transform, false);
                                marker.transform.localPosition = new Vector3(0f, top, 0f);
                            }
                            BuildingMarkerView.AddBorders(marker.transform, edges, tint);
                        }
                    }
                    _buildingObjects[index] = marker;
                    ApplyFog(index);
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

        /// <summary>흙길: 가운데 조각 + 도로/다리/도시·마을로 이어지는 상하좌우 방향마다 팔 하나(위키 Roads — 이웃과 이어진 길).
        /// 팔 조각(Road.Arm)은 -Z 방향으로 만들어져 있어 방향마다 Y축으로 돌린다.</summary>
        private static GameObject CreateRoad(GridWorld grid, Vector2Int pos, Transform parent, float top)
        {
            var root = ModelBuilder.Build("Road.Center", parent, new Vector3(0f, top, 0f));
            if (root == null) return null;
            root.name = "Road";
            foreach (var (d, yaw) in new[] { (Vector2Int.down, 0f), (Vector2Int.up, 180f), (Vector2Int.right, -90f), (Vector2Int.left, 90f) })
            {
                var n = pos + d;
                if (!grid.InBounds(n)) continue;
                var t = grid.GetTile(n);
                if (!(t.HasRoad || t.BuildingId == BuildingDefinition.Bridge || CitySystem.IsSettlementTile(grid, n))) continue;
                var arm = ModelBuilder.Build("Road.Arm", root.transform, Vector3.zero);
                if (arm != null) arm.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            return root;
        }

        private static bool IsHorizontalBridge(GridWorld grid, Vector2Int p)
        {
            bool Land(Vector2Int q) => grid.InBounds(q) && grid.GetTerrain(q) == TerrainType.Land;
            return Land(p + Vector2Int.left) && Land(p + Vector2Int.right);
        }

        /// <summary>viewer 팀 기준 구름을 다시 그린다: 탐험하지 않은 칸은 구름 색 + 그 위 장식/구조물/건물 숨김.
        /// grid.FogEnabled가 false면 전부 보인다.</summary>
        public void RefreshFog(GridWorld grid, Team viewer)
        {
            if (_fogged == null || _fogged.Length != grid.Width * grid.Height) _fogged = new bool[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2Int(x, y);
                int i = grid.Index(pos);
                _fogged[i] = !VisionSystem.IsExplored(grid, viewer, pos);
                _tileViews[i].SetFogged(_fogged[i]);
                if (_cloudObjects == null) _cloudObjects = new GameObject[grid.Width * grid.Height];
                if (_fogged[i] && _cloudObjects[i] == null)
                    _cloudObjects[i] = TerrainFeatureView.CreateCloud(_tileViews[i].transform, pos, _tileTopLocalY[i] + StructureLift);
                ApplyFog(i);
            }
        }

        private void ApplyFog(int index)
        {
            bool visible = _fogged == null || !_fogged[index];
            if (_featureObjects != null && _featureObjects[index] != null) _featureObjects[index].SetActive(visible && !(_hideFeature != null && _hideFeature[index]));
            if (_structureObjects != null && _structureObjects[index] != null) _structureObjects[index].SetActive(visible && !(_hideStructure != null && _hideStructure[index]));
            if (_buildingObjects != null && _buildingObjects[index] != null) _buildingObjects[index].SetActive(visible);
            if (_roadObjects != null && _roadObjects[index] != null) _roadObjects[index].SetActive(visible);
            if (_cloudObjects != null && _cloudObjects[index] != null) _cloudObjects[index].SetActive(!visible);
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
