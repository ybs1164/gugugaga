using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격 행동. 실행 가능 여부 판정과 피해 적용(대상 사망 시 occupant 정리 포함)을 이 클래스가 직접
    /// 담당한다 — 예전 CombatSystem.TryAttack 본체 로직이 그대로 이 안으로 옮겨왔다. CombatSystem은
    /// 이제 이 행동을 찾아 실행한 뒤, 대상의 CounterAction을 찾아 이어서 실행해주는 진입점일 뿐이다.
    /// 반격(CounterAction)은 별도 값을 갖지 않고 이 행동의 Attack/AttackRange를 그대로 재사용한다.
    /// </summary>
    [System.Serializable]
    public class AttackAction : ITargetedAction
    {
        [SerializeField] private int attack;
        [SerializeField] private int attackRange;

        public ActionType GetActionType() => ActionType.Attack;

        /// <summary>CSV 행(UnitCsvRow)의 Attack 파라미터로 인스턴스를 만든다.</summary>
        public static AttackAction FromCsv(int attack, int attackRange) =>
            new AttackAction { attack = attack, attackRange = attackRange };

        public int Attack => attack;
        public int AttackRange => attackRange;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int actorId, int targetId, out int amount)
        {
            amount = 0;
            if (!CanExecute(world, actorId)) return false;
            if (!UnitQueries.IsAlive(world, targetId)) return false;
            if (world.Get<Team>(actorId) == world.Get<Team>(targetId)) return false;
            if (!CombatSystem.IsInAttackRange(world, actorId, targetId)) return false;

            amount = CombatSystem.CalculateDamage(world, actorId, targetId);
            var hp = world.Get<Hp>(targetId);
            hp.Value = Mathf.Max(0, hp.Value - amount);
            world.Set(targetId, hp);
            world.Set(actorId, new HasActed { Value = true });

            if (!UnitQueries.IsAlive(world, targetId))
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);

            return true;
        }
    }
}
