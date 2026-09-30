using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 과업(위키 Monuments의 Task) 진행 갱신과 달성 판정 — 달성한 과업마다 기념물을 한 번 무료로 지을 수 있다
    /// (TileImprovementSystem이 CanBuildMonument를 조회). 순수 함수형, 값은 EconomyWorld.Tasks(TaskProgressData)에 있다.
    /// 과업 표는 Data/TaskDefinition.cs.
    /// </summary>
    public static class TaskSystem
    {
        /// <summary>팀별 진행 값이 없으면 만든다(전투 시작 시 1회).</summary>
        public static void Init(EconomyWorld econ)
        {
            foreach (var team in CitySystem.Teams)
                if (!econ.Tasks.ContainsKey(team)) econ.Tasks[team] = new TaskProgressData();
        }

        public static TaskInfo? Find(string taskId)
        {
            foreach (var t in TaskDefinition.All)
                if (t.Id == taskId) return t;
            return null;
        }

        /// <summary>team이 이번 턴 공격했다(평화주의 과업 초기화용). 반격은 공격이 아니다(위키).</summary>
        public static void RecordAttack(EconomyWorld econ, Team team)
        {
            if (econ != null && econ.Tasks.TryGetValue(team, out var t)) t.AttackedThisTurn = true;
        }

        /// <summary>전투 전후로 살아있던 유닛을 비교해, 죽은 유닛마다 상대 팀의 처치 수를 올린다(반격/스플래시/자폭 포함).</summary>
        public static void RecordDeaths(EconomyWorld econ, EntityWorld world, HashSet<int> aliveBefore)
        {
            if (econ == null || aliveBefore == null) return;
            foreach (var id in aliveBefore)
            {
                if (UnitQueries.IsAlive(world, id)) continue;
                var dead = world.Get<Team>(id);
                foreach (var team in CitySystem.Teams)
                    if (team != dead && econ.Tasks.TryGetValue(team, out var t)) t.Kills++;
            }
        }

        public static HashSet<int> SnapshotAlive(EntityWorld world)
        {
            var set = new HashSet<int>();
            for (int i = 0; i < world.EntityCount; i++)
                if (UnitQueries.IsAlive(world, i)) set.Add(i);
            return set;
        }

        /// <summary>team의 턴이 끝날 때: 이번 턴 공격하지 않았으면 연속 비공격 턴 +1, 공격했으면 0.</summary>
        public static void EndTurn(EconomyWorld econ, Team team)
        {
            if (econ == null || !econ.Tasks.TryGetValue(team, out var t)) return;
            t.TurnsWithoutAttack = t.AttackedThisTurn ? 0 : t.TurnsWithoutAttack + 1;
            t.AttackedThisTurn = false;
        }

        public static bool IsUnlocked(EconomyWorld econ, Team team, TaskInfo task) =>
            string.IsNullOrEmpty(task.UnlockKey) || TechSystem.HasUnlock(econ.TechNodes, econ.Tech[team], task.UnlockKey);

        /// <summary>지금 상태가 과업 조건을 만족하는지(기술 해금 여부는 따로 — IsUnlocked).</summary>
        public static bool IsMet(GridWorld grid, EconomyWorld econ, Team team, TaskInfo task)
        {
            var p = econ.Tasks[team];
            switch (task.Kind)
            {
                case TaskKind.TurnsWithoutAttack: return p.TurnsWithoutAttack >= task.Threshold;
                case TaskKind.GoldHeld: return econ.Resources[team].Gold >= task.Threshold;
                case TaskKind.Kills: return p.Kills >= task.Threshold;
                case TaskKind.AllLighthouses:
                {
                    if (!grid.FogEnabled) return false;
                    int total = VisionSystem.LighthousePositions(grid).Count;
                    return total > 0 && p.LighthousesFound.Count >= total;
                }
                case TaskKind.ConnectedCities:
                {
                    int n = 0;
                    foreach (var c in econ.Cities) if (c.Owner == team && c.ConnectedToCapital) n++;
                    return n >= task.Threshold;
                }
                case TaskKind.CityLevel:
                    foreach (var c in econ.Cities) if (c.Owner == team && c.Level >= task.Threshold) return true;
                    return false;
                case TaskKind.AllTech:
                    foreach (var n in econ.TechNodes) if (!TechSystem.IsUnlocked(econ.Tech[team], n.Id)) return false;
                    return econ.TechNodes.Count > 0;
            }
            return false;
        }

        /// <summary>모든 팀의 과업 달성을 확인해 Completed에 넣고 log에 남긴다. 상태가 바뀌는 곳(턴 시작/끝, 경제 행동,
        /// 전투) 뒤에 부른다.</summary>
        public static void Refresh(GridWorld grid, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            if (econ == null) return;
            foreach (var team in CitySystem.Teams)
            {
                if (!econ.Tasks.TryGetValue(team, out var p)) continue;
                foreach (var task in TaskDefinition.All)
                {
                    if (p.Completed.Contains(task.Id) || !IsUnlocked(econ, team, task) || !IsMet(grid, econ, team, task)) continue;
                    p.Completed.Add(task.Id);
                    log?.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Task, Subject = $"{task.Name} ({MonumentName(task.Id)} 건설 가능)", CityIndex = -1 });
                }
            }
        }

        public static bool CanBuildMonument(EconomyWorld econ, Team team, string taskId) =>
            econ.Tasks.TryGetValue(team, out var p) && p.Completed.Contains(taskId) && !p.MonumentsBuilt.Contains(taskId);

        public static void MarkMonumentBuilt(EconomyWorld econ, Team team, string taskId)
        {
            if (econ.Tasks.TryGetValue(team, out var p)) p.MonumentsBuilt.Add(taskId);
        }

        public static string MonumentName(string taskId)
        {
            foreach (var b in BuildingDefinition.All)
                if (b.TaskId == taskId) return b.Name;
            return taskId;
        }

        /// <summary>HUD용 진행 값(예: 살육자 3/10). 문장이 아니라 숫자로 돌려주고, 진행 링/칸 게이지로 그리는 것은 View가 한다.</summary>
        public static TaskProgress Progress(GridWorld grid, EconomyWorld econ, Team team, TaskInfo task)
        {
            var p = econ.Tasks[team];
            var result = new TaskProgress { Target = task.Threshold };
            switch (task.Kind)
            {
                case TaskKind.TurnsWithoutAttack: result.Current = p.TurnsWithoutAttack; break;
                case TaskKind.GoldHeld: result.Current = econ.Resources[team].Gold; break;
                case TaskKind.Kills: result.Current = p.Kills; break;
                case TaskKind.AllLighthouses:
                    result.Current = p.LighthousesFound.Count;
                    result.Target = VisionSystem.LighthousePositions(grid).Count;
                    break;
                case TaskKind.ConnectedCities:
                    foreach (var c in econ.Cities) if (c.Owner == team && c.ConnectedToCapital) result.Current++;
                    break;
                case TaskKind.CityLevel:
                    foreach (var c in econ.Cities) if (c.Owner == team) result.Current = Mathf.Max(result.Current, c.Level);
                    break;
                case TaskKind.AllTech:
                    result.Current = econ.Tech[team].Unlocked.Count;
                    result.Target = econ.TechNodes.Count;
                    break;
            }
            result.Done = p.Completed.Contains(task.Id);
            result.MonumentBuilt = result.Done && p.MonumentsBuilt.Contains(task.Id);
            if (result.Done) result.Current = Mathf.Max(result.Current, result.Target);
            return result;
        }
    }
}
