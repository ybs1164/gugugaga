namespace TacticsECS
{
    /// <summary>판이 끝난 이유. None이면 아직 진행 중.</summary>
    public enum BattleEndReason
    {
        None,
        Conquest,
        FaithVictory
    }

    /// <summary>
    /// VictorySystem.Evaluate가 계산한 승패 결과. 보관·결과 창 표시는 호출자(BattleController) 몫이다.
    /// 규칙: docs/spec/game/turn.md#승패, docs/spec/game/faith.md#신앙-승리
    /// </summary>
    public struct BattleOutcome
    {
        public BattleEndReason Reason;
        public Team Winner;
    }
}
