using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 도시 하나가 해금한 기술의 집합. 순수 데이터이며, 해금 가능 여부 판정/소모 로직은 TechSystem이
    /// 담당한다. CityResourceData와 마찬가지로 지금은 도시가 하나뿐이라 BattleController가 필드 하나로
    /// 직접 들고 있는다.
    /// </summary>
    public struct TechTreeData
    {
        public HashSet<TechId> Unlocked;

        public static TechTreeData CreateEmpty()
        {
            return new TechTreeData { Unlocked = new HashSet<TechId>() };
        }
    }
}
