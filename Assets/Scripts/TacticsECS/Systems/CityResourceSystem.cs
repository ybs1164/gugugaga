namespace TacticsECS
{
    /// <summary>
    /// 도시 발전 자원(도시 발전도/인구/골드/신앙) 계산을 담당하는 순수 함수형 시스템. 다른 System과
    /// 마찬가지로 자체 상태는 없다 — CityResourceData 값을 인자로 받아 다음 값을 계산해 반환할 뿐이고,
    /// 실제 보관은 호출자(BattleController)가 맡는다.
    ///
    /// "일반 자원 채집(영토 내 획득)"이나 "수도 연결 보너스(도로/해로 연결 시 발전도 +1)"처럼 타일
    /// 소유권/도로 데이터가 필요한 규칙은 타일/영토 시스템이 아직 없어 구현하지 않았다(WaitAction의
    /// IsInOwnTerritory와 같은 이유) — IsConnectedToCapital이 그 자리를 나타내는 플레이스홀더다.
    /// </summary>
    public static class CityResourceSystem
    {
        /// <summary>수도(최초 시작 도시) 보너스 골드 생산량.</summary>
        public const int CapitalGoldBonus = 1;

        /// <summary>해당 팀이 보유한(=인구를 소모하는) 살아있는 유닛 수. 중립/특수 유닛처럼 인구를
        /// 소모하지 않는 유닛 구분은 아직 유닛 데이터에 없어, 지금은 팀 소속 유닛 전부를 센다.</summary>
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

        /// <summary>이 도시가 유닛을 하나 더 보유할 인구 여유가 있는지.</summary>
        public static bool HasPopulationRoom(CityResourceData city, EntityWorld world, Team team)
        {
            return CountPopulation(world, team) < city.PopulationCap;
        }

        /// <summary>매 턴 시작 시 자동 생산되는 골드/신앙을 반영한 다음 CityResourceData를 반환한다.
        /// 신앙은 보유 유닛 수만큼 늘어나되 MaxFaith를 넘지 않는다.</summary>
        public static CityResourceData ApplyTurnStart(CityResourceData city, EntityWorld world, Team team)
        {
            city.Gold += city.GoldProduction + (city.IsCapital ? CapitalGoldBonus : 0);

            int faith = city.Faith + CountPopulation(world, team);
            city.Faith = faith > city.MaxFaith ? city.MaxFaith : faith;

            return city;
        }

        /// <summary>
        /// 이 도시가 수도와 도로/해로로 연결되어 있는지 여부(연결된 도시마다 발전도 +1, 연결 해제 시 -1).
        /// 타일/영토 시스템은 현재 플레이스홀더 상태이므로 항상 false를 반환한다.
        /// 향후 타일 소유권/도로 시스템이 구현되면 실제 연결 여부를 판정해 발전도 보너스를 적용할 수 있다.
        /// </summary>
        public static bool IsConnectedToCapital(GridWorld grid, EntityWorld world, int cityTileId)
        {
            // 플레이스홀더: 영토/도로 시스템 미구현
            return false;
        }
    }
}
