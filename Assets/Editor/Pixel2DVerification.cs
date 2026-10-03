using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace TacticsECS.EditorTools
{
    /// <summary>CLI integration checks for the 2D migration, plus reviewable rendered artifacts.</summary>
    public static class Pixel2DVerification
    {
        private static int _checks;
        private static readonly Color Blue = new Color(.3f,.6f,1f), Red = new Color(1f,.36f,.29f);
        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException("[Pixel2DVerification] FAIL: " + message);
        }
        public static void Run()
        {
            GameDataLoader.LoadAll();
            VerifyCatalog();
            VerifyPixelStyle();
            VerifyCoordinates();
            VerifyFogAndEconomy();
            VerifyUnits();
            VerifyPrefabDependencies();
            VerifyMaximumMap();
            Debug.Log($"[Pixel2DVerification] ALL PASS ({_checks} checks)");
        }
        private static void VerifyCatalog()
        {
            foreach (var id in PixelSpriteCatalog.VisualIds)
            foreach (var part in PixelSpriteCatalog.Layers(id))
                Check(PixelSpriteCatalog.SpriteFor(part) != null, "sprite exists: " + id);
            foreach (var structure in StructureDefinition.All) Check(PixelSpriteCatalog.Has("Structure." + structure.Id), "structure: " + structure.Id);
            foreach (var building in BuildingDefinition.All) Check(PixelSpriteCatalog.Has("Building." + building.Id), "building: " + building.Id);
            foreach (var row in GameTables.Units) Check(PixelSpriteCatalog.Has("Unit." + row.Id), "unit: " + row.Id);
            foreach (string id in new[] { NavalUnitDefinition.Raft.Id }.Concat(NavalUnitDefinition.Upgrades.Select(n => n.Row.Id)).Concat(NavalUnitDefinition.Special.Select(n => n.Id)))
                Check(PixelSpriteCatalog.Has("Boat." + id), "naval: " + id);
            foreach (var tech in GameDataLoader.LoadTechNodes()) Check(PixelSpriteCatalog.Has("Icon." + tech.Icon), "tech icon: " + tech.Icon);
        }
        private static void VerifyPixelStyle()
        {
            PixelSpriteComposer.Clipped.Clear();
            foreach (string id in PixelSpriteCatalog.VisualIds)
            {
                if (id.StartsWith("Ground.") || id.StartsWith("UI.") || id == "Terrain.Cloud") continue;
                foreach (int level in new[] { 1,3,8 }) CheckComposite(PixelSpriteComposer.Compose(id,level,Blue),id + " level " + level);
            }
            // Every tile combination the priority rules can produce stays inside the canvas.
            foreach (string feature in new[] { "Forest","Mountain","Rock" })
            foreach (var structure in StructureDefinition.All)
            {
                var placements = TileVisuals.For(Vector2Int.zero,feature,structure.Id,null,null);
                CheckComposite(PixelSpriteComposer.Compose(placements),feature + "+" + structure.Id);
            }
            var city = new CityData { Level = 8, IsCapital = true, HasWall = true, HasWorkshop = true, ParkCount = 1 };
            CheckComposite(PixelSpriteComposer.Compose(TileVisuals.City(city),8,Blue),"full city");
            foreach (string boat in new[] { "raft","scout","rammer","bomber","dinghy","pirate" })
            foreach (var row in GameTables.Units)
                CheckComposite(PixelSpriteComposer.Compose(new[] { new SpritePlacement("Boat."+boat), new SpritePlacement("Unit."+row.Id,3,7) }),row.Id + " on " + boat);
            Check(PixelSpriteComposer.Clipped.Count == 0,"nothing cut at the canvas edge: " + string.Join(", ",PixelSpriteComposer.Clipped));
        }
        /// <summary>Detail resolution, and every silhouette edge pixel is the shared thick stroke colour.</summary>
        private static void CheckComposite(Sprite sprite, string label)
        {
            Check(sprite.pixelsPerUnit == PixelSpriteCatalog.PixelsPerUnit,"detail pixel grid: " + label);
            int w = (int)sprite.rect.width, h = (int)sprite.rect.height;
            var pixels = sprite.texture.GetPixels32();
            bool uniform = true, any = false;
            for (int y=0;y<h;y++) for (int x=0;x<w;x++)
            {
                var c = pixels[y*w+x];
                if (c.a == 0) continue;
                any = true;
                bool edge = x == 0 || y == 0 || x == w-1 || y == h-1 || pixels[y*w+x-1].a == 0 || pixels[y*w+x+1].a == 0 || pixels[(y-1)*w+x].a == 0 || pixels[(y+1)*w+x].a == 0;
                if (edge) uniform &= c.r == PixelSpriteComposer.Outline.r && c.g == PixelSpriteComposer.Outline.g && c.b == PixelSpriteComposer.Outline.b;
            }
            Check(any,"composite has pixels: " + label);
            Check(uniform,"uniform thick silhouette stroke: " + label);
        }
        private static void VerifyCoordinates()
        {
            var cameraGo = new GameObject("CoordinateCamera");
            var camera = cameraGo.AddComponent<Camera>();
            var rt = new RenderTexture(1280,720,0);
            camera.targetTexture = rt; camera.orthographic = true;
            var grid = new GridWorld(8,5,1.2f,new Vector3(-3,0,7));
            try
            {
                foreach (float zoom in new[] { 2f,5f,12f })
                foreach (var pan in new[] { Vector2.zero,new Vector2(3,-2) })
                {
                    camera.transform.SetPositionAndRotation(new Vector3(pan.x,pan.y,-20),Quaternion.identity);
                    camera.orthographicSize = zoom;
                    for (int y=0;y<grid.Height;y++) for (int x=0;x<grid.Width;x++)
                    {
                        var p = new Vector2Int(x,y);
                        var screen = camera.WorldToScreenPoint(PixelCoordinates.GridToWorld(grid,p));
                        Check(PixelCoordinates.TryScreenToGrid(camera,grid,screen,out var actual) && actual == p,"roundtrip cell " + p);
                    }
                }
                var outside = PixelCoordinates.GridToWorld(grid,new Vector2Int(-1,0));
                Check(!PixelCoordinates.TryScreenToGrid(camera,grid,camera.WorldToScreenPoint(outside),out _),"outside map rejects click");
                var edge = PixelCoordinates.GridToWorld(grid,new Vector2Int(2,2)) + Vector3.right * (.5f*grid.TileSize-.0001f);
                Check(PixelCoordinates.TryScreenToGrid(camera,grid,camera.WorldToScreenPoint(edge),out var left) && left.x == 2,"left side of edge");
                edge.x += .0002f;
                Check(PixelCoordinates.TryScreenToGrid(camera,grid,camera.WorldToScreenPoint(edge),out var right) && right.x == 3,"right side of edge");
            }
            finally { camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraGo); }
        }
        private static void VerifyFogAndEconomy()
        {
            var grid = new GridWorld(4,4); grid.FogEnabled = true;
            var p = new Vector2Int(1,1); grid.SetTileType(p,"Forest"); grid.SetStructure(p,"Resource_Animal");
            var tile = grid.GetTile(p); tile.OwnerTeam=(int)Team.Player; tile.BuildingId=BuildingDefinition.LumberHut; tile.HasRoad=true; grid.SetTile(p,tile);
            var root = new GameObject("FogIntegration"); var view = root.AddComponent<GridView>();
            try
            {
                view.Build(grid); view.RefreshFog(grid,Team.Player);
                view.RefreshStructures(grid,null); view.RefreshEconomy(grid,null,Blue,Red);
                var cell = root.transform.Find("Tile_1_1");
                Check(!cell.Find("Building.LumberHut").gameObject.activeSelf,"regenerated building remains hidden");
                Check(!cell.Find("Road").gameObject.activeSelf,"road remains hidden");
                Check(cell.Find("Structure.Resource_Animal") == null && cell.Find("Terrain.Forest") == null,"building outranks forest and resource");
                tile=grid.GetTile(p); tile.ExploredMask |= 1 << (int)Team.Player; grid.SetTile(p,tile);
                view.RefreshFog(grid,Team.Player);
                Check(cell.Find("Building.LumberHut").gameObject.activeSelf,"explored building becomes visible");
                tile=grid.GetTile(p); tile.BuildingId=string.Empty; grid.SetTile(p,tile); view.RefreshEconomy(grid,null,Blue,Red);
                Check(cell.Find("Building.LumberHut") == null && cell.Find("Terrain.Forest") != null,"forest and resource share one composite");
                Check(cell.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Pixels") == 1,"one object sprite per tile");
                grid.SetStructure(p,string.Empty); view.RefreshStructures(grid,null);
                Check(cell.Find("Terrain.Forest") != null,"forest stays after resource is consumed");
                grid.SetTileType(p,"Grass"); view.RefreshTerrain(grid);
                Check(cell.Find("Terrain.Forest") == null,"cleared forest removed");
                var city = new CityData { Position = new Vector2Int(2,2), Owner = Team.Player, Level = 3 };
                grid.SetTileType(city.Position,"Mountain"); grid.SetStructure(city.Position,"Village"); view.RefreshTerrain(grid);
                view.RefreshStructures(grid,null); view.RefreshEconomy(grid,new[] { city },Blue,Red);
                var cityCell = root.transform.Find("Tile_2_2");
                Check(cityCell.Find("City.Houses") != null && cityCell.Find("Structure.Village") == null && cityCell.Find("Terrain.Mountain") == null,"city outranks structure and feature");
                Check(root.GetComponentsInChildren<MeshFilter>(true).Length == 0,"world uses no primitive meshes");
                Check(root.GetComponentsInChildren<Tilemap>().Length == 2,"ground and fog are shared tilemaps");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void VerifyUnits()
        {
            var rows=GameTables.Units.ToList();
            var root=new GameObject("UnitIntegration"); var spawner=root.AddComponent<UnitSpawner>();
            var grid=new GridWorld(24,3); var world=new EntityWorld();
            var prefab=AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Melee.prefab");
            try
            {
                int x=0;
                foreach(var row in rows)
                {
                    var view=spawner.SpawnFromCsv(grid,world,Team.Player,prefab,row,new Vector2Int(x++,1));
                    Check(view.transform.Find("Unit."+row.Id) != null,"correct body: "+row.Id);
                    Check(view.transform.position == PixelCoordinates.GridToWorld(grid,world.Get<GridPosition>(view.UnitId).Value),"unit uses XY position");
                    Check(view.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0,"no skinned model");
                }
                var first=spawner.SpawnedViews[0];
                var body=first.transform.Find("Unit."+rows[0].Id).GetComponentInChildren<SpriteRenderer>();
                Check(first.GetComponent<SortingGroup>().sortingOrder > PixelSpriteCatalog.ObjectOrder+5000,"units sort above every tile object");
                foreach(string id in new[] {"raft","scout","rammer","bomber","dinghy","pirate"})
                {
                    world.Set(first.UnitId,new Embarked{Value=true,NavalUnitId=id}); first.Refresh(world,first.UnitId);
                    Check(body.sprite.name.StartsWith("Composite_Boat_"+id+"_"),"boat and rider share one sprite: "+id);
                }
                first.SetVisible(false);
                world.Set(first.UnitId,new Embarked{Value=true,NavalUnitId="raft"}); first.Refresh(world,first.UnitId);
                Check(first.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"new boat remains invisible");
                first.SetVisible(true);
                world.Set(first.UnitId,new Embarked()); first.Refresh(world,first.UnitId);
                Check(body.sprite.name.StartsWith("Composite_Unit_"),"disembark removes boat");
                world.Set(first.UnitId,Team.Enemy); world.Set(first.UnitId,new IsGuarding{Value=true}); first.Refresh(world,first.UnitId);
                Check(first.transform.Find("Guarding").gameObject.activeSelf,"guard badge");
                Check(first.transform.Find("Team").GetComponent<SpriteRenderer>().color.r>.9f,"conversion changes team badge");
                world.Set(first.UnitId,new Hp{Value=0}); first.Refresh(world,first.UnitId);
                Check(!first.gameObject.activeSelf,"dead unit hidden");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void VerifyPrefabDependencies()
        {
            foreach(string folder in new[]{"Assets/Prefabs/Units","Assets/Prefabs/Structures","Assets/Prefabs/Tiles"})
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                Check(!AssetDatabase.GetDependencies(path).Any(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)),"no FBX dependency: "+path);
            }
        }
        private static void VerifyMaximumMap()
        {
            var grid = new GridWorld(30,30);
            for (int y=0;y<30;y++) for(int x=0;x<30;x++)
            {
                var p=new Vector2Int(x,y);
                grid.SetTileType(p,(x+y)%4==0 ? "Forest" : "Grass");
                if((x+y)%7==0) grid.SetStructure(p,"Resource_Fruit");
            }
            var root=new GameObject("MaximumMap"); var view=root.AddComponent<GridView>();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                view.Build(grid); view.RefreshStructures(grid,null); view.RefreshEconomy(grid,null,Blue,Red);
                watch.Stop();
                Check(view.Ground.GetUsedTilesCount()<=2,"maximum map shares tile objects");
                Check(root.GetComponentsInChildren<TileView>().Length==900,"maximum preset has 900 cells");
                Debug.Log($"[Pixel2DVerification] 30x30 build+structures+economy: {watch.Elapsed.TotalMilliseconds:F1}ms; tilemap types {view.Ground.GetUsedTilesCount()}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void RefreshAndCapture()
        {
            Pixel2DSetup.Run();
            Capture();
        }
        public static void Capture()
        {
            VerificationSuite.Run();
            Run();
            Directory.CreateDirectory("docs/images/pixel2d");
            CaptureGallery();
            foreach(string name in new[]{"SampleScene","Sandbox"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
                Call(controller,"Awake"); Call(controller,"SetupBattle");
                foreach(var hud in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) PixelUISkin.Apply(hud.gameObject);
                CaptureCamera(Camera.main,"docs/images/pixel2d/"+name+".png",1920,1080,true);
                if(name=="Sandbox")
                {
                    File.WriteAllText("Temp/SandboxUnits.csv",UnitCsvSerializer.Write(GameTables.Units));
                    typeof(BattleController).GetMethod("HandleSandboxLoad",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{Path.GetFullPath("Temp/SandboxUnits.csv")});
                    Call(controller,"HandleGenerateTerrain");
                    CaptureCamera(Camera.main,"docs/images/pixel2d/SandboxGenerated.png",1920,1080,true);
                }
                Check(controller.GetComponentsInChildren<UnitView>().Length > 0 || name=="Sandbox","scene startup");
            }
            Debug.Log("[Pixel2DVerification] CAPTURES DONE");
        }
        private static void Call(BattleController controller,string name) => typeof(BattleController).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
        private static void CaptureGallery()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var ids=PixelSpriteCatalog.VisualIds.Where(id=>!id.StartsWith("Icon.") && !id.StartsWith("UI.")).ToArray();
            const int columns=12;
            int height=Mathf.CeilToInt(ids.Length/(float)columns);
            var grid=new GridWorld(columns,height,1.25f);
            var view=new GameObject("Gallery").AddComponent<GridView>(); view.Build(grid);
            for(int i=0;i<ids.Length;i++)
            {
                var p=new Vector2Int(i%columns,height-1-i/columns);
                var root=PixelSpriteCatalog.Build(ids[i],view.transform,3,Blue);
                root.transform.position=PixelCoordinates.GridToWorld(grid,p);
                var label=new GameObject("Label").AddComponent<TextMesh>();
                label.text=ids[i].Replace("Structure.","S.").Replace("Building.","B."); label.fontSize=24;
                label.anchor=TextAnchor.MiddleCenter; label.color=Color.white;
                label.transform.position=root.transform.position+new Vector3(0,-.5f,-.1f); label.transform.localScale=Vector3.one*.05f;
                label.GetComponent<Renderer>().sortingOrder=PixelSpriteCatalog.OverlayOrder;
            }
            var camera=new GameObject("GalleryCamera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3((columns-1)*.625f,(height-1)*.625f,-20),Quaternion.identity);
            camera.orthographic=true; camera.orthographicSize=Mathf.Max(height*.625f+.35f,(columns*.625f+.35f)/(1920f/1080f));
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.17f,.23f);
            CaptureCamera(camera,"docs/images/pixel2d/catalog.png",1920,1080,false);
        }
        private static void CaptureCamera(Camera camera,string path,int width,int height,bool ui)
        {
            var rt=new RenderTexture(width,height,24);
            camera.targetTexture=rt;
            camera.allowHDR=false; camera.allowMSAA=false;
            var pixel=camera.GetComponent<PixelCamera>(); if(pixel!=null) pixel.Apply();
            if(ui)
            {
                foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay) continue;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                }
                Canvas.ForceUpdateCanvases();
            }
            camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=rt;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
