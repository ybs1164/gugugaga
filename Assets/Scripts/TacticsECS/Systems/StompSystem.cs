using UnityEngine;

namespace TacticsECS
{
    // 규칙: docs/spec/game/unit.md#배
    public static class StompSystem
    {
        public static void Apply(GridWorld grid, EntityWorld world, int actorId)
        {
            var center = world.Get<GridPosition>(actorId).Value;
            var team = world.Get<Team>(actorId);
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i) || world.Get<Team>(i) == team) continue;
                var pos = world.Get<GridPosition>(i).Value;
                if (Mathf.Max(Mathf.Abs(pos.x - center.x), Mathf.Abs(pos.y - center.y)) != 1) continue;
                if (DiplomacySystem.HasPeaceTreaty(grid.Economy, team, world.Get<Team>(i))) continue;
                DiplomacySystem.DeclareWar(grid.Economy, team, world.Get<Team>(i));
                TaskSystem.RecordAttack(grid.Economy, team);
                var hp = world.Get<Hp>(i);
                hp.Value = Mathf.Max(0, hp.Value - CombatSystem.CalculateSplashDamage(world, actorId, i));
                world.Set(i, hp);
                world.Set(i, new Accelerated { Value = false });
                StealthSystem.Reveal(world, i);
                if (!UnitQueries.IsAlive(world, i))
                {
                    grid.RemoveOccupant(pos);
                    VeteranSystem.RecordKill(world, actorId);
                }
            }
        }
    }
}
