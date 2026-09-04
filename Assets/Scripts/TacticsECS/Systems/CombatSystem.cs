using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 판정을 담당하는 시스템.
    /// 사거리는 맨해튼 거리 기준. 공격력/방어력/사거리/방어 태세 가능 여부는 EntityWorld에서
    /// 해당 컴포넌트(Attack/Defense/AttackRange/CanGuard)를 조회해서 읽는다.
    /// </summary>
    public static class CombatSystem
    {
        public static bool IsInAttackRange(EntityWorld world, int attackerId, int targetId)
        {
            var attackerPos = world.Get<GridPosition>(attackerId).Value;
            var targetPos = world.Get<GridPosition>(targetId).Value;
            int dist = PathfindingSystem.Distance(attackerPos, targetPos);
            return dist >= 1 && dist <= world.Get<AttackRange>(attackerId).Value;
        }

        public static int CalculateDamage(EntityWorld world, int attackerId, int targetId)
        {
            int guardBonus = world.Get<IsGuarding>(targetId).Value ? 2 : 0;
            int dmg = world.Get<Attack>(attackerId).Value - (world.Get<Defense>(targetId).Value + guardBonus);
            return Mathf.Max(1, dmg);
        }

        public static bool TryAttack(GridWorld grid, EntityWorld world, int attackerId, int targetId, out int damageDealt)
        {
            damageDealt = 0;

            if (!UnitQueries.IsAlive(world, attackerId) || !UnitQueries.IsAlive(world, targetId)) return false;
            if (world.Get<HasActed>(attackerId).Value) return false;
            if (world.Get<Team>(attackerId) == world.Get<Team>(targetId)) return false;
            if (!IsInAttackRange(world, attackerId, targetId)) return false;

            damageDealt = CalculateDamage(world, attackerId, targetId);
            var hp = world.Get<Hp>(targetId);
            hp.Value = Mathf.Max(0, hp.Value - damageDealt);
            world.Set(targetId, hp);
            world.Set(attackerId, new HasActed { Value = true });

            if (!UnitQueries.IsAlive(world, targetId))
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);

            return true;
        }

        /// <summary>CanGuard 유닛 전용 행동: 공격 대신 방어 태세로 전환해 이번 턴 받는 피해를 줄인다.</summary>
        public static bool TryDefend(EntityWorld world, int unitId)
        {
            if (!UnitQueries.IsAlive(world, unitId) || world.Get<HasActed>(unitId).Value || !world.Get<CanGuard>(unitId).Value)
                return false;

            world.Set(unitId, new IsGuarding { Value = true });
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }
    }
}
