using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>Sandbox 습도 탭 바이옴 드롭다운(여러 개 선택)을 실제 Sandbox 씬에서 Edit Mode로 돌려 본다 — 드롭다운 줄을 골라 선택을 켜고 끈 뒤,
    /// 지형 생성이 고른 바이옴만 쓰는지 확인하고 Logs/BiomeSelection_picked.png로 화면을 찍는다. Play Mode는 CLI -executeMethod에서 살아남지 못해 쓰지 않는다.</summary>
    public static class BiomeSelectionVerification
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            var controller = UnityEngine.Object.FindFirstObjectByType<BattleController>();
            Invoke(controller, "Awake");
            Invoke(controller, "Start");
            var hud = controller.GetComponentInChildren<SandboxHud>(true);
            hud.transform.Find("Canvas/Toolbar/바이옴종족Button").GetComponent<Button>().onClick.Invoke();
            var dropdown = hud.transform.Find("Canvas/BiomeTab/BiomeDropdown").GetComponent<Dropdown>();
            var biomes = (List<BiomeCsvRow>)Invoke(controller, "ActiveBiomes");
            Check(biomes.Count >= 3, "need 3+ biomes");
            Check(dropdown.options.Count == biomes.Count + 2, "dropdown rows = biomes + 2");

            dropdown.value = 2; // 첫 바이옴 켜기
            dropdown.value = 4; // 셋째 바이옴 켜기
            Check(dropdown.value == 0, "dropdown returns to summary row");
            Check(dropdown.options[2].text.StartsWith("[v]") && dropdown.options[4].text.StartsWith("[v]") && dropdown.options[3].text.StartsWith("[ ]"), "toggle marks");
            var regions = (List<BiomeCsvRow>)typeof(BattleController).GetMethod("ResolveRegionBiomes", Any).Invoke(controller, new object[] { false });
            Check(regions.Count == 2 && regions[0].Id == biomes[0].Id && regions[1].Id == biomes[2].Id, "regions = picked biomes");
            Invoke(controller, "HandleGenerateTerrain");
            string status = hud.transform.Find("Canvas/Toolbar/Status").GetComponent<Text>().text;
            Debug.Log($"[BiomeSelectionVerification] caption='{dropdown.options[0].text}' status='{status}'");
            Capture(controller, "Logs/BiomeSelection_picked.png", dropdown);

            dropdown.value = 2; // 다시 누르면 꺼짐
            Check(dropdown.options[2].text.StartsWith("[ ]"), "toggle off");
            regions = (List<BiomeCsvRow>)typeof(BattleController).GetMethod("ResolveRegionBiomes", Any).Invoke(controller, new object[] { false });
            Check(regions.Count == 2 && regions[0].Id == biomes[2].Id && regions[1].Id == biomes[2].Id, "single pick = two regions of it");
            dropdown.value = 1; // 자동으로
            Check(((SortedSet<int>)typeof(BattleController).GetField("_biomeSelection", Any).GetValue(controller)).Count == 0, "clear to auto");
            Check(dropdown.options[0].text == LocalizationSystem.T("UI.SandboxHud.BiomeAuto"), "auto caption");
            Debug.Log("[BiomeSelectionVerification] ALL PASS");
        }

        /// <summary>드롭다운을 펼친 채로 찍는다(페이드 트윈이 돌지 않으므로 목록 알파를 직접 1로).</summary>
        private static void Capture(BattleController controller, string path, Dropdown dropdown)
        {
            // Edit Mode에서는 Dropdown.Awake가 돌지 않아 페이드 트윈 러너가 없다 — 목록은 이미 만들어진 뒤라 그 NRE만 삼킨다.
            try { dropdown.Show(); } catch (NullReferenceException) { }
            foreach (var group in dropdown.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
            var list = dropdown.transform.Find("Dropdown List");
            if (list != null) foreach (var group in list.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
            var camera = (Camera)typeof(BattleController).GetField("_cam", Any).GetValue(controller);
            typeof(Pixel2DVerification).GetMethod("CaptureCamera", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { camera, path, 1600, 900, true });
            Debug.Log("[BiomeSelectionVerification] captured " + path);
        }

        private static object Invoke(BattleController c, string name) => typeof(BattleController).GetMethod(name, Any).Invoke(c, null);

        private static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception("[BiomeSelectionVerification] FAIL: " + what);
        }
    }
}
