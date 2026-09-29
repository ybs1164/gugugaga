using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 은신(위키 Hide/Cloak)의 보이기·드러나기 판정. 순수 함수형, 자체 상태 없음(값은 Hidden 컴포넌트).
    ///   - 은신(HideAction) 유닛은 이동하면 숨는다(OnMoved). 공격/점령/유적 탐험/훈련 때, 또는 적이 그 칸에 들어가려 할 때 드러난다(Reveal).
    ///   - 숨은 유닛은 다른 팀에게 목표가 되지 않는다(IsHiddenFrom) — 공격 대상/AI가 보는 적/영향권(ZoC)/경로 차단에서 빠진다.
    ///   - 적 유닛 바로 옆(8방향)에 숨은 유닛이 있으면 그 적은 "근처에 숨은 적이 있다"는 것만 안다(AdjacentHiddenEnemy — 위키 눈 표시).
    /// </summary>
    public static class StealthSystem
    {
        public static bool IsHidden(EntityWorld world, int unitId) => world.GetOrDefault<Hidden>(unitId).Value;

        /// <summary>unitId가 viewerTeam에게 숨겨져 있는지(같은 팀에게는 항상 보인다).</summary>
        public static bool IsHiddenFrom(EntityWorld world, int unitId, Team viewerTeam) =>
            UnitQueries.IsAlive(world, unitId) && world.Get<Team>(unitId) != viewerTeam && IsHidden(world, unitId);

        public static void OnMoved(EntityWorld world, int unitId)
        {
            if (UnitActionQueries.Find<HideAction>(world, unitId) == null) return;
            var h = world.GetOrDefault<Hidden>(unitId);
            h.Value = true;
            world.Set(unitId, h);
        }

        public static void Reveal(EntityWorld world, int unitId)
        {
            var h = world.GetOrDefault<Hidden>(unitId);
            if (!h.Value) return;
            h.Value = false;
            world.Set(unitId, h);
        }

        /// <summary>자기 팀 턴 시작: 지금 숨어 있는지를 AtTurnStart에 기록한다(TurnSystem).</summary>
        public static void OnTurnStart(EntityWorld world, int unitId)
        {
            var h = world.GetOrDefault<Hidden>(unitId);
            h.AtTurnStart = h.Value;
            world.Set(unitId, h);
        }

        /// <summary>unitId 주변 8칸에 다른 팀의 숨은 유닛이 있는지(위키: 인접한 숨은 Cloak은 눈 표시로 감지된다 — 위치는 모른다).</summary>
        public static bool AdjacentHiddenEnemy(GridWorld grid, EntityWorld world, int unitId)
        {
            var team = world.Get<Team>(unitId);
            foreach (var n in grid.GetNeighbors(world.Get<GridPosition>(unitId).Value, true))
            {
                int occ = grid.GetOccupant(n);
                if (occ != TileData.NoOccupant && IsHiddenFrom(world, occ, team)) return true;
            }
            return false;
        }
    }
}
