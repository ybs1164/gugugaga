using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld에 타일 위 구조물(수도/마을/자원/등대/유적/불가사리)을 배치하는 상태 없는 정적 시스템 —
    /// TerrainGenerationSystem이 채운 지형/앵커 위에 얹는다. 9차 재정비: 원문(docs/PolytopiaMapGeneration.md
    /// 5절)의 순서를 단계별로 그대로 따른다 — 예전엔 마을/자원/유적/불가사리를 셔플된 한 번의 순회에서
    /// 가중치로 경쟁시켜서, 자원을 놓는 시점에 마을이 아직 없어 "모든 도시 2칸 이내" 규칙을 지킬 수 없었다.
    ///   1. 수도 + Suburb/사전 확정 마을(지형 단계에서 위치가 정해진 것)
    ///   2. Post-terrain 마을 포화 채우기 -> 외딴 섬 마을(Pangea/Continents/Waterworld) -> Lakes 육로 보장
    ///   3. 물 칸 얕은 물/깊은 바다 재분류(2단계가 지형을 바꿨을 수 있으므로)
    ///   4. 등대(맵 네 모서리 — 자원보다 먼저 놓아야 모서리 근처 도시의 자원에 자리를 뺏기지 않고 항상 4개가 된다)
    ///   5. 자원: 모든 도시 2칸 이내, Inner(거리 1)/Outer(거리 2) 비율 쿼터
    ///   6. 유적/불가사리 등 개수 기반 구조물(맵 전체 목표 개수, 유적은 맵 크기별 고정 개수표)
    /// BattleController.HandleGenerateTerrain이 TerrainGenerationSystem.Generate가 반환한 anchors로 이어서
    /// 호출한다. 구조물은 순수 시각 요소라 이동/점유 판정에 관여하지 않는다(TileData.StructureId 참고).
    /// </summary>
    public static class StructureGenerationSystem
    {
        public const string CapitalStructureId = "Capital";
        public const string VillageStructureId = "Village";
        public const string RuinStructureId = "Ruin";
        public const string LighthouseStructureId = "Lighthouse";

        /// <summary>자원은 항상 도시(수도/마을)로부터 이 거리(체비쇼프) 이내에만 생긴다(원문 3절). 거리 1 = Inner
        /// City(도시에 인접), 거리 2 = Outer City.</summary>
        public const int ResourceCityRadius = 2;

        /// <summary>외딴 섬 마을의 가장자리 여백 — 사방(8방향)이 물이어야 하므로 맨 가장자리 칸은 제외한다.</summary>
        private const int TinyIslandEdgeMargin = 1;

        /// <summary>Polytopia의 맵 크기별 외딴 섬 마을 개수표(8절) — MapSizePresets(BattleController)의
        /// 변 길이와 같은 값으로 조회한다.</summary>
        private static readonly (int Size, int Count)[] TinyIslandCounts =
        {
            (11, 0), (14, 1), (16, 2), (18, 3), (20, 4), (30, 9)
        };

        /// <summary>Polytopia의 맵 크기별 유적 개수표(9절, 맵 전체 기준). 표에 없는 크기는 약 36칸당 1개.</summary>
        private static readonly (int Size, int Count)[] RuinCounts =
        {
            (11, 4), (14, 5), (16, 7), (18, 9), (20, 11), (30, 23)
        };
        private const float RuinTilesPerRuinFallback = 36f;

        /// <summary>지형 생성 전에 이미 확정된 Suburb/사전 확정 마을 위치(TerrainGenerationSystem.Generate의
        /// out 파라미터)를 받아 Village로 먼저 배치한 뒤, 클래스 주석의 단계 순서대로 나머지를 배치한다.
        /// 뒤 3개 인자는 생략 가능(null=빈 배열, shapeMode 기본값 Freeform = 외딴 섬 마을/Lakes 규칙 비활성).
        /// shapeMode는 맵 타입 전용 규칙(외딴 섬 마을, Lakes 육로 보장, Lakes 유적 물 비율 상한)을 켜는 데만 쓴다.</summary>
        public static void Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, Vector2Int[] anchors, int seed,
            Vector2Int[] suburbPositions = null, Vector2Int[] plannedVillagePositions = null,
            TerrainGenerationSystem.MapShapeMode shapeMode = TerrainGenerationSystem.MapShapeMode.Freeform)
        {
            if (grid == null || biomes == null || biomes.Count == 0 || anchors == null || anchors.Length == 0) return;

            ClearGeneratedStructures(grid);
            PlaceCapitals(grid, anchors);
            PlaceVillagesAt(grid, suburbPositions);
            PlaceVillagesAt(grid, plannedVillagePositions);

            var biomeIndexPerCell = new int[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    biomeIndexPerCell[grid.Index(pos)] = Mathf.Min(ProceduralGenerationUtil.NearestAnchorIndex(anchors, pos), biomes.Count - 1);
                }

            // MinDistance/도시 간격 판정용 배치 목록은 맵 전체에서 하나로 공유한다(12절 보강 4).
            var placedPositionsByStructure = CollectPlacedStructures(grid);
            var rng = new Random(seed);

            // 2단계 — 마을.
            PlacePostTerrainVillages(grid, biomes, biomeIndexPerCell, placedPositionsByStructure, rng);
            if (shapeMode == TerrainGenerationSystem.MapShapeMode.Pangea || shapeMode == TerrainGenerationSystem.MapShapeMode.Continents ||
                shapeMode == TerrainGenerationSystem.MapShapeMode.Waterworld)
                PlaceTinyIslandVillages(grid, biomes, biomeIndexPerCell, placedPositionsByStructure, rng);
            if (shapeMode == TerrainGenerationSystem.MapShapeMode.Lakes)
                EnsureLakesVillageConnections(grid, biomes, biomeIndexPerCell, anchors, placedPositionsByStructure);

            // 3단계 — 2단계가 물을 육지로 바꿨을 수 있으니 얕은 물/깊은 바다를 다시 나눈다(자원의 물고기가 얕은 물 기준).
            TerrainGenerationSystem.ClassifyWaterDepth(grid, biomes, anchors);

            // 4~6단계.
            PlaceLighthouses(grid, placedPositionsByStructure);
            PlaceResources(grid, biomes, biomeIndexPerCell, placedPositionsByStructure, rng);
            PlaceCountedStructures(grid, biomes, biomeIndexPerCell, placedPositionsByStructure, shapeMode, rng);
        }

        /// <summary>엔트리 분류 — InnerRate/OuterRate가 있으면 자원(4단계).</summary>
        private static bool IsResourceEntry(BiomeStructureEntry entry) => entry.InnerRate > 0f || entry.OuterRate > 0f;

        private static bool IsCityStructure(string structureId) =>
            structureId == CapitalStructureId || structureId == VillageStructureId;

        /// <summary>Suburb/사전 확정 마을 위치(지형 생성 단계에서 이미 육지로 확정된 칸)마다 Village 구조물을
        /// 배치한다 — PlaceCapitals와 같은 패턴.</summary>
        private static void PlaceVillagesAt(GridWorld grid, Vector2Int[] positions)
        {
            if (positions == null) return;
            foreach (var pos in positions)
            {
                if (!grid.InBounds(pos) || grid.IsOccupied(pos)) continue;
                if (!string.IsNullOrEmpty(grid.GetStructure(pos))) continue;
                grid.SetStructure(pos, VillageStructureId);
            }
        }

        /// <summary>현재 그리드에 놓인 구조물 위치를 StructureId별로 모은다(MinDistance/도시 간격 판정의 초기 상태).</summary>
        private static Dictionary<string, List<Vector2Int>> CollectPlacedStructures(GridWorld grid)
        {
            var result = new Dictionary<string, List<Vector2Int>>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var structureId = grid.GetStructure(pos);
                    if (!string.IsNullOrEmpty(structureId)) AddPlacedPosition(result, structureId, pos);
                }
            return result;
        }

        /// <summary>놓인 모든 도시(수도/마을) 위치.</summary>
        private static List<Vector2Int> CollectCities(Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            var cities = new List<Vector2Int>();
            if (placedPositionsByStructure.TryGetValue(CapitalStructureId, out var capitals)) cities.AddRange(capitals);
            if (placedPositionsByStructure.TryGetValue(VillageStructureId, out var villages)) cities.AddRange(villages);
            return cities;
        }

        /// <summary>pos가 이미 놓인 수도/마을 중 하나와 minDistance(체비쇼프) 미만으로 가까우면 true.</summary>
        private static bool IsTooCloseToCity(Vector2Int pos, int minDistance, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            foreach (var cityId in new[] { CapitalStructureId, VillageStructureId })
            {
                if (!placedPositionsByStructure.TryGetValue(cityId, out var cities)) continue;
                foreach (var other in cities)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < minDistance) return true;
            }
            return false;
        }

        private static int DistanceToNearestCity(Vector2Int pos, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            int best = int.MaxValue;
            foreach (var cityId in new[] { CapitalStructureId, VillageStructureId })
            {
                if (!placedPositionsByStructure.TryGetValue(cityId, out var cities)) continue;
                foreach (var other in cities)
                    best = Mathf.Min(best, ProceduralGenerationUtil.ChebyshevDistance(other, pos));
            }
            return best;
        }

        /// <summary>수도는 자동 배치라 재생성 시마다 초기화해야 하고, 나머지 구조물도 이전 결과가 남아
        /// "이미 채워진 칸"으로 오인되지 않도록 리셋한다. 유닛이 점유한 칸은 건드리지 않는다.</summary>
        private static void ClearGeneratedStructures(GridWorld grid)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos)) continue;
                    grid.SetStructure(pos, string.Empty);
                }
        }

        /// <summary>바이옴 앵커(TerrainGenerationSystem.Generate가 반환한 수도 위치)마다 수도를 배치한다.</summary>
        private static void PlaceCapitals(GridWorld grid, Vector2Int[] anchors)
        {
            foreach (var anchor in anchors)
            {
                if (!grid.InBounds(anchor) || grid.IsOccupied(anchor)) continue;
                grid.SetStructure(anchor, CapitalStructureId);
            }
        }

        /// <summary>구조물을 놓을 수 있는 빈 칸인지(유닛 미점유 + 구조물 없음 + 지형 생성 완료).</summary>
        private static bool IsFreeCell(GridWorld grid, Vector2Int pos) =>
            !grid.IsOccupied(pos) && string.IsNullOrEmpty(grid.GetStructure(pos)) && !string.IsNullOrEmpty(grid.GetTileType(pos));

        private static bool IsAllowedOn(BiomeStructureEntry entry, string tileType) =>
            entry.AllowedTileTypes != null && Array.IndexOf(entry.AllowedTileTypes, tileType) >= 0;

        private static List<Vector2Int> AllCellsShuffled(GridWorld grid, Random rng)
        {
            var cells = new List<Vector2Int>(grid.Width * grid.Height);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    cells.Add(new Vector2Int(x, y));
            ProceduralGenerationUtil.Shuffle(cells, rng);
            return cells;
        }

        /// <summary>2단계 — Post-terrain 마을(원문 7.4절). 바이옴 CSV의 도시(Village) 엔트리를 맵 전체 칸을 셔플된
        /// 순서로 한 번 훑으며, 허용 타일/가장자리 여백/도시 간격(PostTerrainCityMinDistance와 CSV MinDistance 중
        /// 큰 값)을 지키는 칸마다 놓는다 — 제약이 단조롭게만 강해지므로 한 번의 순회로 "더 넣을 자리가 없을
        /// 때까지"가 된다. 바이옴 순서와 무관하게 맵 전체가 고르게 채워진다(예전엔 바이옴별 루프라 먼저 도는
        /// 바이옴이 경계 자리를 선점했다).</summary>
        private static void PlacePostTerrainVillages(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Dictionary<string, List<Vector2Int>> placedPositionsByStructure, Random rng)
        {
            foreach (var pos in AllCellsShuffled(grid, rng))
            {
                if (!IsFreeCell(grid, pos)) continue;
                var biome = biomes[biomeIndexPerCell[grid.Index(pos)]];
                if (biome.Structures == null) continue;
                var tileType = grid.GetTileType(pos);

                foreach (var entry in biome.Structures)
                {
                    if (!IsCityStructure(entry.StructureId) || IsResourceEntry(entry)) continue;
                    if (!IsAllowedOn(entry, tileType)) continue;
                    if (ViolatesConstraints(grid, pos, entry, placedPositionsByStructure)) continue;
                    grid.SetStructure(pos, entry.StructureId);
                    AddPlacedPosition(placedPositionsByStructure, entry.StructureId, pos);
                    break;
                }
            }
        }

        /// <summary>엔트리 공통 제약(EdgeMargin/MaxDistanceFromCity/도시 간격/같은 Id 최소 거리/8방향 인접 배제).</summary>
        private static bool ViolatesConstraints(GridWorld grid, Vector2Int pos, BiomeStructureEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            if (entry.EdgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, pos) < entry.EdgeMargin) return true;

            if (entry.MaxDistanceFromCity > 0 && DistanceToNearestCity(pos, placedPositionsByStructure) > entry.MaxDistanceFromCity) return true;

            // Post-terrain 도시는 서로 종류가 달라도 같은 간격 규칙을 따르고, CSV 값이 작거나 0이어도 최소
            // PostTerrainCityMinDistance(원문 7.4절 "다른 마을로부터 2칸 이내 금지")는 항상 지킨다.
            if (IsCityStructure(entry.StructureId) &&
                IsTooCloseToCity(pos, Mathf.Max(entry.MinDistance, TerrainGenerationSystem.PostTerrainCityMinDistance), placedPositionsByStructure))
                return true;

            if (entry.MinDistance > 0 && placedPositionsByStructure.TryGetValue(entry.StructureId, out var placed))
                foreach (var other in placed)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < entry.MinDistance) return true;

            if (entry.ExcludeAdjacentStructures != null && entry.ExcludeAdjacentStructures.Length > 0)
            {
                // Polytopia는 대각선도 인접이므로 8방향으로 검사한다(원문 9/10절 "바로 인접 불가").
                foreach (var neighborPos in grid.GetNeighbors(pos, allowDiagonal: true))
                {
                    var neighborStructure = grid.GetStructure(neighborPos);
                    if (string.IsNullOrEmpty(neighborStructure)) continue;
                    foreach (var excluded in entry.ExcludeAdjacentStructures)
                        if (string.Equals(neighborStructure, excluded, StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }

            return false;
        }

        private static void AddPlacedPosition(Dictionary<string, List<Vector2Int>> placedPositionsByStructure, string structureId, Vector2Int pos)
        {
            if (!placedPositionsByStructure.TryGetValue(structureId, out var list))
            {
                list = new List<Vector2Int>();
                placedPositionsByStructure[structureId] = list;
            }
            list.Add(pos);
        }

        /// <summary>외딴 섬 마을(원문 8절 — Continents/Pangea/Waterworld). 8방향 이웃이 전부 물인(= 대각선으로도
        /// 육지와 이어지지 않은) 물 칸을 육지로 바꾸고 마을을 놓는다. 개수는 맵 크기별 표를 따르고, 다른 도시와는
        /// Post-terrain 간격을 지킨다. 육지로 바꿀 때 쓸 TileTypeId는 그 칸 바이옴의 첫 육지 타일이다.</summary>
        private static void PlaceTinyIslandVillages(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Dictionary<string, List<Vector2Int>> placedPositionsByStructure, Random rng)
        {
            // 표에 없는 크기는 표와 같은 비율(약 100칸당 1개)로 근사한다.
            int count = LookupBySize(TinyIslandCounts, grid.Width, Mathf.FloorToInt(grid.Width * grid.Height / 100f));
            if (count <= 0) return;

            var candidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetTerrain(pos) != TerrainType.Water || grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetStructure(pos))) continue;
                    if (ProceduralGenerationUtil.DistanceToEdge(grid, pos) < TinyIslandEdgeMargin) continue;

                    bool surroundedByWater = true;
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                        if (grid.GetTerrain(n) != TerrainType.Water) { surroundedByWater = false; break; }
                    if (surroundedByWater) candidates.Add(pos);
                }
            ProceduralGenerationUtil.Shuffle(candidates, rng);

            int placed = 0;
            foreach (var pos in candidates)
            {
                if (placed >= count) break;
                if (IsTooCloseToCity(pos, TerrainGenerationSystem.PostTerrainCityMinDistance, placedPositionsByStructure)) continue;
                // 앞서 놓인 섬이 이 칸의 이웃을 육지로 만들었을 수 있다(간격 3이면 불가능하지만 방어적으로 재확인).
                bool stillIsolated = true;
                foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                    if (grid.GetTerrain(n) != TerrainType.Water) { stillIsolated = false; break; }
                if (!stillIsolated) continue;

                string landTileId = FindFirstLandTileId(biomes[biomeIndexPerCell[grid.Index(pos)]]);
                if (landTileId == null) continue; // 이 바이옴엔 육지 타일 정의가 없음 — 건너뜀

                grid.SetTerrain(pos, TerrainType.Land);
                grid.SetTileType(pos, landTileId);
                grid.SetStructure(pos, VillageStructureId);
                AddPlacedPosition(placedPositionsByStructure, VillageStructureId, pos);
                placed++;
            }
        }

        /// <summary>Lakes 전용(원문 2절 "모든 플레이어는 최소 2개 마을과 육로로 연결 보장"). 수도마다 4방향으로
        /// 이어진 육지 덩어리 안의 마을 수를 세고, 2개 미만이면 그 덩어리 밖에서 가장 가까운 마을까지 최단 경로의
        /// 물 칸을 육지로 바꿔 "육지 다리"를 놓는다. 연결할 마을이 더 없으면 멈춘다.</summary>
        private static void EnsureLakesVillageConnections(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Vector2Int[] anchors, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            const int RequiredVillages = 2;
            const int MaxBridgesPerCapital = 4;
            foreach (var capital in anchors)
            {
                if (grid.GetStructure(capital) != CapitalStructureId) continue;
                for (int attempt = 0; attempt < MaxBridgesPerCapital; attempt++)
                {
                    var component = LandComponent(grid, capital);
                    int villages = 0;
                    foreach (var p in component)
                        if (grid.GetStructure(p) == VillageStructureId) villages++;
                    if (villages >= RequiredVillages) break;

                    var path = ShortestPathToOutsideVillage(grid, component);
                    if (path == null) break;
                    foreach (var p in path)
                    {
                        if (grid.GetTerrain(p) != TerrainType.Water) continue;
                        string landTileId = FindFirstLandTileId(biomes[biomeIndexPerCell[grid.Index(p)]]);
                        if (landTileId == null) continue;
                        grid.SetTerrain(p, TerrainType.Land);
                        grid.SetTileType(p, landTileId);
                    }
                }
            }
        }

        /// <summary>start에서 4방향으로 이어진 육지 칸 전부.</summary>
        private static HashSet<Vector2Int> LandComponent(GridWorld grid, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int>();
            if (grid.GetTerrain(start) != TerrainType.Land) return seen;
            var stack = new Stack<Vector2Int>();
            seen.Add(start);
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var n in grid.GetNeighbors(p, allowDiagonal: false))
                    if (grid.GetTerrain(n) == TerrainType.Land && seen.Add(n)) stack.Push(n);
            }
            return seen;
        }

        /// <summary>component 전체를 시작점으로 한 4방향 BFS로, component 밖의 가장 가까운 마을 칸까지의 경로
        /// (component 칸 제외, 도착 마을 칸 포함)를 찾는다. 유닛이 선 물 칸은 바꿀 수 없어 지나가지 않는다.</summary>
        private static List<Vector2Int> ShortestPathToOutsideVillage(GridWorld grid, HashSet<Vector2Int> component)
        {
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            // HashSet 순회 순서에 기대지 않도록 좌표 순서로 시작점을 넣는다(시드 재현성).
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (!component.Contains(p)) continue;
                    parent[p] = p;
                    queue.Enqueue(p);
                }

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var n in grid.GetNeighbors(cur, allowDiagonal: false))
                {
                    if (parent.ContainsKey(n)) continue;
                    if (grid.IsOccupied(n) && grid.GetTerrain(n) == TerrainType.Water) continue;
                    parent[n] = cur;
                    if (grid.GetStructure(n) == VillageStructureId)
                    {
                        var path = new List<Vector2Int>();
                        for (var p = n; !component.Contains(p); p = parent[p]) path.Add(p);
                        return path;
                    }
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        /// <summary>5단계 — 자원(원문 3절). 모든 도시(수도/마을)로부터 거리 1(Inner)/2(Outer)인 빈 칸을
        /// (바이옴, Inner/Outer, 타일 타입)별로 묶고, 묶음마다 그 바이옴의 자원 엔트리(InnerRate/OuterRate)를
        /// CSV 순서대로 "칸 수 x 비율"만큼 쿼터로 채운다(Balance Pass 2 "항상 비율대로"). 비율 x 칸 수의
        /// 소수부는 그 확률로 1개를 더해(확률적 반올림) 작은 묶음에서도 평균 비율이 유지되게 한다.</summary>
        private static void PlaceResources(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Dictionary<string, List<Vector2Int>> placedPositionsByStructure, Random rng)
        {
            var cities = CollectCities(placedPositionsByStructure);
            if (cities.Count == 0) return;

            var groupKeys = new List<(int Biome, bool Inner, string TileType)>();
            var groups = new Dictionary<(int Biome, bool Inner, string TileType), List<Vector2Int>>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!IsFreeCell(grid, pos)) continue;
                    int dist = int.MaxValue;
                    foreach (var c in cities) dist = Mathf.Min(dist, ProceduralGenerationUtil.ChebyshevDistance(c, pos));
                    if (dist < 1 || dist > ResourceCityRadius) continue;

                    var key = (biomeIndexPerCell[grid.Index(pos)], dist == 1, grid.GetTileType(pos));
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<Vector2Int>();
                        groups[key] = list;
                        groupKeys.Add(key);
                    }
                    list.Add(pos);
                }

            foreach (var key in groupKeys)
            {
                var biome = biomes[key.Biome];
                if (biome.Structures == null) continue;
                var cells = groups[key];
                ProceduralGenerationUtil.Shuffle(cells, rng);
                int cursor = 0;

                foreach (var entry in biome.Structures)
                {
                    if (!IsResourceEntry(entry) || !IsAllowedOn(entry, key.TileType)) continue;
                    float exact = cells.Count * Mathf.Clamp01(key.Inner ? entry.InnerRate : entry.OuterRate);
                    int target = Mathf.FloorToInt(exact) + (rng.NextDouble() < exact - Mathf.Floor(exact) ? 1 : 0);

                    int placed = 0;
                    while (placed < target && cursor < cells.Count)
                    {
                        var pos = cells[cursor++];
                        if (ViolatesConstraints(grid, pos, entry, placedPositionsByStructure)) continue;
                        grid.SetStructure(pos, entry.StructureId);
                        AddPlacedPosition(placedPositionsByStructure, entry.StructureId, pos);
                        placed++;
                    }
                }
            }
        }

        /// <summary>4단계 — 등대(원문 9/10절에서 유적보다 먼저 배치되고 불가사리 인접 배제 대상). Polytopia처럼 맵
        /// 네 모서리에 하나씩 놓는다(지형 무관). 마을은 가장자리 여백 때문에 모서리에 올 수 없고, 자원보다 먼저
        /// 놓으므로 유닛이 선 칸이 아니면 항상 4개가 된다.</summary>
        private static void PlaceLighthouses(GridWorld grid, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            var corners = new[]
            {
                new Vector2Int(0, 0), new Vector2Int(grid.Width - 1, 0),
                new Vector2Int(0, grid.Height - 1), new Vector2Int(grid.Width - 1, grid.Height - 1)
            };
            foreach (var corner in corners)
            {
                if (!IsFreeCell(grid, corner)) continue;
                grid.SetStructure(corner, LighthouseStructureId);
                AddPlacedPosition(placedPositionsByStructure, LighthouseStructureId, corner);
            }
        }

        /// <summary>6단계 — 개수 기반 구조물(유적/불가사리 등, 도시/자원이 아닌 엔트리). StructureId마다 맵 전체 목표
        /// 개수를 정한다: 바이옴별 EffectiveMinCount(MinCount, CountPerTiles, 그 바이옴의 허용 타일 칸 수)의 합
        /// (예: 불가사리 = 물 25칸당 1개), 단 "Ruin"은 원문의 맵 크기별 고정 개수표, FillRemaining이면 무제한.
        /// CSV에 처음 등장하는 순서대로(샘플 CSV는 유적 -> 불가사리로 원문 순서와 같음) StructureId별로 맵 전체
        /// 칸을 셔플된 순서로 훑으며 배치한다. Lakes 맵에서는 MaxWaterFractionOnLakes로 물 위 비율을 제한한다.</summary>
        private static void PlaceCountedStructures(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Dictionary<string, List<Vector2Int>> placedPositionsByStructure, TerrainGenerationSystem.MapShapeMode shapeMode, Random rng)
        {
            var order = new List<string>();
            var targets = new Dictionary<string, int>();
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
            {
                var biome = biomes[biomeIdx];
                if (biome.Structures == null) continue;
                foreach (var entry in biome.Structures)
                {
                    if (IsCityStructure(entry.StructureId) || IsResourceEntry(entry) || string.IsNullOrEmpty(entry.StructureId)) continue;
                    if (!targets.ContainsKey(entry.StructureId)) { targets[entry.StructureId] = 0; order.Add(entry.StructureId); }
                    if (targets[entry.StructureId] == int.MaxValue) continue;
                    if (entry.FillRemaining) { targets[entry.StructureId] = int.MaxValue; continue; }

                    int eligible = 0;
                    for (int y = 0; y < grid.Height; y++)
                        for (int x = 0; x < grid.Width; x++)
                        {
                            var pos = new Vector2Int(x, y);
                            if (biomeIndexPerCell[grid.Index(pos)] == biomeIdx && IsAllowedOn(entry, grid.GetTileType(pos))) eligible++;
                        }
                    targets[entry.StructureId] += TerrainGenerationSystem.EffectiveMinCount(entry.MinCount, entry.CountPerTiles, eligible);
                }
            }
            if (targets.TryGetValue(RuinStructureId, out var ruinTarget) && ruinTarget != int.MaxValue)
                targets[RuinStructureId] = LookupBySize(RuinCounts, grid.Width,
                    Mathf.Max(1, Mathf.RoundToInt(grid.Width * grid.Height / RuinTilesPerRuinFallback)));

            bool isLakes = shapeMode == TerrainGenerationSystem.MapShapeMode.Lakes;
            foreach (var structureId in order)
            {
                int target = targets[structureId];
                int placed = 0, placedOnWater = 0;
                foreach (var pos in AllCellsShuffled(grid, rng))
                {
                    if (placed >= target) break;
                    if (!IsFreeCell(grid, pos)) continue;
                    var biome = biomes[biomeIndexPerCell[grid.Index(pos)]];
                    if (biome.Structures == null) continue;
                    var tileType = grid.GetTileType(pos);
                    bool isWater = grid.GetTerrain(pos) == TerrainType.Water;

                    var candidates = new List<(BiomeStructureEntry Entry, float Weight)>();
                    foreach (var entry in biome.Structures)
                    {
                        if (entry.StructureId != structureId || IsResourceEntry(entry) || !IsAllowedOn(entry, tileType)) continue;
                        if (ViolatesConstraints(grid, pos, entry, placedPositionsByStructure)) continue;
                        if (isLakes && isWater && entry.MaxWaterFractionOnLakes.HasValue)
                        {
                            float fraction = Mathf.Clamp01(entry.MaxWaterFractionOnLakes.Value);
                            int allowedOnWater = target == int.MaxValue
                                ? Mathf.FloorToInt((placed + 1) * fraction)
                                : Mathf.FloorToInt(target * fraction);
                            if (placedOnWater + 1 > allowedOnWater) continue;
                        }
                        candidates.Add((entry, Mathf.Max(0f, entry.Weight)));
                    }
                    if (candidates.Count == 0) continue;

                    var chosen = ProceduralGenerationUtil.WeightedPick(candidates, rng);
                    grid.SetStructure(pos, chosen.StructureId);
                    AddPlacedPosition(placedPositionsByStructure, chosen.StructureId, pos);
                    placed++;
                    if (isWater) placedOnWater++;
                }
            }
        }

        private static int LookupBySize((int Size, int Count)[] table, int size, int fallback)
        {
            foreach (var (s, c) in table)
                if (s == size) return c;
            return fallback;
        }

        private static string FindFirstLandTileId(BiomeCsvRow biome)
        {
            foreach (var tile in biome.Tiles)
                if (tile.TerrainType == TerrainType.Land) return tile.TileId;
            return null;
        }
    }
}
