using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 반격 패시브. 다른 행동과 달리 플레이어가 직접 고르는 "행동 버튼"이 아니라, 공격을 받았을 때
    /// CombatSystem.TryAttack이 자동으로 찾아 발동시킨다(BattleHud에서도 버튼이 아니라 정보용 배지로만
    /// 표시). CanExecute가 HasActed를 보지 않는 것도 이 때문 — 이미 이번 턴 행동을 마친 유닛도 반격은
    /// 그대로 발동해야 한다. 별도 값은 없다 — 반격 피해량은 AttackAction의 Attack/AttackRange를 그대로 쓴다.
    /// </summary>
    [System.Serializable]
    public class CounterAction : ITargetedAction
    {
        public ActionType GetActionType() => ActionType.Counter;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);

        public bool Execute(GridWorld grid, EntityWorld world, int actorId, int targetId, out int amount)
        {
            amount = 0;
            if (!CanExecute(world, actorId)) return false;
            if (!CombatSystem.IsInAttackRange(world, actorId, targetId)) return false;

            amount = CombatSystem.CalculateDamage(world, actorId, targetId);
            var hp = world.Get<Hp>(targetId);
            hp.Value = Mathf.Max(0, hp.Value - amount);
            world.Set(targetId, hp);

            if (!UnitQueries.IsAlive(world, targetId))
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);

            return true;
        }
    }
}
