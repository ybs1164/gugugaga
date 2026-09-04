namespace TacticsECS
{
    /// <summary>
    /// 유닛 한 종류(타입)의 전투 중 변하지 않는 고정 스탯 모음.
    /// UnitDefinition 프리팹 컴포넌트에서 인스펙터로 채워지고, 스폰 시 UnitWorld에 UnitData와
    /// 별도로 저장된다 (UnitData는 Hp/GridPos/행동 여부처럼 매 턴 바뀌는 값만 갖는다).
    /// </summary>
    [System.Serializable]
    public struct UnitStats
    {
        public int MaxHp;
        public UnitCombatStats Combat;
        public UnitMovement Movement;
    }
}
