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
    /// EdgeMargin) + 맵 크기 비례 개수(CountPerTiles)를 반영했다. 3차 재정비: 습도 배율 지원(2절) +
    /// 계산된 앵커를 StructureGenerationSystem이 재사용할 수 있도록 반환.
    /// Sandbox/BattleController.HandleGenerateTerrain이 호출한다.
    /// </summary>
    public static class TerrainGenerationSystem
    {
        /// <summary>이미 놓인 이웃 타입이 없을 때도 완전히 0이 되지 않도록 노이즈 가중치에 더하는 바닥값.</summary>
        private const float NoiseWeightFloor = 0.25f;

        /// <summary>Pangea 랜드마스 모양(중앙 대륙 + 외곽 바다)을 만들 때 쓰는 방사형 감쇠와 노이즈의
        /// 혼합 비율 — 값이 클수록 원형에 가깝고 작을수록 해안선이 더 들쭉날쭉해진다.</summary>
        private const float PangeaRadialWeight = 0.65f;

        /// <summary>지형을 생성하고, 바이옴별 앵커를 반환한다 — StructureGenerationSystem이 수도(Capital)
        /// 배치와 구조물 영역 판정에 같은 앵커를 재사용한다.
        /// wetnessMultiplier(습도 프리셋, BattleController.WetnessPresets)는 1.0이면 CSV 값 그대로,
        /// 그 외에는 TerrainType이 Water인 엔트리의 InnerWeight/OuterWeight/CountPerTiles에만 배율을
        /// 적용한 임시 복사본으로 생성한다 — 원본 CSV 데이터(biomes)는 건드리지 않는다.
        /// pangeaShape가 true면 완전히 다른 경로를 탄다(docs/PolytopiaMapGeneration.md 7.5절) — 쿼드런트
        /// 앵커/개별 타일 확률/MinDistance에 맡기는 대신, 맵 중앙에서 노이즈로 퍼진 "랜드마스 마스크"를
        /// 먼저 확정하고 그 모양에 맞춰 Land/Water를 강제한다. wetnessMultiplier는 이 경로에서는 쓰이지
        /// 않고 pangeaWaterFraction(기본 0.5 = "습도 약 50%")이 대신 목표 물 비율을 정한다.</summary>
        public static Vector2Int[] Generate(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int seed, float wetnessMultiplier = 1f,
            bool pangeaShape = false, float pangeaWaterFraction = 0.5f)
        {
            if (grid == null || biomes == null || biomes.Count == 0) return Array.Empty<Vector2Int>();

            var rng = new Random(seed);

            if (pangeaShape)
                return GeneratePangea(grid, biomes, rng, pangeaWaterFraction);

            var effectiveBiomes = Mathf.Approximately(wetnessMultiplier, 1f) ? biomes : ApplyWetness(biomes, wetnessMultiplier);

            var (biomeIndexPerCell, anchors) = AssignBiomeRegions(grid, effectiveBiomes.Count, rng);
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, effectiveBiomes.Count);

            ClearGeneratedTiles(grid);
            var placedPositionsByType = BuildInitialPlacedPositions(grid);

            PlaceMinCountQuota(grid, effectiveBiomes, biomeIndexPerCell, regionSizePerBiome, placedPositionsByType, rng);
            FillRemaining(grid, effectiveBiomes, biomeIndexPerCell, anchors, placedPositionsByType, rng);

            return anchors;
        }

        /// <summary>Pangea 전용 파이프라인. (1) 방사형 감쇠 + 노이즈로 "중앙에 뭉친" 랜드마스 마스크를
        /// 목표 물 비율에 정확히 맞춰 만들고, (2) 쿼드런트로 뽑은 앵커를 각자 가장 가까운 육지 칸으로
        /// 옮긴 뒤(완전히 바다인 구역에 앵커가 낙하하는 것을 방지 — 원문은 애초에 쿼드런트를 안 쓰지만,
        /// 이 프로젝트의 바이옴별 색/스타일 분배 자체는 계속 필요해 앵커 개념은 유지하고 위치만 육지로
        /// 보정하는 절충안을 택했다), (3) 마스크가 물인 칸은 그 칸이 속한 바이옴의 물 타일로 직접
        /// 채우고(MinDistance 등 개별 제약을 건너뛰어 바다가 실제로 하나로 이어지게 함), (4) 마스크가
        /// 육지인 칸은 기존 가중치 알고리즘으로 채우되 물 타일 엔트리는 후보에서 제외한다(마스크가 이미
        /// 육지/바다를 결정했으므로 이중으로 물이 섞이지 않도록).</summary>
        private static Vector2Int[] GeneratePangea(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, Random rng, float waterFraction)
        {
            var rawAnchors = GenerateQuadrantAnchors(grid, biomes.Count, rng);
            var landMask = GeneratePangeaLandMask(grid, rng, waterFraction);
            var anchors = SnapAnchorsToLand(grid, rawAnchors, landMask);
            var biomeIndexPerCell = ComputeBiomeIndexPerCell(grid, anchors);

            ClearGeneratedTiles(grid);
            var placedPositionsByType = BuildInitialPlacedPositions(grid);

            ApplyLandmassMask(grid, biomes, biomeIndexPerCell, landMask, placedPositionsByType);

            var landOnlyBiomes = StripWaterTiles(biomes);
            var regionSizePerBiome = CountRegionSizes(biomeIndexPerCell, biomes.Count);
            PlaceMinCountQuota(grid, landOnlyBiomes, biomeIndexPerCell, regionSizePerBiome, placedPositionsByType, rng);
            FillRemaining(grid, landOnlyBiomes, biomeIndexPerCell, anchors, placedPositionsByType, rng);

            return anchors;
        }

        /// <summary>맵 중앙에서 멀어질수록 낮아지는 방사형 점수 + 노이즈를 섞어 칸마다 "육지 점수"를
        /// 매기고, 점수 상위 (1-waterFraction) 비율만큼을 육지로 확정한다 — 임계값을 눈대중으로 튜닝하는
        /// 대신 정확히 목표 물 비율을 맞추기 위해 순위 기반으로 자른다. 결과적으로 중앙은 육지 확률이
        /// 높고 가장자리로 갈수록 바다가 되는, "중앙 대륙 + 외곽 바다" 모양이 나온다.</summary>
        private static bool[] GeneratePangeaLandMask(GridWorld grid, Random rng, float waterFraction)
        {
            var center = new Vector2((grid.Width - 1) * 0.5f, (grid.Height - 1) * 0.5f);
            float maxDist = center.magnitude; // 중심에서 모서리까지 거리 — 방사형 값을 0~1로 정규화하는 기준
            int noiseSeedOffset = rng.Next(0, 1_000_000);

            var order = new List<int>(grid.Width * grid.Height);
            var scores = new float[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int index = grid.Index(new Vector2Int(x, y));
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float radial = maxDist > 0f ? 1f - Mathf.Clamp01(dist / maxDist) : 1f;
                    float noise = NoiseSystem.Sample(x, y, frequency: 0.12f, octaves: 3, noiseSeedOffset);
                    scores[index] = radial * PangeaRadialWeight + noise * (1f - PangeaRadialWeight);
                    order.Add(index);
                }
            }
            order.Sort((a, b) => scores[b].CompareTo(scores[a])); // 점수 내림차순 — 점수 높은 칸(=중앙에 가깝고 노이즈도 우호적)부터 육지

            int landCount = Mathf.RoundToInt(order.Count * (1f - waterFraction));
            var mask = new bool[grid.Width * grid.Height];
            for (int i = 0; i < landCount; i++) mask[order[i]] = true;
            return mask;
        }

        /// <summary>앵커가 이미 육지 칸이면 그대로, 바다 칸이면 가장 가까운 육지 칸으로 옮긴다 — 완전히
        /// 바다인 구역에 앵커(그리고 그 위에 자동 배치되는 수도)가 떨어지는 것을 막는다.</summary>
        private static Vector2Int[] SnapAnchorsToLand(GridWorld grid, Vector2Int[] anchors, bool[] landMask)
        {
            var result = new Vector2Int[anchors.Length];
            for (int i = 0; i < anchors.Length; i++)
                result[i] = landMask[grid.Index(anchors[i])] ? anchors[i] : FindNearestLandCell(grid, anchors[i], landMask);
            return result;
        }

        /// <summary>start를 중심으로 점점 넓어지는 정사각 테두리(체비쇼프 반지름)를 훑어 가장 가까운
        /// 육지 칸을 찾는다. 그리드가 작아(최대 30x30) 성능 문제는 없다.</summary>
        private static Vector2Int FindNearestLandCell(GridWorld grid, Vector2Int start, bool[] landMask)
        {
            int maxRadius = grid.Width + grid.Height;
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 테두리만
                        var p = start + new Vector2Int(dx, dy);
                        if (!grid.InBounds(p)) continue;
                        if (landMask[grid.Index(p)]) return p;
                    }
                }
            }
            return start; // 이론상 도달 안 함(육지가 하나도 없을 때만) — 안전한 폴백
        }

        private static int[] ComputeBiomeIndexPerCell(GridWorld grid, Vector2Int[] anchors)
        {
            var result = new int[grid.Width * grid.Height];
            if (anchors.Length <= 1) return result;

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    result[grid.Index(pos)] = ProceduralGenerationUtil.NearestAnchorIndex(anchors, pos);
                }
            return result;
        }

        /// <summary>랜드마스 마스크가 물인 칸을 그 칸이 속한 바이옴의 물 타일로 직접 채운다 — 개별 타일의
        /// MinDistance/가중치 계산을 거치지 않으므로, 이 칸들이 실제로 하나로 이어진 바다를 이룰 수 있다
        /// (기존 방식은 물 타일끼리 MinDistance 제약이 있어 바다가 절대 뭉칠 수 없었다 — 판게아가
        /// 의도한 것과 정반대였던 원인).</summary>
        private static void ApplyLandmassMask(GridWorld grid, IReadOnlyList<BiomeCsvRow> biomes, int[] biomeIndexPerCell,
            bool[] landMask, Dictionary<string, List<Vector2Int>> placedPositionsByType)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int index = grid.Index(pos);
                    if (landMask[index]) continue; // 육지 칸은 이후 PlaceMinCountQuota/FillRemaining이 채움
                    if (grid.IsOccupied(pos)) continue;

                    var biome = biomes[biomeIndexPerCell[index]];
                    string waterTileId = FindFirstWaterTileId(biome);
                    if (waterTileId == null)
                    {
                        Debug.LogWarning($"[TerrainGenerationSystem] biome '{biome.Id}'에 Water 타입 타일이 정의되어 있지 않아 판게아 바다 칸 {pos}을 채우지 못했습니다.");
                        continue;
                    }

                    grid.SetTerrain(pos, TerrainType.Water);
                    grid.SetTileType(pos, waterTileId);
                    AddPlacedPosition(placedPositionsByType, waterTileId, pos);
                }
            }
        }

        private static string FindFirstWaterTileId(BiomeCsvRow biome)
        {
            foreach (var tile in biome.Tiles)
                if (tile.TerrainType == TerrainType.Water) return tile.TileId;
            return null;
        }

        /// <summary>바이옴 목록을 복제하되 TerrainType이 Water인 타일 엔트리를 전부 뺀다 — 판게아 모드의
        /// 육지 채우기 단계는 랜드마스 마스크가 이미 "여긴 육지"라고 확정한 칸만 다루므로, 물 타일이
        /// 후보로 섞여 들어오면 안 된다.</summary>
        private static List<BiomeCsvRow> StripWaterTiles(IReadOnlyList<BiomeCsvRow> biomes)
        {
            var result = new List<BiomeCsvRow>(biomes.Count);
            foreach (var biome in biomes)
            {
                var clone = new BiomeCsvRow
                {
                    Id = biome.Id,
                    Name = biome.Name,
                    NoiseType = biome.NoiseType,
                    Frequency = biome.Frequency,
                    Octaves = biome.Octaves,
                    SeedOffset = biome.SeedOffset,
                    InnerRadius = biome.InnerRadius,
                    Structures = biome.Structures
                };
                foreach (var entry in biome.Tiles)
                    if (entry.TerrainType != TerrainType.Water) clone.Tiles.Add(entry);
                result.Add(clone);
            }
            return result;
        }

        /// <summary>Water 타입 엔트리만 배율을 적용한 바이옴 목록 복제본을 만든다. InnerWeight/OuterWeight는
        /// 곱하고(직접적인 확률 계수라 배율이 클수록 그대로 더 잘 나옴), CountPerTiles는 나눈다(개수 =
        /// 영역 크기/CountPerTiles라 값이 작아질수록 개수가 늘어나므로 — 습도가 높을수록 더 흔해지려면
        /// 나눠야 한다). 원본 biomes 리스트/엔트리는 값 타입(struct) 복사라 전혀 변경되지 않는다.</summary>
        private static List<BiomeCsvRow> ApplyWetness(IReadOnlyList<BiomeCsvRow> biomes, float wetnessMultiplier)
        {
            var result = new List<BiomeCsvRow>(biomes.Count);
            foreach (var biome in biomes)
            {
                var clone = new BiomeCsvRow
                {
                    Id = biome.Id,
                    Name = biome.Name,
                    NoiseType = biome.NoiseType,
                    Frequency = biome.Frequency,
                    Octaves = biome.Octaves,
                    SeedOffset = biome.SeedOffset,
                    InnerRadius = biome.InnerRadius,
                    Structures = biome.Structures
                };
                foreach (var entry in biome.Tiles)
                {
                    var e = entry;
                    if (e.TerrainType == TerrainType.Water)
                    {
                        e.InnerWeight *= wetnessMultiplier;
                        e.OuterWeight *= wetnessMultiplier;
                        if (e.CountPerTiles > 0f && wetnessMultiplier > 0f) e.CountPerTiles /= wetnessMultiplier;
                    }
                    clone.Tiles.Add(e);
                }
                result.Add(clone);
            }
            return result;
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
            var anchors = GenerateQuadrantAnchors(grid, biomeCount, rng);
            var biomeIndexPerCell = ComputeBiomeIndexPerCell(grid, anchors);
            return (biomeIndexPerCell, anchors);
        }

        /// <summary>바이옴 수만큼 쿼드런트를 나눠 서로 다른 구역에서 앵커를 하나씩 뽑는다(공정성 배치).
        /// 판게아 모드(GeneratePangea)도 같은 함수로 시작점을 뽑은 뒤 육지로 스냅한다.</summary>
        private static Vector2Int[] GenerateQuadrantAnchors(GridWorld grid, int biomeCount, Random rng)
        {
            int quadrantsPerSide = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(biomeCount)));
            var quadrantIndices = new List<int>();
            for (int i = 0; i < quadrantsPerSide * quadrantsPerSide; i++) quadrantIndices.Add(i);
            ProceduralGenerationUtil.Shuffle(quadrantIndices, rng);

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
            return anchors;
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
                    int targetCount = EffectiveMinCount(entry.MinCount, entry.CountPerTiles, regionSizePerBiome[biomeIdx]);
                    if (targetCount <= 0) continue;

                    var candidates = CollectEmptyCellsInBiome(grid, biomeIndexPerCell, biomeIdx);
                    ProceduralGenerationUtil.Shuffle(candidates, rng);

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

        /// <summary>MinCount와 CountPerTiles(이 바이옴/구조물 영역 크기에 비례, Polytopia의 유적/외딴섬
        /// 마을 개수표와 같은 개념) 중 더 큰 값을 실제 목표 개수로 쓴다. StructureGenerationSystem도
        /// 재사용한다.</summary>
        public static int EffectiveMinCount(int minCount, float countPerTiles, int regionSize)
        {
            int fromDensity = countPerTiles > 0f ? Mathf.RoundToInt(regionSize / countPerTiles) : 0;
            return Mathf.Max(minCount, fromDensity);
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
            ProceduralGenerationUtil.Shuffle(emptyCells, rng);

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

                var chosen = candidates.Count == 0 ? biome.Tiles[0] : ProceduralGenerationUtil.WeightedPick(candidates, rng);
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
            if (entry.EdgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, pos) < entry.EdgeMargin) return true;

            if (entry.MinDistance > 0 && placedPositionsByType.TryGetValue(entry.TileId, out var placed))
            {
                foreach (var other in placed)
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < entry.MinDistance) return true;
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
            int distToAnchor = ProceduralGenerationUtil.ChebyshevDistance(anchor, new Vector2Int(x, y));
            float baseWeight = distToAnchor <= biome.InnerRadius ? entry.InnerWeight : entry.OuterWeight;

            float noise = NoiseSystem.Sample(x, y, biome.Frequency, biome.Octaves, biome.SeedOffset + entryIndex * 997);
            return Mathf.Max(0f, baseWeight) * (NoiseWeightFloor + noise);
        }
    }
}
