using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 방어 태세 행동. 별도 값은 없다 — 방어 태세 보너스(CombatSystem.GuardDefenseBonus)와 기본 방어력
    /// (UnitDefinition.Defense)은 이 행동의 보유 여부와 무관하게 항상 적용되는 값이라 여기 두지 않는다.
    /// </summary>
    [System.Serializable]
    public class DefendAction : ISelfAction
    {
        public ActionType GetActionType() => ActionType.Defend;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, out List<int> affectedIds)
        {
            affectedIds = new List<int>();
            if (!CanExecute(world, unitId)) return false;

            world.Set(unitId, new IsGuarding { Value = true });
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }
    }
}
