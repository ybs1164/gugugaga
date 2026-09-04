namespace TacticsECS
{
    /// <summary>
    /// 유닛 타입별 기본 스탯 값. 코드에 하드코딩하지 않고 UnitDefinition 프리팹 컴포넌트에서
    /// 인스펙터로 채워 넣는다. 순수 데이터이며 로직을 갖지 않는다.
    /// </summary>
    [System.Serializable]
    public struct UnitStats
    {
        public int MaxHp;
        public int Attack;
        public int Defense;

        public int MoveRange;
        public int AttackRange;

        public bool CanGuard; // 방어 태세(CombatSystem.TryDefend) 행동이 가능한 타입인지
    }
}
