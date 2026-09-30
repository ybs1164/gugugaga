using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 한 팀이 해금한 기술 Id의 집합. 순수 데이터이며, 해금 가능 여부 판정/소모 로직은 TechSystem이
    /// 담당한다. 팀마다 하나씩 EconomyWorld.Tech에 들어 있다.
    /// </summary>
    public struct TechTreeData
    {
        public HashSet<string> Unlocked;

        /// <summary>이 팀이 연구할 수 있는 기술 Id(종족 기술 그룹 — TechGroups.csv). null이면 트리 전체.</summary>
        public HashSet<string> Allowed;

        public static TechTreeData CreateEmpty()
        {
            return new TechTreeData { Unlocked = new HashSet<string>() };
        }
    }
}
