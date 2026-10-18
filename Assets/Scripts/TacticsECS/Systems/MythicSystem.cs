using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신화 유닛(강림). 자체 상태 없이 Mythic/Dormant 컴포넌트를 읽고 쓴다.
    /// 강림: 대상 아군(원본)을 Dormant로 보존해 그리드에서 빼고 같은 칸에 신화 유닛을 만든다 — 원본의 능력치는 반영하지 않는다.
    /// 유지: 매 자기 턴 시작에 유지 턴 -1, 0이 되면 신화 유닛이 원본과 함께 소멸한다. 신화 유닛이 죽어도 원본이 함께 죽는다(Sweep).
    /// 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class MythicSystem
    {
        public static MythicRow? MythicOf(EconomyWorld econ, Team team)
        {
            var god = TemplePassiveSystem.GodOf(econ, team);
            if (god == null || god.Value.Mythic < 0 || god.Value.Mythic >= GameTables.MythicUnits.Length) return null;
            return GameTables.MythicUnits[god.Value.Mythic];
        }

        /// <summary>신화 유닛의 Units.csv 행 — 불러온 유닛 표에 없으면 기본 유닛 표에서 찾는다.</summary>
        public static UnitCsvRow FindUnitRow(EconomyWorld econ, string id)
        {
            var row = econ != null ? CitySystem.FindUnitRow(econ, id) : null;
            if (row != null) return row;
            foreach (var r in GameTables.Units) if (r.Id == id) return r;
            return null;
        }

        public static bool IsMythic(EntityWorld world, int id) => world.GetOrDefault<Mythic>(id).Active && UnitQueries.IsAlive(world, id);

        /// <summary>팀의 살아 있는 신화 유닛 id, 없으면 -1.</summary>
        public static int FindMythic(EntityWorld world, Team team)
        {
            for (int i = 0; i < world.EntityCount; i++)
                if (world.Get<Team>(i) == team && IsMythic(world, i)) return i;
            return -1;
        }

        // ---------- 강림 ----------

        /// <summary>강림할 수 없으면 이유 문구 키, 있으면 null.</summary>
        public static string CheckDescend(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, Vector2Int target)
        {
            var mythic = MythicOf(econ, team);
            if (mythic == null || FindUnitRow(econ, mythic.Value.Id) == null) return "UI.FaithPunch.NoMythic";
            if (FindMythic(world, team) >= 0) return "UI.FaithPunch.MythicExists";
            int id = grid.GetOccupant(target);
            if (id == TileData.NoOccupant || !UnitQueries.IsAlive(world, id) || world.Get<Team>(id) != team) return "UI.FaithPunch.NoDescendTarget";
            if (world.GetOrDefault<Embarked>(id).Value || grid.GetTerrain(target) != TerrainType.Land) return "UI.FaithPunch.NoDescendTarget";
            return null;
        }

        /// <summary>대상 칸의 아군을 신화 유닛으로 바꾼다. CheckDescend가 null이어야 한다. 새 신화 유닛 id를 돌려주고 원본 id는 originId로 준다.</summary>
        public static int Descend(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, Vector2Int target, out int originId)
        {
            originId = grid.GetOccupant(target);
            var mythic = MythicOf(econ, team).Value;
            var row = FindUnitRow(econ, mythic.Id);

            grid.RemoveOccupant(target);
            world.Set(originId, new Dormant { Value = true });
            PrayerSystem.Cancel(world, originId);

            int id = UnitFactorySystem.CreateFromCsv(grid, world, team, row, target);
            world.Set(id, new Mythic { Active = true, MythicIndex = mythic.Index, TurnsLeft = mythic.Upkeep, Origin = originId });
            world.Set(id, world.Get<HasMoved>(originId));
            world.Set(id, world.Get<HasActed>(originId));
            return id;
        }

        // ---------- 유지 / 제거 ----------

        /// <summary>그 팀 턴 시작: 신화 유닛의 유지 턴 -1. 0이 되면 신화 유닛이 소멸한다(보존된 원본도 함께 사라진다 — 기획서 "지속 시간 경과 시 소멸").
        /// 화면을 갱신해야 할 유닛 id(소멸한 신화 유닛)를 돌려준다.</summary>
        public static List<int> OnTurnStart(GridWorld grid, EntityWorld world, Team team)
        {
            var changed = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) != team || !IsMythic(world, i)) continue;
                var m = world.Get<Mythic>(i);
                m.TurnsLeft--;
                world.Set(i, m);
                if (m.TurnsLeft > 0) continue;
                changed.Add(i);
                Expire(grid, world, i);
            }
            return changed;
        }

        /// <summary>신화 유닛과 보존된 원본을 함께 없앤다.</summary>
        private static void Expire(GridWorld grid, EntityWorld world, int mythicId)
        {
            var m = world.Get<Mythic>(mythicId);
            var pos = world.Get<GridPosition>(mythicId).Value;
            if (grid.GetOccupant(pos) == mythicId) grid.RemoveOccupant(pos);
            world.Set(mythicId, new Hp { Value = 0 });
            m.Active = false;
            world.Set(mythicId, m);
            world.Set(m.Origin, new Hp { Value = 0 });
            world.Set(m.Origin, new Dormant { Value = false });
        }

        /// <summary>죽은 신화 유닛의 원본도 함께 제거한다(상태가 바뀔 때마다 부른다). 함께 죽은 원본 id를 돌려준다.</summary>
        public static List<int> Sweep(GridWorld grid, EntityWorld world)
        {
            var removed = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
            {
                var m = world.GetOrDefault<Mythic>(i);
                if (!m.Active || world.Get<Hp>(i).Value > 0) continue;
                var pos = world.Get<GridPosition>(i).Value;
                if (grid.GetOccupant(pos) == i) grid.RemoveOccupant(pos);
                world.Set(m.Origin, new Hp { Value = 0 });
                world.Set(m.Origin, new Dormant { Value = false });
                m.Active = false;
                world.Set(i, m);
                removed.Add(m.Origin);
            }
            return removed;
        }

        // ---------- 액티브 능력 ----------

        public static bool HasActive(MythicRow m) => m.ActivePunch >= 0 && m.ActivePunch < GameTables.FaithPunches.Length;

        /// <summary>액티브 능력으로 쓰는 펀치 행(보통 신 펀치의 약한 판으로 따로 둔 행). 비용은 그 행의 Cost.</summary>
        public static FaithPunchRow ActivePunchRow(MythicRow m) => GameTables.FaithPunches[m.ActivePunch];

        public static bool CanUseActive(EntityWorld world, int mythicId)
        {
            if (!IsMythic(world, mythicId) || world.Get<HasActed>(mythicId).Value) return false;
            var m = GameTables.MythicUnits[world.Get<Mythic>(mythicId).MythicIndex];
            return HasActive(m) && !FaithPunchSystem.Has(ActivePunchRow(m), FaithEffectKind.Descend);
        }

        /// <summary>대상으로 쓸 수 없으면 이유 문구 키, 쓸 수 있으면 null(사거리, 유지 턴, 그리고 펀치 자체 조건).</summary>
        public static string CheckActive(GridWorld grid, EntityWorld world, EconomyWorld econ, int mythicId, Vector2Int target)
        {
            var state = world.Get<Mythic>(mythicId);
            var m = GameTables.MythicUnits[state.MythicIndex];
            var pos = world.Get<GridPosition>(mythicId).Value;
            if (Mathf.Max(Mathf.Abs(pos.x - target.x), Mathf.Abs(pos.y - target.y)) > m.ActiveReach) return "UI.Mythic.Reason.Reach";
            if (state.TurnsLeft <= m.ActiveUpkeepCost) return "UI.Mythic.Reason.Upkeep";
            return FaithPunchSystem.Check(grid, world, econ, world.Get<Team>(mythicId), ActivePunchRow(m), target);
        }

        /// <summary>액티브 능력 사용: 액티브 펀치를 적용하고 유지 턴을 더 줄이며 이번 턴 행동을 끝낸다.</summary>
        public static FaithPunchResult UseActive(GridWorld grid, EntityWorld world, EconomyWorld econ, int mythicId, Vector2Int target)
        {
            var state = world.Get<Mythic>(mythicId);
            var m = GameTables.MythicUnits[state.MythicIndex];
            if (CheckActive(grid, world, econ, mythicId, target) != null)
                return new FaithPunchResult { UnitIds = new List<int>(), Tiles = new List<Vector2Int>(), SpawnedIds = new List<int>() };
            var result = FaithPunchSystem.Apply(grid, world, econ, world.Get<Team>(mythicId), ActivePunchRow(m), target);
            if (!result.Success) return result;
            state.TurnsLeft -= m.ActiveUpkeepCost;
            world.Set(mythicId, state);
            world.Set(mythicId, new HasActed { Value = true });
            return result;
        }
    }
}
