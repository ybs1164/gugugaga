using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 한 팀의 과업(기념물) 진행 값. 순수 데이터 — 갱신/판정은 TaskSystem. EconomyWorld.Tasks에 팀마다 하나.
    /// 조건이 "지금 상태"로 바로 계산되는 과업(별/연결/도시 레벨/기술)도 한 번 달성하면 Completed에 남는다
    /// (위키: 황제의 무덤은 "한 번이라도 100별을 가지면").
    /// </summary>
    public class TaskProgressData
    {
        /// <summary>이번 턴 이 팀이 공격했는지(턴 종료 시 TurnsWithoutAttack 갱신에 쓰고 초기화).</summary>
        public bool AttackedThisTurn;

        /// <summary>공격 없이 끝낸 연속 턴 수.</summary>
        public int TurnsWithoutAttack;

        /// <summary>이 팀 유닛이 처치한 적 유닛 수(반격/스플래시/자폭 포함).</summary>
        public int Kills;

        /// <summary>발견한 등대 칸.</summary>
        public readonly HashSet<UnityEngine.Vector2Int> LighthousesFound = new HashSet<UnityEngine.Vector2Int>();

        /// <summary>달성한 과업 Id.</summary>
        public readonly HashSet<string> Completed = new HashSet<string>();

        /// <summary>이미 지은 기념물의 과업 Id(과업당 한 번).</summary>
        public readonly HashSet<string> MonumentsBuilt = new HashSet<string>();
    }
}
