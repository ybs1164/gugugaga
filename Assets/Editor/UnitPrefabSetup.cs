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
    ///
    /// 겉모습은 KayKit Adventurers 팩(CC0, Assets/Art/KayKit)의 캐릭터 모델을 자식으로 붙여 만든다.
    /// 각 캐릭터 FBX는 오른손/왼손 소켓(handslot.l/.r) 아래에 여러 무기/방패 변형이 전부 함께 들어있어서,
    /// 유닛 타입에 맞는 것 하나만 켜고 나머지는 꺼서(SetActive) 정리한다.
    /// </summary>
    public static class UnitPrefabSetup
    {
        private const string FolderPath = "Assets/Prefabs/Units";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ModelFolder = "Assets/Art/KayKit/Characters";

        public static void Generate()
        {
            EnsureFolder();

            var meleePrefab = CreatePrefab(
                "Unit_Melee", "Barbarian", modelScale: 0.48f,
                hideNames: new[] { "1H_Axe_Offhand", "Barbarian_Round_Shield", "1H_Axe", "Mug" },
                maxHp: 12, attack: 5, defense: 1, attackRange: 1, canGuard: false,
                moveRange: 3, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                color: new Color(0.2f, 0.5f, 1f));

            var rangedPrefab = CreatePrefab(
                "Unit_Ranged", "Rogue", modelScale: 0.52f,
                hideNames: new[] { "Knife_Offhand", "1H_Crossbow", "Knife", "Throwable" },
                maxHp: 8, attack: 4, defense: 0, attackRange: 3, canGuard: false,
                moveRange: 2, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                color: new Color(0.2f, 0.8f, 0.5f));

            var guardPrefab = CreatePrefab(
                "Unit_Guard", "Knight", modelScale: 0.55f,
                hideNames: new[] { "1H_Sword_Offhand", "Badge_Shield", "Round_Shield", "Spike_Shield", "2H_Sword" },
                maxHp: 18, attack: 3, defense: 3, attackRange: 1, canGuard: true,
                moveRange: 2, ignoreTerrain: false, ignoreUnitBlocking: false, allowDiagonal: false,
                color: new Color(0.1f, 0.3f, 0.7f));

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
            string name, string modelName, float modelScale, string[] hideNames,
            int maxHp, int attack, int defense, int attackRange, bool canGuard,
            int moveRange, bool ignoreTerrain, bool ignoreUnitBlocking, bool allowDiagonal,
            Color color)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{modelName}.fbx");
            if (modelAsset == null)
                throw new System.Exception($"Model asset not found: {ModelFolder}/{modelName}.fbx");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelFolder}/{modelName.ToLowerInvariant()}_texture.png");
            if (texture == null)
                throw new System.Exception($"Body texture not found: {ModelFolder}/{modelName.ToLowerInvariant()}_texture.png");

            var go = new GameObject(name);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, go.transform);
            model.name = modelName;
            model.transform.localPosition = Vector3.zero;
            // KayKit 모델의 기본(FBX 원본) 정면은 -Z를 향해 뒤돌아 있어, 그대로 두면 고정 isometric
            // 카메라(BattleController, yaw 45도)에는 등/망토만 보인다. 180도 돌려 얼굴/무기가 보이게 한다.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one * modelScale;

            foreach (var hideName in hideNames)
            {
                var found = FindDeep(model.transform, hideName);
                if (found == null)
                    Debug.LogWarning($"[UnitPrefabSetup] '{modelName}': hide target '{hideName}' not found");
                else
                    found.gameObject.SetActive(false);
            }

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
            SetPrivateField(def, "color", color);
            SetPrivateField(def, "bodyTexture", texture);
            EditorUtility.SetDirty(def);

            string path = $"{FolderPath}/{name}.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<UnitView>();
        }

        /// <summary>이름으로 자손 Transform을 재귀 탐색한다 (본 경로가 바뀌어도 안전하게 찾기 위함).</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
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
