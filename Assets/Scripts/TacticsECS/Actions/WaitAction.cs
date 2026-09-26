using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 대기(회복) 행동. 이번 턴 유닛의 행동을 종료하고 체력을 회복한다.
    /// 기본 2 회복, 자기 영토(도시 영토, CitySystem) 안에서는 4 회복.
    /// 턴 종료 시 행동하지 않은(HasActed=false) 유닛은 자동으로 이 행동이 적용된다.
    /// 모든 유닛이 기본적으로 이 행동을 보유한다.
    /// </summary>
    [System.Serializable]
    public class WaitAction : ISelfAction
    {
        public const int NormalHealAmount = 2;
        public const int OwnTerritoryHealAmount = 4;

        public ActionType GetActionType() => ActionType.Wait;

        public bool CanExecute(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !world.Get<HasActed>(unitId).Value;

        public bool Execute(GridWorld grid, EntityWorld world, int unitId, out List<int> affectedIds)
        {
            affectedIds = new List<int>();
            if (!CanExecute(world, unitId)) return false;

            int heal = CalculateHealAmount(grid, world, unitId);
            int currentHp = world.Get<Hp>(unitId).Value;
            int maxHp = world.Get<MaxHp>(unitId).Value;
            int newHp = Mathf.Min(maxHp, currentHp + heal);

            world.Set(unitId, new Hp { Value = newHp });
            world.Set(unitId, new HasActed { Value = true });
            world.Set(unitId, new HasMoved { Value = true });
            return true;
        }

        public static int CalculateHealAmount(GridWorld grid, EntityWorld world, int unitId)
        {
            return IsInOwnTerritory(grid, world, unitId) ? OwnTerritoryHealAmount : NormalHealAmount;
        }

        /// <summary>
        /// 자기 영토 내에 위치하는지 여부 — 서 있는 칸의 TileData.OwnerTeam(도시 영토, CitySystem이 채움)이
        /// 유닛의 팀과 같으면 true. 도시가 없는 씬(SampleScene)에서는 모든 칸이 중립이라 항상 false.
        /// </summary>
        public static bool IsInOwnTerritory(GridWorld grid, EntityWorld world, int unitId)
        {
            return CitySystem.IsOwnTerritory(grid, world.Get<Team>(unitId), world.Get<GridPosition>(unitId).Value);
        }
    }
}
