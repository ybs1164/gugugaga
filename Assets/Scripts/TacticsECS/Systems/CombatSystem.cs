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
        /// <summary>방어 태세 중 추가로 붙는 방어력. CalculateDamage/EffectiveDefense가 공유하는 값이라,
        /// UI(BattleHud) 등 다른 곳에서 "방어 태세면 얼마나 더 단단해지는지" 보여줄 때도 이 상수를
        /// 다시 정의하지 않고 EffectiveDefense를 통해서만 읽는다.</summary>
        public const int GuardDefenseBonus = 2;

        public static bool IsInAttackRange(EntityWorld world, int attackerId, int targetId)
        {
            var attackerPos = world.Get<GridPosition>(attackerId).Value;
            var targetPos = world.Get<GridPosition>(targetId).Value;
            int dist = PathfindingSystem.Distance(attackerPos, targetPos);
            return dist >= 1 && dist <= world.Get<AttackRange>(attackerId).Value;
        }

        /// <summary>방어 태세 보너스까지 합산한 실제 방어력.</summary>
        public static int EffectiveDefense(EntityWorld world, int unitId)
        {
            int bonus = world.Get<IsGuarding>(unitId).Value ? GuardDefenseBonus : 0;
            return world.Get<Defense>(unitId).Value + bonus;
        }

        public static int CalculateDamage(EntityWorld world, int attackerId, int targetId)
        {
            int dmg = world.Get<Attack>(attackerId).Value - EffectiveDefense(world, targetId);
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
