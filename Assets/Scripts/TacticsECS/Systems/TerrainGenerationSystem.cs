using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld를 바이옴 CSV 규칙에 따라 절차적으로 채우는 상태 없는 정적 시스템(규칙 3) — 자체 필드를
    /// 갖지 않고, 매번 GridWorld/BiomeCsvRow와 시드를 인자로 받아 결과를 GridWorld에 직접 써넣는다.
    /// docs/PolytopiaMapGeneration.md 기준 2차 재정비: 완전 랜덤 Voronoi 시드 대신 쿼드런트(구역) 기반
    /// 앵커 배치(Polytopia의 수도 배치), 앵커 기준 Inner/Outer 이중 확률, 일반화된 거리 제약(MinDistance/
    /// EdgeMargin) + 맵 크기 비례 개수(CountPerTiles)를 반영했다.
    /// Sandbox/BattleController.HandleGenerateTerrain이 호출한다.
    /// </summary>
    public static class TerrainGenerationSystem
    {
        /// <summary>이미 놓인 이웃 타입이 없을 때도 완전히 0이 되지 않도록 노이즈 가중치에 더하는 바닥값.</summary>
        private const float NoiseWeightFloor = 0.25f;

        public static void Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int seed)
        {
            if (grid == null || biomes == null || biomes.Count == 0) return;

            var rng = new Random(seed);
            var (biomeIndexPerCell, anchors) = AssignBiomeRegions(grid, biomes.Count, rng);
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, biomes.Count);

            ClearGeneratedTiles(grid);
            var placedPositionsByType = BuildInitialPlacedPositions(grid);

            PlaceMinCountQuota(grid, biomes, biomeIndexPerCell, regionSizePerBiome, placedPositionsByType, rng);
            FillRemaining(grid, biomes, biomeIndexPerCell, anchors, placedPositionsByType, rng);
        }

        /// <summary>재생성(다시 "지형 생성" 버튼을 누르는 경우) 시 이전 결과가 "이미 채워진 칸"으로
        /// 오인되지 않도록 리셋한다. 유닛이 점유한 칸은 건드리지 않는다(그 위 지형을 바꾸면 이동 판정이
        /// 깨질 수 있으므로 — TerrainGenerationSystem은 항상 빈 칸만 다시 채운다).</summary>
        private static void ClearGeneratedTiles(GridWorld grid)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos)) continue;
                    grid.SetTileType(pos, string.Empty);
                }
        }

        /// <summary>이미 유닛이 점유해 지우지 않은 칸(ClearGeneratedTiles가 건너뛴 칸)의 TileTypeId를
        /// MinDistance 판정의 초기 상태로 삼는다 — 재생성 시 기존 배치와의 거리 제약도 지켜지도록.</summary>
        private static Dictionary<string, List<Vector2Int>> BuildInitialPlacedPositions(GridWorld grid)
        {
            var result = new Dictionary<string, List<Vector2Int>>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var tileType = grid.GetTileType(pos);
                    if (string.IsNullOrEmpty(tileType)) continue;
                    AddPlacedPosition(result, tileType, pos);
                }
            return result;
        }

        /// <summary>바이옴 수만큼 쿼드런트(구역)를 나눠 서로 다른 구역에 앵커를 하나씩 배정한다
        /// (Polytopia의 "인원수에 따라 4/9/16구역, 구역당 수도 하나" 규칙과 같은 원리 — 완전 랜덤 시드보다
        /// 바이옴들이 맵 전체에 고르게 퍼지는 것을 보장한다). 앵커가 정해지면 각 칸은 기존처럼 가장 가까운
        /// 앵커의 바이옴에 배정된다(Voronoi).</summary>
        private static (int[] BiomeIndexPerCell, Vector2Int[] Anchors) AssignBiomeRegions(GridWorld grid, int biomeCount, Random rng)
        {
            int quadrantsPerSide = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(biomeCount)));
            var quadrantIndices = new List<int>();
            for (int i = 0; i < quadrantsPerSide * quadrantsPerSide; i++) quadrantIndices.Add(i);
            Shuffle(quadrantIndices, rng);

            var anchors = new Vector2Int[biomeCount];
            for (int b = 0; b < biomeCount; b++)
            {
                int q = quadrantIndices[b % quadrantIndices.Count];
                int qx = q % quadrantsPerSide;
                int qy = q / quadrantsPerSide;
                var (xMin, xMax) = QuadrantRange(grid.Width, quadrantsPerSide, qx);
                var (yMin, yMax) = QuadrantRange(grid.Height, quadrantsPerSide, qy);
                anchors[b] = new Vector2Int(rng.Next(xMin, xMax), rng.Next(yMin, yMax));
            }

            var biomeIndexPerCell = new int[grid.Width * grid.Height];
            if (biomeCount > 1)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        int nearest = 0;
                        int nearestDistSq = int.MaxValue;
                        for (int i = 0; i < anchors.Length; i++)
                        {
                            int dx = anchors[i].x - x;
                            int dy = anchors[i].y - y;
                            int distSq = dx * dx + dy * dy;
                            if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = i; }
                        }
                        biomeIndexPerCell[grid.Index(new Vector2Int(x, y))] = nearest;
                    }
                }
            }
            return (biomeIndexPerCell, anchors);
        }

        /// <summary>totalSize를 quadrantsPerSide등분한 구간 중 quadrantIndex번째의 [min, max) 범위.
        /// 나머지가 남는 경우 뒤쪽 구간이 한 칸씩 더 커지는 정도만 허용(균등 분할 근사).</summary>
        private static (int Min, int Max) QuadrantRange(int totalSize, int quadrantsPerSide, int quadrantIndex)
        {
            int min = quadrantIndex * totalSize / quadrantsPerSide;
            int max = (quadrantIndex + 1) * totalSize / quadrantsPerSide;
            max = Mathf.Min(Mathf.Max(max, min + 1), totalSize);
            min = Mathf.Min(min, totalSize - 1);
            return (min, max);
        }

        /// <summary>biomeIndexPerCell을 세서 바이옴별로 실제로 배정된 칸 수를 구한다 — CountPerTiles가
        /// "이 바이옴 영역 안에서 N칸당 하나" 밀도를 뜻하므로(맵 전체 크기가 아니라), 바이옴마다 자기
        /// 영역 크기를 알아야 한다.</summary>
        private static int[] CountRegionSizes(int[] biomeIndexPerCell, int biomeCount)
        {
            var sizes = new int[biomeCount];
            foreach (var idx in biomeIndexPerCell) sizes[idx]++;
            return sizes;
        }

        /// <summary>MinCount(또는 CountPerTiles로 그 바이옴 영역 크기에 비례해 계산한 개수 중 더 큰 값)가
        /// 지정된 엔트리부터, 그 바이옴 영역의 빈 칸 중 제약(EdgeMargin/MinDistance/ExcludeAdjacent)을
        /// 만족하는 칸을 셔플된 순서로 찾아 우선 배치한다. 다 채우지 못하면 경고만 남기고 넘어간다(막힌
        /// 칸 폴백은 FillRemaining이 어차피 나머지 빈 칸을 전부 채워주므로 생성 자체가 실패하지는 않는다).</summary>
        private static void PlaceMinCountQuota(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            int[] regionSizePerBiome, Dictionary<string, List<Vector2Int>> placedPositionsByType, Random rng)
        {
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
            {
                var biome = biomes[biomeIdx];
                for (int entryIdx = 0; entryIdx < biome.Tiles.Count; entryIdx++)
                {
                    var entry = biome.Tiles[entryIdx];
                    int targetCount = EffectiveMinCount(entry, regionSizePerBiome[biomeIdx]);
                    if (targetCount <= 0) continue;

                    var candidates = CollectEmptyCellsInBiome(grid, biomeIndexPerCell, biomeIdx);
                    Shuffle(candidates, rng);

                    int placed = 0;
                    foreach (var pos in candidates)
                    {
                        if (placed >= targetCount) break;
                        if (ViolatesConstraints(grid, pos, entry, placedPositionsByType)) continue;
                        PlaceTile(grid, pos, entry, placedPositionsByType);
                        placed++;
                    }

                    if (placed < targetCount)
                        Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}' tile '{entry.TileId}': 목표 개수 {targetCount}개 중 {placed}개만 배치됨(공간 부족 또는 제약 충돌).");
                }
            }
        }

        /// <summary>MinCount와 CountPerTiles(이 바이옴 영역 크기에 비례, Polytopia의 유적/외딴섬 마을
        /// 개수표와 같은 개념 — 영역 크기 기준이라 바이옴이 몇 개든, 맵 크기가 얼마든 밀도가 일정하게
        /// 유지된다) 중 더 큰 값을 이 엔트리의 실제 목표 개수로 쓴다.</summary>
        private static int EffectiveMinCount(BiomeTileEntry entry, int regionSize)
        {
            int fromDensity = entry.CountPerTiles > 0f ? Mathf.RoundToInt(regionSize / entry.CountPerTiles) : 0;
            return Mathf.Max(entry.MinCount, fromDensity);
        }

        /// <summary>남은 빈 칸을 셔플된 순서로 순회하며, 제약(EdgeMargin/MinDistance/ExcludeAdjacent)을
        /// 위반하는 후보를 제거하고 남은 후보를 앵커 기준 Inner/Outer 가중치 + 노이즈로 랜덤 선택한다.
        /// 후보가 하나도 안 남으면 그 바이옴의 첫 엔트리로 폴백한다(완전히 막힌 칸도 항상 무언가로
        /// 채워지도록).</summary>
        private static void FillRemaining(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            Vector2Int[] anchors, Dictionary<string, List<Vector2Int>> placedPositionsByType, Random rng)
        {
            var emptyCells = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetTileType(pos))) continue;
                    emptyCells.Add(pos);
                }
            Shuffle(emptyCells, rng);

            foreach (var pos in emptyCells)
            {
                int biomeIdx = biomeIndexPerCell[grid.Index(pos)];
                var biome = biomes[biomeIdx];
                if (biome.Tiles.Count == 0) continue;
                var anchor = anchors[biomeIdx];

                var candidates = new List<(BiomeTileEntry Entry, float Weight)>();
                for (int i = 0; i < biome.Tiles.Count; i++)
                {
                    var entry = biome.Tiles[i];
                    if (ViolatesConstraints(grid, pos, entry, placedPositionsByType)) continue;
                    candidates.Add((entry, ComputeWeight(biome, i, entry, anchor, pos.x, pos.y)));
                }

                var chosen = candidates.Count == 0 ? biome.Tiles[0] : WeightedPick(candidates, rng);
                PlaceTile(grid, pos, chosen, placedPositionsByType);
            }
        }

        private static List<Vector2Int> CollectEmptyCellsInBiome(GridWorld grid, int[] biomeIndexPerCell, int biomeIdx)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (biomeIndexPerCell[grid.Index(pos)] != biomeIdx) continue;
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetTileType(pos))) continue;
                    candidates.Add(pos);
                }
            return candidates;
        }

        private static void PlaceTile(GridWorld grid, Vector2Int pos, BiomeTileEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            grid.SetTerrain(pos, entry.TerrainType);
            grid.SetTileType(pos, entry.TileId);
            AddPlacedPosition(placedPositionsByType, entry.TileId, pos);
        }

        private static void AddPlacedPosition(Dictionary<string, List<Vector2Int>> placedPositionsByType, string tileId, Vector2Int pos)
        {
            if (!placedPositionsByType.TryGetValue(tileId, out var list))
            {
                list = new List<Vector2Int>();
                placedPositionsByType[tileId] = list;
            }
            list.Add(pos);
        }

        /// <summary>EdgeMargin(가장자리 여백) / MinDistance(같은 타입끼리 최소 거리, 둘 다 체비쇼프
        /// 거리) / ExcludeAdjacent(바로 인접한 다른 타입 배제, 1차 구현부터 유지) 세 제약을 전부 검사한다.</summary>
        private static bool ViolatesConstraints(GridWorld grid, Vector2Int pos, BiomeTileEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            if (entry.EdgeMargin > 0)
            {
                int distToEdge = Mathf.Min(Mathf.Min(pos.x, pos.y), Mathf.Min(grid.Width - 1 - pos.x, grid.Height - 1 - pos.y));
                if (distToEdge < entry.EdgeMargin) return true;
            }

            if (entry.MinDistance > 0 && placedPositionsByType.TryGetValue(entry.TileId, out var placed))
            {
                foreach (var other in placed)
                {
                    int dist = Mathf.Max(Mathf.Abs(other.x - pos.x), Mathf.Abs(other.y - pos.y));
                    if (dist < entry.MinDistance) return true;
                }
            }

            if (entry.ExcludeAdjacent != null && entry.ExcludeAdjacent.Length > 0)
            {
                foreach (var neighborPos in grid.GetNeighbors(pos, allowDiagonal: false))
                {
                    var neighborType = grid.GetTileType(neighborPos);
                    if (string.IsNullOrEmpty(neighborType)) continue;

                    foreach (var excluded in entry.ExcludeAdjacent)
                        if (string.Equals(neighborType, excluded, StringComparison.OrdinalIgnoreCase))
                            return true;
                }
            }

            return false;
        }

        /// <summary>칸의 앵커(그 바이옴의 쿼드런트 시작점) 기준 체비쇼프 거리가 InnerRadius 이내면
        /// InnerWeight, 아니면 OuterWeight를 기준값으로 쓰고, 엔트리마다 독립된 노이즈(SeedOffset을
        /// 엔트리별로 다르게)로 한 번 더 보정한다 — 그래야 숲/모래 같은 타입이 한 칸씩 흩뿌려지지 않고
        /// 자연스럽게 뭉친 패치로 나온다.</summary>
        private static float ComputeWeight(BiomeCsvRow biome, int entryIndex, BiomeTileEntry entry, Vector2Int anchor, int x, int y)
        {
            int distToAnchor = Mathf.Max(Mathf.Abs(x - anchor.x), Mathf.Abs(y - anchor.y));
            float baseWeight = distToAnchor <= biome.InnerRadius ? entry.InnerWeight : entry.OuterWeight;

            float noise = NoiseSystem.Sample(x, y, biome.Frequency, biome.Octaves, biome.SeedOffset + entryIndex * 997);
            return Mathf.Max(0f, baseWeight) * (NoiseWeightFloor + noise);
        }

        private static BiomeTileEntry WeightedPick(List<(BiomeTileEntry Entry, float Weight)> candidates, Random rng)
        {
            float total = 0f;
            foreach (var c in candidates) total += c.Weight;
            if (total <= 0f) return candidates[rng.Next(candidates.Count)].Entry;

            float roll = (float)(rng.NextDouble() * total);
            float acc = 0f;
            foreach (var c in candidates)
            {
                acc += c.Weight;
                if (roll <= acc) return c.Entry;
            }
            return candidates[candidates.Count - 1].Entry;
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
