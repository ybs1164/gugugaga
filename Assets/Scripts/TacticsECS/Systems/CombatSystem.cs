using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 판정을 담당하는 시스템.
    /// 사거리는 맨해튼 거리 기준. 공격력/방어력/사거리/방어 태세 가능 여부는 UnitWorld.GetStats(id)의
    /// UnitCombatStats에서 읽는다 (전투 중 바뀌지 않는 고정 값이므로 UnitData가 아니라 UnitStats에 있다).
    /// </summary>
    public static class CombatSystem
    {
        public static bool IsInAttackRange(UnitWorld units, int attackerId, int targetId)
        {
            var attacker = units.Get(attackerId);
            var target = units.Get(targetId);
            int dist = PathfindingSystem.Distance(attacker.GridPos, target.GridPos);
            int range = units.GetStats(attackerId).Combat.AttackRange;
            return dist >= 1 && dist <= range;
        }

        public static int CalculateDamage(UnitWorld units, int attackerId, int targetId)
        {
            var target = units.Get(targetId);
            var attackerCombat = units.GetStats(attackerId).Combat;
            var targetCombat = units.GetStats(targetId).Combat;

            int guardBonus = target.TurnState.IsGuarding ? 2 : 0;
            int dmg = attackerCombat.Attack - (targetCombat.Defense + guardBonus);
            return Mathf.Max(1, dmg);
        }

        public static bool TryAttack(GridWorld grid, UnitWorld units, int attackerId, int targetId, out int damageDealt)
        {
            damageDealt = 0;
            var attacker = units.Get(attackerId);
            var target = units.Get(targetId);

            if (!attacker.IsAlive || !target.IsAlive) return false;
            if (attacker.TurnState.HasActed) return false;
            if (attacker.Team == target.Team) return false;
            if (!IsInAttackRange(units, attackerId, targetId)) return false;

            damageDealt = CalculateDamage(units, attackerId, targetId);
            target.Hp = Mathf.Max(0, target.Hp - damageDealt);
            attacker.TurnState.HasActed = true;

            units.Set(targetId, target);
            units.Set(attackerId, attacker);

            if (!target.IsAlive)
                grid.RemoveOccupant(target.GridPos);

            return true;
        }

        /// <summary>CanGuard 유닛 전용 행동: 공격 대신 방어 태세로 전환해 이번 턴 받는 피해를 줄인다.</summary>
        public static bool TryDefend(UnitWorld units, int unitId)
        {
            var unit = units.Get(unitId);
            if (!unit.IsAlive || unit.TurnState.HasActed || !units.GetStats(unitId).Combat.CanGuard) return false;

            unit.TurnState.IsGuarding = true;
            unit.TurnState.HasActed = true;
            units.Set(unitId, unit);
            return true;
        }
    }
}
