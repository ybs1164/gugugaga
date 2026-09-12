using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격 행동. 실행 가능 여부 판정과 피해 적용(대상 사망 시 occupant 정리 포함)을 이 클래스가 직접
    /// 담당한다 — 예전 CombatSystem.TryAttack 본체 로직이 그대로 이 안으로 옮겨왔다. CombatSystem은
    /// 이제 이 행동을 찾아 실행한 뒤, 대상의 CounterAction을 찾아 이어서 실행해주는 진입점일 뿐이다.
    /// 반격(CounterAction)은 별도 값을 갖지 않고 이 행동의 Attack/AttackRange를 그대로 재사용한다.
    /// 기본적으로 이번 턴 이미 이동한 유닛은 공격할 수 없다(이동 또는 공격 중 하나만) — 돌격
    /// (ChargeAction)을 가진 유닛만 그 제약의 예외로 이동 후에도 공격할 수 있다.
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
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value &&
            (!world.Get<HasMoved>(unitId).Value || UnitActionQueries.Find<ChargeAction>(world, unitId) != null);

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
            world.Set(targetId, new Accelerated { Value = false });
            world.Set(actorId, new HasActed { Value = true });

            if (!UnitQueries.IsAlive(world, targetId))
            {
                grid.RemoveOccupant(world.Get<GridPosition>(targetId).Value);

                // 연타: 처치했고 공격자가 이 패시브를 가졌다면, 방금 세운 HasActed를 되돌려 같은 턴에
                // 추가 공격을 허용한다.
                if (UnitActionQueries.Find<ComboAction>(world, actorId) != null)
                    world.Set(actorId, new HasActed { Value = false });
            }
            else if (UnitActionQueries.Find<FreezeAction>(world, actorId) != null)
            {
                // 빙결: 대상이 살아남았고 공격자가 이 패시브를 가졌다면, 대상의 다음 자기 턴 하나를
                // 통째로 행동불능으로 만든다(TurnSystem.ResetUnitStates가 소모).
                world.Set(targetId, new Frozen { Value = true });
            }

            // 스플래시: 공격자가 이 패시브를 가졌다면, 대상(생사 무관 — 마지막 위치 기준) 주변 1블록 내의
            // 다른 적 유닛(공격자 기준)에게도 같은 방식으로 피해를 입힌다.
            if (UnitActionQueries.Find<SplashAction>(world, actorId) != null)
                ApplySplashDamage(grid, world, actorId, targetId);

            return true;
        }

        private static void ApplySplashDamage(GridWorld grid, EntityWorld world, int actorId, int primaryTargetId)
        {
            var center = world.Get<GridPosition>(primaryTargetId).Value;
            var attackerTeam = world.Get<Team>(actorId);

            for (int i = 0; i < world.EntityCount; i++)
            {
                if (i == primaryTargetId || !UnitQueries.IsAlive(world, i)) continue;
                if (world.Get<Team>(i) == attackerTeam) continue;
                if (PathfindingSystem.Distance(center, world.Get<GridPosition>(i).Value) > 1) continue;

                int splashDamage = CombatSystem.CalculateDamage(world, actorId, i);
                var splashHp = world.Get<Hp>(i);
                splashHp.Value = Mathf.Max(0, splashHp.Value - splashDamage);
                world.Set(i, splashHp);
                world.Set(i, new Accelerated { Value = false });

                if (!UnitQueries.IsAlive(world, i))
                    grid.RemoveOccupant(world.Get<GridPosition>(i).Value);
            }
        }
    }
}
