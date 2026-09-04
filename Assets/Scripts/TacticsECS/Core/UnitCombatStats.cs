namespace TacticsECS
{
    /// <summary>
    /// 유닛의 전투 관련 고정 스탯. 전투 중 값이 바뀌지 않으며(현재 Hp 등 런타임 값은 UnitData에 있다),
    /// UnitDefinition 프리팹 컴포넌트에서 인스펙터로 채워진다.
    /// </summary>
    [System.Serializable]
    public struct UnitCombatStats
    {
        public int Attack;
        public int Defense;
        public int AttackRange;

        public bool CanGuard; // 방어 태세(CombatSystem.TryDefend) 행동이 가능한 타입인지
    }
}
