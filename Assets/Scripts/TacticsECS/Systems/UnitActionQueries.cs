using System.Linq;

namespace TacticsECS
{
    /// <summary>
    /// 유닛의 UnitActions 컴포넌트(행동 목록)에서 원하는 종류의 행동 하나를 찾아주는 조회 헬퍼.
    /// MovementSystem/CombatSystem/AbilitySystem과 BattleController(하이라이트 계산)가 공통으로 쓴다 —
    /// "이 유닛이 이 행동을 가지고 있는가"는 ActionType 비트 비교가 아니라 이 목록에 해당 타입의 행동이
    /// 실제로 들어있는지로 판단한다("값보다 행동으로 구분").
    /// </summary>
    public static class UnitActionQueries
    {
        public static T Find<T>(EntityWorld world, int unitId) where T : class, IUnitAction
        {
            var actions = world.Get<UnitActions>(unitId).Value;
            return actions?.OfType<T>().FirstOrDefault();
        }
    }
}
