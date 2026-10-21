using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 플레이 영상: 빠른 한 판(Mobile.unity, 가장 작은 맵)을 시작하고 양쪽 모두 AI(BattleController.RunAiTurnRoutine)로 두게 하면서
    /// Seconds초(게임 시간)를 MP4로 찍는다 → Logs/play_videos/&lt;이름&gt;.mp4 (이름: -recordName, 없으면 시각).
    /// 시간은 Time.captureFramerate로 고정해 찍는 속도와 상관없이 연출 속도가 그대로 남는다. 이어하기 파일을 덮어쓴다.
    /// Play 모드라 에디터 실행 파일로(화면 필요, -nographics 금지) — 보통은 scripts/record_play.sh로 부른다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayRecording
    {
        private const string ActiveKey = "PlayRecording.Active", NameKey = "PlayRecording.Name";
        private const int Fps = 30, Width = 1280, Height = 720, Seconds = 60;
        private const string OutDir = "Logs/play_videos";
        private static BattleController _c;
        private static int _frame, _lastFrameCount = -1, _lastAutoTurn;
        private static double _deadline;
        private static bool _failed;
        private static RenderTexture _rt;
        private static Texture2D _read;
        private static MediaEncoder _encoder;
        private static string _path;

        static PlayRecording()
        {
            if (SessionState.GetBool(ActiveKey, false)) Resume();
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-recordName");
            SessionState.SetString(NameKey, i >= 0 && i + 1 < args.Length ? args[i + 1] : DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(MobileSceneSetup.MobileScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void Resume()
        {
            _deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        private static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception) _failed = true;
        }

        private static object Call(string name, params object[] args) =>
            typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_c, args);
        private static T Field<T>(string name) =>
            (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c);

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(false, "timeout"); return; }
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (_c == null)
                {
                    _c = UnityEngine.Object.FindFirstObjectByType<BattleController>();
                    if (_c == null || Field<TitleHud>("_titleHud") == null) { _c = null; return; }
                    Field<TitleHud>("_titleHud").Hide();
                    Call("StartQuickPlay", new QuickPlaySetup { Tribe = -1, God = TitleHud.RandomGod, MapSize = 0 });
                    StartCapture();
                    return;
                }
                if (Time.frameCount == _lastFrameCount) return;
                _lastFrameCount = Time.frameCount;
                AutoPlayPlayer();
                Capture();
                if (_frame >= Fps * Seconds || Field<bool>("_battleOver")) Finish(!_failed, $"{_frame} frames → {Path.GetFullPath(_path)}");
            }
            catch (Exception e) { Debug.LogException(e); Finish(false, e.Message); }
        }

        /// <summary>아군 턴이 새로 시작되면 적 턴과 같은 AI 루틴을 아군에게도 돌린다.</summary>
        private static void AutoPlayPlayer()
        {
            var turn = Field<TurnState>("_turnState");
            if (turn.ActiveTeam != Team.Player || turn.TurnNumber == _lastAutoTurn) return;
            _lastAutoTurn = turn.TurnNumber;
            _c.StartCoroutine((System.Collections.IEnumerator)Call("RunAiTurnRoutine", Team.Player));
        }

        private static void StartCapture()
        {
            Directory.CreateDirectory(OutDir);
            _path = Path.Combine(OutDir, SessionState.GetString(NameKey, "play") + ".mp4");
            Time.captureFramerate = Fps;
            var cam = Field<Camera>("_cam");
            _rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = _rt;
            // Overlay 캔버스는 카메라 렌더에 안 들어온다 — 찍는 동안 이 카메라 기준 캔버스로 바꾼다.
            foreach (var canvas in _c.GetComponentsInChildren<Canvas>(true).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + .5f;
            }
            Call("PositionCamera"); // 캡처 화면 크기에 맞춰 맵 전체를 담는다
            _read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            _encoder = new MediaEncoder(_path, new VideoTrackAttributes
            {
                frameRate = new MediaRational(Fps), width = Width, height = Height, includeAlpha = false, bitRateMode = VideoBitrateMode.High,
            });
        }

        private static void Capture()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _read.Apply();
            RenderTexture.active = previous;
            _encoder.AddFrame(_read);
            _frame++;
        }

        private static void Finish(bool pass, string detail)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            _encoder?.Dispose();
            Time.captureFramerate = 0;
            Debug.Log("[PlayRecording] " + (pass ? "DONE: " : "FAIL: ") + detail);
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
