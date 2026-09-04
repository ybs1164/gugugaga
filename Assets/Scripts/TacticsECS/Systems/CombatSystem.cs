using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 판정을 담당하는 시스템.
    /// 사거리는 맨해튼 거리 기준. 공격력/방어력/사거리/방어 태세 가능 여부는 UnitWorld의 개별 속성
    /// getter(GetAttack/GetDefense/GetAttackRange/GetCanGuard)에서 읽는다.
    /// </summary>
    public static class CombatSystem
    {
        public static bool IsInAttackRange(UnitWorld units, int attackerId, int targetId)
        {
            int dist = PathfindingSystem.Distance(units.GetGridPos(attackerId), units.GetGridPos(targetId));
            return dist >= 1 && dist <= units.GetAttackRange(attackerId);
        }

        public static int CalculateDamage(UnitWorld units, int attackerId, int targetId)
        {
            int guardBonus = units.GetIsGuarding(targetId) ? 2 : 0;
            int dmg = units.GetAttack(attackerId) - (units.GetDefense(targetId) + guardBonus);
            return Mathf.Max(1, dmg);
        }

        public static bool TryAttack(GridWorld grid, UnitWorld units, int attackerId, int targetId, out int damageDealt)
        {
            damageDealt = 0;

            if (!units.IsAlive(attackerId) || !units.IsAlive(targetId)) return false;
            if (units.GetHasActed(attackerId)) return false;
            if (units.GetTeam(attackerId) == units.GetTeam(targetId)) return false;
            if (!IsInAttackRange(units, attackerId, targetId)) return false;

            damageDealt = CalculateDamage(units, attackerId, targetId);
            units.SetHp(targetId, Mathf.Max(0, units.GetHp(targetId) - damageDealt));
            units.SetHasActed(attackerId, true);

            if (!units.IsAlive(targetId))
                grid.RemoveOccupant(units.GetGridPos(targetId));

            return true;
        }

        /// <summary>CanGuard 유닛 전용 행동: 공격 대신 방어 태세로 전환해 이번 턴 받는 피해를 줄인다.</summary>
        public static bool TryDefend(UnitWorld units, int unitId)
        {
            if (!units.IsAlive(unitId) || units.GetHasActed(unitId) || !units.GetCanGuard(unitId)) return false;

            units.SetIsGuarding(unitId, true);
            units.SetHasActed(unitId, true);
            return true;
        }
    }
}
