using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 유닛 행동 화면(고르기·이동·공격·반격·처치·방어·적 턴)을 데모 씬(SampleScene)에서 Play 모드로 실제 탭처럼 눌러 보고
    /// 한 장씩 찍는다 → Logs/action_ux/frames/*.png, 탭 위치는 taps.csv. GIF는 scripts/make_action_ux_gif.py가 만든다.
    /// 시간은 Time.captureFramerate로 고정해(한 프레임 = 1/Fps초) 찍는 데 걸리는 시간과 상관없이 연출 속도가 그대로 남는다.
    /// Play 모드라 에디터 실행 파일로(화면 필요, -nographics 금지):
    /// "&lt;Unity&gt;/Editor/Unity.exe" -batchmode -projectPath . -logFile Logs/action_ux.log -executeMethod TacticsECS.EditorTools.ActionUxCapture.Run
    /// </summary>
    [InitializeOnLoad]
    public static class ActionUxCapture
    {
        private const string ActiveKey = "ActionUxCapture.Active";
        private const int Fps = 15, Width = 960, Height = 600;
        private const string OutDir = "Logs/action_ux";
        private static int _phase, _frame, _lastFrameCount = -1;
        private static float _nextTime;
        private static double _deadline;
        private static bool _failed;
        private static RenderTexture _rt;
        private static Texture2D _read;
        private static readonly StringBuilder Taps = new StringBuilder("Frame,X,Y\n");
        private static int _warrior, _warrior2, _archer, _defender, _enemy, _enemy2;

        static ActionUxCapture()
        {
            if (SessionState.GetBool(ActiveKey, false)) Resume();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void Resume()
        {
            _deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        private static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) _failed = true;
        }

        private static object Call(BattleController c, string name, params object[] args) =>
            typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, args);
        private static T Field<T>(BattleController c, string name) =>
            (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(false, "timeout at phase " + _phase); return; }
            if (!EditorApplication.isPlaying) return;
            var c = UnityEngine.Object.FindFirstObjectByType<BattleController>();
            if (c == null || Field<GridWorld>(c, "_grid") == null || Field<EntityWorld>(c, "_world") == null) return;
            try
            {
                if (_rt != null && Time.frameCount != _lastFrameCount) { _lastFrameCount = Time.frameCount; Capture(); }
                if (Time.time < _nextTime) return;
                Step(c);
            }
            catch (Exception e) { Debug.LogException(e); Finish(false, e.Message); }
        }

        private static void Wait(float seconds) => _nextTime = Time.time + seconds;

        private static void Step(BattleController c)
        {
            var grid = Field<GridWorld>(c, "_grid");
            var world = Field<EntityWorld>(c, "_world");
            switch (_phase++)
            {
                case 0:
                    Setup(c, grid, world);
                    Wait(1f);
                    break;
                case 1:
                    StartCapture(c);
                    Wait(.7f);
                    break;
                case 2: Tap(c, Pos(world, _warrior)); Wait(1.1f); break; // 고르기: 노란 테두리·파란 이동 칸·정보 패널
                case 3: Tap(c, StandTile(c, grid, world, _warrior, _enemy)); Wait(1.4f); break; // 경로를 따라 이동(Charge가 없어 이동 뒤 공격은 못 한다)
                case 4: Tap(c, Pos(world, _warrior2)); Wait(1.0f); break;
                case 5:
                    Require(Field<List<int>>(c, "_attackableTargets").Contains(_enemy2), "enemy not attackable");
                    Tap(c, Pos(world, _enemy2)); Wait(1.4f); break; // 근접 공격 → 반격, 행동을 마친 유닛은 회색
                case 6: Tap(c, Pos(world, _archer)); Wait(1.0f); break;
                case 7:
                    Require(Field<List<int>>(c, "_attackableTargets").Contains(_enemy2), "enemy not in archer range");
                    Tap(c, Pos(world, _enemy2)); Wait(1.4f); break; // 원거리 공격 — 처치되면 깜빡이며 사라진다
                case 8: Tap(c, Pos(world, _defender)); Wait(1.0f); break;
                case 9: Call(c, "HandleDefendClicked"); Wait(1.0f); break; // 방어 태세(방패 배지), 행동을 마친 유닛은 회색
                case 10: Call(c, "EndTurn"); Wait(.5f); break;
                case 11:
                    if (Field<TurnState>(c, "_turnState").ActiveTeam != Team.Player) { _phase--; Wait(.1f); break; }
                    Wait(1.5f); break; // 적 턴: 이동 → 공격을 하나씩
                default:
                    File.WriteAllText(Path.Combine(OutDir, "taps.csv"), Taps.ToString());
                    Finish(!_failed, $"{_frame} frames → {Path.GetFullPath(OutDir)}");
                    break;
            }
        }

        /// <summary>장면 고르기: 아군 전사 둘·궁수·방패병. 적 근접 유닛 하나는 첫 전사가 세 칸 걸어 닿는 자리로,
        /// 하나는 둘째 전사 바로 옆(궁수 사거리 안)으로 옮긴다.</summary>
        private static void Setup(BattleController c, GridWorld grid, EntityWorld world)
        {
            var views = Field<Dictionary<int, UnitView>>(c, "_viewsById");
            IEnumerable<int> Find(Team team, int move, int range) => views.Keys.Where(id => world.Get<Team>(id) == team &&
                UnitActionQueries.Find<MoveAction>(world, id)?.MoveRange == move && UnitActionQueries.Find<AttackAction>(world, id)?.AttackRange == range)
                .OrderBy(id => Pos(world, id).y);
            _warrior = Find(Team.Player, 3, 1).First();
            _warrior2 = Find(Team.Player, 3, 1).Last();
            _archer = Find(Team.Player, 2, 2).First();
            _defender = Find(Team.Player, 2, 1).First();
            var melee = views.Keys.Where(id => world.Get<Team>(id) == Team.Enemy && UnitActionQueries.Find<AttackAction>(world, id)?.AttackRange == 1 && UnitActionQueries.Find<MoveAction>(world, id)?.MoveRange == 3).ToList(); // 적 전사
            var w = Pos(world, _warrior);
            _enemy = melee.OrderBy(id => PathfindingSystem.Distance(Pos(world, id), w)).First();
            Relocate(grid, world, views, _enemy, w + new Vector2Int(3, 1)); // 꺾이는 길로 닿는 자리
            var w2 = Pos(world, _warrior2);
            _enemy2 = melee.Where(id => id != _enemy).OrderBy(id => PathfindingSystem.Distance(Pos(world, id), w2)).First();
            Relocate(grid, world, views, _enemy2, w2 + new Vector2Int(1, 0));
            Relocate(grid, world, views, _archer, w2 + new Vector2Int(0, -1));
        }

        private static void Relocate(GridWorld grid, EntityWorld world, Dictionary<int, UnitView> views, int id, Vector2Int to)
        {
            Require(grid.InBounds(to) && !grid.IsOccupied(to), "relocation tile busy: " + to);
            grid.RemoveOccupant(Pos(world, id));
            world.Set(id, new GridPosition { Value = to });
            grid.PlaceOccupant(to, id);
            views[id].Refresh(world, id);
        }

        /// <summary>갈 수 있는 칸 중 target을 칠 수 있고 경로가 가장 긴 칸(이동 모습이 잘 보이게).</summary>
        private static Vector2Int StandTile(BattleController c, GridWorld grid, EntityWorld world, int unit, int target)
        {
            int range = UnitActionQueries.Find<AttackAction>(world, unit).AttackRange;
            var tiles = Field<HashSet<Vector2Int>>(c, "_reachableTiles");
            Require(tiles != null, "unit not selected");
            var best = tiles.Where(p => !grid.IsOccupied(p) && PathfindingSystem.Distance(p, Pos(world, target)) <= range)
                .OrderByDescending(p => MovementSystem.PathTo(grid, world, unit, p).Count)
                .ThenByDescending(p => p.x != Pos(world, unit).x && p.y != Pos(world, unit).y) // 꺾이는 길을 먼저
                .ThenBy(p => PathfindingSystem.Distance(p, Pos(world, target)))
                .ToList();
            Require(best.Count > 0, "no stand tile");
            return best[0];
        }

        private static Vector2Int Pos(EntityWorld world, int id) => world.Get<GridPosition>(id).Value;

        private static void Tap(BattleController c, Vector2Int pos)
        {
            var cam = Field<Camera>(c, "_cam");
            var screen = cam.WorldToScreenPoint(PixelCoordinates.GridToWorld(Field<GridWorld>(c, "_grid"), pos));
            Taps.Append($"{_frame},{Mathf.RoundToInt(screen.x)},{Height - 1 - Mathf.RoundToInt(screen.y)}\n");
            Call(c, "HandleClick", (Vector2)screen);
        }

        private static void StartCapture(BattleController c)
        {
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            Directory.CreateDirectory(Path.Combine(OutDir, "frames"));
            Time.captureFramerate = Fps;
            var cam = Field<Camera>(c, "_cam");
            _rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = _rt;
            // Overlay 캔버스는 카메라 렌더에 안 들어온다 — 찍는 동안 이 카메라 기준 캔버스로 바꾼다.
            foreach (var canvas in c.GetComponentsInChildren<Canvas>(true).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + .5f;
            }
            Call(c, "PositionCamera"); // 캡처 화면 크기에 맞춰 맵 전체를 다시 담는다
            _read = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        }

        private static void Capture()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _read.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutDir, "frames", $"{_frame++:D4}.png"), _read.EncodeToPNG());
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Finish(bool pass, string detail)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            Time.captureFramerate = 0;
            Debug.Log("[ActionUxCapture] " + (pass ? "DONE: " : "FAIL: ") + detail);
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
