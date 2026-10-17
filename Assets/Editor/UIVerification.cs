using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 전투 HUD(공용 폰트, 알림·상단 막대, 정보 패널·대화 상자, 기술트리), 유닛 이름표(UnitView.Label), 피해 팝업(DamagePopup) 프리팹 배선,
    /// 샌드박스 구조물 패널을 자동으로 검증하는 배치모드 전용 스크립트. UnitCsvVerification과 같은 패턴 — Play 모드 없이 에디터
    /// 인스턴스화만으로 확인하고 Console 로그의 PASS/FAIL만 보면 된다.
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.UIVerification.Run
    /// </summary>
    public static class UIVerification
    {
        public static void Run()
        {
            GameDataLoader.LoadAll();
            bool ok = VerifyFont() &
                      VerifyBattleHudToastsAndBar() &
                      VerifyUnitLabelAndDamagePopupWiring() &
                      VerifyInspectorAndDialog() &
                      VerifyTechTreePanel() &
                      VerifySandboxStructurePanel();
            Debug.Log(ok ? "[UIVerification] ALL PASS" : "[UIVerification] SOME CHECKS FAILED - see errors above");
        }

        private static BattleHud NewHud()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BattleHud>("Assets/Prefabs/UI/BattleHud.prefab"));
            instance.Init();
            return instance;
        }

        /// <summary>BattleHud가 코드로 만드는 글자와 HpDisplay(TextMesh) 둘 다 공용 폰트(Jua)를 쓰는지.</summary>
        private static bool VerifyFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Jua-Regular.ttf");
            if (font == null) { Debug.LogError("[UIVerification] Jua font asset missing at Assets/Fonts/Jua-Regular.ttf"); return false; }

            var hud = NewHud();
            try
            {
                var badgeText = hud.transform.Find("Canvas/Safe/TopBar/TurnBadge/Number")?.GetComponent<Text>();
                if (badgeText == null || badgeText.font != font) { Debug.LogError("[UIVerification] TurnBadge/Number is not using the shared Jua font"); return false; }
            }
            finally { Object.DestroyImmediate(hud.gameObject); }

            var hpDisplay = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HpDisplay.prefab");
            var numberMesh = hpDisplay.transform.Find("Number")?.GetComponent<TextMesh>();
            if (numberMesh == null || numberMesh.font != font) { Debug.LogError("[UIVerification] HpDisplay/Number TextMesh is not using the shared Jua font"); return false; }

            Debug.Log("[UIVerification] font PASS");
            return true;
        }

        /// <summary>알림(AddLogEntry)은 최근 줄만 남기고, 상단 막대가 턴·별·유닛 수·신앙 값을 그대로 보이며, 경제 없는 전투에서는 경제 칩을 숨기는지.</summary>
        private static bool VerifyBattleHudToastsAndBar()
        {
            var hud = NewHud();
            bool ok = true;
            try
            {
                for (int i = 0; i < 6; i++) hud.AddLogEntry("테스트 로그 줄 " + i);
                var toasts = hud.transform.Find("Canvas/Safe/Toasts");
                if (toasts == null || toasts.childCount != 4) { Debug.LogError($"[UIVerification] toast count mismatch: expected 4, got {(toasts == null ? -1 : toasts.childCount)}"); ok = false; }

                hud.SetTurn(Team.Player, 7);
                var city = CityResourceData.Create(populationCap: 10, starsProduction: 1, isCapital: true);
                city.Stars = 5;
                hud.SetResources(city, unitsUsed: 3);
                hud.SetFaith(4, 12, 2, null);
                string Read(string path)
                {
                    var t = hud.transform.Find("Canvas/Safe/TopBar/" + path);
                    return t != null ? t.GetComponent<Text>().text : "<missing>";
                }
                if (!Read("TurnBadge/Number").Contains("7")) { Debug.LogError("[UIVerification] turn badge: " + Read("TurnBadge/Number")); ok = false; }
                if (!Read("Stars/Text").Contains("5") || !Read("Stars/Text").Contains("+1")) { Debug.LogError("[UIVerification] stars chip: " + Read("Stars/Text")); ok = false; }
                if (!Read("Units/Text").Contains("3/10")) { Debug.LogError("[UIVerification] units chip: " + Read("Units/Text")); ok = false; }
                if (!Read("FaithButton/Label").Contains("4/12")) { Debug.LogError("[UIVerification] faith chip: " + Read("FaithButton/Label")); ok = false; }

                hud.SetEconomyVisible(false);
                if (hud.transform.Find("Canvas/Safe/TopBar/Stars").gameObject.activeSelf) { Debug.LogError("[UIVerification] economy chips should hide without economy"); ok = false; }
            }
            finally { Object.DestroyImmediate(hud.gameObject); }

            if (ok) Debug.Log("[UIVerification] toasts/top bar PASS");
            return ok;
        }

        /// <summary>UnitSpawner가 넘긴 label이 UnitView.Label에 그대로 반영되는지(행동 기록 문구에 쓰임),
        /// 그리고 damagePopupPrefab이 실제 유닛 프리팹에 배선되어 DamagePopup 컴포넌트를 갖고 있는지
        /// 확인한다.</summary>
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

        /// <summary>정보 패널(ShowTile/HideInspector): 구조물 이름·설명과 행동 줄을 보이고, 줄을 누르면 OnClick이 불리며, 비활성 줄은 눌리지 않는다.
        /// 대화 상자(ShowDialog/HideDialog)도 열고 닫힌다.</summary>
        private static bool VerifyInspectorAndDialog()
        {
            var hud = NewHud();
            bool ok = true;
            try
            {
                if (hud.IsInspectorOpen) { Debug.LogError("[UIVerification] inspector should start hidden"); ok = false; }
                int clicked = 0;
                hud.ShowTile("칸", "본문", "Village", new[]
                {
                    new ActionMenuOption { Label = "해 보기", Enabled = true, OnClick = () => clicked++ },
                    new ActionMenuOption { Label = "막힘", Detail = "이유", Enabled = false, OnClick = () => clicked += 100 },
                });
                var content = hud.transform.Find("Canvas/Safe/InspectorArea/Inspector/Scroll/Viewport/Content");
                var texts = string.Join("|", content.GetComponentsInChildren<Text>(true).Select(t => t.text));
                if (!hud.IsInspectorOpen || !texts.Contains("마을") || !texts.Contains("본문")) { Debug.LogError("[UIVerification] inspector text: " + texts); ok = false; }
                var buttons = content.GetComponentsInChildren<Button>(true);
                if (buttons.Length != 2 || buttons[1].interactable) { Debug.LogError($"[UIVerification] inspector option buttons: {buttons.Length}"); ok = false; }
                else buttons[0].onClick.Invoke();
                if (clicked != 1) { Debug.LogError("[UIVerification] inspector option click: " + clicked); ok = false; }
                hud.HideInspector();
                if (hud.IsInspectorOpen) { Debug.LogError("[UIVerification] HideInspector did not hide the panel"); ok = false; }

                hud.ShowDialog("제목", "본문", new[] { new ActionMenuOption { Label = "확인", Enabled = true, Primary = true, OnClick = hud.HideDialog } });
                if (!hud.IsDialogOpen) { Debug.LogError("[UIVerification] dialog did not open"); ok = false; }
                var dialogButton = hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Button");
                if (dialogButton == null) { Debug.LogError("[UIVerification] dialog button missing"); ok = false; }
                else dialogButton.onClick.Invoke();
                if (hud.IsDialogOpen) { Debug.LogError("[UIVerification] dialog button did not close it"); ok = false; }
            }
            finally { Object.DestroyImmediate(hud.gameObject); }

            if (ok) Debug.Log("[UIVerification] inspector/dialog PASS");
            return ok;
        }

        /// <summary>기술트리(TechTreeHud): 노드를 고르면 상세 정보가 바뀌고, 선행 기술/별 부족 판정에 따라 해금 버튼이 켜지고 꺼지며,
        /// 해금 버튼을 누르면 OnUnlockRequested가 발생한다.</summary>
        private static bool VerifyTechTreePanel()
        {
            var go = new GameObject("UIVerification_TechTree");
            bool ok = true;
            try
            {
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TechTreeHud>("Assets/Prefabs/UI/TechTreeHud.prefab"), go.transform);
                instance.name = "TechTreeHud";
                instance.Init(GameDataLoader.LoadTechNodes());
                instance.Open();
                if (!instance.IsOpen) { Debug.LogError("[UIVerification] TechTree: Open did not show the panel"); ok = false; }

                var tech = TechTreeData.CreateEmpty();
                tech.Unlocked.Add("Climbing");
                var city = CityResourceData.Create(populationCap: 10, starsProduction: 1, isCapital: true);
                city.Stars = 10;
                instance.SetState(tech, city, cityCount: 1);

                var tree = instance.transform.Find("Canvas/Safe/Tree/Content");
                var detail = instance.transform.Find("Canvas/Safe/Detail");
                var nameText = detail.Find("Name").GetComponent<Text>();
                var statusText = detail.Find("Status").GetComponent<Text>();
                var unlockButton = detail.Find("UnlockButton").GetComponent<Button>();

                // 선행 기술(등산)이 해금되어 있고 별(10)이 비용보다 많은 2티어 노드 -> 해금 가능.
                tree.Find("Meditation").GetComponent<Button>().onClick.Invoke();
                if (!nameText.text.Contains("명상") || !unlockButton.interactable)
                {
                    Debug.LogError($"[UIVerification] TechTree: expected Meditation to be unlockable, name='{nameText.text}', interactable={unlockButton.interactable}, status='{statusText.text}'");
                    ok = false;
                }
                // 선행 기술(명상)이 아직 없는 3티어 노드 -> 해금 불가.
                tree.Find("Philosophy").GetComponent<Button>().onClick.Invoke();
                if (unlockButton.interactable || !statusText.text.Contains("선행 기술"))
                {
                    Debug.LogError($"[UIVerification] TechTree: expected Philosophy to require a prerequisite, interactable={unlockButton.interactable}, status='{statusText.text}'");
                    ok = false;
                }
                string requested = null;
                instance.OnUnlockRequested += id => requested = id;
                tree.Find("Meditation").GetComponent<Button>().onClick.Invoke();
                unlockButton.onClick.Invoke();
                if (requested != "Meditation") { Debug.LogError($"[UIVerification] TechTree: expected OnUnlockRequested(Meditation), got {requested}"); ok = false; }
                instance.Close();
                if (instance.IsOpen) { Debug.LogError("[UIVerification] TechTree: Close did not hide the panel"); ok = false; }
            }
            finally { Object.DestroyImmediate(go); }

            if (ok) Debug.Log("[UIVerification] tech tree panel PASS");
            return ok;
        }

        /// <summary>SandboxHud(전투 시작 전 배치 단계)의 구조물 패널이 StructureDefinition 표를 보여주는지 확인한다 —
        /// BattleController.UpdateStructureHover가 매 프레임 호출하는 대상이 이 메서드다.</summary>
        private static bool VerifySandboxStructurePanel()
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<SandboxHud>("Assets/Prefabs/UI/SandboxHud.prefab");
            var instance = Object.Instantiate(hudPrefab);
            bool ok = true;

            try
            {
                instance.Init();

                var panel = instance.transform.Find("Canvas/StructurePanel");
                if (panel == null) { Debug.LogError("[UIVerification] SandboxHud StructurePanel not found under Canvas"); return false; }
                if (panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIVerification] SandboxHud StructurePanel should start hidden");
                    ok = false;
                }

                instance.ShowStructurePanel("Resource_Ore");
                var nameText = panel.Find("NameText").GetComponent<Text>();
                if (!panel.gameObject.activeSelf || nameText.text != "광물 자원")
                {
                    Debug.LogError($"[UIVerification] SandboxHud StructurePanel(Resource_Ore) mismatch: active={panel.gameObject.activeSelf}, name='{nameText.text}'");
                    ok = false;
                }

                instance.HideStructurePanel();
                if (panel.gameObject.activeSelf)
                {
                    Debug.LogError("[UIVerification] SandboxHud HideStructurePanel did not deactivate the panel");
                    ok = false;
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }

            if (ok) Debug.Log("[UIVerification] sandbox structure panel PASS");
            return ok;
        }
    }
}
