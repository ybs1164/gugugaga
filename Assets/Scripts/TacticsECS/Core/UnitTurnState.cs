namespace TacticsECS
{
    /// <summary>
    /// 턴마다 초기화되는 유닛의 행동 상태. 순수 데이터이며 로직을 갖지 않는다.
    /// TurnManager.ResetUnitStates가 매 턴 팀 소속 유닛들의 이 값을 리셋한다.
    /// </summary>
    [System.Serializable]
    public struct UnitTurnState
    {
        public bool HasMoved;
        public bool HasActed;
        public bool IsGuarding; // CanGuard 유닛 전용: 이번 턴 방어 태세인지
    }
}
