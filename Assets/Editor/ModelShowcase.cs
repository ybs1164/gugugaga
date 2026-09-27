using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 건물/타일/유닛 모델을 전투 화면과 같은 등각 구도로 찍어 PNG로 저장하고, 모델별 비교 지표(렌더러 수, 삼각형 수,
    /// 건물끼리 실루엣이 얼마나 겹치는지 — IoU)를 CSV로 남기는 에디터 도구. 모델링 개선(docs/ModelingPlan.md)의 전/후
    /// 비교와 위키 이미지 대조에 쓴다. Play 모드 없이 Edit 모드에서 임시 씬을 만들어 RenderTexture로 찍는다
    /// (-executeMethod는 Play 모드를 기다리지 못한다). -nographics 없이 실행해야 픽셀이 나온다.
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.ModelShowcase.Run [-showcaseOut &lt;폴더&gt;]
    /// 기본 출력: Logs/Showcase/ (buildings.png, tiles.png, units.png, metrics.csv)
    /// </summary>
    public static class ModelShowcase
    {
        private const float TileSize = 1.2f;
        private const int Shot = 1600;
        private static readonly Vector3 SilhouetteSpot = new Vector3(500f, 0f, 500f);

        public static void Run()
        {
            GameDataLoader.LoadAll();
            string outDir = OutDir();
            Directory.CreateDirectory(outDir);
            var metrics = new StringBuilder("category,id,renderers,triangles,silhouetteMaxIoU,silhouetteMeanIoU\n");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            ShotBuildings(outDir, metrics);
            ShotTiles(outDir);
            ShotUnits(outDir, metrics);

            File.WriteAllText(Path.Combine(outDir, "metrics.csv"), metrics.ToString());
            Debug.Log("[ModelShowcase] wrote " + outDir);
        }

        private static string OutDir()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-showcaseOut");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            return Path.Combine(Application.dataPath, "..", "Logs", "Showcase");
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static GridView BuildGrid(GridWorld grid)
        {
            var go = new GameObject("Grid");
            var view = go.AddComponent<GridView>();
            view.Build(grid, Load("Assets/Prefabs/Tiles/Tile_Land.prefab"), Load("Assets/Prefabs/Tiles/Tile_Water.prefab"));
            return view;
        }

        private static Dictionary<string, GameObject> StructurePrefabs()
        {
            var map = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Structures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).Replace("Structure_", "");
                var prefab = Load(path);
                if (name.StartsWith("Resource")) map["Resource_" + name.Substring("Resource".Length)] = prefab;
                else map[name] = prefab;
            }
            if (map.TryGetValue("Resource_Ore", out var ore)) map["Resource_Metal"] = ore;
            return map;
        }

        // ---------- 건물 ----------

        private static void ShotBuildings(string outDir, StringBuilder metrics)
        {
            var ids = BuildingDefinition.All.Where(b => !b.IsRoad).Select(b => b.Id).ToList();
            const int cols = 7;
            int buildingRows = (ids.Count + cols - 1) / cols;
            int rows = buildingRows + 3;
            var grid = new GridWorld(cols, rows, TileSize);
            for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            // 1~3줄: 모든 건물(레벨 1) — 실제 게임 경로(GridView.RefreshEconomy)로 그린다.
            for (int i = 0; i < ids.Count; i++)
            {
                var p = new Vector2Int(i % cols, i / cols);
                var info = BuildingDefinition.All.First(b => b.Id == ids[i]);
                if ((info.Terrain & TileClass.Field) == 0)
                {
                    if ((info.Terrain & TileClass.Forest) != 0) grid.SetTileType(p, TerrainGenerationSystem.ForestTileId);
                    else if ((info.Terrain & TileClass.Mountain) != 0) grid.SetTileType(p, TerrainGenerationSystem.MountainTileId);
                    else { grid.SetTerrain(p, TerrainType.Water); grid.SetTileType(p, "Water"); }
                }
                var t = grid.GetTile(p);
                t.BuildingId = ids[i];
                t.OwnerTeam = (int)Team.Player;
                t.BuildingTurn = 1;
                grid.SetTile(p, t);
            }
            // 마지막 줄: 도시(레벨 1 / 3+성벽 / 5 수도+공방+공원+성벽 / 8 수도) + 도로.
            int cityRow = rows - 1;
            var cities = new List<CityData>
            {
                new CityData { Owner = Team.Player, Level = 1, Position = new Vector2Int(0, cityRow) },
                new CityData { Owner = Team.Enemy, Level = 3, HasWall = true, Position = new Vector2Int(1, cityRow) },
                new CityData { Owner = Team.Player, Level = 5, IsCapital = true, HasWall = true, HasWorkshop = true, ParkCount = 1, Position = new Vector2Int(2, cityRow) },
                new CityData { Owner = Team.Enemy, Level = 8, IsCapital = true, HasWorkshop = true, Position = new Vector2Int(3, cityRow) },
            };
            for (int x = 4; x < cols; x++) { var t = grid.GetTile(new Vector2Int(x, cityRow)); t.HasRoad = true; grid.SetTile(new Vector2Int(x, cityRow), t); }

            var view = BuildGrid(grid);
            view.RefreshTerrain(grid);
            view.RefreshEconomy(grid, cities, new Color(0.2f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f), 1);
            // 가운데 두 줄: 레벨에 따라 바뀌는 모델(신전 1~5, 대장간 0/4, 제재소 1/6, 시장 1/8, 풍차 1/4).
            var levelShots = new (string Id, int Level)[]
            {
                ("Temple", 1), ("Temple", 2), ("Temple", 3), ("Temple", 4), ("Temple", 5), ("Forge", 0), ("Forge", 4),
                ("Sawmill", 1), ("Sawmill", 6), ("Market", 1), ("Market", 8), ("Windmill", 1), ("Windmill", 4), ("MountainTemple", 5),
            };
            for (int i = 0; i < levelShots.Length; i++)
            {
                var pos = new Vector2Int(i % cols, buildingRows + i / cols);
                var tile = view.transform.Find($"Tile_{pos.x}_{pos.y}");
                BuildingMarkerView.CreateBuilding(levelShots[i].Id, tile, TileTop(tile), levelShots[i].Level, false, new Color(0.2f, 0.5f, 1f));
            }
            Capture(view.gameObject, grid, Path.Combine(outDir, "buildings.png"), 0.56f);

            // 비교 지표: 건물마다 따로 세운 뒤 실루엣 마스크끼리 IoU.
            var masks = new Dictionary<string, bool[]>();
            foreach (var id in ids)
            {
                var root = BuildingMarkerView.CreateBuilding(id, null, 0f, 1, false);
                if (root == null) continue;
                root.transform.position = SilhouetteSpot; // 격자와 겹치지 않게 멀리서 따로 찍는다.
                masks[id] = Silhouette(root);
                var renderers = root.GetComponentsInChildren<Renderer>();
                int tris = root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0);
                metrics.Append($"building,{id},{renderers.Length},{tris},");
                metrics.Append("{IOU_" + id + "}\n");
                Object.DestroyImmediate(root);
            }
            string text = metrics.ToString();
            foreach (var id in masks.Keys)
            {
                var others = masks.Where(kv => kv.Key != id).Select(kv => IoU(masks[id], kv.Value)).ToList();
                text = text.Replace("{IOU_" + id + "}", $"{others.Max():0.000},{others.Average():0.000}");
            }
            metrics.Clear().Append(text);
            Object.DestroyImmediate(view.gameObject);
        }

        private static float TileTop(Transform tile)
        {
            var r = tile.GetComponentInChildren<Renderer>();
            float sy = tile.lossyScale.y;
            return r == null || Mathf.Approximately(sy, 0f) ? 0f : (r.bounds.max.y - tile.position.y) / sy + 0.001f;
        }

        // ---------- 타일 ----------

        private static void ShotTiles(string outDir)
        {
            string[] types = { "Grass", "Grass", "Grass", TerrainGenerationSystem.ForestTileId, TerrainGenerationSystem.MountainTileId, TerrainGenerationSystem.MountainTileId, "Water", "Water", TerrainGenerationSystem.OceanTileId, "Grass" };
            string[] structures = { "", "Resource_Fruit", "Resource_Crop", "Resource_Animal", "", "Resource_Metal", "", "Resource_Fish", "Starfish", "Village" };
            var grid = new GridWorld(types.Length, 2, TileSize);
            for (int x = 0; x < types.Length; x++)
                for (int y = 0; y < 2; y++)
                {
                    var p = new Vector2Int(x, y);
                    bool water = types[x] == "Water" || types[x] == TerrainGenerationSystem.OceanTileId;
                    if (water) grid.SetTerrain(p, TerrainType.Water);
                    grid.SetTileType(p, types[x]);
                    if (y == 1) grid.SetStructure(p, structures[x]);
                }
            // 구름(탐험 안 한 칸) 한 칸.
            grid.FogEnabled = true;
            for (int x = 0; x < types.Length; x++) for (int y = 0; y < 2; y++)
                if (!(x == types.Length - 1 && y == 0)) VisionSystem.Reveal(grid, Team.Player, new Vector2Int(x, y), 0);
            var view = BuildGrid(grid);
            view.RefreshTerrain(grid);
            view.RefreshStructures(grid, StructurePrefabs());
            view.RefreshFog(grid, Team.Player);
            Capture(view.gameObject, grid, Path.Combine(outDir, "tiles.png"));
            Object.DestroyImmediate(view.gameObject);
        }

        // ---------- 유닛 ----------

        private static void ShotUnits(string outDir, StringBuilder metrics)
        {
            var rows = UnitCsvSerializer.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", "SandboxUnits.csv")));
            string[] naval = { NavalUnitDefinition.RaftId, "scout", "rammer", "bomber" };
            int cols = Mathf.Max(rows.Count, naval.Length);
            var grid = new GridWorld(cols, 2, TileSize);
            for (int x = 0; x < cols; x++)
            {
                grid.SetTileType(new Vector2Int(x, 0), "Grass");
                grid.SetTerrain(new Vector2Int(x, 1), TerrainType.Water);
                grid.SetTileType(new Vector2Int(x, 1), "Water");
            }
            var view = BuildGrid(grid);
            view.RefreshTerrain(grid);
            var world = new EntityWorld();
            var spawner = new GameObject("Spawner").AddComponent<UnitSpawner>();
            spawner.transform.SetParent(view.transform, false);
            for (int i = 0; i < rows.Count; i++)
            {
                var prefab = Load($"Assets/Prefabs/Units/Unit_{rows[i].BaseVisual}.prefab");
                if (prefab == null) continue;
                var u = spawner.SpawnFromCsv(grid, world, i % 2 == 0 ? Team.Player : Team.Enemy, prefab.GetComponent<UnitView>(), rows[i], new Vector2Int(i, 0));
                var rs = u.GetComponentsInChildren<Renderer>().Where(r => !(r is MeshRenderer mr && mr.GetComponent<TextMesh>() != null)).ToArray();
                int tris = u.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s => s.sharedMesh.triangles.Length / 3) +
                           u.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0);
                metrics.Append($"unit,{rows[i].Id},{rs.Length},{tris},,\n");
            }
            var infantry = rows.First();
            var basePrefab = Load($"Assets/Prefabs/Units/Unit_{infantry.BaseVisual}.prefab").GetComponent<UnitView>();
            for (int i = 0; i < naval.Length; i++)
            {
                var pos = new Vector2Int(i, 1);
                var u = spawner.SpawnFromCsv(grid, world, Team.Player, basePrefab, infantry, pos);
                world.Set(u.UnitId, new Embarked { Value = true, NavalUnitId = naval[i] });
                u.Refresh(world, u.UnitId);
            }
            Capture(view.gameObject, grid, Path.Combine(outDir, "units.png"), 0.8f);
            Object.DestroyImmediate(view.gameObject);
        }

        // ---------- 촬영 ----------

        private static Camera MakeCamera(Vector3 focus, float orthoSize, int size)
        {
            var camGo = new GameObject("ShowcaseCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.93f, 0.93f);
            var rot = Quaternion.Euler(35.264f, 45f, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = focus - rot * Vector3.forward * 40f;
            cam.targetTexture = new RenderTexture(size, size, 24);
            return cam;
        }

        private static void Capture(GameObject root, GridWorld grid, string path, float zoom = 0.62f)
        {
            var center = (grid.GridToWorld(Vector2Int.zero) + grid.GridToWorld(new Vector2Int(grid.Width - 1, grid.Height - 1))) * 0.5f;
            float span = Mathf.Max(grid.Width, grid.Height) * TileSize;
            var cam = MakeCamera(center, span * zoom, Shot);
            File.WriteAllBytes(path, Render(cam).EncodeToPNG());
            Object.DestroyImmediate(cam.gameObject);
        }

        private static Texture2D Render(Camera cam)
        {
            var rt = cam.targetTexture;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex;
        }

        /// <summary>원점에 세운 모델 하나를 64x64로 찍어 배경이 아닌 픽셀을 true로 한 실루엣.</summary>
        private static bool[] Silhouette(GameObject model)
        {
            var cam = MakeCamera(SilhouetteSpot + new Vector3(0f, 0.25f, 0f), 0.6f, 64);
            cam.backgroundColor = Color.black;
            var tex = Render(cam);
            var px = tex.GetPixels();
            Object.DestroyImmediate(cam.gameObject);
            return px.Select(c => c.r + c.g + c.b > 0.02f).ToArray();
        }

        private static float IoU(bool[] a, bool[] b)
        {
            int inter = 0, uni = 0;
            for (int i = 0; i < a.Length; i++) { if (a[i] && b[i]) inter++; if (a[i] || b[i]) uni++; }
            return uni == 0 ? 0f : (float)inter / uni;
        }
    }
}
