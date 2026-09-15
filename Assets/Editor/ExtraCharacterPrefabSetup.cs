using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 샌드박스 CSV 전용 추가 BaseVisual 프리팹(RogueHooded/Mage/SkeletonWarrior/SkeletonMage)을 만드는
    /// 1회성 배치 도구. Unity CLI(-executeMethod)로만 실행한다 (에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.ExtraCharacterPrefabSetup.Generate
    ///
    /// UnitPrefabSetup(기존 Melee/Ranged/Guard 생성 도구)과 별개 파일로 둔 이유: 그 스크립트는 UnitDefinition이
    /// canGuard/attack/moveRange 등을 직접 필드로 가지고 있던 예전 버전 기준으로 작성돼 지금 다시 실행하면
    /// (그 필드들이 지금은 actions 리스트 안으로 옮겨가 존재하지 않아) 예외로 즉시 중단되고, 무엇보다 Guard
    /// 프리팹은 이후 사용자가 MaxHp를 9로 직접 조정해둔 값이 있어 재실행 시 되돌아가 버린다 — 기존 3개는
    /// 건드리지 않고 새 4개만 추가하기 위해 별도 도구로 분리했다.
    ///
    /// 겉모습은 KayKit CC0 저작물 두 팩에서 가져온다 — Adventurers 팩(Assets/Art/KayKit/Characters, 이미
    /// 프로젝트에서 쓰던 것과 같은 소스)의 Mage/RogueHooded, Skeletons 팩(Assets/Art/KayKitSkeletons/Characters,
    /// 같은 작가 CC0 자매 팩)의 Skeleton_Warrior/Skeleton_Mage. 라이선스 원문은 각각
    /// Assets/Art/KayKit/LICENSE.txt / Assets/Art/KayKitSkeletons/LICENSE.txt.
    /// </summary>
    public static class ExtraCharacterPrefabSetup
    {
        private const string FolderPath = "Assets/Prefabs/Units";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath))
                throw new System.Exception($"{FolderPath} not found — run UnitPrefabSetup.Generate first.");

            // RogueHooded는 기존 Rogue와 같은 리그(손 소켓 아래 크로스보우/나이프 변형 세트가 그대로 들어있다) —
            // 이미 쓰던 Rogue와 동일한 hideNames로 2H_Crossbow만 남기고 정리한다. 텍스처도 기존 rogue_texture.png를
            // 그대로 재사용(모델 파일에 별도 텍스처가 없다 — 몸통 UV가 Rogue와 동일).
            var rogueHooded = CreatePrefab(
                "Unit_RogueHooded",
                modelPath: "Assets/Art/KayKit/Characters/RogueHooded.fbx",
                texturePath: "Assets/Art/KayKit/Characters/rogue_texture.png",
                modelScale: 0.52f, yRotationDegrees: 180f,
                hideNames: new[] { "Knife_Offhand", "1H_Crossbow", "Knife", "Throwable" },
                maxHp: 8, defense: 0,
                actions: new List<IUnitAction> { MoveAction.FromCsv(3), AttackAction.FromCsv(4, 1) });

            // Mage: 왼손 스펠북(닫힌 버전만 남김) + 오른손 지팡이(2H_Staff만 남김, 1H_Wand는 숨김).
            var mage = CreatePrefab(
                "Unit_Mage",
                modelPath: "Assets/Art/KayKit/Characters/Mage.fbx",
                texturePath: "Assets/Art/KayKit/Characters/mage_texture.png",
                modelScale: 0.5f, yRotationDegrees: 180f,
                hideNames: new[] { "1H_Wand", "Spellbook_open" },
                maxHp: 7, defense: 0,
                actions: new List<IUnitAction> { MoveAction.FromCsv(2), AttackAction.FromCsv(2, 2) });

            // Skeleton 계열 두 종은 손 소켓 아래에 무기 변형이 아예 없다(맨손 리그) — hideNames 없이 그대로 쓴다.
            var skeletonWarrior = CreatePrefab(
                "Unit_SkeletonWarrior",
                modelPath: "Assets/Art/KayKitSkeletons/Characters/Skeleton_Warrior.fbx",
                texturePath: "Assets/Art/KayKitSkeletons/Characters/skeleton_texture.png",
                modelScale: 0.5f, yRotationDegrees: 180f,
                hideNames: System.Array.Empty<string>(),
                maxHp: 14, defense: 2,
                actions: new List<IUnitAction> { MoveAction.FromCsv(1), AttackAction.FromCsv(3, 1) });

            var skeletonMage = CreatePrefab(
                "Unit_SkeletonMage",
                modelPath: "Assets/Art/KayKitSkeletons/Characters/Skeleton_Mage.fbx",
                texturePath: "Assets/Art/KayKitSkeletons/Characters/skeleton_texture.png",
                modelScale: 0.5f, yRotationDegrees: 180f,
                hideNames: System.Array.Empty<string>(),
                maxHp: 8, defense: 0,
                actions: new List<IUnitAction> { MoveAction.FromCsv(2), AttackAction.FromCsv(2, 2) });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignToScene(ScenePath, rogueHooded, mage, skeletonWarrior, skeletonMage);

            // Sandbox.unity는 SampleScene을 복제해 만든 별도 파일(SandboxSceneSetup 참고)이라, SampleScene에
            // 새로 연결한 필드가 자동으로 따라오지 않는다 — 이미 존재한다면 여기서도 같은 프리팹을 연결해준다.
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null)
                AssignToScene(SandboxScenePath, rogueHooded, mage, skeletonWarrior, skeletonMage);

            Debug.Log("[ExtraCharacterPrefabSetup] Done: RogueHooded/Mage/SkeletonWarrior/SkeletonMage prefabs created under " + FolderPath + " and wired into " + ScenePath +
                (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null ? " and " + SandboxScenePath : ""));
        }

        private static UnitView CreatePrefab(
            string name, string modelPath, string texturePath, float modelScale, float yRotationDegrees, string[] hideNames,
            int maxHp, int defense, List<IUnitAction> actions)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
                throw new System.Exception($"Model asset not found: {modelPath}");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                throw new System.Exception($"Body texture not found: {texturePath}");

            var go = new GameObject(name);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, go.transform);
            model.name = modelAsset.name;
            model.transform.localPosition = Vector3.zero;
            // KayKit 리그 원본 정면은 -Z(고정 isometric 카메라 반대쪽)를 향해 있어, 기존 Barbarian/Knight/Rogue와
            // 동일하게 180도 돌려 얼굴/무기가 카메라 쪽을 보게 한다(UnitPrefabSetup.cs와 같은 처리).
            model.transform.localRotation = Quaternion.Euler(0f, yRotationDegrees, 0f);
            model.transform.localScale = Vector3.one * modelScale;

            foreach (var hideName in hideNames)
            {
                var found = FindDeep(model.transform, hideName);
                if (found == null)
                    Debug.LogWarning($"[ExtraCharacterPrefabSetup] '{name}': hide target '{hideName}' not found");
                else
                    found.gameObject.SetActive(false);
            }

            go.AddComponent<UnitView>();
            var def = go.AddComponent<UnitDefinition>();

            SetPrivateField(def, "maxHp", maxHp);
            SetPrivateField(def, "defense", defense);
            SetPrivateField(def, "actions", actions);
            SetPrivateField(def, "bodyTexture", texture);
            EditorUtility.SetDirty(def);

            string path = $"{FolderPath}/{name}.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<UnitView>();
        }

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

        private static void AssignToScene(string scenePath, UnitView rogueHooded, UnitView mage, UnitView skeletonWarrior, UnitView skeletonMage)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[ExtraCharacterPrefabSetup] BattleController not found in " + scenePath);
                return;
            }

            SetPrivateField(controller, "rogueHoodedPrefab", rogueHooded);
            SetPrivateField(controller, "magePrefab", mage);
            SetPrivateField(controller, "skeletonWarriorPrefab", skeletonWarrior);
            SetPrivateField(controller, "skeletonMagePrefab", skeletonMage);
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
