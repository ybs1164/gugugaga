using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 판정을 담당하는 시스템.
    /// 사거리는 맨해튼 거리 기준. 공격력/방어력/사거리는 EntityWorld에서 해당 컴포넌트
    /// (Attack/Defense/AttackRange)를 조회해서 읽고, 공격/방어를 실제로 쓸 수 있는지는
    /// AvailableActions(ActionType.Attack/Defend 플래그)로 판단한다.
    /// TryAttack은 공격이 성사되면 대상의 반격 패시브(ActionType.Counter)도 함께 확인한다 —
    /// 반격은 플레이어가 고르는 행동이 아니라 "공격을 받았을 때" 항상 자동으로 발동하는 패시브다.
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

        /// <summary>공격이 실제로 성사됐는지와 별개로, 대상이 반격(ActionType.Counter)으로 되돌려준
        /// 피해량을 counterDamageDealt로 함께 돌려준다(반격이 없었으면 0).</summary>
        public static bool TryAttack(GridWorld grid, EntityWorld world, int attackerId, int targetId, out int damageDealt, out int counterDamageDealt)
        {
            damageDealt = 0;
            counterDamageDealt = 0;

            if (!UnitQueries.IsAlive(world, attackerId) || !UnitQueries.IsAlive(world, targetId)) return false;
            if (world.Get<HasActed>(attackerId).Value) return false;
            if (!world.Get<AvailableActions>(attackerId).Value.HasFlag(ActionType.Attack)) return false;
            if (world.Get<Team>(attackerId) == world.Get<Team>(targetId)) return false;
            if (!IsInAttackRange(world, attackerId, targetId)) return false;

            damageDealt = CalculateDamage(world, attackerId, targetId);
            var hp = world.Get<Hp>(targetId);
            hp.Value = Mathf.Max(0, hp.Value - damageDealt);
            world.Set(targetId, hp);
            world.Set(attackerId, new HasActed { Value = true });

            if (!UnitQueries.IsAlive(world, targetId))
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);
            else
                TryCounter(grid, world, defenderId: targetId, attackerId: attackerId, out counterDamageDealt);

            return true;
        }

        /// <summary>패시브: 공격을 받고 살아남은 대상(defenderId)이 ActionType.Counter를 가지고 있고
        /// 공격자가 대상 자신의 사거리 안에 있으면, 대상이 자동으로 공격자에게 피해를 되돌려준다.
        /// 플레이어가 고르는 "행동"이 아니라 공격을 받았을 때 항상 발동하는 패시브라서, 일반 행동과
        /// 달리 HasActed는 건드리지 않는다(대상이 이미 자기 턴에 행동을 마쳤어도 반격은 그대로 발동).</summary>
        private static bool TryCounter(GridWorld grid, EntityWorld world, int defenderId, int attackerId, out int counterDamage)
        {
            counterDamage = 0;

            if (!world.Get<AvailableActions>(defenderId).Value.HasFlag(ActionType.Counter)) return false;
            if (!IsInAttackRange(world, defenderId, attackerId)) return false;

            counterDamage = CalculateDamage(world, defenderId, attackerId);
            var hp = world.Get<Hp>(attackerId);
            hp.Value = Mathf.Max(0, hp.Value - counterDamage);
            world.Set(attackerId, hp);

            if (!UnitQueries.IsAlive(world, attackerId))
                grid.RemoveOccupant(world.Get<GridPosition>(attackerId).Value);

            return true;
        }

        /// <summary>ActionType.Defend를 가진 유닛 전용 행동: 공격 대신 방어 태세로 전환해 이번 턴 받는 피해를 줄인다.</summary>
        public static bool TryDefend(EntityWorld world, int unitId)
        {
            if (!UnitQueries.IsAlive(world, unitId) || world.Get<HasActed>(unitId).Value ||
                !world.Get<AvailableActions>(unitId).Value.HasFlag(ActionType.Defend))
                return false;

            world.Set(unitId, new IsGuarding { Value = true });
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }
    }
}
