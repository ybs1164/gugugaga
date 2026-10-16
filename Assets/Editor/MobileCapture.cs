using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 모바일 씬(Mobile.unity) 화면 점검 캡처 — 같은 장면을 휴대폰(2400x1080, 400dpi, 카메라 구멍·제스처 바만큼 안전 영역을 줄임)과
    /// PC(1920x1080, 96dpi) 두 벌로 찍는다: 시작 화면, 종족 고르기, 전투 첫 화면, 유닛 선택, 수도 칸, 기술 트리, 신앙 펀치 목록, 메뉴, 결과.
    /// 에디트 모드로 동기 실행(Play Mode 없음, FaithGameCapture와 같은 방식). 판을 시작하면 자동 저장이 한 번 쓰인다(이어하기 파일).
    /// 사용법(화면 필요, -nographics 금지): unity run . -- -logFile Logs/mobile_capture.log -executeMethod TacticsECS.EditorTools.MobileCapture.Run
    /// 결과: Logs/mobile_capture/*.png (문서용이 아니라 점검용)
    /// </summary>
    public static class MobileCapture
    {
        private const string OutDir = "Logs/mobile_capture";
        private static BattleController _c;
        private static string _prefix;
        private static int _w, _h;

        private static T Field<T>(string name) => (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);
        private static object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var old in Directory.GetFiles(OutDir, "*.png")) File.Delete(old);
            GameDataLoader.LoadAll();
            try
            {
                Capture("phone", 2400, 1080, 400f, new Rect(0.04f, 0.04f, 0.92f, 0.96f));
                Capture("pc", 1920, 1080, 96f, new Rect(0f, 0f, 1f, 1f));
            }
            finally
            {
                UiScale.DpiOverride = null;
                UiScale.SafeAreaOverride = null;
            }
            Debug.Log("[MobileCapture] DONE " + Path.GetFullPath(OutDir));
        }

        private static void Capture(string prefix, int width, int height, float dpi, Rect safe)
        {
            _prefix = prefix; _w = width; _h = height;
            UiScale.DpiOverride = dpi;
            UiScale.SafeAreaOverride = safe;
            EditorSceneManager.OpenScene(MobileSceneSetup.MobileScenePath);
            _c = Object.FindFirstObjectByType<BattleController>();
            Call("Awake"); Call("SetupBattle"); // quickPlay → 시작 화면

            var title = Field<TitleHud>("_titleHud");
            Shot("01_title");
            title.ShowTribeSelect();
            Shot("02_tribes");
            title.Hide();

            Call("StartQuickPlay", 0);
            Call("FocusStartCameraFor", (float)height);
            Shot("03_start");

            var world = Field<EntityWorld>("_world");
            foreach (DictionaryEntry e in (IDictionary)Field<object>("_viewsById"))
            {
                int id = (int)e.Key;
                if (world.Get<Team>(id) != Team.Player) continue;
                Call("FocusUnit", id);
                Shot("04_unit");
                Call("ClearSelection");
                break;
            }

            var econ = Field<EconomyWorld>("_econ");
            Call("FocusTile", econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position);
            Shot("05_capital");
            Call("ClearSelection");

            var tech = Field<TechTreeHud>("_techTreeHud");
            tech.Open();
            Shot("06_tech");
            tech.Close();

            var r = econ.Resources[Team.Player]; r.Faith = 8; econ.Resources[Team.Player] = r; // 펀치 하나는 켜진 모습으로
            Call("OpenPunchList");
            Shot("07_faith");
            Call("CancelPunch");

            Call("OpenMenu");
            Shot("08_menu");
            Field<BattleHud>("_hud").HideDialog();

            Call("ShowResult", true, "UI.Result.Conquest"); // EndBattle은 저장을 지우므로 결과 창만
            Shot("09_result");
        }

        private static void Shot(string key)
        {
            if (Field<EconomyWorld>("_econ") != null) Call("RefreshEconomyViews", false);
            Pixel2DVerification.CaptureCamera(Camera.main, Path.Combine(OutDir, $"{_prefix}_{key}.png"), _w, _h, true);
        }
    }
}
