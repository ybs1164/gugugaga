using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld를 바이옴 CSV 규칙(노이즈 + 확률 가중치 + 인접 배제 + 최소 개수)에 따라 절차적으로
    /// 채우는 상태 없는 정적 시스템(규칙 3) — 자체 필드를 갖지 않고, 매번 GridWorld/BiomeCsvRow와 시드를
    /// 인자로 받아 결과를 GridWorld에 직접 써넣는다. Sandbox/BattleController.HandleGenerateTerrain이 호출한다.
    /// </summary>
    public static class TerrainGenerationSystem
    {
        /// <summary>이미 놓인 이웃 타입이 없을 때도 완전히 0이 되지 않도록 노이즈 가중치에 더하는 바닥값.</summary>
        private const float NoiseWeightFloor = 0.25f;

        public static void Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int seed)
        {
            if (grid == null || biomes == null || biomes.Count == 0) return;

            var rng = new Random(seed);
            var biomeIndexPerCell = AssignBiomeRegions(grid, biomes.Count, rng);

            ClearGeneratedTiles(grid);
            PlaceMinCountQuota(grid, biomes, biomeIndexPerCell, rng);
            FillRemaining(grid, biomes, biomeIndexPerCell, rng);
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

        /// <summary>바이옴이 하나면 그리드 전체를 그 바이옴으로. 여러 개면 바이옴 수만큼 랜덤 시드점을
        /// 찍고 각 칸을 가장 가까운 시드점의 바이옴에 배정하는 간단한 Voronoi 분할.</summary>
        private static int[] AssignBiomeRegions(GridWorld grid, int biomeCount, Random rng)
        {
            var result = new int[grid.Width * grid.Height];
            if (biomeCount <= 1) return result; // 전부 0번(유일한 바이옴)

            var seeds = new Vector2Int[biomeCount];
            for (int i = 0; i < biomeCount; i++)
                seeds[i] = new Vector2Int(rng.Next(0, grid.Width), rng.Next(0, grid.Height));

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int nearest = 0;
                    int nearestDistSq = int.MaxValue;
                    for (int i = 0; i < seeds.Length; i++)
                    {
                        int dx = seeds[i].x - x;
                        int dy = seeds[i].y - y;
                        int distSq = dx * dx + dy * dy;
                        if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = i; }
                    }
                    result[grid.Index(new Vector2Int(x, y))] = nearest;
                }
            }
            return result;
        }

        /// <summary>MinCount가 지정된 엔트리부터, 그 바이옴 영역의 빈 칸 중 인접 배제를 만족하는 칸을
        /// 셔플된 순서로 찾아 우선 배치한다. 다 채우지 못하면 경고만 남기고 넘어간다(막힌 칸 폴백은
        /// FillRemaining이 어차피 나머지 빈 칸을 전부 채워주므로 생성 자체가 실패하지는 않는다).</summary>
        private static void PlaceMinCountQuota(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell, Random rng)
        {
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
            {
                var biome = biomes[biomeIdx];
                for (int entryIdx = 0; entryIdx < biome.Tiles.Count; entryIdx++)
                {
                    var entry = biome.Tiles[entryIdx];
                    if (entry.MinCount <= 0) continue;

                    var candidates = CollectEmptyCellsInBiome(grid, biomeIndexPerCell, biomeIdx);
                    Shuffle(candidates, rng);

                    int placed = 0;
                    foreach (var pos in candidates)
                    {
                        if (placed >= entry.MinCount) break;
                        if (ViolatesAdjacency(grid, pos, entry)) continue;
                        PlaceTile(grid, pos, entry);
                        placed++;
                    }

                    if (placed < entry.MinCount)
                        Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}' tile '{entry.TileId}': MinCount {entry.MinCount}개 중 {placed}개만 배치됨(공간 부족 또는 인접 규칙 충돌).");
                }
            }
        }

        /// <summary>남은 빈 칸을 셔플된 순서로 순회하며, 이웃에 이미 놓인 타일 타입 기준으로 인접 배제
        /// 위반 후보를 제거하고 남은 후보를 노이즈 가중치로 랜덤 선택한다. 후보가 하나도 안 남으면 그
        /// 바이옴의 첫 엔트리로 폴백한다(완전히 막힌 칸도 항상 무언가로 채워지도록).</summary>
        private static void FillRemaining(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell, Random rng)
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
                var biome = biomes[biomeIndexPerCell[grid.Index(pos)]];
                if (biome.Tiles.Count == 0) continue;

                var candidates = new List<(BiomeTileEntry Entry, float Weight)>();
                for (int i = 0; i < biome.Tiles.Count; i++)
                {
                    var entry = biome.Tiles[i];
                    if (ViolatesAdjacency(grid, pos, entry)) continue;
                    candidates.Add((entry, ComputeWeight(biome, i, entry, pos.x, pos.y)));
                }

                var chosen = candidates.Count == 0 ? biome.Tiles[0] : WeightedPick(candidates, rng);
                PlaceTile(grid, pos, chosen);
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

        private static void PlaceTile(GridWorld grid, Vector2Int pos, BiomeTileEntry entry)
        {
            grid.SetTerrain(pos, entry.TerrainType);
            grid.SetTileType(pos, entry.TileId);
        }

        private static bool ViolatesAdjacency(GridWorld grid, Vector2Int pos, BiomeTileEntry entry)
        {
            if (entry.ExcludeAdjacent == null || entry.ExcludeAdjacent.Length == 0) return false;

            foreach (var neighborPos in grid.GetNeighbors(pos, allowDiagonal: false))
            {
                var neighborType = grid.GetTileType(neighborPos);
                if (string.IsNullOrEmpty(neighborType)) continue;

                foreach (var excluded in entry.ExcludeAdjacent)
                    if (string.Equals(neighborType, excluded, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
        }

        /// <summary>바이옴이 CSV로 제공하는 노이즈 파라미터(Frequency/Octaves)를 그대로 쓰되, 엔트리마다
        /// SeedOffset을 다르게 줘서 같은 바이옴 안에서도 타일 타입마다 독립된 노이즈장을 갖게 한다 —
        /// 그래야 숲/모래 같은 타입이 한 칸씩 흩뿌려지지 않고 자연스럽게 뭉친 패치로 나온다.</summary>
        private static float ComputeWeight(BiomeCsvRow biome, int entryIndex, BiomeTileEntry entry, int x, int y)
        {
            float noise = NoiseSystem.Sample(x, y, biome.Frequency, biome.Octaves, biome.SeedOffset + entryIndex * 997);
            return Mathf.Max(0f, entry.Weight) * (NoiseWeightFloor + noise);
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
