using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// UIPrefabSetup으로 새로 생긴 UI(공용 폰트, 좌상단 유닛 로스터, 우상단 행동 로그, 유닛 이름표
    /// (UnitView.Label), 피해 팝업(DamagePopup) 프리팹 배선)를 자동으로 검증하는 배치모드 전용 스크립트.
    /// UnitCsvVerification과 같은 패턴 — Play 모드 없이 에디터 인스턴스화만으로 확인하고 Console 로그의
    /// PASS/FAIL만 보면 된다.
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.UIVerification.Run
    /// </summary>
    public static class UIVerification
    {
        public static void Run()
        {
            bool ok = VerifyFont() &
                      VerifyBattleHudRosterAndLog() &
                      VerifyUnitLabelAndDamagePopupWiring();
            Debug.Log(ok ? "[UIVerification] ALL PASS" : "[UIVerification] SOME CHECKS FAILED - see errors above");
        }

        /// <summary>TurnBadge(UI.Text)와 HpDisplay(TextMesh) 둘 다 공용 폰트(Jua)를 실제로 쓰고 있는지 —
        /// UIPrefabSetup.LoadUiFont/ApplyUiFont가 모든 Text 생성 경로에 빠짐없이 적용됐는지 확인한다.</summary>
        private static bool VerifyFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Jua-Regular.ttf");
            if (font == null) { Debug.LogError("[UIVerification] Jua font asset missing at Assets/Fonts/Jua-Regular.ttf"); return false; }

            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleHud.prefab");
            var badgeText = hud.transform.Find("Canvas/TurnBadge/Number")?.GetComponent<Text>();
            if (badgeText == null || badgeText.font != font) { Debug.LogError("[UIVerification] TurnBadge/Number is not using the shared Jua font"); return false; }

            var hpDisplay = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HpDisplay.prefab");
            var numberMesh = hpDisplay.transform.Find("Number")?.GetComponent<TextMesh>();
            if (numberMesh == null || numberMesh.font != font) { Debug.LogError("[UIVerification] HpDisplay/Number TextMesh is not using the shared Jua font"); return false; }

            Debug.Log("[UIVerification] font PASS");
            return true;
        }

        /// <summary>좌상단 유닛 로스터(BattleHud.SetRoster)와 우상단 행동 로그(BattleHud.AddLogEntry)가
        /// 실제로 행을 만들어내는지 확인한다.</summary>
        private static bool VerifyBattleHudRosterAndLog()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<BattleHud>("Assets/Prefabs/UI/BattleHud.prefab");
            var instance = Object.Instantiate(hudPrefab);
            bool ok = true;

            try
            {
                instance.Init();

                var world = new EntityWorld();
                int id = world.CreateEntity();
                world.Set(id, Team.Player);
                world.Set(id, new Hp { Value = 7 });

                instance.SetRoster(world);
                var rosterContent = instance.transform.Find("Canvas/UnitRoster/Viewport/Content");
                if (rosterContent == null || rosterContent.childCount != 1)
                {
                    Debug.LogError($"[UIVerification] roster row count mismatch: expected 1, got {(rosterContent == null ? -1 : rosterContent.childCount)}");
                    ok = false;
                }

                instance.AddLogEntry("테스트 로그 줄");
                instance.AddLogEntry("테스트 로그 줄 2");
                var logContent = instance.transform.Find("Canvas/ActionLog/Content");
                if (logContent == null || logContent.childCount != 2)
                {
                    Debug.LogError($"[UIVerification] log line count mismatch: expected 2, got {(logContent == null ? -1 : logContent.childCount)}");
                    ok = false;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }

            if (ok) Debug.Log("[UIVerification] roster/log PASS");
            return ok;
        }

        /// <summary>UnitSpawner가 넘긴 label이 UnitView.Label에 그대로 반영되는지(행동 로그 문구에 쓰임),
        /// 그리고 damagePopupPrefab이 실제 유닛 프리팹에 배선되어 DamagePopup 컴포넌트를 갖고 있는지
        /// (UIPrefabSetup.PatchUnitPrefabs) 확인한다.</summary>
        private static bool VerifyUnitLabelAndDamagePopupWiring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Melee.prefab");
            var grid = new GridWorld(4, 4, 1f);
            var world = new EntityWorld();
            var spawnerGo = new GameObject("UIVerification_Spawner");
            bool ok = true;

            try
            {
                var spawner = spawnerGo.AddComponent<UnitSpawner>();
                var view = spawner.Spawn(grid, world, Team.Player, prefab, Vector2Int.zero, "테스트유닛");

                if (view.Label != "테스트유닛")
                {
                    Debug.LogError($"[UIVerification] UnitView.Label mismatch: got '{view.Label}'");
                    ok = false;
                }

                var field = typeof(UnitView).GetField("damagePopupPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                var wired = field.GetValue(view) as Transform;
                if (wired == null)
                {
                    Debug.LogError("[UIVerification] Unit_Melee.damagePopupPrefab is not wired");
                    ok = false;
                }
                else if (wired.GetComponent<DamagePopup>() == null)
                {
                    Debug.LogError("[UIVerification] DamagePopup prefab is missing the DamagePopup component");
                    ok = false;
                }
            }
            finally
            {
                Object.DestroyImmediate(spawnerGo);
            }

            if (ok) Debug.Log("[UIVerification] unit label / damage popup wiring PASS");
            return ok;
        }
    }
}
