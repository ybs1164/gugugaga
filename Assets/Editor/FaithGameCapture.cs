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
    /// 실제 전투 화면(BattleController, 모든 HUD 포함)에서 양 팀을 AI로 끝까지 진행시키며 신앙의 중요한 순간마다 화면을 PNG로 남긴다.
    /// 첫 신전, 첫 기도, 플레이어 신앙 펀치 UI(목록·대상·확인), 첫 신앙 펀치, 강림(신화 유닛), 신화 유닛 메뉴, 대신전 착공·완성, 신앙 승리.
    /// 맵은 무작위라 필요한 순간이 다 나오지 않으면 새 맵으로 몇 번 다시 한다. 에디트 모드로 동기 실행(Play Mode 없음).
    /// 사용법(화면 필요, -nographics 금지): unity run . -- -executeMethod TacticsECS.EditorTools.FaithGameCapture.Run
    /// 결과: docs/images/faith_game/*.png
    /// </summary>
    public static class FaithGameCapture
    {
        private const string OutDir = "docs/images/faith_game";
        private const int MaxTurns = 100;
        private static int MaxAttempts => int.TryParse(System.Environment.GetEnvironmentVariable("FAITH_CAPTURE_ATTEMPTS"), out var v) ? v : 6;

        private static BattleController _c;
        private static readonly Dictionary<string, string> Shots = new Dictionary<string, string>();
        private static readonly string[] Wanted = { "02_first_temple", "03_first_prayer", "04_player_punch_list", "05_player_punch_targeting", "06_player_punch_confirm", "07_first_punch",
            "08_descent_mythic", "09_mythic_menu", "10_great_temple_built", "11_great_temple_waiting", "12_victory" };

        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            string savedLanguage = PlayerPrefs.GetString("Language", string.Empty);
            LanguagePreference.Save("ko");
            LanguagePreference.Apply();
            GameDataLoader.LoadAll();

            int best = -1;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var tempDir = Path.Combine(OutDir, "_attempt");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);
                Shots.Clear();
                Play(tempDir);
                int got = Wanted.Count(w => Shots.ContainsKey(w));
                Debug.Log($"[FaithGameCapture] attempt {attempt}: {got}/{Wanted.Length} moments");
                if (got > best)
                {
                    best = got;
                    foreach (var f in Directory.GetFiles(OutDir, "*.png")) File.Delete(f);
                    foreach (var f in Directory.GetFiles(tempDir, "*.png")) File.Copy(f, Path.Combine(OutDir, Path.GetFileName(f)), true);
                }
                if (got == Wanted.Length) break;
            }
            if (Directory.Exists(Path.Combine(OutDir, "_attempt"))) Directory.Delete(Path.Combine(OutDir, "_attempt"), true);

            if (string.IsNullOrEmpty(savedLanguage)) PlayerPrefs.DeleteKey("Language"); else LanguagePreference.Save(savedLanguage);
            LanguagePreference.Apply();
            GameDataLoader.LoadAll();
            Debug.Log($"[FaithGameCapture] DONE best {best}/{Wanted.Length}");
        }

        // ---------- 반사 도우미 ----------

        private static T Field<T>(string name) => (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);
        private static void SetField(string name, object value) => typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_c, value);
        private static object Call(string name, params object[] args) => typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);

        private static EconomyWorld Econ => Field<EconomyWorld>("_econ");
        private static EntityWorld World => Field<EntityWorld>("_world");
        private static GridWorld Grid => Field<GridWorld>("_grid");

        /// <summary>focus 칸으로 카메라를 옮겨 화면을 찍는다(UI 포함). 이미 찍은 순간은 건너뛴다.</summary>
        private static void Shot(string key, string dir, Vector2Int? focus, float size = 5.5f)
        {
            if (Shots.ContainsKey(key)) return;
            Call("RefreshEconomyViews", false);
            var cam = Camera.main;
            var pixel = cam.GetComponent<PixelCamera>();
            if (focus.HasValue && pixel != null)
            {
                var p = PixelCoordinates.GridToWorld(Grid, focus.Value);
                pixel.RequestedPosition = new Vector3(p.x, p.y, pixel.RequestedPosition.z);
                pixel.RequestedSize = size;
            }
            string path = Path.Combine(dir, key + ".png");
            Pixel2DVerification.CaptureCamera(cam, path, 1920, 1080, true);
            Shots[key] = path;
        }

        // ---------- 한 판 ----------

        private static void Play(string dir)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            _c = Object.FindFirstObjectByType<BattleController>();
            Call("Awake"); Call("SetupBattle");
            int sun = System.Array.FindIndex(GameTables.Gods, g => g.Id == "Sun");
            int storm = System.Array.FindIndex(GameTables.Gods, g => g.Id == "Storm");
            var gods = Field<Dictionary<Team, int>>("_godByTeam");
            gods[Team.Player] = sun; gods[Team.Enemy] = storm;
            Call("HandleGenerateTerrain");

            SetField("_unitRows", new List<UnitCsvRow>(GameTables.Units));
            SetField("_placementActive", false);
            var sandboxHud = Field<SandboxHud>("_sandboxHud");
            if (sandboxHud != null) Object.DestroyImmediate(sandboxHud.gameObject);
            SetField("_sandboxHud", null);
            SetField("_placementController", null);
            Call("SetBattleOnlyHudVisible", true);
            Call("BeginBattle");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) PixelUISkin.Apply(canvas.gameObject);

            // 관전 화면이라 맵 전체를 보이게 한다(안개 해제).
            var grid = Grid;
            for (int i = 0; i < grid.Tiles.Length; i++) grid.Tiles[i].ExploredMask |= 1 << (int)Team.Player;
            Call("RefreshEconomyViews", true);
            var camPixel = Camera.main.GetComponent<PixelCamera>();
            float overviewSize = camPixel != null ? camPixel.RequestedSize : 8f;
            var overviewPos = camPixel != null ? camPixel.RequestedPosition : Vector3.zero;
            Shot("01_start_overview", dir, null);

            var seen = new HashSet<string>();
            bool peace = false;
            bool playerPunchUiDone = false;
            for (int turn = 1; turn <= MaxTurns; turn++)
            {
                foreach (var team in CitySystem.Teams)
                {
                    if (!(turn == 1 && team == Team.Player)) StartTeamTurn(team, turn);
                    if (turn % 10 == 0 && team == Team.Player) Debug.Log("[FaithGameCapture] " + Status(turn));
                    var econ = Econ; var world = World;
                    if (Field<bool>("_battleOver")) { FinishVictory(dir); return; }

                    if (team == Team.Player && !playerPunchUiDone) playerPunchUiDone = TryPlayerPunchUi(dir);
                    if (team == Team.Player) TryMythicMenu(dir);

                    var hpBefore = (Dictionary<int, int>)Call("SnapshotHp");
                    var log = EconomyAI.RunTurn(grid, world, econ, team);
                    Call("ProcessEconomyLog", log);
                    Call("ShowAiFaithEffects", hpBefore);
                    Call("RefreshEconomyViews", true);
                    ScanEvents(dir, team, log, hpBefore, seen);

                    EnemyAI.RunTurn(grid, world, econ, team, (List<Vector2Int>)Call("CaptureTargets", team));
                    Call("RefreshEconomyViews", false);
                    Call("RefreshAllViews");
                    ScanEvents(dir, team, null, hpBefore, seen);
                    // 신앙 장면을 끝까지 보여 주려고, 첫 신앙 펀치를 찍은 뒤(또는 15턴 뒤) 두 팀에 평화 조약을 건다 — 정복으로 판이 일찍 끝나지 않게.
                    if (!peace && (Shots.ContainsKey("07_first_punch") || turn >= 15))
                    {
                        DiplomacySystem.SetPeaceTreaty(econ, Team.Player, Team.Enemy, true);
                        peace = true;
                    }

                    AbilitySystem.ApplyTurnEndWait(grid, world, team);
                    TaskSystem.EndTurn(econ, team);
                    Call("RefreshAllViews");

                    if (econ.FaithWinner >= 0 || Field<bool>("_battleOver")) { FinishVictory(dir); return; }
                    if (CitySystem.HasLost(econ, team == Team.Player ? Team.Enemy : Team.Player) || CitySystem.HasLost(econ, team)) { Call("CheckBattleEnd"); return; }
                }
            }
            if (camPixel != null) { camPixel.RequestedPosition = overviewPos; camPixel.RequestedSize = overviewSize; }
        }

        /// <summary>BattleController 턴 흐름(TurnSystem.StartTurn → HandleTurnStart)을 한 팀분 재현한다. 적 팀의 HandleTurnStart는 코루틴을 시작하므로 그동안 _battleOver로 막는다.</summary>
        private static void StartTeamTurn(Team team, int turn)
        {
            foreach (var popup in Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)) Object.DestroyImmediate(popup.gameObject); // 이전 단계의 피해 팝업이 화면을 덮지 않게
            var state = TurnSystem.StartTurn(World, team, turn);
            SetField("_turnState", state);
            bool wasOver = Field<bool>("_battleOver");
            if (team == Team.Enemy) SetField("_battleOver", true);
            Call("HandleTurnStart", team, turn);
            if (team == Team.Enemy) SetField("_battleOver", wasOver);
            if (team == Team.Enemy && Econ.FaithWinner >= 0 && !wasOver) Call("CheckBattleEnd");
        }

        private static void FinishVictory(string dir)
        {
            var econ = Econ;
            if (!Field<bool>("_battleOver")) Call("CheckBattleEnd");
            Vector2Int? focus = null;
            if (econ.FaithWinner >= 0)
            {
                int gt = GreatTempleSystem.IndexOf(econ, (Team)econ.FaithWinner);
                if (gt >= 0) focus = econ.GreatTemples[gt].Tiles[0];
            }
            Shot("12_victory", dir, focus, 6f);
        }

        private static string Status(int turn)
        {
            var econ = Econ; var world = World; var grid = Grid;
            string Team_(Team t) =>
                $"{t}: faith {econ.Resources[t].Faith}/{econ.Resources[t].MaxFaith} temples {TemplePassiveSystem.Temples(grid, t).Count} task {(econ.Tasks[t].Completed.Contains(GreatTempleSystem.TaskId) ? 1 : 0)} " +
                $"stars {econ.Resources[t].Stars} units {FaithSystem.CountUnits(world, t)} techs {econ.Tech[t].Unlocked.Count} gt {GreatTempleSystem.IndexOf(econ, t)}";
            return $"t{turn} {Team_(Team.Player)} | {Team_(Team.Enemy)}";
        }

        // ---------- 장면 탐지 ----------

        private static void ScanEvents(string dir, Team team, List<EconomyLogEntry> log, Dictionary<int, int> hpBefore, HashSet<string> seen)
        {
            var grid = Grid; var world = World; var econ = Econ;

            if (!Shots.ContainsKey("02_first_temple"))
                foreach (var t in CitySystem.Teams)
                {
                    var temples = TemplePassiveSystem.Temples(grid, t);
                    if (temples.Count > 0) { Shot("02_first_temple", dir, temples[0]); break; }
                }

            if (!Shots.ContainsKey("03_first_prayer"))
                for (int i = 0; i < world.EntityCount; i++)
                    if (UnitQueries.IsAlive(world, i) && world.GetOrDefault<Praying>(i).Value)
                    { Shot("03_first_prayer", dir, world.Get<GridPosition>(i).Value, 4.5f); break; }

            if (log != null && !Shots.ContainsKey("07_first_punch"))
            {
                string prefix = LocalizationSystem.T("UI.FaithPunch.Log").Split('{')[0];
                if (log.Any(e => e.Kind == EconomyLogKind.Action && e.Subject != null && e.Subject.StartsWith(prefix)))
                {
                    Vector2Int? focus = null;
                    foreach (var kv in hpBefore)
                    {
                        int after = UnitQueries.IsAlive(world, kv.Key) ? world.Get<Hp>(kv.Key).Value : 0;
                        if (after < kv.Value) { focus = world.Get<GridPosition>(kv.Key).Value; break; }
                    }
                    Shot("07_first_punch", dir, focus, 5f);
                }
            }

            if (!Shots.ContainsKey("08_descent_mythic"))
                foreach (var t in CitySystem.Teams)
                {
                    int m = MythicSystem.FindMythic(world, t);
                    if (m >= 0) { Shot("08_descent_mythic", dir, world.Get<GridPosition>(m).Value, 4.5f); break; }
                }

            foreach (var t in econ.GreatTemples)
            {
                var focus = t.Tiles[0];
                if (t.State == GreatTempleState.Building) Shot("10_great_temple_built", dir, focus, 6f);
                else Shot("11_great_temple_waiting", dir, focus, 6f);
            }
        }

        /// <summary>플레이어 차례 시작에 신앙이 모이면 신앙 펀치 UI(목록 → 대상 선택 → 확인)를 한 번 보여준다. 찍었으면 true.</summary>
        private static bool TryPlayerPunchUi(string dir)
        {
            var econ = Econ; var grid = Grid; var world = World;
            var punches = FaithPunchSystem.Available(econ, Team.Player);
            if (punches.Count == 0 || econ.Resources[Team.Player].Faith < punches.Max(p => p.Cost)) return false;
            var capital = econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position;

            Call("OpenPunchList");
            Shot("04_player_punch_list", dir, capital, 6f);
            int strike = punches.FindIndex(p => p.Kind == FaithPunchKind.Strike);
            Call("HandlePunchSelected", Mathf.Max(0, strike));
            Shot("05_player_punch_targeting", dir, capital, 6f);

            Vector2Int target = capital;
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == Team.Enemy) { target = world.Get<GridPosition>(i).Value; break; }
            Call("PreviewPunchTarget", target);
            Shot("06_player_punch_confirm", dir, target, 5f);
            Call("CancelPunch");
            return true;
        }

        private static void TryMythicMenu(string dir)
        {
            if (Shots.ContainsKey("09_mythic_menu")) return;
            var world = World;
            int m = MythicSystem.FindMythic(world, Team.Player);
            if (m < 0) return;
            Call("FocusUnit", m);
            Shot("09_mythic_menu", dir, world.Get<GridPosition>(m).Value, 5f);
            Call("ClearSelection");
        }
    }
}
