using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 치유/자폭 행동의 진입점. 실제 판정/효과 적용은 HealAction/SelfDestructAction
    /// (Assets/Scripts/TacticsECS/Actions)이 직접 담당하고, 이 System은 UnitActionQueries로 그 행동을
    /// 찾아 위임할 뿐이다.
    /// </summary>
    public static class AbilitySystem
    {
        public static bool TryHeal(GridWorld grid, EntityWorld world, int healerId, out List<int> healedIds)
        {
            var heal = UnitActionQueries.Find<HealAction>(world, healerId);
            if (heal == null) { healedIds = new List<int>(); return false; }
            return heal.Execute(grid, world, healerId, out healedIds);
        }

        public static bool TrySelfDestruct(GridWorld grid, EntityWorld world, int unitId, out List<int> damagedIds)
        {
            var selfDestruct = UnitActionQueries.Find<SelfDestructAction>(world, unitId);
            if (selfDestruct == null) { damagedIds = new List<int>(); return false; }
            return selfDestruct.Execute(grid, world, unitId, out damagedIds);
        }
    }
}
