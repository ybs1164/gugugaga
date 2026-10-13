using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>종족 영역 바이옴을 실제 Sandbox 씬에서 Edit Mode로 돌려 본다 — 팀마다 종족을 골라 지형을 생성하고, 영역 수 = 팀 수,
    /// 종족 평지(GroundTile)·배수가 지형에 반영되는지 확인한 뒤 Logs/TribeBiome.png로 화면을 찍는다. Play Mode는 CLI -executeMethod에서 살아남지 못해 쓰지 않는다.</summary>
    public static class TribeBiomeVerification
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            var controller = UnityEngine.Object.FindFirstObjectByType<BattleController>();
            Invoke(controller, "Awake");
            Invoke(controller, "Start");
            var hud = controller.GetComponentInChildren<SandboxHud>(true);
            Check(hud.transform.Find("Canvas/TribeTab/BiomeDropdown") == null, "no biome picker");
            var biomes = (List<BiomeCsvRow>)Invoke(controller, "ActiveBiomes");

            // 종족 없음: 팀마다 첫 바이옴 그대로.
            var regions = Regions(controller);
            Check(regions.Count == CitySystem.Teams.Length && regions.All(r => r.Id == biomes[0].Id), "no tribe = first biome per team");
            Check(TribeSystem.FieldTileId(regions[0]) == TribeSystem.FieldTileId(biomes[0]), "no tribe keeps biome field");

            int bardur = Tribe("Bardur"), xinxi = Tribe("XinXi");
            Select(controller, Team.Player, xinxi);
            Select(controller, Team.Enemy, bardur);
            regions = Regions(controller);
            var x = GameTables.Tribes[xinxi];
            var b = GameTables.Tribes[bardur];
            Check(regions.Count == 2 && regions[0].Id == x.Biome && regions[1].Id == b.Biome, "regions follow tribe Biome");
            Check(TribeSystem.FieldTileId(regions[0]) == x.GroundTile && TribeSystem.FieldTileId(regions[1]) == b.GroundTile, "field replaced by GroundTile");
            var baseX = TribeSystem.BaseBiome(x, biomes);
            Check(Mathf.Approximately(regions[0].MountainRate, baseX.MountainRate * x.MountainRate), "mountain rate multiplied");
            Check(regions[1].Structures.Where(s => s.StructureId == TribeSystem.CropId).All(s => s.InnerRate == 0f && s.OuterRate == 0f), "Bardur crop rate 0");
            Check(TribeSystem.FieldTileId(baseX) != x.GroundTile, "base biome untouched");

            Invoke(controller, "HandleGenerateTerrain");
            var grid = (GridWorld)typeof(BattleController).GetField("_grid", Any).GetValue(controller);
            int xField = 0, bField = 0, bCrops = 0, featureOnTribeGround = 0, featureNoGround = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int i = 0; i < grid.Width; i++)
            {
                var p = new Vector2Int(i, y);
                string type = grid.GetTileType(p);
                if (type == x.GroundTile) xField++;
                if (type == b.GroundTile) { bField++; if (grid.GetStructure(p) == TribeSystem.CropId) bCrops++; }
                if (type == TerrainGenerationSystem.ForestTileId || type == TerrainGenerationSystem.MountainTileId)
                {
                    string ground = grid.GetTile(p).GroundTileId;
                    if (string.IsNullOrEmpty(ground)) featureNoGround++;
                    else if (ground == x.GroundTile || ground == b.GroundTile) featureOnTribeGround++;
                }
            }
            Check(xField > 0 && bField > 0, $"both tribe fields on map ({xField}/{bField})");
            Check(bCrops == 0, "no crops on Bardur field");
            Check(featureNoGround == 0 && featureOnTribeGround > 0, $"forest/mountain remember their ground ({featureOnTribeGround} on tribe fields, {featureNoGround} without)");
            Check(PixelSpriteCatalog.Has("Ground." + x.GroundTile) && PixelSpriteCatalog.Has("Ground." + b.GroundTile), "tribe field sprites");
            var capitals = (Dictionary<Team, Vector2Int>)typeof(BattleController).GetField("_tribeCapitals", Any).GetValue(controller);
            Check(capitals.Count == 2, "one capital per team");
            string status = hud.transform.Find("Canvas/Toolbar/Status").GetComponent<Text>().text;
            Debug.Log($"[TribeBiomeVerification] fields {xField}/{bField} status='{status}'");
            Capture(controller, "Logs/TribeBiome.png");
            Debug.Log("[TribeBiomeVerification] ALL PASS");
        }

        private static List<BiomeCsvRow> Regions(BattleController c) => (List<BiomeCsvRow>)Invoke(c, "ResolveRegionBiomes");

        private static void Select(BattleController c, Team team, int tribe) =>
            typeof(BattleController).GetMethod("HandleTribeSelected", Any).Invoke(c, new object[] { team, tribe });

        private static int Tribe(string id)
        {
            int i = Array.FindIndex(GameTables.Tribes, t => t.Id == id);
            Check(i >= 0, "tribe " + id);
            return i;
        }

        private static void Capture(BattleController controller, string path)
        {
            var camera = (Camera)typeof(BattleController).GetField("_cam", Any).GetValue(controller);
            Pixel2DVerification.CaptureCamera(camera, path, 1600, 900, true);
            Debug.Log("[TribeBiomeVerification] captured " + path);
        }

        private static object Invoke(BattleController c, string name) => typeof(BattleController).GetMethod(name, Any).Invoke(c, null);

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception("[TribeBiomeVerification] FAIL: " + what);
        }
    }
}
