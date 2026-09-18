using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld에 타일 위 구조물(수도/유적/자원/불가사리)을 배치하는 상태 없는 정적 시스템 —
    /// TerrainGenerationSystem이 채운 지형/앵커 위에 얹는다(docs/PolytopiaMapGeneration.md
    /// 3(자원)/6(수도)/9(유적)/10(불가사리)절). BattleController.HandleGenerateTerrain이
    /// TerrainGenerationSystem.Generate가 반환한 anchors로 이어서 호출한다. 구조물은 순수 시각 요소라
    /// 이동/점유 판정에 관여하지 않는다(TileData.StructureId 참고).
    /// </summary>
    public static class StructureGenerationSystem
    {
        public const string CapitalStructureId = "Capital";

        public static void Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, Vector2Int[] anchors, int seed)
        {
            if (grid == null || biomes == null || anchors == null || anchors.Length == 0) return;

            ClearGeneratedStructures(grid);
            PlaceCapitals(grid, anchors);

            var biomeIndexPerCell = new int[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    biomeIndexPerCell[grid.Index(pos)] = ProceduralGenerationUtil.NearestAnchorIndex(anchors, pos);
                }
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, biomes.Count);

            var rng = new Random(seed);
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
                PlaceBiomeStructures(grid, biomes[biomeIdx], biomeIndexPerCell, biomeIdx, regionSizePerBiome[biomeIdx], rng);
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

        /// <summary>바이옴 앵커(Polytopia의 "수도 역할" — TerrainGenerationSystem.AssignBiomeRegions
        /// 참고)마다 그 칸에 수도를 자동 배치한다. CSV 엔트리가 아니다 — 앵커 자체가 이미 그 바이옴의
        /// 중심점이라는 의미를 갖고 있어서 별도 규칙 없이 그대로 재사용한다.</summary>
        private static void PlaceCapitals(GridWorld grid, Vector2Int[] anchors)
        {
            foreach (var anchor in anchors)
            {
                if (!grid.InBounds(anchor) || grid.IsOccupied(anchor)) continue;
                grid.SetStructure(anchor, CapitalStructureId);
            }
        }

        private static int[] CountRegionSizes(int[] biomeIndexPerCell, int biomeCount)
        {
            var sizes = new int[biomeCount];
            foreach (var idx in biomeIndexPerCell) sizes[idx]++;
            return sizes;
        }

        /// <summary>이 바이옴의 Structures 엔트리들을 한 번에 처리한다. 엔트리마다 목표 개수(MinCount/
        /// CountPerTiles)를 미리 정해두고, 그 바이옴 영역의 빈 칸(구조물 없음, 유닛 미점유, 지형 생성이
        /// 끝난 칸)을 셔플된 순서로 순회하며, 각 칸에서 AllowedTileTypes/EdgeMargin/MinDistance를 만족하고
        /// 아직 목표를 채우지 못한 엔트리들 중 Weight 비례로 하나를 뽑아 배치한다 — 여러 엔트리가 같은
        /// 타일 타입을 두고 경쟁할 때(예: Ruin과 Resource_Food가 둘 다 Grass에 놓일 수 있음) Weight가
        /// 실제로 승률에 반영되도록 이런 구조를 택했다.</summary>
        private static void PlaceBiomeStructures(GridWorld grid, BiomeCsvRow biome, int[] biomeIndexPerCell, int biomeIdx, int regionSize, Random rng)
        {
            if (biome.Structures == null || biome.Structures.Count == 0) return;

            var remaining = new int[biome.Structures.Count];
            bool anyTarget = false;
            for (int i = 0; i < biome.Structures.Count; i++)
            {
                remaining[i] = TerrainGenerationSystem.EffectiveMinCount(biome.Structures[i].MinCount, biome.Structures[i].CountPerTiles, regionSize);
                if (remaining[i] > 0) anyTarget = true;
            }
            if (!anyTarget) return;

            var eligibleCells = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (biomeIndexPerCell[grid.Index(pos)] != biomeIdx) continue;
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetStructure(pos))) continue;
                    if (string.IsNullOrEmpty(grid.GetTileType(pos))) continue;
                    eligibleCells.Add(pos);
                }
            ProceduralGenerationUtil.Shuffle(eligibleCells, rng);

            var placedPositionsByStructure = new Dictionary<string, List<Vector2Int>>();

            foreach (var pos in eligibleCells)
            {
                var tileType = grid.GetTileType(pos);
                var candidates = new List<(int Entry, float Weight)>();
                for (int i = 0; i < biome.Structures.Count; i++)
                {
                    if (remaining[i] <= 0) continue;
                    var entry = biome.Structures[i];
                    if (entry.AllowedTileTypes == null || Array.IndexOf(entry.AllowedTileTypes, tileType) < 0) continue;
                    if (ViolatesConstraints(grid, pos, entry, placedPositionsByStructure)) continue;
                    candidates.Add((i, Mathf.Max(0f, entry.Weight)));
                }
                if (candidates.Count == 0) continue;

                int chosenIndex = ProceduralGenerationUtil.WeightedPick(candidates, rng);
                var chosenEntry = biome.Structures[chosenIndex];
                grid.SetStructure(pos, chosenEntry.StructureId);
                AddPlacedPosition(placedPositionsByStructure, chosenEntry.StructureId, pos);
                remaining[chosenIndex]--;
            }
        }

        private static bool ViolatesConstraints(GridWorld grid, Vector2Int pos, BiomeStructureEntry entry, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            if (entry.EdgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, pos) < entry.EdgeMargin) return true;

            if (entry.MinDistance > 0 && placedPositionsByStructure.TryGetValue(entry.StructureId, out var placed))
                foreach (var other in placed)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < entry.MinDistance) return true;

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
    }
}
