using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 관련 순수 계산(사거리, 방어력, 피해량)과, 공격/방어 행동의 진입점을 담당하는 시스템.
    /// 실제 실행 가능 여부 판정/효과 적용은 AttackAction/DefendAction/CounterAction
    /// (Assets/Scripts/TacticsECS/Actions)이 직접 담당하고, 이 클래스는 UnitActionQueries로 그 행동을
    /// 찾아 위임하거나(TryAttack/TryDefend), 여러 행동이 공유하는 계산 함수(IsInAttackRange/
    /// EffectiveDefense/CalculateDamage)를 제공한다 — 이 계산 함수들은 BattleHud(UI 표시)에서도
    /// 그대로 재사용한다.
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

        /// <summary>공격 행동(AttackAction)을 찾아 실행하고, 공격이 성사되어 대상이 살아남았으면 대상의
        /// 반격 행동(CounterAction)을 찾아 이어서 실행한다. 공격이 실제로 성사됐는지와 별개로, 대상이
        /// 반격으로 되돌려준 피해량을 counterDamageDealt로 함께 돌려준다(반격이 없었거나 발동하지
        /// 않았으면 0).</summary>
        public static bool TryAttack(GridWorld grid, EntityWorld world, int attackerId, int targetId, out int damageDealt, out int counterDamageDealt)
        {
            damageDealt = 0;
            counterDamageDealt = 0;

            var attack = UnitActionQueries.Find<AttackAction>(world, attackerId);
            if (attack == null || !attack.Execute(grid, world, attackerId, targetId, out damageDealt)) return false;

            if (UnitQueries.IsAlive(world, targetId))
            {
                var counter = UnitActionQueries.Find<CounterAction>(world, targetId);
                counter?.Execute(grid, world, targetId, attackerId, out counterDamageDealt);
            }

            return true;
        }

        /// <summary>방어 행동(DefendAction)을 찾아 실행한다.</summary>
        public static bool TryDefend(GridWorld grid, EntityWorld world, int unitId)
        {
            var defend = UnitActionQueries.Find<DefendAction>(world, unitId);
            return defend != null && defend.Execute(grid, world, unitId, out _);
        }
    }
}
