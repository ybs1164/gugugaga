using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 규칙 기반 AI(한 팀의 경제 턴 일부 — EconomyAI.RunTurn이 부른다). 자체 상태 없음. 신(Gods.csv)을 고른 팀만 신앙을 쓴다.
    ///   1. 대신전(RunEarly — 건설·훈련보다 먼저): 과업을 달성했고 지을 자리가 있으면 짓는다.
    ///   2. 신전: 신전이 모자라면 수도 가까이에 짓는다(기도와 신 패시브가 신전 곁에서 이뤄지므로).
    ///   3. 신앙 펀치: 위협받거나, 대신전 과업을 이미 채웠거나, 신앙이 가득 차 낭비될 때만 쓴다. 펀치의 효과(FaithEffects)마다 점수를 매겨 —
    ///      적 피해·아군 회복·강림(보이는 적 근처의 센 유닛) — 합이 가장 큰 펀치·대상 하나를 고른다.
    ///   4. 신화 유닛 액티브: 사거리 안에서 가장 많은 적에게 닿는 칸에 쓴다.
    ///   5. 기도: 신전 곁에서 적이 가까이 없는 유닛은 기도한다(행동 종료 — 이후 EnemyAI가 건너뛴다).
    /// 규칙: docs/spec/game/ai.md
    /// </summary>
    public static class FaithAI
    {
        /// <summary>건설·훈련보다 먼저(별을 빼앗기기 전에): 대신전을 지을 수 있으면 짓는다.</summary>
        public static void RunEarly(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            if (econ == null || TemplePassiveSystem.GodOf(econ, team) == null) return;
            BuildGreatTemple(grid, world, econ, team, log);
        }

        public static void RunTurn(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            if (econ == null || TemplePassiveSystem.GodOf(econ, team) == null) return;
            BuildTemple(grid, world, econ, team, log);
            UsePunches(grid, world, econ, team, log);
            UseMythicActive(grid, world, econ, team, log);
            Pray(grid, world, econ, team);
        }

        private static bool TaskDone(EconomyWorld econ, Team team) =>
            econ.Tasks.TryGetValue(team, out var progress) && progress.Completed.Contains(GreatTempleSystem.TaskId);

        private static int TaskThreshold() => TaskSystem.Find(GreatTempleSystem.TaskId)?.Threshold ?? 0;

        private static void AddLog(List<EconomyLogEntry> log, Team team, EconomyLogKind kind, string subject) =>
            log?.Add(new EconomyLogEntry { Team = team, Kind = kind, Subject = subject, CityIndex = -1 });

        // ---------- 연구 ----------

        /// <summary>신앙을 쓰려면 먼저 열어야 하는 기술을 향한 다음 한 걸음(없으면 null). EconomyAI.Research가 가장 싼 기술보다 먼저 고른다.
        /// 1) 신전을 지을 수 있는 기술 → 2) 대신전 과업을 여는 기술. 목표 기술이 깊으면 아직 안 연 선행 기술 중 가장 얕은 것부터 연구한다.</summary>
        public static string PreferredTech(EconomyWorld econ, Team team)
        {
            if (econ == null || TemplePassiveSystem.GodOf(econ, team) == null) return null;
            var tech = econ.Tech[team];

            var templeKeys = new List<string>();
            foreach (var b in BuildingDefinition.All)
                if (b.FaithCap > 0 && !string.IsNullOrEmpty(b.UnlockKey)) templeKeys.Add(b.UnlockKey);
            bool anyTemple = false;
            foreach (var key in templeKeys) if (TechSystem.HasUnlock(econ.TechNodes, tech, key)) anyTemple = true;
            if (!anyTemple && templeKeys.Count > 0) return NextStepToward(econ, team, templeKeys);

            var task = TaskSystem.Find(GreatTempleSystem.TaskId);
            if (task != null && !string.IsNullOrEmpty(task.Value.UnlockKey) && !TaskDone(econ, team) && !TechSystem.HasUnlock(econ.TechNodes, tech, task.Value.UnlockKey))
                return NextStepToward(econ, team, new List<string> { task.Value.UnlockKey });
            return null;
        }

        /// <summary>keys 중 하나를 여는 가장 얕은(티어가 낮은) 기술을 목표로, 아직 열지 않은 가장 이른 선행 기술(없으면 목표 자신)을 돌려준다.</summary>
        private static string NextStepToward(EconomyWorld econ, Team team, List<string> keys)
        {
            var tech = econ.Tech[team];
            TechNodeData? goal = null;
            foreach (var node in econ.TechNodes)
            {
                if (node.Unlocks == null || tech.Unlocked.Contains(node.Id) || (tech.Allowed != null && !tech.Allowed.Contains(node.Id))) continue;
                bool match = false;
                foreach (var k in node.Unlocks) if (keys.Contains(k)) match = true;
                if (match && (goal == null || node.Tier < goal.Value.Tier)) goal = node;
            }
            if (goal == null) return null;

            // 목표에서 거슬러 올라가 아직 열지 않은 가장 이른 기술을 찾는다.
            var step = goal.Value;
            for (int guard = 0; guard < 4 && !string.IsNullOrEmpty(step.ParentId); guard++)
            {
                var parent = TechSystem.Find(econ.TechNodes, step.ParentId);
                if (parent == null || tech.Unlocked.Contains(parent.Value.Id)) break;
                step = parent.Value;
            }
            return step.Id;
        }

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

        /// <summary>대신전을 지을 수 있는 상태(과업 달성, 아직 없음, 자리 있음)인데 별이 모자라면 그 비용만큼 다른 지출(연구·건설·훈련)에서 남겨 둔다.
        /// 안 그러면 AI가 별을 매 턴 다 써서 대신전 비용이 영영 모이지 않는다.</summary>
        public static int StarReserve(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var info = GreatTempleSystem.Info();
            if (info == null || econ == null || TemplePassiveSystem.GodOf(econ, team) == null) return 0;
            if (GreatTempleSystem.IndexOf(econ, team) >= 0 || !TaskDone(econ, team)) return 0;
            return GreatTempleSystem.AnySite(grid, world, econ, team) != null ? info.Value.Cost : 0;
        }

        /// <summary>수도 2칸 안의 자기 영토에서 가장 싼 신전 하나(FaithCap이 있는 건물)를 짓는다. 별이 조금 남을 때만.</summary>
        private static void BuildTemple(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, List<EconomyLogEntry> log)
        {
            // 기본 AI.FaithBaseTemples개. 대신전 과업 기준치가 신앙 최대치보다 크면(채울 수 없으면) AI.FaithMaxTemples까지 더 지어 최대치를 늘린다.
            int have = TemplePassiveSystem.Temples(grid, team).Count;
            bool needCap = !TaskDone(econ, team) && econ.Resources[team].MaxFaith < TaskThreshold();
            if (have >= (needCap ? GameRules.AI.FaithMaxTemples : GameRules.AI.FaithBaseTemples)) return;
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
            // 아직 신화 유닛이 없고 강림을 쓸 수 있는 신이면 그 비용만큼 신앙을 남긴다 — 싼 펀치에 다 써 버려 강림까지 못 모으는 일을 막는다.
            int descentCost = 0;
            if (MythicSystem.FindMythic(world, team) < 0)
                foreach (var punch in punches)
                    if (FaithPunchSystem.Has(punch, FaithEffectKind.Descend)) descentCost = punch.Cost;
            for (int n = 0; n < GameRules.AI.FaithMaxPunchesPerTurn; n++)
            {
                var res = econ.Resources[team];
                bool taskDone = TaskDone(econ, team);
                if (!(threatened || taskDone || res.Faith >= res.MaxFaith)) return;

                FaithPunchRow? best = null;
                Vector2Int bestTarget = default;
                float bestScore = 0f;
                foreach (var punch in punches)
                {
                    if (punch.Cost > res.Faith) continue;
                    if (descentCost > 0 && !FaithPunchSystem.Has(punch, FaithEffectKind.Descend) && res.Faith - punch.Cost < descentCost) continue;
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

        /// <summary>후보 대상 칸: 적에게 피해를 주는 효과가 있으면 보이는 적 유닛 칸, 아군 회복·강림이 있으면 자기 유닛 칸. 칸 효과(구조물·지형)만 있는 펀치는 쓰지 않는다.</summary>
        private static List<Vector2Int> Candidates(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch)
        {
            var tiles = new List<Vector2Int>();
            bool enemies = false, allies = false;
            foreach (var e in FaithPunchSystem.Effects(punch))
            {
                if (e.Effect == FaithEffectKind.Damage && e.Side == FaithSide.Enemy) enemies = true;
                if ((e.Effect == FaithEffectKind.Heal && e.Side == FaithSide.Ally) || e.Effect == FaithEffectKind.Descend) allies = true;
            }
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i)) continue;
                bool own = world.Get<Team>(i) == team;
                if (own ? !allies : !enemies) continue;
                var pos = world.Get<GridPosition>(i).Value;
                if (!own && !VisionSystem.IsExplored(grid, team, pos)) continue;
                tiles.Add(pos);
            }
            return tiles;
        }

        /// <summary>대상 칸의 점수 = 효과마다 점수의 합: 적 피해 = 깎는 체력(처치는 가산), 아군 회복 = 회복량(크기만큼 못 채우면 0),
        /// 강림 = 보이는 적이 가까운 센 유닛(첫 신화 유닛이면 AI.FaithDescendBonus 가산). 아군 피해·적 회복은 그만큼 뺀다.</summary>
        private static float Score(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int target)
        {
            float score = 0f;
            foreach (var e in FaithPunchSystem.Effects(punch))
            {
                int amount = FaithPunchSystem.Amount(e);
                float part = 0f;
                switch (e.Effect)
                {
                    case FaithEffectKind.Damage:
                        foreach (int id in FaithEffectSystem.Units(grid, world, team, e, target, punch.Range))
                        {
                            int hp = world.Get<Hp>(id).Value;
                            part += Mathf.Min(amount, hp - e.MinHp) + (e.MinHp <= 0 && hp <= amount ? 5 : 0);
                        }
                        break;
                    case FaithEffectKind.Heal:
                        foreach (int id in FaithEffectSystem.Units(grid, world, team, e, target, punch.Range))
                            part += Mathf.Min(amount, world.Get<MaxHp>(id).Value - world.Get<Hp>(id).Value);
                        if (part < amount) part = 0f;
                        break;
                    case FaithEffectKind.Descend:
                        int unit = grid.GetOccupant(target);
                        if (unit == TileData.NoOccupant || world.Get<Team>(unit) != team || MythicSystem.IsMythic(world, unit) ||
                            !EnemyNear(grid, world, team, target, GameRules.AI.FaithPrayerSafeDistance + 2)) break;
                        part = 1f + world.Get<Hp>(unit).Value * 0.1f + world.Get<Attack>(unit).Value * 0.5f;
                        if (MythicSystem.FindMythic(world, team) < 0) part += GameRules.AI.FaithDescendBonus; // 첫 신화 유닛은 판 내내 남는다
                        break;
                }
                bool helpsUs = (e.Effect == FaithEffectKind.Damage) == (e.Side == FaithSide.Enemy) || e.Effect == FaithEffectKind.Descend;
                score += helpsUs ? part : -part;
            }
            return score;
        }

        // ---------- 신화 유닛 ----------

        /// <summary>unit이 지금 자리에서 칠 수 있는 적 중 가장 큰 공격 가치 = 주 피해 + 스플래시 피해 합(Score와 같은 체력 단위). 칠 적이 없으면 0.</summary>
        private static float AttackValue(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int unit)
        {
            if (UnitActionQueries.Find<AttackAction>(world, unit) == null) return 0f;
            bool splash = UnitActionQueries.Find<SplashAction>(world, unit) != null;
            float best = 0f;
            for (int target = 0; target < world.EntityCount; target++)
            {
                if (!UnitQueries.IsAlive(world, target) || world.Get<Team>(target) == team || !CombatSystem.IsInAttackRange(world, unit, target)) continue;
                if (DiplomacySystem.HasPeaceTreaty(econ, team, world.Get<Team>(target)) || StealthSystem.IsHiddenFrom(world, target, team)) continue;
                float value = CombatSystem.CalculateDamage(world, unit, target);
                var center = world.Get<GridPosition>(target).Value;
                if (splash)
                    for (int other = 0; other < world.EntityCount; other++)
                    {
                        if (other == target || !UnitQueries.IsAlive(world, other) || world.Get<Team>(other) == team) continue;
                        var p = world.Get<GridPosition>(other).Value;
                        if (Mathf.Max(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) == 1) value += CombatSystem.CalculateSplashDamage(world, unit, other);
                    }
                best = Mathf.Max(best, value);
            }
            return best;
        }

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
            // 액티브는 이번 턴 행동을 끝낸다 — 지금 칠 수 있는 적에게 일반 공격(스플래시 포함)이 그 이상이면 공격하도록 아껴 둔다.
            if (bestScore <= AttackValue(grid, world, econ, team, mythic)) return;
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
                if (EnemyNear(grid, world, team, world.Get<GridPosition>(id).Value, GameRules.AI.FaithPrayerSafeDistance)) continue;
                PrayerSystem.Pray(grid, world, id);
            }
        }
    }
}
