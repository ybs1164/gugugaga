using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 경제가 켜진 전투에서 상태가 바뀔 때마다(행동·턴 시작·AI 턴 뒤) 다시 맞춰야 하는 파생 상태 갱신.
    /// 두 단계로 나눈다: CollectEvents가 시야·과업 달성 기록을 모으면, 호출자가 그 기록(유닛 스폰 포함)을 처리한 뒤
    /// RefreshState로 나머지(대신전·신화 정리·신전 오라·생산량·기술 효과)를 맞춘다 — 새로 스폰된 유닛까지 반영되도록.
    /// </summary>
    public static class EconomyRefreshSystem
    {
        /// <summary>시야(유닛 주변/영토 밝히기, 등대 발견)와 과업 달성을 확인해 기록을 log에 더한다.</summary>
        public static void CollectEvents(GridWorld grid, EntityWorld world, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            VisionSystem.Refresh(grid, world, econ, log);
            TaskSystem.Refresh(grid, econ, log);
        }

        /// <summary>대신전 상태, 조건이 깨진 신화 유닛 정리, 신전 오라, 팀별 생산량, 기술 효과(지형 제한·방어 보너스)를
        /// 다시 계산한다. 정리돼 사라진 신화 유닛 id를 돌려준다(호출자가 View를 갱신).</summary>
        public static List<int> RefreshState(GridWorld grid, EntityWorld world, EconomyWorld econ)
        {
            GreatTempleSystem.Refresh(grid, world, econ, econ.Turn);
            var removedMythics = MythicSystem.Sweep(grid, world);
            TemplePassiveSystem.RefreshAuras(grid, world, econ);
            foreach (var team in CitySystem.Teams)
                econ.Resources[team] = CityResourceSystem.RefreshProduction(econ.Resources[team], grid, world, econ, team);
            TechEffectSystem.RefreshUnits(grid, world, econ);
            return removedMythics;
        }
    }
}
