using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// Kenney "Tower Defense Kit"(CC0, Assets/Art/Tiles/Kenney/tile.fbx)의 타일 메시로 Tile_Land/
    /// Tile_Water 프리팹을 만들어주는 1회성 배치 도구. UIPrefabSetup과 같은 이유로 Unity CLI
    /// (-executeMethod)로만 실행한다(에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.TileAssetSetup.GenerateAll -quit
    ///
    /// 여기서 만드는 프리팹은 메시(모양)만 담고 있다 — 실제로 화면에 보이는 색(육지=초록/물=파랑, 이동/공격
    /// 하이라이트)은 GridView.Build가 타일마다 매번 새 RuntimeMaterial을 만들어 갈아끼운다(TileView 참고).
    /// 그래서 여기서 지정하는 색은 프리팹을 에디터에서 미리보기할 때만 의미가 있다.
    /// </summary>
    public static class TileAssetSetup
    {
        private const string TileMeshPath = "Assets/Art/Tiles/Kenney/tile.fbx";
        private const string PrefabFolder = "Assets/Prefabs/Tiles";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        private static readonly Color LandPreviewColor = new Color(0.42f, 0.68f, 0.35f);
        private static readonly Color WaterPreviewColor = new Color(0.25f, 0.45f, 0.85f);

        public static void GenerateAll()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Tiles");

            var landPrefab = GenerateTilePrefab("Tile_Land", LandPreviewColor);
            var waterPrefab = GenerateTilePrefab("Tile_Water", WaterPreviewColor);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (landPrefab != null && waterPrefab != null)
                AssignToScenes(landPrefab, waterPrefab);

            Debug.Log("[TileAssetSetup] GenerateAll done");
        }

        private static GameObject GenerateTilePrefab(string prefabName, Color previewColor)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(TileMeshPath);
            if (mesh == null)
            {
                Debug.LogError($"[TileAssetSetup] tile mesh not found: {TileMeshPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(mesh);
            instance.name = prefabName;

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.sharedMaterial = RuntimeMaterial.CreateColored(previewColor);

            string path = $"{PrefabFolder}/{prefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        /// <summary>SampleScene/Sandbox의 BattleController에 landTilePrefab/waterTilePrefab을 채워 넣는다.
        /// UIPrefabSetup.AssignToScene과 같은 패턴(SetPrivateField + 씬 저장). Sandbox에는 추가로, 새로
        /// 만든 물 유닛(뗏목/정찰선/충각선/범선)을 실제로 테스트해볼 수 있도록 그리드 한쪽 절반을 물로
        /// 채운 데모 waterTiles도 함께 설정한다(SampleScene의 기존 데모 전투는 건드리지 않는다).</summary>
        private static void AssignToScenes(GameObject landPrefab, GameObject waterPrefab)
        {
            AssignToScene(ScenePath, landPrefab, waterPrefab, addDemoWater: false);
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null)
                AssignToScene(SandboxScenePath, landPrefab, waterPrefab, addDemoWater: false);
        }

        private static void AssignToScene(string scenePath, GameObject landPrefab, GameObject waterPrefab, bool addDemoWater)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[TileAssetSetup] BattleController not found in " + scenePath);
                return;
            }

            SetPrivateField(controller, "landTilePrefab", landPrefab);
            SetPrivateField(controller, "waterTilePrefab", waterPrefab);

            if (addDemoWater)
            {
                int width = (int)GetPrivateField(controller, "gridWidth");
                int height = (int)GetPrivateField(controller, "gridHeight");
                var waterTiles = new List<Vector2Int>();
                // 그리드 오른쪽 1/3을 물로 채워, 물 유닛(뗏목/정찰선/충각선/범선)이 실제로 들어갈 수 있는
                // 지형과 육지 유닛이 못 건너가는 경계를 바로 확인할 수 있게 한다.
                int waterStartX = Mathf.Max(0, width - Mathf.Max(1, width / 3));
                for (int x = waterStartX; x < width; x++)
                    for (int y = 0; y < height; y++)
                        waterTiles.Add(new Vector2Int(x, y));
                SetPrivateField(controller, "waterTiles", waterTiles);
            }

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}
