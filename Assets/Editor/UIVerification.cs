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
                      VerifyUnitLabelAndDamagePopupWiring() &
                      VerifyCityResourceBar() &
                      VerifyTechTreePanel() &
                      VerifyStructurePanel();
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

        /// <summary>CityResourceBar.prefab(도시 발전도/인구/골드/신앙 표시)이 SetResources로 넘긴 값을
        /// 그대로 4칸에 반영하는지 확인한다.</summary>
        private static bool VerifyCityResourceBar()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<CityResourceHud>("Assets/Prefabs/UI/CityResourceBar.prefab");
            var instance = Object.Instantiate(hudPrefab);
            bool ok = true;

            try
            {
                instance.Init();

                var city = CityResourceData.Create(populationCap: 10, goldProduction: 1, developmentProduction: 1, maxFaith: 10, isCapital: true);
                city.Development = 2;
                city.Gold = 5;
                city.Faith = 4;
                instance.SetResources(city, populationUsed: 3);

                var bar = instance.transform.Find("Canvas/Bar");
                void CheckSlot(string slotName, string expected)
                {
                    var text = bar.Find(slotName + "/Number")?.GetComponent<Text>();
                    if (text == null || text.text != expected)
                    {
                        Debug.LogError($"[UIVerification] CityResourceBar {slotName} mismatch: expected '{expected}', got '{(text == null ? "<missing>" : text.text)}'");
                        ok = false;
                    }
                }

                CheckSlot("Development", "2");
                CheckSlot("Population", "3/10");
                CheckSlot("Gold", "5");
                CheckSlot("Faith", "4/10");
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }

            if (ok) Debug.Log("[UIVerification] city resource bar PASS");
            return ok;
        }

        /// <summary>TechTreePanel.prefab(기술트리)이 노드 선택 시 상세 정보를 제대로 보여주고, 선행 기술/
        /// 발전도 부족 판정에 따라 해금 버튼 활성화를 올바르게 바꾸며, 실제로 해금 버튼을 누르면
        /// OnUnlockRequested가 발생하는지 확인한다.</summary>
        private static bool VerifyTechTreePanel()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<TechTreeHud>("Assets/Prefabs/UI/TechTreePanel.prefab");
            var instance = Object.Instantiate(hudPrefab);
            bool ok = true;

            try
            {
                instance.Init();

                var tech = TechTreeData.CreateEmpty();
                tech.Unlocked.Add(TechId.Mountaineering);
                var city = CityResourceData.Create(populationCap: 10, goldProduction: 1, developmentProduction: 1, maxFaith: 10, isCapital: true);
                city.Development = 10;
                instance.SetState(tech, city);

                var tree = instance.transform.Find("Canvas/Panel/Tree");
                var detail = instance.transform.Find("Canvas/Panel/Detail");
                var nameText = detail.Find("Name").GetComponent<Text>();
                var statusText = detail.Find("Status").GetComponent<Text>();
                var unlockButton = detail.Find("UnlockButton").GetComponent<Button>();

                // 선행 기술(등산)이 해금되어 있고 발전도(10)가 비용(5)보다 많은 2티어 노드 -> 해금 가능해야 한다.
                tree.Find(TechId.Meditation.ToString()).GetComponent<Button>().onClick.Invoke();
                if (!nameText.text.Contains("명상") || !unlockButton.interactable)
                {
                    Debug.LogError($"[UIVerification] TechTreePanel: expected Meditation to be unlockable, name='{nameText.text}', interactable={unlockButton.interactable}, status='{statusText.text}'");
                    ok = false;
                }

                // 선행 기술(명상)이 아직 해금되지 않은 3티어 노드 -> 해금 불가여야 한다.
                tree.Find(TechId.Philosophy.ToString()).GetComponent<Button>().onClick.Invoke();
                if (unlockButton.interactable || !statusText.text.Contains("선행 기술"))
                {
                    Debug.LogError($"[UIVerification] TechTreePanel: expected Philosophy to require a prerequisite, interactable={unlockButton.interactable}, status='{statusText.text}'");
                    ok = false;
                }

                // 해금 버튼 클릭 -> OnUnlockRequested(Meditation) 발생 확인.
                TechId requested = TechId.None;
                instance.OnUnlockRequested += id => requested = id;
                tree.Find(TechId.Meditation.ToString()).GetComponent<Button>().onClick.Invoke();
                unlockButton.onClick.Invoke();
                if (requested != TechId.Meditation)
                {
                    Debug.LogError($"[UIVerification] TechTreePanel: expected OnUnlockRequested(Meditation), got {requested}");
                    ok = false;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }

            if (ok) Debug.Log("[UIVerification] tech tree panel PASS");
            return ok;
        }

        /// <summary>BattleHud.ShowStructurePanel/HideStructurePanel(건물 선택 시 정보 패널)이 StructureDefinition
        /// 테이블의 이름/설명을 그대로 반영하고, 정의되지 않은 StructureId는 패널을 띄우지 않으며, Hide 호출로
        /// 실제로 비활성화되는지 확인한다.</summary>
        private static bool VerifyStructurePanel()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<BattleHud>("Assets/Prefabs/UI/BattleHud.prefab");
            var instance = Object.Instantiate(hudPrefab);
            bool ok = true;

            try
            {
                instance.Init();

                var panel = instance.transform.Find("Canvas/StructurePanel");
                if (panel == null) { Debug.LogError("[UIVerification] StructurePanel not found under BattleHud/Canvas"); return false; }
                if (panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIVerification] StructurePanel should start hidden");
                    ok = false;
                }

                instance.ShowStructurePanel("Village");
                var nameText = panel.Find("NameText").GetComponent<Text>();
                var descriptionText = panel.Find("DescriptionText").GetComponent<Text>();
                if (!panel.gameObject.activeSelf || nameText.text != "마을" || string.IsNullOrEmpty(descriptionText.text))
                {
                    Debug.LogError($"[UIVerification] StructurePanel(Village) mismatch: active={panel.gameObject.activeSelf}, name='{nameText.text}', desc='{descriptionText.text}'");
                    ok = false;
                }

                instance.ShowStructurePanel("NoSuchStructure");
                if (panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIVerification] StructurePanel should hide for an unknown StructureId");
                    ok = false;
                }

                instance.ShowStructurePanel("Capital");
                instance.HideStructurePanel();
                if (panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIVerification] HideStructurePanel did not deactivate the panel");
                    ok = false;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }

            if (ok) Debug.Log("[UIVerification] structure panel PASS");
            return ok;
        }
    }
}
