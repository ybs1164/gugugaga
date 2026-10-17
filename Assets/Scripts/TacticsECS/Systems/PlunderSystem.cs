using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 약탈(PlunderAction) — 순수 함수형, 자체 상태 없음. 규칙: docs/spec/game/unit.md#약탈
    ///   조건: 약탈 유닛이 살아 있고 이번 턴 행동 전이며(이동했다면 돌격 필요), 대상은 공격 사거리(맨해튼, 최소 1) 안의 다른 팀 도시.
    ///   평화조약 상대 도시, 이미 침투·약탈당한 도시(주인의 다음 턴까지 — CityData.Infiltrated)는 안 된다.
    ///   효과: 약탈한 팀이 그 도시의 별 수입(CitySystem.CityStarsIncome)만큼 즉시 받고, 도시는 주인의 다음 턴에 별을 만들지 않는다.
    ///   공격으로 친다(전쟁 선포, 은신 해제, 평화주의 과업 실패). 유닛은 남고 행동을 쓴다.
    /// </summary>
    public static class PlunderSystem
    {
        public static bool CanPlunder(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, Vector2Int cityPos)
        {
            if (econ == null || !UnitQueries.IsAlive(world, unitId)) return false;
            if (UnitActionQueries.Find<PlunderAction>(world, unitId) == null || world.Get<HasActed>(unitId).Value) return false;
            if (world.Get<HasMoved>(unitId).Value && UnitActionQueries.Find<ChargeAction>(world, unitId) == null) return false;
            int dist = PathfindingSystem.Distance(world.Get<GridPosition>(unitId).Value, cityPos);
            if (dist < 1 || dist > Mathf.Max(1, world.Get<AttackRange>(unitId).Value)) return false;
            int city = CitySystem.FindCityAt(econ, cityPos);
            if (city < 0) return false;
            var c = econ.Cities[city];
            var team = world.Get<Team>(unitId);
            if (c.Owner == team || c.Infiltrated || DiplomacySystem.HasPeaceTreaty(econ, c.Owner, team)) return false;
            return VisionSystem.IsExplored(grid, team, cityPos);
        }

        /// <summary>unitId가 지금 약탈할 수 있는 도시 칸들.</summary>
        public static List<Vector2Int> Targets(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId)
        {
            var list = new List<Vector2Int>();
            if (econ == null) return list;
            foreach (var c in econ.Cities)
                if (CanPlunder(grid, world, econ, unitId, c.Position)) list.Add(c.Position);
            return list;
        }

        /// <summary>약탈한다. 빼앗은 별 수(못 하면 -1).</summary>
        public static int Plunder(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, Vector2Int cityPos, List<EconomyLogEntry> log)
        {
            if (!CanPlunder(grid, world, econ, unitId, cityPos)) return -1;
            var team = world.Get<Team>(unitId);
            int cityIndex = CitySystem.FindCityAt(econ, cityPos);
            var city = econ.Cities[cityIndex];
            int stolen = CitySystem.CityStarsIncome(grid, world, city);

            StealthSystem.Reveal(world, unitId);
            PrayerSystem.Cancel(world, unitId);
            DiplomacySystem.DeclareWar(econ, team, city.Owner);
            TaskSystem.RecordAttack(econ, team);
            world.Set(unitId, new HasActed { Value = true });

            var res = econ.Resources[team];
            res.Stars += stolen;
            econ.Resources[team] = res;
            city.Infiltrated = true;
            econ.Cities[cityIndex] = city;

            log?.Add(new EconomyLogEntry
            {
                Team = team, Kind = EconomyLogKind.Action, Position = cityPos, CityIndex = cityIndex,
                Subject = LocalizationSystem.F("UI.Event.Plunder", city.Name, stolen)
            });
            return stolen;
        }
    }
}
