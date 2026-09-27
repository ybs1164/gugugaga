using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 시야(구름) — 폴리토피아 위키 Terrain(Cloud)/Unit Skills(Scout)/Explorer/Lighthouse 규칙. 순수 함수형, 자체 상태 없음.
    /// 팀마다 "탐험한 칸"을 TileData.ExploredMask 비트로 기록하고(한 번 밝히면 계속 보인다), GridWorld.FogEnabled가
    /// false면(경제 없는 씬/옛 검증) 모든 판정이 "보임"을 돌려준다. 상수는 Data/VisionDefinition.cs.
    /// </summary>
    public static class VisionSystem
    {
        public static int TeamBit(Team team) => 1 << (int)team;

        public static bool IsExplored(GridWorld grid, Team team, Vector2Int p)
        {
            if (!grid.InBounds(p)) return false;
            return !grid.FogEnabled || (grid.GetTile(p).ExploredMask & TeamBit(team)) != 0;
        }

        public static int CountExplored(GridWorld grid, Team team)
        {
            int n = 0;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (IsExplored(grid, team, new Vector2Int(x, y))) n++;
            return n;
        }

        /// <summary>center 주변 반경 radius(체비쇼프) 칸을 team에게 밝힌다. 새로 밝혀진 칸 수를 돌려준다.</summary>
        public static int Reveal(GridWorld grid, Team team, Vector2Int center, int radius)
        {
            int bit = TeamBit(team);
            int revealed = 0;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var p = center + new Vector2Int(dx, dy);
                if (!grid.InBounds(p)) continue;
                var t = grid.GetTile(p);
                if ((t.ExploredMask & bit) != 0) continue;
                t.ExploredMask |= bit;
                grid.SetTile(p, t);
                revealed++;
            }
            return revealed;
        }

        /// <summary>유닛이 밝히는 반경: VisionRange(기본 1, 정찰 2), 산 위면 최소 2.</summary>
        public static int SightRadius(GridWorld grid, EntityWorld world, int unitId)
        {
            int r = Mathf.Max(VisionDefinition.BaseSightRadius, world.GetOrDefault<VisionRange>(unitId).Value);
            var pos = world.Get<GridPosition>(unitId).Value;
            if (grid.GetTileType(pos) == TerrainGenerationSystem.MountainTileId) r = Mathf.Max(r, VisionDefinition.ExtendedSightRadius);
            return Mathf.Min(r, VisionDefinition.ExtendedSightRadius);
        }

        /// <summary>시야 전체 갱신: 살아있는 모든 유닛 주변 + 모든 영토 칸을 그 팀에게 밝히고, 경제가 있으면 처음 밝힌
        /// 등대를 처리한다(수도 인구 +1, 탐험가 과업). 이동/스폰/점령/턴 시작 뒤에 부른다.</summary>
        public static void Refresh(GridWorld grid, EntityWorld world, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            if (!grid.FogEnabled) return;
            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;
                Reveal(grid, world.Get<Team>(id), world.Get<GridPosition>(id).Value, SightRadius(grid, world, id));
            }
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                var t = grid.GetTile(p);
                if (t.OwnerTeam == TileData.NoOwner) continue;
                Reveal(grid, (Team)t.OwnerTeam, p, 0);
            }
            if (econ != null)
            {
                RevealCapitals(grid, econ);
                ProcessLighthouses(grid, econ, log);
            }
        }

        /// <summary>수도 시야(Vision.Capital)를 가진 팀에게 다른 팀 수도 칸을 밝힌다.</summary>
        public static void RevealCapitals(GridWorld grid, EconomyWorld econ)
        {
            foreach (var team in CitySystem.Teams)
            {
                if (!econ.Tech.TryGetValue(team, out var tech) || !TechSystem.HasUnlock(econ.TechNodes, tech, VisionDefinition.CapitalVisionKey)) continue;
                foreach (var city in econ.Cities)
                    if (city.IsCapital && city.Owner != team) Reveal(grid, team, city.Position, 0);
            }
        }

        /// <summary>팀이 새로 밝힌 등대마다 수도 인구 +1(위키 Lighthouse — 수도가 없으면 가장 오래된 도시).</summary>
        public static void ProcessLighthouses(GridWorld grid, EconomyWorld econ, List<EconomyLogEntry> log)
        {
            if (!grid.FogEnabled) return;
            var lighthouses = LighthousePositions(grid);
            foreach (var team in CitySystem.Teams)
            {
                if (!econ.Tasks.TryGetValue(team, out var tasks)) continue;
                foreach (var p in lighthouses)
                {
                    if (tasks.LighthousesFound.Contains(p) || !IsExplored(grid, team, p)) continue;
                    tasks.LighthousesFound.Add(p);
                    int city = CitySystem.FindCapital(econ, team);
                    if (city < 0) city = OldestCity(econ, team);
                    log?.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Discover, Subject = "등대 발견", Position = p, CityIndex = city });
                    if (city >= 0) CitySystem.AddPopulation(econ, city, VisionDefinition.LighthousePopulation, log);
                }
            }
        }

        public static List<Vector2Int> LighthousePositions(GridWorld grid)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (grid.GetStructure(p) == StructureGenerationSystem.LighthouseStructureId) list.Add(p);
            }
            return list;
        }

        private static int OldestCity(EconomyWorld econ, Team team)
        {
            for (int i = 0; i < econ.Cities.Count; i++)
                if (econ.Cities[i].Owner == team) return i;
            return -1;
        }

        // ---------- 탐험가 ----------

        /// <summary>탐험가가 이 칸에 설 수 있는지(위키: 산/얕은 물/깊은 바다는 등산/배 타기/항해 필요, 유닛 위도 통과).</summary>
        public static bool CanExplorerEnter(GridWorld grid, EconomyWorld econ, Team team, Vector2Int p)
        {
            if (!grid.InBounds(p) || !grid.IsWalkable(p)) return false;
            var nodes = econ.TechNodes;
            var tech = econ.Tech[team];
            bool Gate(string key) => !TechSystem.IsKeyGated(nodes, key) || TechSystem.HasUnlock(nodes, tech, key);
            switch (TileImprovementSystem.Classify(grid, p))
            {
                case TileClass.Mountain: return Gate(VisionDefinition.ExplorerMountainKey);
                case TileClass.ShallowWater: return Gate(VisionDefinition.ExplorerShallowWaterKey);
                case TileClass.Ocean: return Gate(VisionDefinition.ExplorerOceanKey);
                default: return true;
            }
        }

        /// <summary>
        /// 탐험가 한 명을 start에서 출발시켜 VisionDefinition.ExplorerMoves번 움직인다(위키 Explorer "Movement Details"):
        ///   1. 4칸 이내에서 가장 가까운 구름 칸 쪽으로 한 걸음(8방향).
        ///   2. 같은 거리면 그 걸음으로 걷히는 구름이 많은 쪽(최대 4로 캡) + 등대가 있으면 가산점.
        ///   3. 이미 지나온 칸은 감점(되돌아가기 회피), 그래도 같으면 무작위(econ.RandomCounter — 재현 가능).
        ///   4. 4칸 안에 구름이 없으면 아무 이웃으로(되돌아가기 회피).
        /// 움직일 때마다 주변 3x3을 밝힌다. 마지막에 등대 발견을 처리한다.
        /// </summary>
        public static void RunExplorer(GridWorld grid, EconomyWorld econ, Team team, Vector2Int start, List<EconomyLogEntry> log)
        {
            if (!grid.FogEnabled) return;
            var pos = start;
            Reveal(grid, team, pos, VisionDefinition.BaseSightRadius);
            var visited = new HashSet<Vector2Int> { pos };

            for (int move = 0; move < VisionDefinition.ExplorerMoves; move++)
            {
                // BFS: 각 칸까지의 거리와 "그 칸으로 가는 첫 걸음". 가장 가까운 구름 거리의 첫 걸음들만 후보로 모은다.
                var dist = new Dictionary<Vector2Int, int> { [pos] = 0 };
                var firstStep = new Dictionary<Vector2Int, Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(pos);
                int bestFogDist = int.MaxValue;
                var stepScores = new Dictionary<Vector2Int, float>();
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    int d = dist[cur];
                    if (d >= bestFogDist || d >= VisionDefinition.ExplorerScanRange) continue;
                    foreach (var n in grid.GetNeighbors(cur, true))
                    {
                        if (dist.ContainsKey(n) || !CanExplorerEnter(grid, econ, team, n)) continue;
                        dist[n] = d + 1;
                        var step = d == 0 ? n : firstStep[cur];
                        firstStep[n] = step;
                        if (!IsExplored(grid, team, n) && d + 1 <= bestFogDist)
                        {
                            bestFogDist = d + 1;
                            float score = FogScore(grid, team, step);
                            if (!stepScores.TryGetValue(step, out var s) || score > s) stepScores[step] = score;
                        }
                        queue.Enqueue(n);
                    }
                }

                if (stepScores.Count == 0)
                    foreach (var n in grid.GetNeighbors(pos, true))
                        if (CanExplorerEnter(grid, econ, team, n)) stepScores[n] = 0f;

                var options = new List<Vector2Int>();
                float best = float.MinValue;
                foreach (var kv in stepScores)
                {
                    float score = kv.Value - (visited.Contains(kv.Key) ? 0.5f : 0f);
                    if (score > best + 0.0001f) { best = score; options.Clear(); options.Add(kv.Key); }
                    else if (Mathf.Abs(score - best) <= 0.0001f) options.Add(kv.Key);
                }
                if (options.Count == 0) break; // 갈 곳이 전혀 없음(1칸 섬) — 위키: 탐험가가 사라진다.
                options.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                pos = options[CitySystem.NextRandom(econ, options.Count)];
                visited.Add(pos);
                Reveal(grid, team, pos, VisionDefinition.BaseSightRadius);
            }
            ProcessLighthouses(grid, econ, log);
        }

        /// <summary>step으로 한 걸음 갔을 때 걷히는 구름 칸 수(최대 4) + 등대가 있으면 3.5(위키: "a little over clear 3").</summary>
        private static float FogScore(GridWorld grid, Team team, Vector2Int step)
        {
            int fog = 0;
            bool lighthouse = false;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var p = step + new Vector2Int(dx, dy);
                if (!grid.InBounds(p) || IsExplored(grid, team, p)) continue;
                fog++;
                if (grid.GetStructure(p) == StructureGenerationSystem.LighthouseStructureId) lighthouse = true;
            }
            return Mathf.Min(fog, 4) + (lighthouse ? 3.5f : 0f);
        }
    }
}
