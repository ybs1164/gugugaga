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

        /// <summary>대기(회복) 행동을 실행한다.</summary>
        public static bool TryWait(GridWorld grid, EntityWorld world, int unitId)
        {
            var wait = UnitActionQueries.Find<WaitAction>(world, unitId);
            if (wait != null) return wait.Execute(grid, world, unitId, out _);

            var fallback = new WaitAction();
            return fallback.Execute(grid, world, unitId, out _);
        }

        /// <summary>턴 종료 시 해당 팀의 모든 미행동(!HasActed) 유닛을 자동으로 대기(회복) 처리한다.</summary>
        public static List<int> ApplyTurnEndWait(GridWorld grid, EntityWorld world, Team team)
        {
            var waitedIds = new List<int>();
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (!UnitQueries.IsAlive(world, i)) continue;
                if (world.Get<Team>(i) != team) continue;
                if (world.Get<HasActed>(i).Value) continue;

                if (TryWait(grid, world, i))
                    waitedIds.Add(i);
            }
            return waitedIds;
        }
    }
}
