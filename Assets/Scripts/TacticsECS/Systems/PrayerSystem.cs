using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 기도(신전 근처 유닛의 행동). 자체 상태 없이 Praying 컴포넌트와 타일의 신전(BuildingInfo.FaithCap)만 읽고 쓴다.
    /// 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class PrayerSystem
    {
        /// <summary>유닛 주변(체비쇼프 PrayRange)에 자기 팀 영토의 신전이 있는지.</summary>
        public static bool IsNearTemple(GridWorld grid, EntityWorld world, int unitId)
        {
            var center = world.Get<GridPosition>(unitId).Value;
            int team = (int)world.Get<Team>(unitId);
            int r = GameRules.Faith.PrayRange;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                var p = new Vector2Int(center.x + dx, center.y + dy);
                if (!grid.InBounds(p)) continue;
                var tile = grid.GetTile(p);
                if (tile.OwnerTeam != team) continue;
                var info = TileImprovementSystem.FindBuilding(tile.BuildingId);
                if (info != null && info.Value.FaithCap > 0) return true;
            }
            return false;
        }

        public static bool CanPray(GridWorld grid, EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) &&
            !world.Get<HasActed>(unitId).Value &&
            !world.GetOrDefault<Praying>(unitId).Value &&
            IsNearTemple(grid, world, unitId);

        /// <summary>기도를 시작한다. 이번 턴 행동은 끝난다.</summary>
        public static bool Pray(GridWorld grid, EntityWorld world, int unitId)
        {
            if (!CanPray(grid, world, unitId)) return false;
            world.Set(unitId, new Praying { Value = true });
            world.Set(unitId, new HasMoved { Value = true });
            world.Set(unitId, new HasActed { Value = true });
            return true;
        }

        /// <summary>피격되면 기도가 강제로 풀린다.</summary>
        public static void OnHit(EntityWorld world, int unitId) => Cancel(world, unitId);

        public static void Cancel(EntityWorld world, int unitId)
        {
            if (world.GetOrDefault<Praying>(unitId).Value) world.Set(unitId, new Praying { Value = false });
        }

        /// <summary>팀의 기도 중인 유닛 수(살아 있고 아직 신전 근처인 유닛만).</summary>
        public static int CountPraying(GridWorld grid, EntityWorld world, Team team)
        {
            int count = 0;
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) != team || !UnitQueries.IsAlive(world, i)) continue;
                if (world.GetOrDefault<Praying>(i).Value && IsNearTemple(grid, world, i)) count++;
            }
            return count;
        }

        /// <summary>신전이 사라진 유닛의 기도를 푼다(턴 시작 지급 전에 호출).</summary>
        public static void ReleaseStale(GridWorld grid, EntityWorld world, Team team)
        {
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) != team) continue;
                if (world.GetOrDefault<Praying>(i).Value && (!UnitQueries.IsAlive(world, i) || !IsNearTemple(grid, world, i)))
                    Cancel(world, i);
            }
        }
    }
}
