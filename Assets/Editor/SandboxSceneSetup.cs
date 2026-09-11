using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// Assets/Scenes/SampleScene.unity를 복제해 Sandbox.unity를 만들고, 그 안의 BattleController를
    /// sandboxMode = true로 켜주는 1회성 배치 도구. Unity CLI(-executeMethod)로만 실행한다
    /// (에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.SandboxSceneSetup.Generate -quit
    ///
    /// SampleScene의 BattleController는 이미 Melee/Ranged/Guard 프리팹을 들고 있으므로(UnitPrefabSetup이
    /// 연결해둔 값), 씬을 복제하기만 해도 CSV의 BaseVisual("Melee"/"Ranged"/"Guard")과 맞는 프리팹 참조가
    /// 그대로 따라온다 — 새로 연결할 필요가 없다.
    /// </summary>
    public static class SandboxSceneSetup
    {
        private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        public static void Generate()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null)
            {
                Debug.LogWarning($"[SandboxSceneSetup] {SandboxScenePath} already exists — skipping copy, only re-applying sandboxMode.");
            }
            else if (!AssetDatabase.CopyAsset(SourceScenePath, SandboxScenePath))
            {
                Debug.LogError($"[SandboxSceneSetup] Failed to copy {SourceScenePath} -> {SandboxScenePath}");
                return;
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(SandboxScenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError($"[SandboxSceneSetup] BattleController not found in {SandboxScenePath}");
                return;
            }

            SetPrivateField(controller, "sandboxMode", true);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SandboxSceneSetup] Done: {SandboxScenePath} ready with sandboxMode=true.");
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
