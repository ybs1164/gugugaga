using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// Unit 프리팹(Melee/Ranged/Guard)을 생성하고 SampleScene의 BattleController에 연결하는
    /// 1회성 배치 도구. Unity CLI(-executeMethod)로만 실행한다 (에디터 GUI 직접 조작 금지).
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.UnitPrefabSetup.Generate
    /// </summary>
    public static class UnitPrefabSetup
    {
        private const string FolderPath = "Assets/Prefabs/Units";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Generate()
        {
            EnsureFolder();

            var meleePrefab = CreatePrefab(
                "Unit_Melee", new Vector3(0.5f, 0.5f, 0.5f),
                maxHp: 12, attack: 5, defense: 1, attackRange: 1, canGuard: false,
                moveRange: 3, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                playerColor: new Color(0.2f, 0.5f, 1f), enemyColor: new Color(1f, 0.4f, 0.3f));

            var rangedPrefab = CreatePrefab(
                "Unit_Ranged", new Vector3(0.5f, 0.5f, 0.5f),
                maxHp: 8, attack: 4, defense: 0, attackRange: 3, canGuard: false,
                moveRange: 2, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                playerColor: new Color(0.2f, 0.8f, 0.5f), enemyColor: new Color(1f, 0.7f, 0.2f));

            var guardPrefab = CreatePrefab(
                "Unit_Guard", new Vector3(0.7f, 0.6f, 0.7f),
                maxHp: 18, attack: 3, defense: 3, attackRange: 1, canGuard: true,
                moveRange: 2, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                playerColor: new Color(0.1f, 0.3f, 0.7f), enemyColor: new Color(0.6f, 0.1f, 0.1f));

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

        private static UnitView CreatePrefab(
            string name, Vector3 scale,
            int maxHp, int attack, int defense, int attackRange, bool canGuard,
            int moveRange, bool ignoreTerrain, bool ignoreUnitBlocking, bool allowDiagonal,
            Color playerColor, Color enemyColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.localScale = scale;

            go.AddComponent<UnitView>();
            var def = go.AddComponent<UnitDefinition>();

            SetPrivateField(def, "maxHp", maxHp);
            SetPrivateField(def, "attack", attack);
            SetPrivateField(def, "defense", defense);
            SetPrivateField(def, "attackRange", attackRange);
            SetPrivateField(def, "canGuard", canGuard);
            SetPrivateField(def, "moveRange", moveRange);
            SetPrivateField(def, "ignoreTerrain", ignoreTerrain);
            SetPrivateField(def, "ignoreUnitBlocking", ignoreUnitBlocking);
            SetPrivateField(def, "allowDiagonal", allowDiagonal);
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
