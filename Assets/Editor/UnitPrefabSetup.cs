using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// Unit 프리팹(Melee/Ranged/Guard)을 생성하고 SampleScene의 BattleController에 연결하는
    /// 1회성 배치 도구. Unity CLI(-executeMethod)로만 실행한다 (에디터 GUI 직접 조작 금지).
    /// 사용법: unity run . -- -batchmode -quit -nographics
    ///         -executeMethod TacticsECS.EditorTools.UnitPrefabSetup.Generate
    /// </summary>
    public static class UnitPrefabSetup
    {
        private const string FolderPath = "Assets/Prefabs/Units";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Generate()
        {
            EnsureFolder();

            var meleePrefab = CreatePrefab(
                "Unit_Melee",
                new Vector3(0.5f, 0.5f, 0.5f),
                new UnitStats { MaxHp = 12, Attack = 5, Defense = 1, MoveRange = 3, AttackRange = 1, CanGuard = false },
                new Color(0.2f, 0.5f, 1f), new Color(1f, 0.4f, 0.3f));

            var rangedPrefab = CreatePrefab(
                "Unit_Ranged",
                new Vector3(0.5f, 0.5f, 0.5f),
                new UnitStats { MaxHp = 8, Attack = 4, Defense = 0, MoveRange = 2, AttackRange = 3, CanGuard = false },
                new Color(0.2f, 0.8f, 0.5f), new Color(1f, 0.7f, 0.2f));

            var guardPrefab = CreatePrefab(
                "Unit_Guard",
                new Vector3(0.7f, 0.6f, 0.7f),
                new UnitStats { MaxHp = 18, Attack = 3, Defense = 3, MoveRange = 2, AttackRange = 1, CanGuard = true },
                new Color(0.1f, 0.3f, 0.7f), new Color(0.6f, 0.1f, 0.1f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignToScene(meleePrefab, rangedPrefab, guardPrefab);

            Debug.Log("[UnitPrefabSetup] Done: prefabs created under " + FolderPath + " and wired into " + ScenePath);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Units");
        }

        private static UnitView CreatePrefab(string name, Vector3 scale, UnitStats stats, Color playerColor, Color enemyColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.localScale = scale;

            go.AddComponent<UnitView>();
            var def = go.AddComponent<UnitDefinition>();

            SetPrivateField(def, "stats", stats);
            SetPrivateField(def, "playerColor", playerColor);
            SetPrivateField(def, "enemyColor", enemyColor);
            EditorUtility.SetDirty(def);

            string path = $"{FolderPath}/{name}.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<UnitView>();
        }

        private static void AssignToScene(UnitView melee, UnitView ranged, UnitView guard)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[UnitPrefabSetup] BattleController not found in " + ScenePath);
                return;
            }

            SetPrivateField(controller, "meleePrefab", melee);
            SetPrivateField(controller, "rangedPrefab", ranged);
            SetPrivateField(controller, "guardPrefab", guard);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
