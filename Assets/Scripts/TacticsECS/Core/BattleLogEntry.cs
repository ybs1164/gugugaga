namespace TacticsECS
{
    /// <summary>우측 상단 행동 로그(BattleHud.AddLogEntry) 한 줄이 나타낼 수 있는 행동 종류.
    /// Counter는 Attack과 따로 둔다 — 자동 발동임을 로그 문구("반격")로 구분해서 보여주기 위함이다.</summary>
    public enum BattleLogVerb
    {
        Move,
        Attack,
        Counter,
        Defend,
        Heal,
        SelfDestruct,
        Wait,
        Defeated
    }

    /// <summary>행동 로그 한 줄에 대응하는 값 하나. 계산이나 판정 로직은 전혀 없다 — BattleController가
    /// System 호출 결과(TryAttack의 out 값, 반환된 id 목록 등)를 그대로 옮겨 담아 만들고, BattleHud에
    /// 넘기기 직전 문구로 바꾼다(BattleController.FormatLogEntry).</summary>
    [System.Serializable]
    public struct BattleLogEntry
    {
        /// <summary>TargetId가 없는 행동(이동/방어/치유/자폭/대기/쓰러짐)에 쓰는 값.</summary>
        public const int NoTarget = -1;

        public int ActorId;
        public BattleLogVerb Verb;

        /// <summary>공격/반격의 대상. 그 외 행동에서는 NoTarget.</summary>
        public int TargetId;

        /// <summary>피해량(공격/반격), 회복 인원 수(치유)·대상 수(자폭), 회복량(대기) 등 행동별 부가값.
        /// 없으면 0.</summary>
        public int Amount;
    }
}
