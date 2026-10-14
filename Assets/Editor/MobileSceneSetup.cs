using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// Assets/Scenes/Sandbox.unity를 복제해 모바일용 가벼운 한 판 씬(Mobile.unity)을 만든다. 샌드박스 씬은 도시 자원·기술트리·신앙 HUD
    /// 프리팹이 이미 연결돼 있으므로 복제한 뒤 BattleController를 quickPlay(설정 화면 없이 바로 시작)로 바꾸고 맵을 가장 작은 프리셋으로 줄인다.
    /// 이미 있으면 복제는 건너뛰고 값만 다시 적용한다. Unity CLI로만 실행한다(CLAUDE.md 규칙 1):
    /// unity run . -- -logFile Logs/mobile_scene.log -executeMethod TacticsECS.EditorTools.MobileSceneSetup.Generate
    /// </summary>
    public static class MobileSceneSetup
    {
        private const string SourceScenePath = "Assets/Scenes/Sandbox.unity";
        public const string MobileScenePath = "Assets/Scenes/Mobile.unity";
        /// <summary>BattleController.MapSizePresets에서 가장 작은 "Tiny"와 같은 한 변 — InitMapSizeSelection이 이 값에 가장 가까운 프리셋을 고른다.</summary>
        private const int MapSize = 11;

        public static void Generate()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(MobileScenePath) != null)
                Debug.LogWarning($"[MobileSceneSetup] {MobileScenePath} already exists — skipping copy, only re-applying settings.");
            else if (!AssetDatabase.CopyAsset(SourceScenePath, MobileScenePath))
                throw new System.InvalidOperationException($"[MobileSceneSetup] Failed to copy {SourceScenePath} -> {MobileScenePath}");
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(MobileScenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
                throw new System.InvalidOperationException($"[MobileSceneSetup] BattleController not found in {MobileScenePath}");

            SetPrivateField(controller, "sandboxMode", false);
            SetPrivateField(controller, "quickPlay", true);
            SetPrivateField(controller, "gridWidth", MapSize);
            SetPrivateField(controller, "gridHeight", MapSize);
            SetPrivateField(controller, "stressTestExtraUnitsPerTeam", 0);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MobileSceneSetup] PASS: {MobileScenePath} ready (quickPlay, {MapSize}x{MapSize}).");
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
