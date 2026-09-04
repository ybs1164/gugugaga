namespace TacticsECS
{
    /// <summary>
    /// 근접(Melee) / 원거리(Ranged) / 방어(Guard) 세 타입.
    /// 타입별 스탯/사거리 기본값은 UnitData.Create에서 정의한다.
    /// </summary>
    public enum UnitType
    {
        Melee,
        Ranged,
        Guard
    }
}
