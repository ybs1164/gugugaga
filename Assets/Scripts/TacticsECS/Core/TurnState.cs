namespace TacticsECS
{
    /// <summary>
    /// 현재 턴 진행 상태(누구 차례인지, 몇 턴째인지)를 담는 값.
    /// TurnSystem은 이 값을 들고 있지 않고 항상 인자로 받아 다음 상태를 계산해 반환하기만 한다
    /// (Systems는 상태를 갖지 않는다) — 실제 보관은 오케스트레이터인 BattleController의 필드가 맡는다.
    /// </summary>
    [System.Serializable]
    public struct TurnState
    {
        public Team ActiveTeam;
        public int TurnNumber;
    }
}
