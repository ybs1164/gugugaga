using UnityEngine;

namespace TacticsECS
{
    public enum GreatTempleState
    {
        /// <summary>보호 기간 진행 중. 인접 칸에 적이 들어오면 처음부터 다시 센다.</summary>
        Building,

        /// <summary>보호를 마쳐 타이브레이크를 기다리는 중.</summary>
        Waiting,
    }

    /// <summary>대신전 하나(여러 칸). 건설·보호·타이브레이크 상태 값만 담는다 — 규칙은 Systems/GreatTempleSystem, docs/spec/game/faith.md.</summary>
    public struct GreatTempleData
    {
        public Team Owner;
        public Vector2Int[] Tiles;
        public GreatTempleState State;

        /// <summary>보호 기간을 세기 시작한 턴. 적이 인접하면 지금 턴으로 다시 정해진다.</summary>
        public int StartTurn;

        /// <summary>보호를 마친 턴(State가 Waiting일 때).</summary>
        public int CompleteTurn;
    }
}
