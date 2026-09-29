using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 반격 패시브. 다른 행동과 달리 플레이어가 직접 고르는 "행동 버튼"이 아니라, 공격을 받았을 때
    /// CombatSystem.TryAttack이 자동으로 찾아 발동시킨다(BattleHud에서도 버튼이 아니라 정보용 배지로만
    /// 표시). CanExecute가 HasActed를 보지 않는 것도 이 때문 — 이미 이번 턴 행동을 마친 유닛도 반격은
    /// 그대로 발동해야 한다. 별도 값은 없다 — 사거리는 AttackAction의 AttackRange, 피해는 위키 공식의 방어 쪽 몫
    /// (방어력 x 공격 전 체력 비율 x 방어 보너스, CombatSystem.CalculateRetaliation). 위키에서는 뻣뻣함(Stiff)이 없는 모든
    /// 유닛이 반격하므로, 유닛 CSV에서 위키 유닛에는 이 패시브를 준다.
    /// Execute에서 팀이 같으면 실패시키는 것은, 전향(ConvertAction)으로 방금 아군이 된 대상이 반격으로
    /// 되돌려 공격하는 것을 막기 위함이다 — CombatSystem.TryAttack이 전향 처리를 반격 판정보다 먼저
    /// 하므로, 여기 도달했을 때 이미 팀이 바뀌어 있으면 전향된 경우다.
    /// </summary>
    [System.Serializable]
    public class CounterAction : ITargetedAction
    {
        public ActionType GetActionType() => ActionType.Counter;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);

        /// <summary>반격자(actorId)의 현재 체력으로 반격한다. 공격 직후의 반격은 CombatSystem.TryAttack이 공격 전 체력을 넘기는
        /// ExecuteRetaliation을 쓴다.</summary>
        public bool Execute(GridWorld grid, EntityWorld world, int actorId, int targetId, out int amount) =>
            ExecuteRetaliation(grid, world, actorId, targetId, UnitQueries.IsAlive(world, actorId) ? world.Get<Hp>(actorId).Value : 0, out amount);

        /// <summary>위키 Combat: 반격 피해 = 방어 쪽 몫(defenseResult) — 방어력과 "공격받기 전" 체력으로 계산한다. 반격 피해가 0이면
        /// 반격하지 않는다(위키 "deals 0 retaliation damage").</summary>
        public bool ExecuteRetaliation(GridWorld grid, EntityWorld world, int actorId, int targetId, int actorHpBeforeHit, out int amount)
        {
            amount = 0;
            if (!CanExecute(world, actorId)) return false;
            if (world.Get<Team>(actorId) == world.Get<Team>(targetId)) return false;
            if (!CombatSystem.IsInAttackRange(world, actorId, targetId)) return false;

            amount = CombatSystem.CalculateRetaliation(world, targetId, actorId, actorHpBeforeHit);
            if (amount <= 0) { amount = 0; return false; }
            var hp = world.Get<Hp>(targetId);
            hp.Value = Mathf.Max(0, hp.Value - amount);
            world.Set(targetId, hp);
            world.Set(targetId, new Accelerated { Value = false });

            if (!UnitQueries.IsAlive(world, targetId))
            {
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);
                VeteranSystem.RecordKill(world, actorId); // 위키: 반격 처치도 베테랑 처치 수에 든다
            }

            return true;
        }
    }
}
