using UnityEngine;

namespace TacticsECS
{
    // 규칙: docs/spec/game/unit.md#배
    public static class StompSystem
    {
        public static void Apply(GridWorld grid, EntityWorld world, int actorId) =>
            CombatSystem.ApplySplashAround(grid, world, actorId, world.Get<GridPosition>(actorId).Value);
    }
}
