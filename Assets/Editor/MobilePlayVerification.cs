using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 모바일 씬(Mobile.unity) Play 모드 스모크 테스트 — 한 판의 흐름 전체(docs/spec/game/session.md):
    /// 시작 화면(저장 없음 → 이어하기 꺼짐) → 종족 고르기 → 시작, 가상 터치스크린으로 끌기 팬·핀치 줌·탭 선택,
    /// 턴 종료 뒤 다음 내 턴에 자동 저장, 메인 화면 → 이어하기(적이 움직인 뒤의 상태가 그대로인지), 기술 트리 끌기·핀치(지도는 그대로),
    /// 결과의 "새 게임"처럼 종족 고르기부터 다시 시작.
    /// 시작할 때 이어하기 파일(Application.persistentDataPath/quickplay.sav)을 지운다.
    /// Pixel2DPlayVerification과 같은 방식(SessionState로 도메인 리로드 뒤 이어서 진행). Play 모드라 unity run이 아니라 에디터 실행 파일로:
    /// "&lt;Unity&gt;/Editor/Unity.exe" -batchmode -projectPath . -logFile Logs/mobile_play.log -executeMethod TacticsECS.EditorTools.MobilePlayVerification.Run
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
        private static string _snapshot;
        private static InputSettings.EditorInputBehaviorInPlayMode? _inputBehavior;
        private static InputSettings.BackgroundBehavior? _backgroundBehavior;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "quickplay.sav");

        static MobilePlayVerification()
        {
            if (SessionState.GetBool(ActiveKey, false)) Resume();
        }

        public static void Run()
        {
            SaveSystem.Delete(SavePath);
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(MobileSceneSetup.MobileScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void Resume()
        {
            _deadline = EditorApplication.timeSinceStartup + 180;
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

        /// <summary>화면의 그 이름 버튼(켜져 있는 것)을 실제로 누른다 — 이벤트 배선까지 확인하려고 메서드를 직접 부르지 않는다.</summary>
        private static void Press(BattleController controller, string buttonName)
        {
            var button = controller.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == buttonName);
            if (button == null) throw new Exception("button not found: " + buttonName);
            if (!button.interactable) throw new Exception("button disabled: " + buttonName);
            button.onClick.Invoke();
        }

        private static bool ButtonEnabled(BattleController controller, string buttonName) =>
            controller.GetComponentsInChildren<Button>().Any(b => b.name == buttonName && b.interactable);

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
                        // 시작 화면: 저장이 없으니 이어하기는 꺼져 있다. 새로하기 → 종족 고르기 → 첫 종족 → 시작.
                        if (Field<EconomyWorld>(controller, "_econ") != null) throw new Exception("battle started before the title screen");
                        if (ButtonEnabled(controller, "ContinueButton")) throw new Exception("continue enabled without a save");
                        Press(controller, "NewGameButton");
                        Press(controller, "Tribe0");
                        Press(controller, "StartButton");
                        break;
                    case 1:
                        CheckQuickPlayStarted(controller, "start");
                        if (Field<Dictionary<Team, int>>(controller, "_tribeByTeam")[Team.Player] != 0) throw new Exception("picked tribe not used");
                        if (!SaveSystem.Exists(SavePath)) throw new Exception("no autosave at turn 1");
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
                    case 2:
                        Touch(1, TouchPhase.Moved, Center + new Vector2(120f, 0f));
                        break;
                    case 3:
                        Touch(1, TouchPhase.Ended, Center + new Vector2(120f, 0f));
                        break;
                    case 4:
                        // 손가락을 오른쪽으로 끌면 지도가 따라와야 하므로 카메라 초점은 왼쪽으로 간다.
                        if (Field<Vector3>(controller, "_cameraFocus").x >= _focusBefore.x) throw new Exception("drag did not pan camera");
                        if (Field<Vector2Int?>(controller, "_focusPos") != _focusAtStart) throw new Exception("drag was treated as a tap");
                        _sizeBefore = pixelCamera.RequestedSize;
                        Touch(1, TouchPhase.Began, Center - new Vector2(40f, 0f));
                        Touch(2, TouchPhase.Began, Center + new Vector2(40f, 0f));
                        break;
                    case 5:
                        Touch(1, TouchPhase.Moved, Center - new Vector2(160f, 0f));
                        Touch(2, TouchPhase.Moved, Center + new Vector2(160f, 0f));
                        break;
                    case 6:
                        Touch(1, TouchPhase.Ended, Center - new Vector2(160f, 0f));
                        Touch(2, TouchPhase.Ended, Center + new Vector2(160f, 0f));
                        break;
                    case 7:
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
                    case 8:
                        Touch(1, TouchPhase.Began, Center);
                        break;
                    case 9:
                        Touch(1, TouchPhase.Ended, Center);
                        break;
                    case 10:
                        var focus = Field<Vector2Int?>(controller, "_focusPos");
                        if (focus != _tapTarget) throw new Exception($"tap focused {focus}, expected capital {_tapTarget}");
                        Call(controller, "ClearSelection");
                        Press(controller, "EndTurnButton");
                        break;
                    case 11:
                        var turn = Field<TurnState>(controller, "_turnState");
                        if (turn.ActiveTeam != Team.Player || turn.TurnNumber < 2) return; // 적 턴이 끝나길 기다린다
                        var save = SaveSystem.TryRead(SavePath, out var error);
                        if (save == null || save.Turn.TurnNumber != turn.TurnNumber) throw new Exception($"autosave at turn {turn.TurnNumber} missing ({error ?? "turn " + save.Turn.TurnNumber})");
                        Debug.Log("[MobilePlayVerification] end turn / autosave PASS: turn " + turn.TurnNumber);
                        // 적이 한 턴 움직인 뒤의 판으로 메뉴 → 메인 화면(저장하고 시작 화면으로) → 이어하기.
                        _snapshot = Snapshot(controller);
                        Press(controller, "MenuButton");
                        Press(controller, PressMenuItem(controller, "UI.Menu.MainMenu"));
                        break;
                    case 12:
                        if (Field<EconomyWorld>(controller, "_econ") != null) throw new Exception("main menu did not leave the battle");
                        Press(controller, "ContinueButton");
                        break;
                    case 13:
                        string loaded = Snapshot(controller);
                        if (loaded != _snapshot) throw new Exception($"continue state differs:\n saved  {_snapshot}\n loaded {loaded}");
                        Debug.Log("[MobilePlayVerification] save/continue PASS: " + loaded.Split('\n')[0]);
                        Press(controller, "TechButton");
                        break;
                    case 14:
                        // 기술 트리: 한 손가락으로 끌면 나무가 따라오고(ScrollRect), 지도 카메라는 그대로다.
                        var tech = Field<TechTreeHud>(controller, "_techTreeHud");
                        if (!tech.IsOpen) throw new Exception("tech tree did not open");
                        _treeBefore = TreeContent(tech).anchoredPosition;
                        _focusBefore = Field<Vector3>(controller, "_cameraFocus");
                        // 배치모드 에디터는 창 포커스가 없어 EventSystem이 가상 터치를 UI로 보내지 않는다 — 나무 창 맨 위 물체(레이캐스트 결과)에
                        // 끌기 이벤트를 직접 보낸다. 받는 쪽(부모로 올라가 ScrollRect)과 나무를 옮길 수 있는지는 실제와 같다.
                        DragUi(TreePoint(tech), new Vector2(60f, 40f));
                        break;
                    case 15:
                    case 16:
                        break; // 관성 이동이 멈출 시간
                    case 17:
                        tech = Field<TechTreeHud>(controller, "_techTreeHud");
                        var moved = TreeContent(tech).anchoredPosition - _treeBefore;
                        if (moved.x <= 1f || moved.y <= 1f) throw new Exception("tech tree drag did not move the tree: " + moved);
                        if (Field<Vector3>(controller, "_cameraFocus") != _focusBefore) throw new Exception("map camera moved under the tech tree");
                        _sizeBefore = tech.Zoom;
                        Touch(1, TouchPhase.Began, TreePoint(tech) - new Vector2(30f, 0f));
                        Touch(2, TouchPhase.Began, TreePoint(tech) + new Vector2(30f, 0f));
                        break;
                    case 18:
                        tech = Field<TechTreeHud>(controller, "_techTreeHud");
                        Touch(1, TouchPhase.Moved, TreePoint(tech) - new Vector2(90f, 0f));
                        Touch(2, TouchPhase.Moved, TreePoint(tech) + new Vector2(90f, 0f));
                        break;
                    case 19:
                        tech = Field<TechTreeHud>(controller, "_techTreeHud");
                        Touch(1, TouchPhase.Ended, TreePoint(tech) - new Vector2(90f, 0f));
                        Touch(2, TouchPhase.Ended, TreePoint(tech) + new Vector2(90f, 0f));
                        break;
                    case 20:
                        tech = Field<TechTreeHud>(controller, "_techTreeHud");
                        if (tech.Zoom <= _sizeBefore) throw new Exception($"tech tree pinch did not zoom in ({_sizeBefore} -> {tech.Zoom})");
                        Debug.Log($"[MobilePlayVerification] tech tree drag/pinch PASS: zoom {_sizeBefore:F2} -> {tech.Zoom:F2}");
                        tech.Close();
                        Call(controller, "ReturnToSetup", true); // 결과 창·메뉴의 "새 게임"
                        break;
                    case 21:
                        Press(controller, "StartButton");
                        break;
                    case 22:
                        CheckQuickPlayStarted(controller, "new game");
                        Finish(!_failed, "title / tribe pick / drag pan / pinch zoom / tap select / end turn autosave / save-continue / tech tree drag+pinch / new game");
                        return;
                }
                _phase++;
                _nextTime = EditorApplication.timeSinceStartup + Step;
            }
            catch (Exception e) { Debug.LogException(e); Finish(false, e.Message); }
        }

        private static Vector2 _treeBefore;

        /// <summary>그 화면 점에서 시작해 delta만큼 끄는 UI 끌기(누름 → 시작 → 끌기 → 끝). 맨 위에 맞은 물체에서 처리할 곳을 부모로 찾아 올라간다(uGUI와 같다).</summary>
        private static void DragUi(Vector2 start, Vector2 delta)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = start, pressPosition = start, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            es.RaycastAll(data, hits);
            if (hits.Count == 0) throw new Exception("nothing under " + start);
            data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
            var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(hits[0].gameObject);
            if (handler == null) throw new Exception("no drag handler above " + hits[0].gameObject.name);
            UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.initializePotentialDrag);
            UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            data.position = start + delta;
            data.delta = delta;
            UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
        }

        private static RectTransform TreeContent(TechTreeHud tech) => (RectTransform)tech.transform.Find("Canvas/Safe/Tree/Content");

        /// <summary>기술 트리 나무 창 가운데의 화면 좌표(오버레이 캔버스라 카메라 없음).</summary>
        private static Vector2 TreePoint(TechTreeHud tech)
        {
            var viewport = (RectTransform)tech.transform.Find("Canvas/Safe/Tree");
            return RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center));
        }

        /// <summary>대화 상자 버튼은 모두 이름이 "Button"이라 글자로 찾는다 — 그 버튼에 고유 이름을 붙여 Press로 누를 수 있게 한다.</summary>
        private static string PressMenuItem(BattleController controller, string labelKey)
        {
            string label = LocalizationSystem.T(labelKey);
            var button = controller.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Button" && b.GetComponentInChildren<Text>().text == label);
            if (button == null) throw new Exception("menu item not found: " + label);
            button.name = labelKey;
            return labelKey;
        }

        /// <summary>저장·불러오기 비교용 요약: 턴, 별, 도시, 살아 있는 유닛(번호·자리·체력·이동 여부), 탐험한 칸 수.</summary>
        private static string Snapshot(BattleController controller)
        {
            var econ = Field<EconomyWorld>(controller, "_econ");
            var world = Field<EntityWorld>(controller, "_world");
            var grid = Field<GridWorld>(controller, "_grid");
            var turn = Field<TurnState>(controller, "_turnState");
            var sb = new StringBuilder();
            sb.Append($"turn {turn.TurnNumber} {turn.ActiveTeam} stars {econ.Resources[Team.Player].Stars}/{econ.Resources[Team.Enemy].Stars} cities {econ.Cities.Count} views {controller.GetComponentsInChildren<UnitView>().Length}\n");
            for (int id = 0; id < world.EntityCount; id++)
                if (UnitQueries.IsAlive(world, id))
                    sb.Append($"{id}:{world.Get<Team>(id)}@{world.Get<GridPosition>(id).Value} hp{world.Get<Hp>(id).Value} moved{world.Get<HasMoved>(id).Value};");
            int explored = 0;
            for (int i = 0; i < grid.Tiles.Length; i++) if ((grid.Tiles[i].ExploredMask & (1 << (int)Team.Player)) != 0) explored++;
            sb.Append(" explored " + explored);
            return sb.ToString();
        }

        private static void CheckQuickPlayStarted(BattleController controller, string when)
        {
            if (controller.GetComponentInChildren<SandboxHud>() != null) throw new Exception(when + ": sandbox HUD should not exist");
            var title = Field<TitleHud>(controller, "_titleHud");
            if (title != null && title.IsOpen) throw new Exception(when + ": title screen still open");
            var econ = Field<EconomyWorld>(controller, "_econ");
            if (econ == null) throw new Exception(when + ": battle did not start");
            foreach (var team in CitySystem.Teams)
            {
                if (CitySystem.FindCapital(econ, team) < 0) throw new Exception($"{when}: {team} capital missing");
                if (Field<Dictionary<Team, int>>(controller, "_tribeByTeam")[team] < 0) throw new Exception($"{when}: {team} tribe not picked");
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
