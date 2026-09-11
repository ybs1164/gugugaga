using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 치유 행동. 사거리 내의 모든 아군(자신 제외)의 체력을 회복시킨다 — 예전 AbilitySystem.TryHeal
    /// 본체 로직이 그대로 이 안으로 옮겨왔다.
    /// </summary>
    [System.Serializable]
    public class HealAction : ISelfAction
    {
        [Tooltip("치유 행동을 가진 유닛이 회복시키는 체력량.")]
        [SerializeField] private int healAmount;
        [Tooltip("치유가 닿는 사거리(맨해튼 거리). 사거리 내의 모든 아군(자신 제외)이 대상이 된다.")]
        [SerializeField] private int healRange;

        public ActionType GetActionType() => ActionType.Heal;

        /// <summary>CSV 행(UnitCsvRow)의 Heal 파라미터로 인스턴스를 만든다.</summary>
        public static HealAction FromCsv(int healAmount, int healRange) =>
            new HealAction { healAmount = healAmount, healRange = healRange };

        public int HealAmount => healAmount;
        public int HealRange => healRange;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, out List<int> affectedIds)
        {
            affectedIds = new List<int>();
            if (!CanExecute(world, unitId)) return false;

            var healerPos = world.Get<GridPosition>(unitId).Value;
            var team = world.Get<Team>(unitId);
            int range = world.Get<HealRange>(unitId).Value;
            int amount = world.Get<HealAmount>(unitId).Value;

            for (int i = 0; i < world.EntityCount; i++)
            {
                if (i == unitId || !UnitQueries.IsAlive(world, i) || world.Get<Team>(i) != team) continue;
                if (PathfindingSystem.Distance(healerPos, world.Get<GridPosition>(i).Value) > range) continue;

                var hp = world.Get<Hp>(i);
                int maxHp = world.Get<MaxHp>(i).Value;
                int healedValue = Mathf.Min(maxHp, hp.Value + amount);
                if (healedValue == hp.Value) continue;

                hp.Value = healedValue;
                world.Set(i, hp);
                affectedIds.Add(i);
            }

            world.Set(unitId, new HasActed { Value = true });
            return true;
        }
    }
}
