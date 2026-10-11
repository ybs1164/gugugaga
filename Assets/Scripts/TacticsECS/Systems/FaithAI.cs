using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 규칙 기반 AI(한 팀의 경제 턴 일부 — EconomyAI.RunTurn이 부른다). 자체 상태 없음. 신(Gods.csv)을 고른 팀만 신앙을 쓴다.
    ///   1. 대신전: 과업을 달성했고 지을 자리가 있으면 짓는다.
    ///   2. 신전: 신전이 모자라면 수도 가까이에 짓는다(기도와 신 패시브가 신전 곁에서 이뤄지므로).
    ///   3. 신앙 펀치: 위협받거나, 대신전 과업을 이미 채웠거나, 신앙이 가득 차 낭비될 때만 쓴다. 천벌(피해를 가장 많이 주는 칸)·축복(회복량이 가장 큰 칸)·
    ///      강림(보이는 적 근처의 가장 센 유닛)을 후보 대상마다 점수를 매겨 가장 좋은 하나를 고른다.
    ///   4. 신화 유닛 액티브: 사거리 안에서 가장 많은 적에게 닿는 칸에 쓴다.
    ///   5. 기도: 신전 곁에서 적이 가까이 없는 유닛은 기도한다(행동 종료 — 이후 EnemyAI가 건너뛴다).
    /// 규칙: docs/spec/game/ai.md
    /// </summary>
    public static class FaithAI
    {
        /// <summary>이 거리(맨해튼) 안에 보이는 적이 있으면 그 유닛은 기도하지 않고 싸운다.</summary>
        private const int PrayerSafeDistance = 4;
        private const int MaxTemples = 2;
        private const int MaxPunchesPerTurn = 2;

        public static void RunTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            if (econ == null || TemplePassiveSystem.GodOf(econ, team) == null) return;
            BuildGreatTemple(grid, world, econ, team, log);
            BuildTemple(grid, world, econ, team, log);
            UsePunches(grid, world, econ, team, log);
            UseMythicActive(grid, world, econ, team, log);
            Pray(grid, world, econ, team);
        }

        private static void AddLog(List<EconomyLogEntry> log, Team team, EconomyLogKind kind, string subject) =>
            log?.Add(new EconomyLogEntry { Team = team, Kind = kind, Subject = subject, CityIndex = -1 });

        // ---------- 건설 ----------

        private static void BuildGreatTemple(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            var info = GreatTempleSystem.Info();
            if (info == null || GreatTempleSystem.IndexOf(econ, team) >= 0) return;
            if (!econ.Tasks.TryGetValue(team, out var progress) || !progress.Completed.Contains(GreatTempleSystem.TaskId)) return;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (grid.GetTile(p).OwnerTeam != (int)team || GreatTempleSystem.CanBuild(grid, world, econ, team, p) != null) continue;
                if (GreatTempleSystem.Build(grid, world, econ, team, p, econ.Turn)) AddLog(log, team, EconomyLogKind.Build, info.Value.Name);
                return;
            }
        }

        /// <summary>수도 2칸 안의 자기 영토에서 가장 싼 신전 하나(FaithCap이 있는 건물)를 짓는다. 별이 조금 남을 때만.</summary>
        private static void BuildTemple(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            if (TemplePassiveSystem.Temples(grid, team).Count >= MaxTemples) return;
            int capital = CitySystem.FindCapital(econ, team);
            if (capital < 0) return;
            var center = econ.Cities[capital].Position;

            Vector2Int? bestPos = null;
            string bestId = null;
            int bestCost = int.MaxValue;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                var p = center + new Vector2Int(dx, dy);
                if (!grid.InBounds(p) || grid.GetTile(p).OwnerTeam != (int)team) continue;
                foreach (var option in TileImprovementSystem.GetOptions(grid, econ, team, p))
                {
                    if (!option.IsBuilding || !option.Enabled) continue;
                    var building = TileImprovementSystem.FindBuilding(option.Id);
                    if (building == null || building.Value.FaithCap <= 0 || option.Cost >= bestCost) continue;
                    bestCost = option.Cost; bestPos = p; bestId = option.Id;
                }
            }
            if (bestPos == null || econ.Resources[team].Stars < bestCost + 2) return;
            var logBuffer = new List<EconomyLogEntry>();
            if (TileImprovementSystem.Execute(grid, econ, team, bestPos.Value, bestId, logBuffer)) log?.AddRange(logBuffer);
        }

        // ---------- 신앙 펀치 ----------

        private static void UsePunches(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            var punches = FaithPunchSystem.Available(econ, team);
            if (punches.Count == 0) return;
            bool threatened = EconomyAI.IsThreatened(grid, world, econ, team);
            for (int n = 0; n < MaxPunchesPerTurn; n++)
            {
                var res = econ.Resources[team];
                bool taskDone = econ.Tasks.TryGetValue(team, out var progress) && progress.Completed.Contains(GreatTempleSystem.TaskId);
                if (!(threatened || taskDone || res.Faith >= res.MaxFaith)) return;

                FaithPunchRow? best = null;
                Vector2Int bestTarget = default;
                float bestScore = 0f;
                foreach (var punch in punches)
                {
                    if (punch.Cost > res.Faith) continue;
                    foreach (var target in Candidates(grid, world, team, punch))
                    {
                        if (FaithPunchSystem.Check(grid, world, econ, team, punch, target) != null) continue;
                        float score = Score(grid, world, team, punch, target);
                        if (score > bestScore) { bestScore = score; best = punch; bestTarget = target; }
                    }
                }
                if (best == null) return;

                var result = FaithPunchSystem.Apply(grid, world, econ, team, best.Value, bestTarget);
                if (!result.Success) return;
                AddLog(log, team, EconomyLogKind.Action, LocalizationSystem.F("UI.FaithPunch.Log", best.Value.Name, result.UnitIds.Count + result.Tiles.Count));
            }
        }

        /// <summary>펀치 유형별 후보 대상 칸: 천벌 = 보이는 적 유닛 칸, 축복 = 다친 아군 칸, 강림 = 자기 유닛 칸. 그 밖의 유형은 쓰지 않는다.</summary>
        private static List<Vector2Int> Candidates(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch)
        {
            var tiles = new List<Vector2Int>();
            if (punch.Kind != FaithPunchKind.Strike && punch.Kind != FaithPunchKind.Boost && punch.Kind != FaithPunchKind.Descend) return tiles;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i)) continue;
                bool own = world.Get<Team>(i) == team;
                if ((punch.Kind == FaithPunchKind.Strike) == own) continue;
                var pos = world.Get<GridPosition>(i).Value;
                if (!own && !VisionSystem.IsExplored(grid, team, pos)) continue;
                tiles.Add(pos);
            }
            return tiles;
        }

        /// <summary>대상 칸의 점수: 천벌 = 피해(처치는 가산), 축복 = 회복량, 강림 = 보이는 적이 가까운 센 유닛.</summary>
        private static float Score(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int target)
        {
            float score = 0f;
            switch (punch.Kind)
            {
                case FaithPunchKind.Strike:
                    foreach (int id in UnitsAround(grid, world, punch, target))
                    {
                        if (world.Get<Team>(id) == team) continue;
                        int hp = world.Get<Hp>(id).Value;
                        score += Mathf.Min(punch.Value, hp) + (hp <= punch.Value ? 5 : 0);
                    }
                    return score;
                case FaithPunchKind.Boost:
                    foreach (int id in UnitsAround(grid, world, punch, target))
                        if (world.Get<Team>(id) == team)
                            score += Mathf.Min(punch.Value, world.Get<MaxHp>(id).Value - world.Get<Hp>(id).Value);
                    return score >= punch.Value ? score : 0f;
                case FaithPunchKind.Descend:
                    int unit = grid.GetOccupant(target);
                    if (unit == TileData.NoOccupant || MythicSystem.IsMythic(world, unit) || !EnemyNear(grid, world, team, target, PrayerSafeDistance + 2)) return 0f;
                    return 1f + world.Get<Hp>(unit).Value * 0.1f + world.Get<Attack>(unit).Value * 0.5f;
                default:
                    return 0f;
            }
        }

        private static List<int> UnitsAround(GridWorld grid, EntityWorld world, FaithPunchRow punch, Vector2Int center)
        {
            var ids = new List<int>();
            foreach (var p in FaithPunchSystem.Area(grid, punch, center))
            {
                int id = grid.GetOccupant(p);
                if (id != TileData.NoOccupant && UnitQueries.IsAlive(world, id)) ids.Add(id);
            }
            return ids;
        }

        // ---------- 신화 유닛 ----------

        private static void UseMythicActive(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            int mythic = MythicSystem.FindMythic(world, team);
            if (mythic < 0 || !MythicSystem.CanUseActive(world, mythic)) return;
            var row = GameTables.MythicUnits[world.Get<Mythic>(mythic).MythicIndex];
            var punch = MythicSystem.ActivePunchRow(row);

            Vector2Int? best = null;
            float bestScore = 0f;
            foreach (var target in Candidates(grid, world, team, punch))
            {
                if (MythicSystem.CheckActive(grid, world, econ, mythic, target) != null) continue;
                float score = Score(grid, world, team, punch, target);
                if (score > bestScore) { bestScore = score; best = target; }
            }
            if (best == null) return;
            var result = MythicSystem.UseActive(grid, world, econ, mythic, best.Value);
            if (result.Success) AddLog(log, team, EconomyLogKind.Action, LocalizationSystem.F("UI.FaithPunch.Log", punch.Name, result.UnitIds.Count + result.Tiles.Count));
        }

        // ---------- 기도 ----------

        private static bool EnemyNear(GridWorld grid, EntityWorld world, Team team, Vector2Int pos, int distance)
        {
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) == team) continue;
                var p = world.Get<GridPosition>(i).Value;
                if (VisionSystem.IsExplored(grid, team, p) && PathfindingSystem.Distance(pos, p) <= distance) return true;
            }
            return false;
        }

        private static void Pray(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            for (int id = 0; id < world.EntityCount; id++)
            {
                if (world.Get<Team>(id) != team || MythicSystem.IsMythic(world, id)) continue;
                if (!PrayerSystem.CanPray(grid, world, id)) continue;
                if (EnemyNear(grid, world, team, world.Get<GridPosition>(id).Value, PrayerSafeDistance)) continue;
                PrayerSystem.Pray(grid, world, id);
            }
        }
    }
}
