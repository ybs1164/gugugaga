using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 판정을 담당하는 시스템.
    /// 사거리는 맨해튼 거리 기준: 근접(Guard 포함) = 1, 원거리 = 최대 3.
    /// </summary>
    public static class CombatSystem
    {
        public static bool IsInAttackRange(UnitData attacker, UnitData target)
        {
            int dist = PathfindingSystem.Distance(attacker.GridPos, target.GridPos);
            return dist >= 1 && dist <= attacker.AttackRange;
        }

        public static int CalculateDamage(UnitData attacker, UnitData defender)
        {
            int guardBonus = defender.IsGuarding ? 2 : 0;
            int dmg = attacker.Attack - (defender.Defense + guardBonus);
            return Mathf.Max(1, dmg);
        }

        public static bool TryAttack(GridWorld grid, UnitWorld units, int attackerId, int targetId, out int damageDealt)
        {
            damageDealt = 0;
            var attacker = units.Get(attackerId);
            var target = units.Get(targetId);

            if (!attacker.IsAlive || !target.IsAlive) return false;
            if (attacker.HasActed) return false;
            if (attacker.Team == target.Team) return false;
            if (!IsInAttackRange(attacker, target)) return false;

            damageDealt = CalculateDamage(attacker, target);
            target.Hp = Mathf.Max(0, target.Hp - damageDealt);
            attacker.HasActed = true;

            units.Set(targetId, target);
            units.Set(attackerId, attacker);

            if (!target.IsAlive)
                grid.RemoveOccupant(target.GridPos);

            return true;
        }

        /// <summary>Guard 타입 전용 행동: 공격 대신 방어 태세로 전환해 이번 턴 받는 피해를 줄인다.</summary>
        public static bool TryDefend(UnitWorld units, int unitId)
        {
            var unit = units.Get(unitId);
            if (!unit.IsAlive || unit.HasActed || unit.Type != UnitType.Guard) return false;

            unit.IsGuarding = true;
            unit.HasActed = true;
            units.Set(unitId, unit);
            return true;
        }
    }
}
