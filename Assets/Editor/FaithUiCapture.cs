using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 신앙 HUD(FaithHud / FaithPunchHud)와 연결 UI를 상태별로 PNG로 남기는 에디트 모드 캡처. Play Mode 없이 Sandbox 씬을 열어 전투를 시작하고
    /// 상태를 직접 만든 뒤 Pixel2DVerification.CaptureCamera로 찍는다(UI 픽셀 정렬 검사 포함).
    /// 사용법(화면 필요, -nographics 금지):
    ///   unity run . -- -executeMethod TacticsECS.EditorTools.FaithUiCapture.Run
    /// 결과: docs/images/faith/*.png
    /// </summary>
    public static class FaithUiCapture
    {
        private const string OutDir = "docs/images/faith";
        private static BattleController _c;
        private static string _lang;

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            string saved = PlayerPrefs.GetString("Language", string.Empty); // 전투 씬 Awake가 읽는 언어 설정 — 끝나면 되돌린다
            foreach (var lang in new[] { "ko", "en" })
            {
                _lang = lang;
                LanguagePreference.Save(lang);
                LanguagePreference.Apply();
                GameDataLoader.LoadAll();
                Scenario(true);
            }
            if (string.IsNullOrEmpty(saved)) PlayerPrefs.DeleteKey("Language"); else LanguagePreference.Save(saved);
            LanguagePreference.Apply();
            GameDataLoader.LoadAll();
            Debug.Log("[FaithUiCapture] DONE");
        }

        // ---------- 반사 도우미 ----------

        private static T Field<T>(string name) => (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);
        private static void SetField(string name, object value) => typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_c, value);
        private static object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);

        private static void Shot(string name, int width = 1920, int height = 1080)
        {
            Call("RefreshEconomyViews", false);
            Pixel2DVerification.CaptureCamera(Camera.main, $"{OutDir}/{_lang}_{name}.png", width, height, true);
        }

        private static void SetResources(int faith, int max, int stars = -1)
        {
            var econ = Field<EconomyWorld>("_econ");
            var r = econ.Resources[Team.Player];
            r.MaxFaith = max; r.Faith = faith;
            if (stars >= 0) r.Stars = stars;
            econ.Resources[Team.Player] = r;
        }

        // ---------- 시나리오 ----------

        private static void Scenario(bool full)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            _c = Object.FindFirstObjectByType<BattleController>();
            Call("Awake"); Call("SetupBattle");

            int sun = System.Array.FindIndex(GameTables.Gods, g => g.Id == "Sun");
            Field<Dictionary<Team, int>>("_godByTeam")[Team.Player] = sun;
            Call("HandleGenerateTerrain");

            // 전투 시작(HandleSandboxStartBattle과 같은 순서 — 에디트 모드에서는 Destroy가 안 되므로 배치 HUD는 직접 지운다).
            SetField("_unitRows", new List<UnitCsvRow>(GameTables.Units));
            SetField("_placementActive", false);
            var sandboxHud = Field<SandboxHud>("_sandboxHud");
            if (sandboxHud != null) Object.DestroyImmediate(sandboxHud.gameObject);
            SetField("_sandboxHud", null);
            SetField("_placementController", null);
            Call("SetBattleOnlyHudVisible", true);
            Call("BeginBattle");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) PixelUIScaler.Attach(canvas.gameObject);

            var econ = Field<EconomyWorld>("_econ");
            var world = Field<EntityWorld>("_world");
            var grid = Field<GridWorld>("_grid");
            int capital = CitySystem.FindCapital(econ, Team.Player);
            var capPos = econ.Cities[capital].Position;

            // 1) 처음 상태 — 신앙 0
            SetResources(0, 12);
            Shot("01_hud_start");

            // 2) 신전 + 기도 중인 유닛 → 수입 미리보기
            var temple = FindFreeNear(grid, capPos);
            var tile = grid.GetTile(temple); tile.BuildingId = "Temple"; tile.BuildingTurn = 1; grid.SetTile(temple, tile);
            int unit = world.EntityCount > 0 ? Enumerable.Range(0, world.EntityCount).First(i => world.Get<Team>(i) == Team.Player && UnitQueries.IsAlive(world, i)) : -1;
            PlaceNear(grid, world, unit, temple);
            PrayerSystem.Pray(grid, world, unit);
            SetResources(14, 20);
            Shot("02_hud_praying");

            // 3) 대신전 상태 줄(내 진행 + 상대 소식)
            econ.GreatTemples.Add(new GreatTempleData { Owner = Team.Player, Tiles = new[] { capPos }, State = GreatTempleState.Building, StartTurn = 1, CompleteTurn = -1 });
            econ.GreatTemples.Add(new GreatTempleData { Owner = Team.Enemy, Tiles = new[] { new Vector2Int(grid.Width - 2, grid.Height - 2) }, State = GreatTempleState.Building, StartTurn = 1, CompleteTurn = -1 });
            Shot("03_hud_great_temple");
            econ.GreatTemples.Clear();

            if (!full) { Finish(); return; }

            // 4) 펀치 목록: 신앙이 모자랄 때 / 충분할 때
            SetResources(7, 20, 50);
            Call("OpenPunchList");
            Shot("04_punch_list_low");
            Shot("04_punch_list_low_1280x720", 1280, 720);
            Call("CancelPunch");
            SetResources(20, 20, 50);
            Call("OpenPunchList");
            Shot("05_punch_list_full");
            Shot("05_punch_list_full_1280x720", 1280, 720);
            Shot("05_punch_list_full_960x540", 960, 540);

            // 5) 대상 선택 → 확인(가능 / 불가)
            Call("HandlePunchSelected", 1); // Upheaval 등 두 번째 펀치
            Shot("06_punch_targeting");
            var free = FindFreeNear(grid, capPos, 2);
            Call("PreviewPunchTarget", free);
            Shot("07_punch_confirm_ok");
            Call("PreviewPunchTarget", capPos); // 도시 칸 — 막힘
            Shot("08_punch_confirm_blocked");
            Call("CancelPunch");

            // 6) 강림 → 신화 유닛 메뉴
            var god = GameTables.Gods[sun];
            var descent = GameTables.FaithPunches[god.Punches.Select(i => i).First(i => FaithPunchSystem.Has(GameTables.FaithPunches[i], FaithEffectKind.Descend))];
            SetResources(20, 20, 50);
            var unitPos = world.Get<GridPosition>(unit).Value;
            var result = FaithPunchSystem.Apply(grid, world, econ, Team.Player, descent, unitPos);
            foreach (int spawned in result.SpawnedIds) Call("AttachSpawnedView", spawned);
            int mythic = result.SpawnedIds.Count > 0 ? result.SpawnedIds[0] : -1;
            if (mythic >= 0)
            {
                world.Set(mythic, new HasMoved { Value = false });
                world.Set(mythic, new HasActed { Value = false });
                Call("FocusUnit", mythic);
                Shot("09_mythic_menu");
            }
            Finish();
        }

        private static void Finish()
        {
            Call("ClearSelection");
        }

        private static Vector2Int FindFreeNear(GridWorld grid, Vector2Int center, int radius = 1)
        {
            for (int r = 1; r <= radius + 2; r++)
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var p = new Vector2Int(center.x + dx, center.y + dy);
                    if (!grid.InBounds(p)) continue;
                    var t = grid.GetTile(p);
                    if (t.Terrain == TerrainType.Land && t.Walkable && t.OccupantId == TileData.NoOccupant && string.IsNullOrEmpty(t.BuildingId) && string.IsNullOrEmpty(t.StructureId)) return p;
                }
            return center;
        }

        /// <summary>유닛을 신전 바로 옆 빈 칸으로 옮긴다(화면 갱신 포함).</summary>
        private static void PlaceNear(GridWorld grid, EntityWorld world, int unit, Vector2Int temple)
        {
            var spot = FindFreeNear(grid, temple);
            grid.RemoveOccupant(world.Get<GridPosition>(unit).Value);
            world.Set(unit, new GridPosition { Value = spot });
            grid.PlaceOccupant(spot, unit);
            world.Set(unit, new HasActed { Value = false });
            world.Set(unit, new HasMoved { Value = false });
            var views = Field<Dictionary<int, UnitView>>("_viewsById");
            if (views.TryGetValue(unit, out var view)) view.Refresh(world, unit);
        }
    }
}
