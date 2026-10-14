using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 모바일 씬(Mobile.unity) Play 모드 스모크 테스트: 빠른 시작으로 바로 전투가 열리는지, 가상 터치스크린으로 끌기 팬·핀치 줌·탭 선택이
    /// 되는지, "다시 시작"이 새 판을 여는지 본다. Pixel2DPlayVerification과 같은 방식(SessionState로 도메인 리로드 뒤 이어서 진행).
    /// unity run . -- -logFile Logs/mobile_play.log -executeMethod TacticsECS.EditorTools.MobilePlayVerification.Run
    /// </summary>
    [InitializeOnLoad]
    public static class MobilePlayVerification
    {
        private const string ActiveKey = "MobilePlay.Active";
        private const float Step = .25f;
        private static int _phase;
        private static double _nextTime, _deadline;
        private static bool _failed;
        private static Touchscreen _touch;
        private static Vector3 _focusBefore;
        private static float _sizeBefore;
        private static Vector2Int _tapTarget;
        private static Vector2Int? _focusAtStart;
        private static InputSettings.EditorInputBehaviorInPlayMode? _inputBehavior;
        private static InputSettings.BackgroundBehavior? _backgroundBehavior;

        static MobilePlayVerification()
        {
            if (SessionState.GetBool(ActiveKey, false)) Resume();
        }

        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(MobileSceneSetup.MobileScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void Resume()
        {
            _deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        private static void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) _failed = true;
        }

        private static object Call(BattleController controller, string name, params object[] args) =>
            typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);
        private static T Field<T>(BattleController controller, string name) =>
            (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        private static void SetField(BattleController controller, string name, object value) =>
            typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

        private static void Touch(int id, TouchPhase phase, Vector2 position) =>
            InputSystem.QueueStateEvent(_touch, new TouchState { touchId = id, phase = phase, position = position });

        private static Vector2 Center => new Vector2(Screen.width * .5f, Screen.height * .5f);

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline) { Finish(false, "timeout at phase " + _phase); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _nextTime) return;
            var controller = UnityEngine.Object.FindFirstObjectByType<BattleController>();
            if (controller == null || Field<GridWorld>(controller, "_grid") == null) return;
            try
            {
                var pixelCamera = Field<PixelCamera>(controller, "_pixelCamera");
                switch (_phase)
                {
                    case 0:
                        CheckQuickPlayStarted(controller, "start");
                        // 배치모드에는 포커스된 Game 뷰가 없다 — 기본 설정이면 가상 터치가 플레이어 쪽 입력 상태에 들어가지 않거나(Game 뷰 포커스),
                        // 앱이 백그라운드로 취급돼 장치가 꺼진다(backgroundBehavior). 이 검증 동안만 둘 다 끈다.
                        _inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                        _backgroundBehavior = InputSystem.settings.backgroundBehavior;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        _touch = InputSystem.AddDevice<Touchscreen>();
                        _focusBefore = Field<Vector3>(controller, "_cameraFocus");
                        _focusAtStart = Field<Vector2Int?>(controller, "_focusPos");
                        Touch(1, TouchPhase.Began, Center);
                        break;
                    case 1:
                        Touch(1, TouchPhase.Moved, Center + new Vector2(120f, 0f));
                        break;
                    case 2:
                        Touch(1, TouchPhase.Ended, Center + new Vector2(120f, 0f));
                        break;
                    case 3:
                        // 손가락을 오른쪽으로 끌면 지도가 따라와야 하므로 카메라 초점은 왼쪽으로 간다.
                        if (Field<Vector3>(controller, "_cameraFocus").x >= _focusBefore.x) throw new Exception("drag did not pan camera");
                        if (Field<Vector2Int?>(controller, "_focusPos") != _focusAtStart) throw new Exception("drag was treated as a tap");
                        _sizeBefore = pixelCamera.RequestedSize;
                        Touch(1, TouchPhase.Began, Center - new Vector2(40f, 0f));
                        Touch(2, TouchPhase.Began, Center + new Vector2(40f, 0f));
                        break;
                    case 4:
                        Touch(1, TouchPhase.Moved, Center - new Vector2(160f, 0f));
                        Touch(2, TouchPhase.Moved, Center + new Vector2(160f, 0f));
                        break;
                    case 5:
                        Touch(1, TouchPhase.Ended, Center - new Vector2(160f, 0f));
                        Touch(2, TouchPhase.Ended, Center + new Vector2(160f, 0f));
                        break;
                    case 6:
                        if (pixelCamera.RequestedSize >= _sizeBefore) throw new Exception("pinch did not zoom in");
                        if (Field<Vector2Int?>(controller, "_focusPos") != _focusAtStart) throw new Exception("pinch was treated as a tap");
                        // 아군 수도를 화면 가운데로 옮긴 뒤 가운데를 탭하면 그 칸에 포커스가 가야 한다.
                        var econ = Field<EconomyWorld>(controller, "_econ");
                        var grid = Field<GridWorld>(controller, "_grid");
                        _tapTarget = econ.Cities[CitySystem.FindCapital(econ, Team.Player)].Position;
                        SetField(controller, "_cameraFocus", PixelCoordinates.GridToWorld(grid, _tapTarget));
                        SetField(controller, "_focusPos", null);
                        Call(controller, "ApplyCameraTransform");
                        break;
                    case 7:
                        Touch(1, TouchPhase.Began, Center);
                        break;
                    case 8:
                        Touch(1, TouchPhase.Ended, Center);
                        break;
                    case 9:
                        var focus = Field<Vector2Int?>(controller, "_focusPos");
                        if (focus != _tapTarget) throw new Exception($"tap focused {focus}, expected capital {_tapTarget}");
                        Call(controller, "HandleReturnToSetup");
                        break;
                    case 10:
                        CheckQuickPlayStarted(controller, "restart");
                        Finish(!_failed, "quick play start / drag pan / pinch zoom / tap select / restart");
                        return;
                }
                _phase++;
                _nextTime = EditorApplication.timeSinceStartup + Step;
            }
            catch (Exception e) { Debug.LogException(e); Finish(false, e.Message); }
        }

        private static void CheckQuickPlayStarted(BattleController controller, string when)
        {
            if (controller.GetComponentInChildren<SandboxHud>() != null) throw new Exception(when + ": sandbox HUD should not exist");
            var econ = Field<EconomyWorld>(controller, "_econ");
            if (econ == null) throw new Exception(when + ": battle did not start");
            foreach (var team in CitySystem.Teams)
            {
                if (CitySystem.FindCapital(econ, team) < 0) throw new Exception($"{when}: {team} capital missing");
                if (Field<System.Collections.Generic.Dictionary<Team, int>>(controller, "_tribeByTeam")[team] < 0) throw new Exception($"{when}: {team} tribe not picked");
            }
            if (controller.GetComponentsInChildren<UnitView>().Length < 2) throw new Exception(when + ": starting units missing");
            Debug.Log($"[MobilePlayVerification] {when} PASS: {Field<GridWorld>(controller, "_grid").Width}x{Field<GridWorld>(controller, "_grid").Height}");
        }

        private static void Finish(bool pass, string detail)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            if (_touch != null) InputSystem.RemoveDevice(_touch);
            if (_inputBehavior != null) InputSystem.settings.editorInputBehaviorInPlayMode = _inputBehavior.Value;
            if (_backgroundBehavior != null) InputSystem.settings.backgroundBehavior = _backgroundBehavior.Value;
            Debug.Log("[MobilePlayVerification] " + (pass ? "ALL PASS: " : "FAIL: ") + detail);
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
