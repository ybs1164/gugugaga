using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 대상 선택 없이 자기 자신을 기준으로 즉시 발동하는 행동. DefendAction/HealAction/SelfDestructAction이
    /// 구현한다. affectedIds는 이 행동으로 값이 바뀐 "자신 이외의" 유닛 id 목록 — 호출자(BattleController)가
    /// 그 유닛들의 View만 갱신하는 데 쓴다(방어는 항상 비어있고, 치유는 회복된 아군, 자폭은 피해 입은 적).
    /// </summary>
    public interface ISelfAction : IUnitAction
    {
        bool Execute(GridWorld grid, EntityWorld world, int unitId, out List<int> affectedIds);
    }
}
