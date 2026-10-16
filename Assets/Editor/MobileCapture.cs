using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 모바일 씬(Mobile.unity)을 휴대폰 해상도로 찍는다 — 빠른 시작 첫 화면, 유닛 선택, 수도 칸 메뉴, 기술 트리, 신앙 펀치 목록, 결과 화면.
    /// 가로 20:9(2400x1080)와 세로(1080x2400) 첫 화면. 에디트 모드로 동기 실행(Play Mode 없음, FaithGameCapture와 같은 방식).
    /// 사용법(화면 필요, -nographics 금지): unity run . -- -logFile Logs/mobile_capture.log -executeMethod TacticsECS.EditorTools.MobileCapture.Run
    /// 결과: Logs/mobile_capture/*.png (문서용이 아니라 점검용)
    /// </summary>
    public static class MobileCapture
    {
        private const string OutDir = "Logs/mobile_capture";
        private const int W = 2400, H = 1080;
        private static BattleController _c;

        private static T Field<T>(string name) => (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);
        private static object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            GameDataLoader.LoadAll();
            EditorSceneManager.OpenScene(MobileSceneSetup.MobileScenePath);
            _c = Object.FindFirstObjectByType<BattleController>();
            Call("Awake"); Call("SetupBattle");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) PixelUISkin.Apply(canvas.gameObject);
            Call("RefreshEconomyViews", true);

            Shot("01_start_landscape", W, H);
            Shot("01_start_portrait", H, W);

            var world = Field<EntityWorld>("_world");
            foreach (DictionaryEntry e in (IDictionary)Field<object>("_viewsById"))
            {
                int id = (int)e.Key;
                if (world.Get<Team>(id) != Team.Player) continue;
                Call("FocusUnit", id);
                Shot("02_unit_selected", W, H);
                Call("ClearSelection");
                break;
            }

            var econ = Field<EconomyWorld>("_econ");
            Call("FocusTile", econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position);
            Shot("03_capital_tile_menu", W, H);
            Call("ClearSelection");

            var tech = Field<TechTreeHud>("_techTreeHud");
            if (tech != null)
            {
                typeof(TechTreeHud).GetMethod("TogglePanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tech, null);
                Shot("04_tech_tree", W, H);
                typeof(TechTreeHud).GetMethod("TogglePanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tech, null);
            }

            Call("OpenPunchList");
            Shot("05_faith_punch_list", W, H);
            Call("CancelPunch");

            Field<BattleHud>("_hud").ShowBattleEnd(true);
            Shot("06_battle_end", W, H);
            Debug.Log("[MobileCapture] DONE " + Path.GetFullPath(OutDir));
        }

        private static void Shot(string key, int width, int height)
        {
            Call("RefreshEconomyViews", false);
            Pixel2DVerification.CaptureCamera(Camera.main, Path.Combine(OutDir, key + ".png"), width, height, true);
        }
    }
}
