using UnityEngine;

namespace TacticsECS
{
    public enum EconomyLogKind { Capture, Research, Build, Action, Train, LevelUp, Reward, Explore, Disband }

    /// <summary>
    /// 경제 행동(점령/연구/건설/훈련/레벨업 등) 한 건의 기록. BattleLogEntry와 같은 목적 — Systems(EconomyAI,
    /// CitySystem)는 View를 모르므로 "무슨 일이 있었는지"만 값으로 돌려주고, 문구로 바꾸는 건 BattleController다.
    /// Kind == Train/Reward(SuperUnit)/Explore(유닛 보상)처럼 유닛 스폰이 필요한 항목은 BattleController가
    /// SpawnRequest를 보고 실제 스폰(View 필요)까지 이어서 처리한다.
    /// </summary>
    public struct EconomyLogEntry
    {
        public Team Team;
        public EconomyLogKind Kind;
        /// <summary>대상 이름/Id(기술 Id, 건물 Id, 유닛 CSV Id, 보상 이름 등).</summary>
        public string Subject;
        public Vector2Int Position;
        public int CityIndex;
        /// <summary>비어있지 않으면 이 유닛 CSV Id를 Position(또는 그 근처 빈 칸)에 스폰해야 한다.</summary>
        public string SpawnUnitId;
    }
}
