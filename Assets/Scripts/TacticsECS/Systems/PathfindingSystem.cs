using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 순수 함수 형태의 시스템(정적 클래스). GridWorld/EntityWorld 데이터만 읽어서 결과를 계산하고,
    /// 상태는 전혀 들고 있지 않는다. 이동하는 엔티티의 컴포넌트(MoveRange/Accelerated/MoveDomain/Embarked)와 보유 패시브
    /// (IgnoreTerrainAction/IgnoreUnitBlockingAction/AllowDiagonalAction/InfiltrateAction)에 따라 실효 이동 거리/대각선
    /// 이동/지형 무시/유닛 무시 여부가 달라진다.
    ///
    /// 폴리토피아 위키 Movement 규칙:
    ///   - 이동 비용 1, 도로(또는 도시/마을/다리) 칸끼리의 이동은 0.5. 적 영토의 도로는 쓸 수 없다. 반 칸을 정수로 다루려고
    ///     내부 비용은 2배(일반 2, 도로 1)로, 이동력도 2배로 계산한다.
    ///   - 험지(숲/산, 도로가 없는 숲)에 들어가면 그 턴에는 더 못 간다(들어가는 것은 가능).
    ///   - 적 유닛과 인접한 칸(Zone of Control)에 들어가면 더 못 간다. 잠입(Infiltrate)/유닛 무시 패시브는 예외(위키 Creep).
    ///   - 구름(아직 탐험하지 않은 칸, VisionSystem)에는 들어갈 수 없다.
    ///   - 육지 유닛은 다리가 놓인 물 칸을 육지처럼 지나가고, 자기 항구 칸에 들어가면 뗏목이 되므로 거기서 멈춘다.
    ///     배(승선 유닛)는 물을 다니다 육지 칸에 내릴 수 있고 내리면 멈춘다(위키 Carry).
    /// 원문과 다른 점: 이 프로젝트는 기본 4방향 이동(AllowDiagonal 패시브만 8방향)이라 Zone of Control의 "인접"도 이동 방향과
    /// 같은 기준(4방향, 대각선 패시브면 8방향)으로 본다.
    /// </summary>
    public static class PathfindingSystem
    {
        /// <summary>내부 비용 단위: 일반 칸 한 걸음 = 2, 도로 칸끼리 = 1.</summary>
        public const int StepCost = 2;
        public const int RoadStepCost = 1;

        /// <summary>
        /// start에서 selfUnitId의 실효 이동 거리(MovementSystem.EffectiveMoveRange — 가속 보너스 포함)
        /// 이내로 이동 가능한 모든 타일을 계산한다(도로 0.5 비용/험지·ZoC 정지/구름/다리/항구 규칙 포함 — 클래스 설명).
        /// IgnoreUnitBlockingAction이 없으면 다른 유닛이 서 있는 타일은 통과/정지 모두 불가(자기 자신은 예외),
        /// AllowDiagonalAction이 있으면 8방향, 없으면 4방향으로 탐색한다.
        /// 반환값은 각 타일의 직전 타일(경로 역추적용), reachableSet은 도달 가능한 타일 전체.
        /// </summary>
        public static Dictionary<Vector2Int, Vector2Int> GetReachable(
            GridWorld grid, EntityWorld world, Vector2Int start, int selfUnitId,
            out HashSet<Vector2Int> reachableSet)
        {
            int budget = MovementSystem.EffectiveMoveRange(world, selfUnitId) * StepCost;
            bool ignoreUnitBlocking = UnitActionQueries.Find<IgnoreUnitBlockingAction>(world, selfUnitId) != null;
            bool allowDiagonal = UnitActionQueries.Find<AllowDiagonalAction>(world, selfUnitId) != null;
            bool ignoreZoc = ignoreUnitBlocking || UnitActionQueries.Find<InfiltrateAction>(world, selfUnitId) != null;
            var team = world.Get<Team>(selfUnitId);

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var cost = new Dictionary<Vector2Int, int> { [start] = 0 };
            var stopped = new HashSet<Vector2Int>();
            var buckets = new List<Vector2Int>[budget + 1];
            for (int i = 0; i <= budget; i++) buckets[i] = new List<Vector2Int>();
            buckets[0].Add(start);

            for (int c = 0; c <= budget; c++)
            {
                for (int k = 0; k < buckets[c].Count; k++)
                {
                    var cur = buckets[c][k];
                    if (cost[cur] != c || stopped.Contains(cur)) continue;

                    foreach (var next in grid.GetNeighbors(cur, allowDiagonal))
                    {
                        if (!CanEnter(grid, world, selfUnitId, next, out bool stop)) continue;
                        if (IsBlockedByOccupant(grid, world, selfUnitId, next, ignoreUnitBlocking)) continue;
                        if (!ignoreZoc && IsInEnemyZone(grid, world, team, next, allowDiagonal)) stop = true;

                        int nc = c + (IsRoadStep(grid, world, selfUnitId, cur, next) ? RoadStepCost : StepCost);
                        if (nc > budget || (cost.TryGetValue(next, out var old) && old <= nc)) continue;
                        cost[next] = nc;
                        cameFrom[next] = cur;
                        if (stop) stopped.Add(next); else stopped.Remove(next);
                        buckets[nc].Add(next);
                    }
                }
            }

            reachableSet = new HashSet<Vector2Int>(cost.Keys);
            return cameFrom;
        }

        public static int Distance(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>
        /// unitId가 tile에 들어갈(설) 수 있는지 — 구름/지형(MoveDomain, 다리, 항구 승선, 배의 하선)/기술 잠금 판정.
        /// stop이면 들어간 뒤 그 턴에 더 움직일 수 없다(험지, 승선, 하선). 유닛 점유/ZoC는 여기서 보지 않는다.
        /// GetReachable(경로 탐색)과 MoveAction.Execute(실제 이동)가 공유한다.
        /// </summary>
        public static bool CanEnter(GridWorld grid, EntityWorld world, int unitId, Vector2Int tile, out bool stop)
        {
            stop = false;
            if (!grid.InBounds(tile)) return false;
            var team = world.Get<Team>(unitId);
            if (!VisionSystem.IsExplored(grid, team, tile)) return false;
            if (UnitActionQueries.Find<IgnoreTerrainAction>(world, unitId) != null) return true;
            if (!grid.IsWalkable(tile) || TechEffectSystem.IsTerrainLocked(grid, world, unitId, tile)) return false;

            var t = grid.GetTile(tile);
            bool embarked = world.GetOrDefault<Embarked>(unitId).Value;
            var domain = world.Get<MoveDomain>(unitId).Value;

            if (embarked)
            {
                if (t.Terrain == TerrainType.Water) return true;
                stop = true; // 하선
                return true;
            }
            if (domain == TerrainType.Water) return t.Terrain == TerrainType.Water;

            // 육지 유닛
            if (t.Terrain == TerrainType.Water)
            {
                if (t.BuildingId == BuildingDefinition.Bridge && !IsEnemyTerritory(t, team)) return true;
                if (t.BuildingId == BuildingDefinition.Port && t.OwnerTeam == (int)team) { stop = true; return true; }
                return false;
            }
            var cls = TileImprovementSystem.Classify(grid, tile);
            if (cls == TileClass.Mountain || (cls == TileClass.Forest && !IsRoadFor(grid, team, tile))) stop = true;
            return true;
        }

        /// <summary>이 칸이 team에게 "도로"인지(도로/다리/도시·마을 칸, 적 영토는 제외) — 위키 Movement#Roads.</summary>
        public static bool IsRoadFor(GridWorld grid, Team team, Vector2Int p)
        {
            var t = grid.GetTile(p);
            if (IsEnemyTerritory(t, team)) return false;
            return t.HasRoad || t.BuildingId == BuildingDefinition.Bridge || CitySystem.IsSettlementTile(grid, p);
        }

        private static bool IsEnemyTerritory(TileData t, Team team) => t.OwnerTeam != TileData.NoOwner && t.OwnerTeam != (int)team;

        /// <summary>cur → next가 도로 비용(0.5)인지: 배가 아닌 유닛이 도로 칸에서 도로 칸으로 갈 때.</summary>
        private static bool IsRoadStep(GridWorld grid, EntityWorld world, int unitId, Vector2Int cur, Vector2Int next)
        {
            if (world.GetOrDefault<Embarked>(unitId).Value || world.Get<MoveDomain>(unitId).Value == TerrainType.Water) return false;
            var team = world.Get<Team>(unitId);
            return IsRoadFor(grid, team, cur) && IsRoadFor(grid, team, next);
        }

        /// <summary>tile 이웃(이동 방향 기준)에 살아있는 적 유닛이 있는지 — Zone of Control.</summary>
        public static bool IsInEnemyZone(GridWorld grid, EntityWorld world, Team team, Vector2Int tile, bool allowDiagonal)
        {
            foreach (var n in grid.GetNeighbors(tile, allowDiagonal))
            {
                int occ = grid.GetOccupant(n);
                if (occ != TileData.NoOccupant && UnitQueries.IsAlive(world, occ) && world.Get<Team>(occ) != team) return true;
            }
            return false;
        }

        /// <summary>tile의 점유자가 selfUnitId의 이동을 막는지 판정한다(빈 타일/자기 자신은 막지 않음).
        /// ignoreUnitBlocking이 true면 점유자가 누구든 무시하고, 그렇지 않아도 selfUnitId가 잠입
        /// (InfiltrateAction)을 가졌으면 적 팀 점유자에 의한 차단만 추가로 무시한다 — 아군에 의한
        /// 차단은 잠입으로도 무시되지 않는다. GetReachable(경로 탐색)과 MoveAction.Execute(실제 이동)가
        /// 이 판정을 공유한다.</summary>
        public static bool IsBlockedByOccupant(GridWorld grid, EntityWorld world, int selfUnitId, Vector2Int tile, bool ignoreUnitBlocking)
        {
            int occ = grid.GetOccupant(tile);
            if (occ == TileData.NoOccupant || occ == selfUnitId) return false;
            if (ignoreUnitBlocking) return false;
            if (world.Get<Team>(occ) != world.Get<Team>(selfUnitId) &&
                UnitActionQueries.Find<InfiltrateAction>(world, selfUnitId) != null) return false;
            return true;
        }
    }
}
