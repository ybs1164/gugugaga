namespace TacticsECS
{
    /// <summary>
    /// 팀 자원(도시 발전도/인구/골드/신앙) 계산을 담당하는 순수 함수형 시스템. 자체 상태는 없다 —
    /// CityResourceData 값을 인자로 받아 다음 값을 계산해 반환할 뿐이고, 실제 보관은 EconomyWorld가 맡는다.
    ///
    /// 예전엔 타일/영토 시스템이 없어 생산량이 인스펙터 고정값이었고 수도 연결 판정은 항상 false인
    /// 플레이스홀더였다. 이제 도시(CityData)/영토(TileData.OwnerCity)/도로·항구가 생겨서 생산량은 도시 목록으로
    /// 계산하고(CitySystem.GoldIncome/DevelopmentIncome/UnitCapacity), 수도 연결은 CitySystem.RefreshConnections가
    /// 도로망을 탐색해 CityData.ConnectedToCapital에 기록한다.
    /// </summary>
    public static class CityResourceSystem
    {
        /// <summary>해당 팀이 보유한(=인구를 소모하는) 살아있는 유닛 수.</summary>
        public static int CountPopulation(EntityWorld world, Team team)
        {
            int count = 0;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (UnitQueries.IsAlive(world, i) && world.Get<Team>(i) == team)
                    count++;
            }
            return count;
        }

        /// <summary>이 팀이 유닛을 하나 더 보유할 인구 여유가 있는지.</summary>
        public static bool HasPopulationRoom(CityResourceData resources, EntityWorld world, Team team)
        {
            return CountPopulation(world, team) < resources.PopulationCap;
        }

        /// <summary>생산량/인구 상한만 도시 목록으로 다시 계산한다(자원 자체는 늘리지 않음) — 건설/점령 직후
        /// HUD에 바뀐 수입을 바로 보여줄 때 쓴다.</summary>
        public static CityResourceData RefreshProduction(CityResourceData resources, GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            resources.GoldProduction = CitySystem.GoldIncome(grid, world, econ, team);
            resources.DevelopmentProduction = CitySystem.DevelopmentIncome(econ, team);
            resources.PopulationCap = CitySystem.UnitCapacity(econ, team);
            resources.IsCapital = CitySystem.FindCapital(econ, team) >= 0;
            return resources;
        }

        /// <summary>매 턴 시작 시 자동 생산되는 발전도/골드/신앙을 반영한 다음 값을 반환한다. 신앙은 보유 유닛
        /// 수만큼 늘어나되 MaxFaith를 넘지 않는다.</summary>
        public static CityResourceData ApplyTurnStart(CityResourceData resources, GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            resources = RefreshProduction(resources, grid, world, econ, team);
            resources.Development += resources.DevelopmentProduction;
            resources.Gold += resources.GoldProduction;

            int faith = resources.Faith + CountPopulation(world, team);
            resources.Faith = faith > resources.MaxFaith ? resources.MaxFaith : faith;
            return resources;
        }

        /// <summary>
        /// 이 도시가 수도와 도로/해로로 연결되어 있는지(연결된 도시마다 발전도 +1, 인구 +1). 실제 탐색은
        /// CitySystem.RefreshConnections가 도로/도시/항구를 따라 수행하고 결과를 CityData에 기록한다.
        /// </summary>
        public static bool IsConnectedToCapital(EconomyWorld econ, int cityIndex)
        {
            return econ != null && cityIndex >= 0 && cityIndex < econ.Cities.Count && econ.Cities[cityIndex].ConnectedToCapital;
        }
    }
}
