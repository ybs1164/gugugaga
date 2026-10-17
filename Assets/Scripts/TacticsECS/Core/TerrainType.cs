namespace TacticsECS
{
    /// <summary>
    /// 타일이 속한 지형의 종류(Land/Water). TileData(타일 쪽)와 MoveDomain(유닛 쪽, Core/UnitComponents.cs) 양쪽에서
    /// 재사용하는 값 타입이다. 타일은 Land/Water만 쓰고, Amphibious/Air는 유닛 이동 타입 전용 값이다.
    /// 어느 칸에 들어갈 수 있는지는 PathfindingSystem.CanEnter — 규칙: docs/spec/game/movement.md#이동-타입
    /// </summary>
    public enum TerrainType
    {
        Land,
        Water,
        /// <summary>수륙양용(유닛 전용): 육지와 얕은 물을 다닌다. 배가 되지 않는다.</summary>
        Amphibious,
        /// <summary>공중(유닛 전용): 지형을 무시하고 다닌다. 방어 보너스 없음, 배가 되지 않는다.</summary>
        Air,
    }
}
