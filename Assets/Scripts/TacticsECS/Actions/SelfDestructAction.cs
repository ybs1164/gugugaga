using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 자폭 행동. 별도 값은 없다 — 피해량은 발동 시점의 남은 체력(Hp)을 그대로 쓴다. 예전
    /// AbilitySystem.TrySelfDestruct 본체 로직이 그대로 이 안으로 옮겨왔다.
    /// </summary>
    [System.Serializable]
    public class SelfDestructAction : ISelfAction
    {
        public ActionType GetActionType() => ActionType.SelfDestruct;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, out List<int> affectedIds)
        {
            affectedIds = new List<int>();
            if (!CanExecute(world, unitId)) return false;

            var selfPos = world.Get<GridPosition>(unitId).Value;
            var team = world.Get<Team>(unitId);
            int damage = world.Get<Hp>(unitId).Value;

            for (int i = 0; i < world.EntityCount; i++)
            {
                if (i == unitId || !UnitQueries.IsAlive(world, i) || world.Get<Team>(i) == team) continue;
                if (PathfindingSystem.Distance(selfPos, world.Get<GridPosition>(i).Value) > 1) continue;

                var hp = world.Get<Hp>(i);
                hp.Value = Mathf.Max(0, hp.Value - damage);
                world.Set(i, hp);
                world.Set(i, new Accelerated { Value = false });
                affectedIds.Add(i);

                if (!UnitQueries.IsAlive(world, i))
                    grid.RemoveOccupant(world.Get<GridPosition>(i).Value);
            }

            world.Set(unitId, new Hp { Value = 0 });
            world.Set(unitId, new HasActed { Value = true });
            grid.RemoveOccupant(selfPos);

            return true;
        }
    }
}
