using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// GridWorld에 타일 위 구조물(수도/유적/자원/불가사리/마을)을 배치하는 상태 없는 정적 시스템 —
    /// TerrainGenerationSystem이 채운 지형/앵커 위에 얹는다(docs/PolytopiaMapGeneration.md
    /// 3(자원)/6(수도)/7(마을)/8(외딴섬 마을)/9(유적)/10(불가사리)절). BattleController.HandleGenerateTerrain이
    /// TerrainGenerationSystem.Generate가 반환한 anchors로 이어서 호출한다. 구조물은 순수 시각 요소라
    /// 이동/점유 판정에 관여하지 않는다(TileData.StructureId 참고).
    /// </summary>
    public static class StructureGenerationSystem
    {
        public const string CapitalStructureId = "Capital";
        public const string VillageStructureId = "Village";

        /// <summary>Polytopia의 맵 크기별 외딴 섬 마을 개수표(8절) — MapSizePresets(BattleController)의
        /// 변 길이와 같은 값으로 조회한다.</summary>
        private static readonly (int Size, int Count)[] TinyIslandCounts =
        {
            (11, 0), (14, 1), (16, 2), (18, 3), (20, 4), (30, 9)
        };

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

            var rng = new Random(seed);
            for (int biomeIdx = 0; biomeIdx < biomes.Count; biomeIdx++)
                PlaceBiomeStructures(grid, biomes[biomeIdx], biomeIndexPerCell, biomeIdx, anchors[biomeIdx], rng);

            PlaceTinyIslandVillages(grid, biomes, biomeIndexPerCell, rng);
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

        /// <summary>이 바이옴의 Structures 엔트리들을 한 번에 처리한다. 엔트리마다 목표 개수를
        /// AllowedTileTypes에 해당하는 칸 수(전체 영역이 아니라 그 타입이 실제로 있는 칸 수 — 예:
        /// Starfish는 Water 타일 수 기준)로 미리 정해두고, 그 바이옴 영역의 빈 칸(구조물 없음, 유닛
        /// 미점유, 지형 생성이 끝난 칸)을 셔플된 순서로 순회하며, 각 칸에서 AllowedTileTypes/EdgeMargin/
        /// MinDistance/MaxDistanceFromAnchor/MaxWaterFraction/ExcludeAdjacentStructures를 만족하고 아직
        /// 목표를 채우지 못한(또는 FillRemaining인) 엔트리들 중 Weight 비례로 하나를 뽑아 배치한다 —
        /// 여러 엔트리가 같은 타일 타입을 두고 경쟁할 때(예: Ruin과 Resource_Food가 둘 다 Grass에 놓일
        /// 수 있음) Weight가 실제로 승률에 반영되도록 이런 구조를 택했다.</summary>
        private static void PlaceBiomeStructures(GridWorld grid, BiomeCsvRow biome, int[] biomeIndexPerCell, int biomeIdx, Vector2Int anchor, Random rng)
        {
            if (biome.Structures == null || biome.Structures.Count == 0) return;
            var entries = biome.Structures;

            var eligibleCounts = new int[entries.Count];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (biomeIndexPerCell[grid.Index(pos)] != biomeIdx) continue;
                    var tileType = grid.GetTileType(pos);
                    if (string.IsNullOrEmpty(tileType)) continue;
                    for (int i = 0; i < entries.Count; i++)
                        if (entries[i].AllowedTileTypes != null && Array.IndexOf(entries[i].AllowedTileTypes, tileType) >= 0)
                            eligibleCounts[i]++;
                }

            var remaining = new int[entries.Count];
            bool anyActive = false;
            for (int i = 0; i < entries.Count; i++)
            {
                remaining[i] = entries[i].FillRemaining ? int.MaxValue : TerrainGenerationSystem.EffectiveMinCount(entries[i].MinCount, entries[i].CountPerTiles, eligibleCounts[i]);
                if (remaining[i] > 0) anyActive = true;
            }
            if (!anyActive) return;

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
            var totalPlacedByEntry = new int[entries.Count];
            var waterPlacedByEntry = new int[entries.Count];

            foreach (var pos in eligibleCells)
            {
                var tileType = grid.GetTileType(pos);
                bool isWaterTile = grid.GetTerrain(pos) == TerrainType.Water;

                var candidates = new List<(int Entry, float Weight)>();
                for (int i = 0; i < entries.Count; i++)
                {
                    if (remaining[i] <= 0) continue;
                    var entry = entries[i];
                    if (entry.AllowedTileTypes == null || Array.IndexOf(entry.AllowedTileTypes, tileType) < 0) continue;
                    if (ViolatesConstraints(grid, pos, entry, anchor, placedPositionsByStructure)) continue;
                    if (isWaterTile && entry.MaxWaterFraction.HasValue && entry.MaxWaterFraction.Value < 1f)
                    {
                        int allowedWater = Mathf.CeilToInt((totalPlacedByEntry[i] + 1) * entry.MaxWaterFraction.Value);
                        if (waterPlacedByEntry[i] + 1 > allowedWater) continue;
                    }
                    candidates.Add((i, Mathf.Max(0f, entry.Weight)));
                }
                if (candidates.Count == 0) continue;

                int chosenIndex = ProceduralGenerationUtil.WeightedPick(candidates, rng);
                var chosenEntry = entries[chosenIndex];
                grid.SetStructure(pos, chosenEntry.StructureId);
                AddPlacedPosition(placedPositionsByStructure, chosenEntry.StructureId, pos);
                if (remaining[chosenIndex] != int.MaxValue) remaining[chosenIndex]--;
                totalPlacedByEntry[chosenIndex]++;
                if (isWaterTile) waterPlacedByEntry[chosenIndex]++;
            }
        }

        private static bool ViolatesConstraints(GridWorld grid, Vector2Int pos, BiomeStructureEntry entry, Vector2Int anchor, Dictionary<string, List<Vector2Int>> placedPositionsByStructure)
        {
            if (entry.EdgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, pos) < entry.EdgeMargin) return true;

            if (entry.MaxDistanceFromAnchor > 0 && ProceduralGenerationUtil.ChebyshevDistance(pos, anchor) > entry.MaxDistanceFromAnchor) return true;

            if (entry.MinDistance > 0 && placedPositionsByStructure.TryGetValue(entry.StructureId, out var placed))
                foreach (var other in placed)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < entry.MinDistance) return true;

            if (entry.ExcludeAdjacentStructures != null && entry.ExcludeAdjacentStructures.Length > 0)
            {
                foreach (var neighborPos in grid.GetNeighbors(pos, allowDiagonal: false))
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

        /// <summary>본토와 육로로 연결되지 않은(4방향 이웃이 전부 물인) 물 타일을 찾아 그 칸을 육지로
        /// 바꾸고 마을을 배치한다(8절). 개수는 맵 크기(그리드 한 변 길이)로 정해진 표를 그대로 따른다.
        /// 육지로 바꿀 때 쓸 TileTypeId는 그 칸이 속한 바이옴의 첫 번째 육지 타일 엔트리를 재사용한다.</summary>
        private static void PlaceTinyIslandVillages(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell, Random rng)
        {
            int count = 0;
            foreach (var (size, c) in TinyIslandCounts)
                if (grid.Width == size) { count = c; break; }
            if (count <= 0) return;

            var candidates = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetTerrain(pos) != TerrainType.Water) continue;
                    if (grid.IsOccupied(pos)) continue;
                    if (!string.IsNullOrEmpty(grid.GetStructure(pos))) continue;

                    bool surroundedByWater = true;
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                        if (grid.GetTerrain(n) != TerrainType.Water) { surroundedByWater = false; break; }
                    if (surroundedByWater) candidates.Add(pos);
                }
            ProceduralGenerationUtil.Shuffle(candidates, rng);

            int placed = 0;
            foreach (var pos in candidates)
            {
                if (placed >= count) break;

                var biome = biomes[biomeIndexPerCell[grid.Index(pos)]];
                string landTileId = FindFirstLandTileId(biome);
                if (landTileId == null) continue; // 이 바이옴엔 육지 타일 정의가 없음 — 건너뜀

                grid.SetTerrain(pos, TerrainType.Land);
                grid.SetTileType(pos, landTileId);
                grid.SetStructure(pos, VillageStructureId);
                placed++;
            }
        }

        private static string FindFirstLandTileId(BiomeCsvRow biome)
        {
            foreach (var tile in biome.Tiles)
                if (tile.TerrainType == TerrainType.Land) return tile.TileId;
            return null;
        }
    }
}
