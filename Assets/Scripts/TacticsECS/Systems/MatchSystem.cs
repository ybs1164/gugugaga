using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 화면 없이 빠른 한 판을 만들고 AI끼리 턴을 진행한다 — BattleController의 빠른 한 판 시작(StartQuickPlay → HandleGenerateTerrain →
    /// InitEconomy)과 AI 턴(HandleTurnStart → RunAiTurnRoutine → EndTurn)에서 연출만 뺀 같은 순서. 시뮬레이터(tools/TacticsSim)·
    /// 학습·검증이 쓴다. 자체 상태 없음 — 판의 상태는 MatchState(Data)에만 있다.
    /// </summary>
    public static class MatchSystem
    {
        /// <summary>맵 타입 이름(MapTypes.csv Id) → 땅 모양. 표에 없는 이름은 Freeform.</summary>
        public static MapShapeMode ShapeMode(string mapType)
        {
            foreach (var row in GameTables.MapTypes)
                if (row.Id == mapType) return row.Shape;
            return MapShapeMode.Freeform;
        }

        private static TribeRow? Tribe(int index) => TribeSystem.IsValid(index) ? GameTables.Tribes[index] : (TribeRow?)null;

        /// <summary>지형 생성 → 수도·시작 조건 → 경제 시작까지 하고, 1턴 플레이어 차례(턴 시작 처리 전)로 둔다.</summary>
        public static MatchState Create(MatchSetup setup)
        {
            var grid = new GridWorld(setup.MapSize, setup.MapSize);
            var tribes = new Dictionary<Team, TribeRow?> { [Team.Player] = Tribe(setup.PlayerTribe), [Team.Enemy] = Tribe(setup.EnemyTribe) };
            var gods = new Dictionary<Team, int> { [Team.Player] = setup.PlayerGod, [Team.Enemy] = setup.EnemyGod };

            var regions = TribeSystem.RegionBiomes(CitySystem.Teams.Select(t => tribes[t]).ToList(), GameDataLoader.LoadTribeTerrain());
            var mode = ShapeMode(setup.MapType);
            var outline = TerrainGenerationSystem.PlanOutline(grid, regions.Count, setup.Seed, mode, setup.WaterRatio);
            var anchors = TerrainGenerationSystem.GenerateFromOutline(grid, regions, outline, setup.Seed);
            StructureGenerationSystem.Generate(grid, regions, anchors, setup.Seed, outline.Suburbs, outline.PlannedVillages, mode);

            var capitals = new Dictionary<Team, Vector2Int>();
            int startChanges = 0;
            for (int i = 0; i < CitySystem.Teams.Length && i < anchors.Length; i++)
            {
                var team = CitySystem.Teams[i];
                capitals[team] = anchors[i];
                var condition = tribes[team] != null ? TribeSystem.StartCondition(tribes[team].Value) : null;
                if (condition != null)
                    startChanges += StartConditionSystem.Apply(grid, anchors[i], condition.Value, GameTables.StartConditionRules, setup.MapType, setup.Seed + i);
            }
            if (startChanges > 0) TerrainGenerationSystem.ClassifyWaterDepth(grid, regions, anchors);

            var world = new EntityWorld();
            var econ = EconomySetupSystem.Create(TechGroupSystem.BuildTechNodes(), EconomySetupSystem.DefaultUnitRows(), gods, tribes);
            var state = new MatchState { Setup = setup, Grid = grid, World = world, Econ = econ };
            Spawn(state, EconomySetupSystem.Start(grid, world, econ, capitals, tribes));
            Refresh(state);
            state.Turn = TurnSystem.StartTurn(world, Team.Player, 1);
            return state;
        }

        /// <summary>지금 차례 팀의 한 턴(턴 시작 처리 → 경제 AI → 군사 AI → 턴 종료)을 진행한다. 판이 끝나면 Outcome이 정해진다.</summary>
        public static void PlayAiTurn(MatchState s)
        {
            if (IsOver(s)) return;
            var team = s.Turn.ActiveTeam;
            TurnSystem.StartEconomyTurn(s.Grid, s.World, s.Econ, team, s.Turn.TurnNumber);
            Refresh(s);
            if (CheckEnd(s)) return;
            GrowSystem.OnTurnStart(s.Grid, s.World, s.Econ, team);

            Spawn(s, EconomyAI.RunTurn(s.Grid, s.World, s.Econ, team));
            Refresh(s);
            if (CheckEnd(s)) return;
            EnemyAI.RunTurn(s.Grid, s.World, s.Econ, team, CitySystem.CaptureTargets(s.Grid, s.Econ, team));
            Refresh(s);
            if (CheckEnd(s)) return;

            AbilitySystem.ApplyTurnEndWait(s.Grid, s.World, team);
            TaskSystem.EndTurn(s.Econ, team);
            s.Turn = TurnSystem.EndTurn(s.World, s.Turn);
        }

        public static bool IsOver(MatchState s) => s.Outcome.Reason != BattleEndReason.None;

        private static bool CheckEnd(MatchState s)
        {
            s.Outcome = VictorySystem.Evaluate(s.World, s.Econ);
            return IsOver(s);
        }

        /// <summary>BattleController.RefreshEconomyViews의 상태 부분: 경제 사건을 모아 유닛 스폰을 처리하고 파생 상태를 맞춘다.</summary>
        private static void Refresh(MatchState s)
        {
            var log = new List<EconomyLogEntry>();
            EconomyRefreshSystem.CollectEvents(s.Grid, s.World, s.Econ, log);
            Spawn(s, log);
            EconomyRefreshSystem.RefreshState(s.Grid, s.World, s.Econ);
        }

        private static void Spawn(MatchState s, List<EconomyLogEntry> log)
        {
            foreach (var e in log)
                if (!string.IsNullOrEmpty(e.SpawnUnitId))
                    UnitFactorySystem.SpawnEconomyUnit(s.Grid, s.World, s.Econ, e.Team, e.SpawnUnitId, e.Position, e.CityIndex, e.SpawnVeteran, e.SpawnBoatId);
        }
    }
}
