namespace TacticsECS
{
    /// <summary>
    /// "유닛"이라는 의미를 해석하는 쪽은 EntityWorld가 아니라 이런 System이어야 한다.
    /// Hp/Team 같은 컴포넌트를 조합해 판정하는, 여러 System이 공통으로 쓰는 조회 로직 모음.
    /// </summary>
    public static class UnitQueries
    {
        public static bool IsAlive(EntityWorld world, int id) => world.Get<Hp>(id).Value > 0;

        public static bool AnyAlive(EntityWorld world, Team team)
        {
            for (int i = 0; i < world.EntityCount; i++)
                if (world.Get<Team>(i) == team && IsAlive(world, i)) return true;
            return false;
        }
    }
}
