using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// TurnSystem.StartEconomyTurn이 바꾼 유닛 목록. 호출자가 이 유닛들의 View를 다시 그린다.
    /// </summary>
    public struct EconomyTurnStartResult
    {
        /// <summary>신전 패시브(TemplePassiveSystem.OnTurnStart)에 맞은 유닛.</summary>
        public List<int> TempleHitIds;

        /// <summary>유지 기간이 끝나 돌아간 신화 유닛(MythicSystem.OnTurnStart).</summary>
        public List<int> ExpiredMythicIds;
    }
}
