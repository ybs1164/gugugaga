using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 팀이 고른 신(Gods.csv)의 신전 패시브. 자체 상태는 없다 — 신전(FaithCap이 있는 건물)과 기도 중인 유닛 수에서 매번 다시 계산한다.
    /// 효과 크기 = Value + PerPrayer × 그 신전 곁에서 기도 중인 유닛 수(0 미만이면 0). 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class TemplePassiveSystem
    {
        /// <summary>체비쇼프 거리(8방향 인접 = 1). PathfindingSystem.Distance는 맨해튼이다.</summary>
        private static int Reach(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        public static GodRow? GodOf(EconomyWorld econ, Team team)
        {
            if (econ == null || !econ.GodIds.TryGetValue(team, out int i) || i < 0 || i >= GameTables.Gods.Length) return null;
            return GameTables.Gods[i];
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
                if (Reach(temple, world.Get<GridPosition>(i).Value) <= GameRules.Faith.PrayRange) count++;
            }
            return count;
        }

        public static float Strength(GodRow god, int prayers) => Mathf.Max(0f, god.Value + god.PerPrayer * prayers);

        /// <summary>신전 하나의 현재 효과 크기(신전 메뉴 표시용). 신이 없으면 0.</summary>
        public static float StrengthAt(EntityWorld world, EconomyWorld econ, Team team, Vector2Int temple)
        {
            var god = GodOf(econ, team);
            return god == null ? 0f : Strength(god.Value, PrayersAt(world, team, temple));
        }

        private static float Sum(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, GodPassiveKind kind)
        {
            var god = GodOf(econ, team);
            if (god == null || god.Value.Passive != kind) return 0f;
            float sum = 0f;
            foreach (var temple in Temples(grid, team)) sum += Strength(god.Value, PrayersAt(world, team, temple));
            return sum;
        }

        /// <summary>신앙형: 신전마다 신앙 최대 보유량 증가.</summary>
        public static int FaithCapBonus(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team) =>
            Mathf.RoundToInt(Sum(grid, world, econ, team, GodPassiveKind.FaithCap));

        /// <summary>골드형: 신전마다 턴당 별 증가.</summary>
        public static int GoldIncome(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team) =>
            Mathf.RoundToInt(Sum(grid, world, econ, team, GodPassiveKind.Gold));

        /// <summary>오라형·버프형: 신전에 인접한(체비쇼프 1) 아군 유닛의 TempleAura를 모두 다시 계산한다.
        /// 오라형 Target = Attack | Defense(항상), 버프형(방어 태세 중일 때만 방어력). 위치·기도·건물이 바뀔 때마다 부른다.</summary>
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
                if (god == null || (god.Value.Passive != GodPassiveKind.Aura && god.Value.Passive != GodPassiveKind.Buff)) continue;
                foreach (var temple in Temples(grid, team))
                {
                    float strength = Strength(god.Value, PrayersAt(world, team, temple));
                    if (strength <= 0f) continue;
                    for (int i = 0; i < world.EntityCount; i++)
                    {
                        if (world.Get<Team>(i) != team || !UnitQueries.IsAlive(world, i)) continue;
                        if (Reach(temple, world.Get<GridPosition>(i).Value) > 1) continue;
                        var aura = world.GetOrDefault<TempleAura>(i);
                        if (god.Value.Passive == GodPassiveKind.Buff) aura.GuardDefense += strength;
                        else if (god.Value.Target == "Attack") aura.Attack += strength;
                        else aura.Defense += strength;
                        world.Set(i, aura);
                    }
                }
            }
        }

        /// <summary>그 팀 턴 시작에 한 번: 피해형(인접 적 유닛에게 피해, 체력을 1 아래로 깎지 않는다)과 자원형(Period 턴마다 인접 빈 칸에 구조물 생성).
        /// 피해를 입은 유닛 id를 돌려준다(화면 갱신용).</summary>
        public static List<int> OnTurnStart(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turnNumber)
        {
            var damaged = new List<int>();
            var god = GodOf(econ, team);
            if (god == null) return damaged;
            foreach (var temple in Temples(grid, team))
            {
                float strength = Strength(god.Value, PrayersAt(world, team, temple));
                if (strength <= 0f) continue;
                if (god.Value.Passive == GodPassiveKind.Damage) DamageAdjacentEnemies(world, team, temple, Mathf.RoundToInt(strength), damaged);
                else if (god.Value.Passive == GodPassiveKind.Resource && god.Value.Period > 0 && turnNumber % god.Value.Period == 0)
                    SpawnResources(grid, team, temple, god.Value.Target, Mathf.RoundToInt(strength), turnNumber);
            }
            return damaged;
        }

        private static void DamageAdjacentEnemies(EntityWorld world, Team team, Vector2Int temple, int amount, List<int> damaged)
        {
            if (amount <= 0) return;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) == team || !UnitQueries.IsAlive(world, i)) continue;
                if (Reach(temple, world.Get<GridPosition>(i).Value) > 1) continue;
                var hp = world.Get<Hp>(i);
                int next = Mathf.Max(1, hp.Value - amount);
                if (next == hp.Value) continue;
                hp.Value = next;
                world.Set(i, hp);
                PrayerSystem.OnHit(world, i);
                damaged.Add(i);
            }
        }

        /// <summary>신전 인접 8칸 중 자기 영토의 빈 육지 칸에 count개. 시작 방향은 턴 번호로 돌려 같은 칸만 쓰지 않게 한다.</summary>
        private static void SpawnResources(GridWorld grid, Team team, Vector2Int temple, string structureId, int count, int turnNumber)
        {
            if (string.IsNullOrEmpty(structureId)) return;
            for (int k = 0; k < GridWorld.Dir8.Length && count > 0; k++)
            {
                var p = temple + GridWorld.Dir8[(k + turnNumber) % GridWorld.Dir8.Length];
                if (!grid.InBounds(p)) continue;
                var tile = grid.GetTile(p);
                if (tile.OwnerTeam != (int)team || tile.Terrain != TerrainType.Land || !tile.Walkable) continue;
                if (!string.IsNullOrEmpty(tile.StructureId) || !string.IsNullOrEmpty(tile.BuildingId) || tile.OccupantId != TileData.NoOccupant) continue;
                tile.StructureId = structureId;
                grid.SetTile(p, tile);
                count--;
            }
        }
    }
}
