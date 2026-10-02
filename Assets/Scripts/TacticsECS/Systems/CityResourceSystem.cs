namespace TacticsECS
{
    /// <summary>
    /// 팀 자원(별/유닛 수용량) 계산을 담당하는 순수 함수형 시스템. 자체 상태는 없다 —
    /// CityResourceData 값을 인자로 받아 다음 값을 계산해 반환할 뿐이고, 실제 보관은 EconomyWorld가 맡는다.
    ///
    /// 예전엔 타일/영토 시스템이 없어 생산량이 인스펙터 고정값이었고 수도 연결 판정은 항상 false인
    /// 플레이스홀더였다. 이제 도시(CityData)/영토(TileData.OwnerCity)/도로·항구가 생겨서 생산량은 도시 목록으로
    /// 계산하고(CitySystem.StarsIncome/UnitCapacity), 수도 연결은 CitySystem.RefreshConnections가
    /// 도로망을 탐색해 CityData.ConnectedToCapital에 기록한다.
    /// </summary>
    public static class CityResourceSystem
    {
        /// <summary>해당 팀에서 도시 자리를 차지하는(소속 도시가 있는) 살아있는 유닛 수 — 자원 바의 "유닛 수/수용량"(CitySystem.TakesUnitSlot).</summary>
        public static int CountPopulation(EntityWorld world, Team team)
        {
            int count = 0;
            for (int i = 0; i < world.EntityCount; i++)
                if (world.Get<Team>(i) == team && CitySystem.TakesUnitSlot(world, i)) count++;
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
            resources.StarsProduction = CitySystem.StarsIncome(grid, world, econ, team);
            resources.PopulationCap = CitySystem.UnitCapacity(econ, team);
            resources.IsCapital = CitySystem.FindCapital(econ, team) >= 0;
            return resources;
        }

        /// <summary>매 턴 시작 시 도시에서 생산한 별을 한 번 지급한다.</summary>
        public static CityResourceData ApplyTurnStart(CityResourceData resources, GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            resources = RefreshProduction(resources, grid, world, econ, team);
            resources.Stars += resources.StarsProduction;

            // 침투당한 도시는 이번(주인의) 턴 수입이 0이었다 — 이제 해제한다(위키 Cloak).
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                var c = econ.Cities[i];
                if (c.Owner != team || !c.Infiltrated) continue;
                c.Infiltrated = false;
                econ.Cities[i] = c;
            }
            resources = RefreshProduction(resources, grid, world, econ, team);
            return resources;
        }

        /// <summary>
        /// 이 도시가 수도와 도로/해로로 연결되어 있는지(연결 시 도시와 수도 인구 +1). 실제 탐색은
        /// CitySystem.RefreshConnections가 도로/도시/항구를 따라 수행하고 결과를 CityData에 기록한다.
        /// </summary>
        public static bool IsConnectedToCapital(EconomyWorld econ, int cityIndex)
        {
            return econ != null && cityIndex >= 0 && cityIndex < econ.Cities.Count && econ.Cities[cityIndex].ConnectedToCapital;
        }
    }
}
