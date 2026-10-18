using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 팀이 고른 신(Gods.csv)의 신전 패시브(TemplePassives.csv → FaithEffects.csv). 자체 상태는 없다 — 신전(FaithCap이 있는 건물)과 기도 중인 유닛 수에서 매번 다시 계산한다.
    /// 효과 하나의 크기 = Amount + PerPrayer × 그 신전 곁에서 기도 중인 유닛 수(0 미만이면 0). 대상은 신전에서 패시브 Radius 안. 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class TemplePassiveSystem
    {
        public static GodRow? GodOf(EconomyWorld econ, Team team)
        {
            if (econ == null || !econ.GodIds.TryGetValue(team, out int i) || i < 0 || i >= GameTables.Gods.Length) return null;
            return GameTables.Gods[i];
        }

        public static List<TemplePassiveRow> Passives(GodRow god)
        {
            var list = new List<TemplePassiveRow>();
            foreach (int i in god.Passives ?? new int[0])
                if (i >= 0 && i < GameTables.TemplePassives.Length) list.Add(GameTables.TemplePassives[i]);
            return list;
        }

        /// <summary>팀 영토의 신전 칸들.</summary>
        public static List<Vector2Int> Temples(GridWorld grid, Team team)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                var tile = grid.GetTile(p);
                if (tile.OwnerTeam != (int)team || string.IsNullOrEmpty(tile.BuildingId)) continue;
                var info = TileImprovementSystem.FindBuilding(tile.BuildingId);
                if (info != null && info.Value.FaithCap > 0) list.Add(p);
            }
            return list;
        }

        /// <summary>신전 곁(PrayRange)에서 기도 중인 팀 유닛 수.</summary>
        public static int PrayersAt(EntityWorld world, Team team, Vector2Int temple)
        {
            int count = 0;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) != team || !UnitQueries.IsAlive(world, i) || !world.GetOrDefault<Praying>(i).Value) continue;
                if (FaithEffectSystem.Chebyshev(temple, world.Get<GridPosition>(i).Value) <= GameRules.Faith.PrayRange) count++;
            }
            return count;
        }

        /// <summary>신전 하나의 현재 효과 크기들(신전 메뉴 표시용, 패시브·효과 순). 신이 없으면 빈 목록.</summary>
        public static List<float> StrengthAt(EntityWorld world, EconomyWorld econ, Team team, Vector2Int temple)
        {
            var list = new List<float>();
            var god = GodOf(econ, team);
            if (god == null) return list;
            int prayers = PrayersAt(world, team, temple);
            foreach (var passive in Passives(god.Value))
                foreach (var e in FaithEffectSystem.Rows(passive.Effects))
                    list.Add(FaithEffectSystem.Strength(e, prayers));
            return list;
        }

        /// <summary>팀의 모든 신전 × 패시브 효과 중 kind 유형 크기의 합.</summary>
        private static float Sum(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithEffectKind kind)
        {
            var god = GodOf(econ, team);
            if (god == null) return 0f;
            float sum = 0f;
            var passives = Passives(god.Value);
            foreach (var temple in Temples(grid, team))
            {
                int prayers = PrayersAt(world, team, temple);
                foreach (var passive in passives)
                    foreach (var e in FaithEffectSystem.Rows(passive.Effects))
                        if (e.Effect == kind) sum += FaithEffectSystem.Strength(e, prayers);
            }
            return sum;
        }

        /// <summary>FaithCap 효과: 신전마다 신앙 최대 보유량 증가.</summary>
        public static int FaithCapBonus(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team) =>
            Mathf.RoundToInt(Sum(grid, world, econ, team, FaithEffectKind.FaithCap));

        /// <summary>Stars 효과: 신전마다 턴당 별 증가.</summary>
        public static int GoldIncome(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team) =>
            Mathf.RoundToInt(Sum(grid, world, econ, team, FaithEffectKind.Stars));

        /// <summary>Stat·GuardStat 효과: 신전 Radius 안 유닛(Side)의 TempleAura를 모두 다시 계산한다.
        /// Stat = 늘 Attack/Defense, GuardStat = 방어 태세 중일 때만 방어력. 위치·기도·건물이 바뀔 때마다 부른다.</summary>
        public static void RefreshAuras(GridWorld grid, EntityWorld world, EconomyWorld econ)
        {
            for (int i = 0; i < world.EntityCount; i++)
            {
                var old = world.GetOrDefault<TempleAura>(i);
                if (old.Attack != 0f || old.Defense != 0f || old.GuardDefense != 0f) world.Set(i, new TempleAura());
            }

            if (econ == null) return;
            foreach (var team in CitySystem.Teams)
            {
                var god = GodOf(econ, team);
                if (god == null) continue;
                var passives = Passives(god.Value);
                foreach (var temple in Temples(grid, team))
                {
                    int prayers = PrayersAt(world, team, temple);
                    foreach (var passive in passives)
                        foreach (var e in FaithEffectSystem.Rows(passive.Effects))
                        {
                            if (e.Effect != FaithEffectKind.Stat && e.Effect != FaithEffectKind.GuardStat) continue;
                            float strength = FaithEffectSystem.Strength(e, prayers);
                            if (strength <= 0f) continue;
                            foreach (int id in FaithEffectSystem.Units(grid, world, team, e, temple, passive.Radius))
                            {
                                var aura = world.GetOrDefault<TempleAura>(id);
                                if (e.Effect == FaithEffectKind.GuardStat) aura.GuardDefense += strength;
                                else if (e.Stat == FaithStat.Attack) aura.Attack += strength;
                                else aura.Defense += strength;
                                world.Set(id, aura);
                            }
                        }
                }
            }
        }

        /// <summary>그 팀 턴 시작에 한 번: 턴 시작 효과(Damage·Heal·Structure)를 패시브 Period 턴마다 발동한다.
        /// 체력이 바뀐 유닛 id를 돌려준다(화면 갱신용).</summary>
        public static List<int> OnTurnStart(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turnNumber)
        {
            var changed = new List<int>();
            var god = GodOf(econ, team);
            if (god == null) return changed;
            var passives = Passives(god.Value);
            foreach (var temple in Temples(grid, team))
            {
                int prayers = PrayersAt(world, team, temple);
                foreach (var passive in passives)
                {
                    if (passive.Period < 1 || turnNumber % passive.Period != 0) continue;
                    foreach (var e in FaithEffectSystem.Rows(passive.Effects))
                    {
                        int amount = Mathf.RoundToInt(FaithEffectSystem.Strength(e, prayers));
                        if (amount <= 0) continue;
                        switch (e.Effect)
                        {
                            case FaithEffectKind.Damage:
                                FaithEffectSystem.Damage(grid, world, econ, team, e, amount, FaithEffectSystem.Units(grid, world, team, e, temple, passive.Radius), changed);
                                break;
                            case FaithEffectKind.Heal:
                                FaithEffectSystem.Heal(world, amount, FaithEffectSystem.Units(grid, world, team, e, temple, passive.Radius), changed);
                                break;
                            case FaithEffectKind.Structure:
                                FaithEffectSystem.SpawnStructures(grid, e, FaithEffectSystem.StructureTiles(grid, team, e, temple, passive.Radius, turnNumber), amount, new List<Vector2Int>());
                                break;
                        }
                    }
                }
            }
            return changed;
        }
    }
}
