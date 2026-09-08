using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어(CombatSystem)나 이동(MovementSystem)에 속하지 않는 특수 행동(치유, 자폭)을 담당하는
    /// 시스템. 다른 System과 마찬가지로 EntityWorld/GridWorld를 읽고 쓸 뿐 자체 상태는 없다.
    /// </summary>
    public static class AbilitySystem
    {
        /// <summary>
        /// 사거리 내의 모든 아군(자신 제외)의 체력을 시전자의 HealAmount만큼 회복시킨다.
        /// 실제로 회복된 대상 id 목록을 반환해, 호출자(BattleController)가 그 유닛들의 View만 갱신하게 한다.
        /// </summary>
        public static bool TryHeal(EntityWorld world, int healerId, out List<int> healedIds)
        {
            healedIds = new List<int>();

            if (!UnitQueries.IsAlive(world, healerId) || world.Get<HasActed>(healerId).Value) return false;
            if (!world.Get<AvailableActions>(healerId).Value.HasFlag(ActionType.Heal)) return false;

            var healerPos = world.Get<GridPosition>(healerId).Value;
            var team = world.Get<Team>(healerId);
            int range = world.Get<HealRange>(healerId).Value;
            int amount = world.Get<HealAmount>(healerId).Value;

            for (int i = 0; i < world.EntityCount; i++)
            {
                if (i == healerId || !UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                if (PathfindingSystem.Distance(healerPos, world.Get<GridPosition>(i).Value) > range) continue;

                var hp = world.Get<Hp>(i);
                int maxHp = world.Get<MaxHp>(i).Value;
                int healedValue = Mathf.Min(maxHp, hp.Value + amount);
                if (healedValue == hp.Value) continue;

                hp.Value = healedValue;
                world.Set(i, hp);
                healedIds.Add(i);
            }

            world.Set(healerId, new HasActed { Value = true });
            return true;
        }

        /// <summary>
        /// 행동 유닛을 즉시 제거하고, 주위 1블럭(맨해튼 거리 1 이내) 안의 모든 적에게 제거되는 시점의
        /// 남은 체력만큼 피해를 입힌다. 피해를 입은(대상) id 목록을 반환한다.
        /// </summary>
        public static bool TrySelfDestruct(GridWorld grid, EntityWorld world, int unitId, out List<int> damagedIds)
        {
            damagedIds = new List<int>();

            if (!UnitQueries.IsAlive(world, unitId) || world.Get<HasActed>(unitId).Value) return false;
            if (!world.Get<AvailableActions>(unitId).Value.HasFlag(ActionType.SelfDestruct)) return false;

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
                damagedIds.Add(i);

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
