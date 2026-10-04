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
                        WikiParityVerification.Verify();
                        foreach (var key in GameDataLoader.SandboxCsvPaths)
                        {
                            var selected = Path.GetFullPath("Temp/Selected_" + key.Replace('/', '_') + ".csv");
                            File.WriteAllText(selected, GameDataLoader.Read(key, null, TableSource.Resources));
                            Call(controller,"HandleSandboxCsvSelected", key, selected);
                        }
                        var hud = controller.GetComponentInChildren<SandboxHud>();
                        if(hud.transform.Find("Canvas/Palette") != null) throw new Exception("placement palette still exists");
                        if(hud.transform.Find("Canvas/CsvFiles/Viewport/Content").childCount != GameDataLoader.SandboxCsvPaths.Length)
                            throw new Exception("CSV selector count mismatch");
                        var invalid = Path.GetFullPath("Temp/InvalidSpriteCatalog.csv");
                        File.WriteAllText(invalid,"VisualId,Sheet,Scale\nBroken,Pattern/Solid,0\n");
                        Call(controller,"HandleSandboxCsvSelected","Pixel2D/SpriteCatalog",invalid);
                        PixelSpriteCatalog.Validate(); // 실패한 선택 뒤 이전 유효한 카탈로그가 유지되어야 한다.
                        var changed = Path.GetFullPath("Temp/Selected_Pixel2D_SpriteCatalog.csv");
                        File.WriteAllText(changed,"VisualId,Sheet,Scale\nBroken,Pattern/Solid,0\n");
                        Call(controller,"ReloadSandboxCsv");
                        PixelSpriteCatalog.Validate(); // 다시 읽기 중 잘못된 카탈로그는 기본 파일로 복구한다.
                        Call(controller,"HandleTribeSelected",Team.Player,6);
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
                        var econ = Field<EconomyWorld>(controller,"_econ");
                        var grid = Field<GridWorld>(controller,"_grid");
                        int capital = CitySystem.FindCapital(econ,Team.Player);
                        if(capital < 0 || econ.Cities[capital].Level != 3 || econ.Cities[capital].HasWorkshop || econ.Cities[capital].HasWall)
                            throw new Exception("Luxidoor play-mode starting capital mismatch");
                        econ.Tech[Team.Player].Unlocked.Add("Diplomacy");
                        var resources = econ.Resources[Team.Player]; resources.Stars += 10; econ.Resources[Team.Player] = resources;
                        int foreign = CitySystem.FindCapital(econ,Team.Enemy);
                        if(foreign < 0) throw new Exception("foreign capital missing");
                        VisionSystem.Reveal(grid,Team.Player,econ.Cities[foreign].Position,0);
                        Call(controller,"HandleTileOption",econ.Cities[foreign].Position,"Embassy");
                        if(!econ.Cities[foreign].HasEmbassy || econ.Embassies.Count != 1) throw new Exception("embassy controller/UI path failed");
                        Debug.Log("[Pixel2DPlayVerification] Luxidoor capital and Embassy controller path PASS");
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
