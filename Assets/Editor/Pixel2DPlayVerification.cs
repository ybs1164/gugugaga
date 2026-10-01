using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>Batchmode play smoke test: scene startup, regeneration, economics, AI coroutine and restart.</summary>
    [InitializeOnLoad]
    public static class Pixel2DPlayVerification
    {
        private const string ActiveKey="Pixel2DPlay.Active";
        private static int _phase;
        private static double _nextTime, _deadline;
        private static bool _failed;
        static Pixel2DPlayVerification()
        {
            if(SessionState.GetBool(ActiveKey,false)) Resume();
        }
        public static void Run()
        {
            SessionState.SetBool(ActiveKey,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void Resume()
        {
            _deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=Tick;
            Application.logMessageReceived+=Log;
        }
        private static void Log(string text,string stack,LogType type)
        {
            if(type==LogType.Exception || type==LogType.Error) _failed=true;
        }
        private static void Call(BattleController controller,string name,params object[] args) =>
            typeof(BattleController).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,args);
        private static T Field<T>(BattleController controller,string name) =>
            (T)typeof(BattleController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>_deadline) { Finish(false,"timeout"); return; }
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<_nextTime) return;
            var controller=UnityEngine.Object.FindFirstObjectByType<BattleController>();
            if(controller==null || Field<GridWorld>(controller,"_grid")==null) return;
            try
            {
                switch(_phase)
                {
                    case 0:
                        Call(controller,"HandleSandboxLoad",Path.GetFullPath("SandboxUnits.csv"));
                        Call(controller,"HandleGenerateTerrain");
                        _nextTime=EditorApplication.timeSinceStartup+.5; _phase++;
                        break;
                    case 1:
                        Call(controller,"HandleSandboxStartBattle");
                        _nextTime=EditorApplication.timeSinceStartup+.5; _phase++;
                        break;
                    case 2:
                        if(controller.GetComponentsInChildren<UnitView>().Length<2) throw new Exception("starting units missing");
                        if(Field<EconomyWorld>(controller,"_econ")==null) throw new Exception("economy missing");
                        Call(controller,"EndTurn");
                        _nextTime=EditorApplication.timeSinceStartup+4; _phase++;
                        break;
                    case 3:
                        if(Field<TurnState>(controller,"_turnState").ActiveTeam!=Team.Player) return;
                        Call(controller,"HandleReturnToSetup");
                        _nextTime=EditorApplication.timeSinceStartup+.5; _phase++;
                        break;
                    case 4:
                        if(!Field<bool>(controller,"_placementActive")) throw new Exception("restart did not restore placement");
                        Finish(!_failed,"startup/map generation/start battle/AI turn/restart");
                        break;
                }
            }
            catch(Exception e) { Debug.LogException(e); Finish(false,e.Message); }
        }
        private static void Finish(bool pass,string detail)
        {
            SessionState.SetBool(ActiveKey,false);
            EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
            Debug.Log("[Pixel2DPlayVerification] "+(pass?"ALL PASS: ":"FAIL: ")+detail);
            EditorApplication.Exit(pass?0:1);
        }
    }
}
