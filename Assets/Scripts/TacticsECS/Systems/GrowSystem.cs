using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 성장(위키 Unit Skills "Grow", Dragon Egg): Growth 컴포넌트를 가진 유닛은 자기 턴 시작마다 남은 턴이 1 줄고, 0이 되면 같은 칸에서
    /// Grow.Into 유닛으로 바뀐다. 받은 피해는 그대로 이어진다(위키 "Damage is inherited"), 소속 도시·처치 수도 이어진다. 배에 탄 동안에는
    /// 바뀌지 않고 내린 뒤 다음 턴 시작에 바뀐다(위키 Dragon Egg). 순수 함수형, 자체 상태 없음. 규칙: docs/spec/game/unit.md#성장
    /// </summary>
    public static class GrowSystem
    {
        /// <summary>team 턴 시작에 부른다. 새로 생긴 유닛 id 목록(View는 호출자가 붙인다).</summary>
        public static List<int> OnTurnStart(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var grown = new List<int>();
            int count = world.EntityCount;
            for (int i = 0; i < count; i++)
            {
                if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                var g = world.GetOrDefault<Growth>(i);
                if (string.IsNullOrEmpty(g.Into)) continue;
                if (g.TurnsLeft > 0) { g.TurnsLeft--; world.Set(i, g); }
                if (g.TurnsLeft > 0 || EmbarkSystem.IsEmbarked(world, i)) continue;
                int id = Grow(grid, world, econ, i, g.Into);
                if (id >= 0) grown.Add(id);
            }
            return grown;
        }

        /// <summary>unitId를 intoId 유닛으로 바꾼다. 새 유닛 id, 유닛 행이 없으면 -1(성장은 멈춘다).</summary>
        public static int Grow(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, string intoId)
        {
            var row = MythicSystem.FindUnitRow(econ, intoId);
            if (row == null) { world.Set(unitId, new Growth()); return -1; }
            var team = world.Get<Team>(unitId);
            var pos = world.Get<GridPosition>(unitId).Value;
            int damage = world.Get<MaxHp>(unitId).Value - world.Get<Hp>(unitId).Value;

            grid.RemoveOccupant(pos);
            world.Set(unitId, new Hp { Value = 0 });

            int id = UnitFactorySystem.CreateFromCsv(grid, world, team, row, pos);
            world.Set(id, new Hp { Value = System.Math.Max(1, world.Get<MaxHp>(id).Value - damage) });
            world.Set(id, world.GetOrDefault<HomeCity>(unitId));
            world.Set(id, world.GetOrDefault<Kills>(unitId));
            world.Set(id, world.Get<HasMoved>(unitId));
            world.Set(id, world.Get<HasActed>(unitId));
            return id;
        }
    }
}
