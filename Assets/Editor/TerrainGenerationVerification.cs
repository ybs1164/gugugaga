using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 바이옴 지형 생성 파이프라인(CSV 왕복, 노이즈/Inner-Outer 가중치/제약(인접배제·최소거리·가장자리
    /// 여백)/맵 크기 비례 개수/쿼드런트 공정 배치/시드 재현성)을 자동으로 검증하는 배치모드 전용
    /// 스크립트. UnitCsvVerification과 같은 패턴 — Play 모드 없이 Console 로그의 PASS/FAIL만 확인하면
    /// 된다. docs/PolytopiaMapGeneration.md 기준 2차 재정비 반영.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.TerrainGenerationVerification.Run -quit
    /// </summary>
    public static class TerrainGenerationVerification
    {
        private const string SampleCsvRelativePath = "docs/sample_biomes.csv";

        public static void Run()
        {
            bool ok = VerifyRoundTrip() &
                      VerifySingleBiomeRules() &
                      VerifyDeterministicSeed() &
                      VerifyMultiBiomeSmoke() &
                      VerifyQuadrantFairness() &
                      VerifyWetnessMultiplier() &
                      VerifyPangeaShape() &
                      VerifyOtherMapShapes() &
                      VerifyPreTerrainVillagePlanning() &
                      VerifyGuaranteedLandAtReservedPositions() &
                      VerifyCapitalPlacementRules() &
                      VerifyCitiesNeverAdjacent() &
                      VerifyForestMountainLayer() &
                      VerifyWaterDepthClassification() &
                      VerifyContinentsShape();
            Debug.Log(ok ? "[TerrainGenerationVerification] ALL PASS" : "[TerrainGenerationVerification] SOME CHECKS FAILED - see errors above");
        }

        private static string ReadCsv(string relativePath) => File.ReadAllText(Path.Combine(Application.dataPath, "..", relativePath));

        /// <summary>Parse -> Write -> 재파싱해서 값이 그대로 보존되는지 확인한다(새 필드 전부 포함).</summary>
        private static bool VerifyRoundTrip()
        {
            var absolutePath = Path.Combine(Application.dataPath, "..", SampleCsvRelativePath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[TerrainGenerationVerification] csv not found: {absolutePath}");
                return false;
            }

            var original = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var rewritten = BiomeCsvSerializer.Parse(BiomeCsvSerializer.Write(original));

            if (original.Count == 0)
            {
                Debug.LogError($"[TerrainGenerationVerification] {SampleCsvRelativePath} parsed to 0 rows");
                return false;
            }
            if (original.Count != rewritten.Count)
            {
                Debug.LogError($"[TerrainGenerationVerification] round-trip row count mismatch: {original.Count} vs {rewritten.Count}");
                return false;
            }

            bool ok = true;
            for (int i = 0; i < original.Count; i++)
            {
                var a = original[i];
                var b = rewritten[i];
                bool same = a.Id == b.Id && a.Name == b.Name && a.NoiseType == b.NoiseType &&
                    Mathf.Approximately(a.Frequency, b.Frequency) && a.Octaves == b.Octaves && a.SeedOffset == b.SeedOffset &&
                    a.InnerRadius == b.InnerRadius && a.Tiles.Count == b.Tiles.Count && a.Structures.Count == b.Structures.Count &&
                    Mathf.Approximately(a.MountainRate, b.MountainRate) && Mathf.Approximately(a.ForestRate, b.ForestRate);

                if (same)
                {
                    for (int t = 0; t < a.Tiles.Count; t++)
                    {
                        var ta = a.Tiles[t];
                        var tb = b.Tiles[t];
                        bool tileSame = ta.TileId == tb.TileId && ta.TerrainType == tb.TerrainType &&
                            Mathf.Approximately(ta.InnerWeight, tb.InnerWeight) && Mathf.Approximately(ta.OuterWeight, tb.OuterWeight) &&
                            ta.MinCount == tb.MinCount && Mathf.Approximately(ta.CountPerTiles, tb.CountPerTiles) &&
                            ta.MinDistance == tb.MinDistance && ta.EdgeMargin == tb.EdgeMargin &&
                            string.Join("|", ta.ExcludeAdjacent) == string.Join("|", tb.ExcludeAdjacent);
                        if (!tileSame) { same = false; break; }
                    }
                }

                if (same)
                {
                    for (int s = 0; s < a.Structures.Count; s++)
                    {
                        var sa = a.Structures[s];
                        var sb = b.Structures[s];
                        bool structureSame = sa.StructureId == sb.StructureId &&
                            string.Join("|", sa.AllowedTileTypes) == string.Join("|", sb.AllowedTileTypes) &&
                            Mathf.Approximately(sa.Weight, sb.Weight) && sa.MinCount == sb.MinCount &&
                            Mathf.Approximately(sa.CountPerTiles, sb.CountPerTiles) &&
                            sa.MinDistance == sb.MinDistance && sa.EdgeMargin == sb.EdgeMargin &&
                            sa.MaxDistanceFromCity == sb.MaxDistanceFromCity &&
                            sa.MaxWaterFractionOnLakes.HasValue == sb.MaxWaterFractionOnLakes.HasValue &&
                            (!sa.MaxWaterFractionOnLakes.HasValue || Mathf.Approximately(sa.MaxWaterFractionOnLakes.Value, sb.MaxWaterFractionOnLakes.Value)) &&
                            sa.FillRemaining == sb.FillRemaining &&
                            Mathf.Approximately(sa.InnerRate, sb.InnerRate) && Mathf.Approximately(sa.OuterRate, sb.OuterRate) &&
                            string.Join("|", sa.ExcludeAdjacentStructures) == string.Join("|", sb.ExcludeAdjacentStructures);
                        if (!structureSame) { same = false; break; }
                    }
                }

                if (!same)
                {
                    Debug.LogError($"[TerrainGenerationVerification] round-trip mismatch on row {i} ({a.Id})");
                    ok = false;
                }
            }

            if (ok) Debug.Log($"[TerrainGenerationVerification] round-trip PASS ({original.Count} biomes)");
            return ok;
        }

        /// <summary>바이옴 1개짜리 그리드(Voronoi 분할이 개입하지 않아 판정이 모호하지 않음)로 MinCount/
        /// ExcludeAdjacent/MinDistance/EdgeMargin/CountPerTiles가 전부 실제로 지켜지는지 정확히 검증한다.</summary>
        private static bool VerifySingleBiomeRules()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Grassland", Name = "평원", NoiseType = "Perlin", Frequency = 0.15f, Octaves = 3, SeedOffset = 101, InnerRadius = 3,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 0.6f, OuterWeight = 0.6f },
                    new BiomeTileEntry { TileId = "Forest", TerrainType = TerrainType.Land, InnerWeight = 0.1f, OuterWeight = 0.35f, MinCount = 3, ExcludeAdjacent = new[] { "Water" } },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 0.1f, OuterWeight = 0.15f, MinCount = 2, ExcludeAdjacent = new[] { "Forest" } },
                    // EdgeMargin=1/MinDistance=3인 상태로 12x12(단일 바이옴이라 영역 크기=144) 전체에서
                    // CountPerTiles=30 -> round(144/30)=5개를 목표로 한다. 여백 뺀 유효 영역(10칸 폭)에서도
                    // 3칸 간격 격자(0,3,6,9)가 4x4=16자리 나오므로 5개는 넉넉히 배치 가능해야 한다.
                    new BiomeTileEntry { TileId = "Ruin", TerrainType = TerrainType.Land, InnerWeight = 0.05f, OuterWeight = 0.05f, CountPerTiles = 30f, MinDistance = 3, EdgeMargin = 1 },
                }
            };

            var grid = new GridWorld(12, 12, 1f);
            TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 42);

            var counts = new Dictionary<string, int>();
            var ruinPositions = new List<Vector2Int>();
            bool ok = true;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var tileType = grid.GetTileType(pos);
                    if (string.IsNullOrEmpty(tileType))
                    {
                        Debug.LogError($"[TerrainGenerationVerification] cell {pos} left empty after generation");
                        ok = false;
                        continue;
                    }
                    counts[tileType] = counts.TryGetValue(tileType, out var c) ? c + 1 : 1;
                    if (tileType == "Ruin") ruinPositions.Add(pos);

                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                    {
                        var neighborType = grid.GetTileType(n);
                        if (tileType == "Forest" && neighborType == "Water")
                        {
                            Debug.LogError($"[TerrainGenerationVerification] adjacency violation: Forest {pos} next to Water {n}");
                            ok = false;
                        }
                        if (tileType == "Water" && neighborType == "Forest")
                        {
                            Debug.LogError($"[TerrainGenerationVerification] adjacency violation: Water {pos} next to Forest {n}");
                            ok = false;
                        }
                    }
                }
            }

            if (!counts.TryGetValue("Forest", out var forestCount) || forestCount < 3)
            {
                Debug.LogError($"[TerrainGenerationVerification] Forest MinCount(3) not satisfied, got {(counts.TryGetValue("Forest", out var fc) ? fc : 0)}");
                ok = false;
            }
            if (!counts.TryGetValue("Water", out var waterCount) || waterCount < 2)
            {
                Debug.LogError($"[TerrainGenerationVerification] Water MinCount(2) not satisfied, got {(counts.TryGetValue("Water", out var wc) ? wc : 0)}");
                ok = false;
            }
            if (ruinPositions.Count < 5)
            {
                Debug.LogError($"[TerrainGenerationVerification] Ruin CountPerTiles(30 on 144 tiles -> 5) not satisfied, got {ruinPositions.Count}");
                ok = false;
            }
            for (int i = 0; i < ruinPositions.Count; i++)
            {
                var p = ruinPositions[i];
                int distToEdge = Mathf.Min(Mathf.Min(p.x, p.y), Mathf.Min(grid.Width - 1 - p.x, grid.Height - 1 - p.y));
                if (distToEdge < 1)
                {
                    Debug.LogError($"[TerrainGenerationVerification] Ruin {p} violates EdgeMargin(1)");
                    ok = false;
                }
                for (int j = i + 1; j < ruinPositions.Count; j++)
                {
                    var q = ruinPositions[j];
                    int dist = Mathf.Max(Mathf.Abs(p.x - q.x), Mathf.Abs(p.y - q.y));
                    if (dist < 3)
                    {
                        Debug.LogError($"[TerrainGenerationVerification] Ruin {p} and {q} violate MinDistance(3): dist={dist}");
                        ok = false;
                    }
                }
            }

            if (ok) Debug.Log($"[TerrainGenerationVerification] single-biome rules PASS (Forest={forestCount}, Water={waterCount}, Ruin={ruinPositions.Count})");
            return ok;
        }

        /// <summary>같은 시드로 두 번 생성하면 완전히 같은 결과가 나오는지(결정론적) 확인한다.</summary>
        private static bool VerifyDeterministicSeed()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var gridA = new GridWorld(16, 16, 1f);
            var gridB = new GridWorld(16, 16, 1f);
            TerrainGenerationSystem.Generate(gridA, biomes, seed: 777);
            TerrainGenerationSystem.Generate(gridB, biomes, seed: 777);

            bool ok = true;
            for (int y = 0; y < gridA.Height; y++)
                for (int x = 0; x < gridA.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (gridA.GetTileType(pos) != gridB.GetTileType(pos) || gridA.GetTerrain(pos) != gridB.GetTerrain(pos))
                    {
                        Debug.LogError($"[TerrainGenerationVerification] seed determinism mismatch at {pos}");
                        ok = false;
                    }
                }

            if (ok) Debug.Log("[TerrainGenerationVerification] deterministic seed PASS");
            return ok;
        }

        /// <summary>docs/sample_biomes.csv 3바이옴을 넓은 그리드에 실제로 생성해서 예외 없이 끝나고
        /// 모든 칸이 채워지는지 확인하는 통합 스모크 테스트(각 바이옴이 CountPerTiles로 개수를 정하므로
        /// 정확한 개수 하한 검증은 VerifySingleBiomeRules가 모호함 없이 다룬다).</summary>
        private static bool VerifyMultiBiomeSmoke()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var grid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed: 2024);

            bool ok = true;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    if (string.IsNullOrEmpty(grid.GetTileType(new Vector2Int(x, y))))
                    {
                        Debug.LogError($"[TerrainGenerationVerification] multi-biome: cell ({x},{y}) left empty");
                        ok = false;
                    }
                }

            if (ok) Debug.Log("[TerrainGenerationVerification] multi-biome smoke PASS");
            return ok;
        }

        /// <summary>바이옴 수만큼의 앵커가 서로 다른 쿼드런트(구역)에 배치되는지(공정성 규칙) 확인한다.
        /// TerrainGenerationSystem은 앵커를 외부에 노출하지 않으므로, 바이옴마다 서로 다른(겹치지 않는)
        /// 단일 TileId만 쓰는 합성 바이옴 4개를 만들어(sample_biomes.csv는 여러 바이옴이 "Water"를
        /// 공유해서 칸 -> 바이옴 역추적이 모호함) 생성 결과에서 각 바이옴이 차지한 칸들이 가장 많이 몰린
        /// 구역(사분면)을 그 바이옴의 근거지로 보고, 서로 다른 바이옴끼리 근거지가 겹치지 않는지로
        /// 간접 확인한다 — 완전 랜덤 시드였다면(1차 구현) 우연히 겹칠 수 있지만 쿼드런트 배치는 이를
        /// 구조적으로 방지한다.</summary>
        private static bool VerifyQuadrantFairness()
        {
            var biomes = new List<BiomeCsvRow>();
            string[] tileIds = { "TileA", "TileB", "TileC", "TileD" };
            foreach (var tileId in tileIds)
            {
                biomes.Add(new BiomeCsvRow
                {
                    Id = tileId, Name = tileId, Frequency = 0.1f, Octaves = 2, SeedOffset = 1, InnerRadius = 1,
                    Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = tileId, TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } }
                });
            }

            var grid = new GridWorld(24, 24, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed: 55);

            // 4개 바이옴 -> quadrantsPerSide = ceil(sqrt(4)) = 2, 즉 2x2=4구역에 하나씩 배정되어야 함.
            var cellCountPerQuadrantPerBiome = new int[tileIds.Length, 4];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var tileType = grid.GetTileType(new Vector2Int(x, y));
                    int biomeIdx = System.Array.IndexOf(tileIds, tileType);
                    if (biomeIdx < 0) continue;
                    int quadrant = (x < grid.Width / 2 ? 0 : 1) + (y < grid.Height / 2 ? 0 : 2);
                    cellCountPerQuadrantPerBiome[biomeIdx, quadrant]++;
                }
            }

            var homeQuadrantByBiome = new int[tileIds.Length];
            for (int b = 0; b < tileIds.Length; b++)
            {
                int bestQuadrant = -1, bestCount = -1;
                for (int q = 0; q < 4; q++)
                    if (cellCountPerQuadrantPerBiome[b, q] > bestCount) { bestCount = cellCountPerQuadrantPerBiome[b, q]; bestQuadrant = q; }
                homeQuadrantByBiome[b] = bestQuadrant;
            }

            bool ok = true;
            var seenQuadrants = new HashSet<int>();
            for (int b = 0; b < tileIds.Length; b++)
            {
                if (!seenQuadrants.Add(homeQuadrantByBiome[b]))
                {
                    Debug.LogError($"[TerrainGenerationVerification] quadrant fairness violation: biome '{tileIds[b]}' shares home quadrant {homeQuadrantByBiome[b]} with another biome");
                    ok = false;
                }
            }

            if (ok) Debug.Log("[TerrainGenerationVerification] quadrant fairness PASS");
            return ok;
        }

        /// <summary>습도 배율(BattleController.WetnessPresets, 3차 재정비)이 실제로 Water 타일 비율을
        /// 바꾸는지 확인한다 — 같은 시드/바이옴으로 낮은 배율과 높은 배율 두 번 생성해서 Water 타일 수가
        /// 유의미하게 달라지는지 비교한다.</summary>
        private static bool VerifyWetnessMultiplier()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));

            var dryGrid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(dryGrid, biomes, seed: 999, wetnessMultiplier: 0.1f);
            int dryWaterCount = CountTerrain(dryGrid, TerrainType.Water); // 얕은 물 + 깊은 바다(Ocean) 전부

            var wetGrid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(wetGrid, biomes, seed: 999, wetnessMultiplier: 3.0f);
            int wetWaterCount = CountTerrain(wetGrid, TerrainType.Water);

            bool ok = wetWaterCount > dryWaterCount;
            if (!ok)
                Debug.LogError($"[TerrainGenerationVerification] wetness multiplier had no effect: dry={dryWaterCount} wet={wetWaterCount} (wet 배율이 더 많은 Water 타일을 만들어야 함)");
            else
                Debug.Log($"[TerrainGenerationVerification] wetness multiplier PASS (dry={dryWaterCount}, wet={wetWaterCount})");
            return ok;
        }

        private static int CountTerrain(GridWorld grid, TerrainType terrain)
        {
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetTerrain(new Vector2Int(x, y)) == terrain) count++;
            return count;
        }

        private static int CountTileType(GridWorld grid, string tileType)
        {
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetTileType(new Vector2Int(x, y)) == tileType) count++;
            return count;
        }

        /// <summary>판게아 모드(docs/PolytopiaMapGeneration.md 7.5절 "중앙 대륙 + 외곽 바다")가 실제로
        /// 그 모양을 만드는지 확인한다 — (1) 목표 물 비율에 가깝게 맞는지, (2) 물 타일끼리 실제로 서로
        /// 인접할 수 있는지(기존 MinDistance 제약 때문에 판게아 이전엔 불가능했던 부분 — 이게 이번
        /// 수정의 핵심), (3) 중앙이 가장자리보다 육지 비율이 확실히 높은지(방사형 중앙 집중 확인),
        /// (4) 같은 시드면 같은 결과가 나오는지.</summary>
        private static bool VerifyPangeaShape()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var grid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed: 4242, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Pangea, targetWaterFraction: 0.5f);

            bool ok = true;

            // (1) 목표 물 비율(0.5)에 근접하는지 — 반올림/바이옴별 Water 타일 유무 오차를 감안해 ±0.1 허용.
            int waterCount = 0, totalCount = grid.Width * grid.Height;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetTerrain(new Vector2Int(x, y)) == TerrainType.Water) waterCount++;
            float waterFraction = (float)waterCount / totalCount;
            if (Mathf.Abs(waterFraction - 0.5f) > 0.1f)
            {
                Debug.LogError($"[TerrainGenerationVerification] Pangea water fraction off target: {waterFraction:F2} (목표 0.5 ±0.1)");
                ok = false;
            }

            // (2) 물 타일끼리 실제로 인접 가능한지 — 기존 MinDistance 제약이라면 절대 불가능했던 부분.
            bool foundAdjacentWater = false;
            for (int y = 0; y < grid.Height && !foundAdjacentWater; y++)
                for (int x = 0; x < grid.Width && !foundAdjacentWater; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetTerrain(pos) != TerrainType.Water) continue;
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                        if (grid.GetTerrain(n) == TerrainType.Water) { foundAdjacentWater = true; break; }
                }
            if (!foundAdjacentWater)
            {
                Debug.LogError("[TerrainGenerationVerification] Pangea: no two Water tiles are adjacent — 바다가 하나로 안 이어짐(MinDistance가 여전히 걸리고 있을 가능성)");
                ok = false;
            }

            // (3) 중앙이 가장자리(네 모서리)보다 육지 비율이 뚜렷하게 높은지.
            float centerLandFraction = SampleLandFraction(grid, grid.Width / 2, grid.Height / 2, radius: 3);
            float cornerLandFraction = (SampleLandFraction(grid, 0, 0, 2) + SampleLandFraction(grid, grid.Width - 1, 0, 2) +
                SampleLandFraction(grid, 0, grid.Height - 1, 2) + SampleLandFraction(grid, grid.Width - 1, grid.Height - 1, 2)) / 4f;
            if (centerLandFraction <= cornerLandFraction)
            {
                Debug.LogError($"[TerrainGenerationVerification] Pangea center-vs-corner land fraction not concentrated: center={centerLandFraction:F2} corners={cornerLandFraction:F2}");
                ok = false;
            }

            // (4) 시드 결정론.
            var gridB = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(gridB, biomes, seed: 4242, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Pangea, targetWaterFraction: 0.5f);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetTerrain(pos) != gridB.GetTerrain(pos) || grid.GetTileType(pos) != gridB.GetTileType(pos))
                    {
                        Debug.LogError($"[TerrainGenerationVerification] Pangea seed determinism mismatch at {pos}");
                        ok = false;
                    }
                }

            if (ok) Debug.Log($"[TerrainGenerationVerification] Pangea shape PASS (water={waterFraction:F2}, center land={centerLandFraction:F2}, corner land={cornerLandFraction:F2})");
            return ok;
        }

        /// <summary>Pangea 랜드마스 마스크 기법을 일반화한 나머지 4종(Lakes/Continents/Archipelago/
        /// Waterworld — Drylands는 마스크가 필요 없어 제외)이 각자 목표 물 비율에 근접하면서 그 타입
        /// 고유의 모양 특징도 나타내는지 확인한다. BattleController.WetnessPresets의 대표 습도값을
        /// 그대로 목표치로 쓴다.</summary>
        private static bool VerifyOtherMapShapes()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            bool ok = true;

            ok &= VerifyMapShapeWaterFraction(biomes, TerrainGenerationSystem.MapShapeMode.Lakes, targetWaterFraction: 0.275f, tolerance: 0.15f, seed: 111);
            ok &= VerifyMapShapeWaterFraction(biomes, TerrainGenerationSystem.MapShapeMode.Continents, targetWaterFraction: 0.55f, tolerance: 0.15f, seed: 222);
            ok &= VerifyMapShapeWaterFraction(biomes, TerrainGenerationSystem.MapShapeMode.Archipelago, targetWaterFraction: 0.70f, tolerance: 0.15f, seed: 333);
            ok &= VerifyMapShapeWaterFraction(biomes, TerrainGenerationSystem.MapShapeMode.Waterworld, targetWaterFraction: 0.95f, tolerance: 0.15f, seed: 444);

            // Lakes: 가장자리 육지 다리(ForceBorderLand)가 실제로 걸리는지 — 맨 바깥 테두리 칸 대부분이 육지여야 함.
            {
                var grid = new GridWorld(20, 20, 1f);
                TerrainGenerationSystem.Generate(grid, biomes, seed: 111, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Lakes, targetWaterFraction: 0.275f);
                int borderLand = 0, borderTotal = 0;
                for (int x = 0; x < grid.Width; x++)
                {
                    borderTotal += 2;
                    if (grid.GetTerrain(new Vector2Int(x, 0)) == TerrainType.Land) borderLand++;
                    if (grid.GetTerrain(new Vector2Int(x, grid.Height - 1)) == TerrainType.Land) borderLand++;
                }
                for (int y = 1; y < grid.Height - 1; y++)
                {
                    borderTotal += 2;
                    if (grid.GetTerrain(new Vector2Int(0, y)) == TerrainType.Land) borderLand++;
                    if (grid.GetTerrain(new Vector2Int(grid.Width - 1, y)) == TerrainType.Land) borderLand++;
                }
                float borderLandFraction = (float)borderLand / borderTotal;
                if (borderLandFraction < 0.9f)
                {
                    Debug.LogError($"[TerrainGenerationVerification] Lakes border-land fraction too low: {borderLandFraction:F2} (ForceBorderLand이 안 먹히는 듯)");
                    ok = false;
                }
                else
                {
                    Debug.Log($"[TerrainGenerationVerification] Lakes border-land PASS (border land={borderLandFraction:F2})");
                }
            }

            // Waterworld: 거의 전부 물인 상황에서도 바이옴 앵커(수도 자리)는 항상 육지에 있어야 함(ProtectAnchors).
            {
                var grid = new GridWorld(20, 20, 1f);
                var anchors = TerrainGenerationSystem.Generate(grid, biomes, seed: 444, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Waterworld, targetWaterFraction: 0.95f);
                bool allAnchorsOnLand = true;
                foreach (var a in anchors)
                    if (grid.GetTerrain(a) != TerrainType.Land) allAnchorsOnLand = false;
                if (!allAnchorsOnLand)
                {
                    Debug.LogError("[TerrainGenerationVerification] Waterworld: some biome anchors landed on Water (ProtectAnchors/SnapAnchorsToLand 실패)");
                    ok = false;
                }
                else
                {
                    Debug.Log("[TerrainGenerationVerification] Waterworld anchors-on-land PASS");
                }
            }

            // Archipelago: 노이즈 비중이 높아 Continents보다 육지가 더 잘게 쪼개져야 함(연결된 육지 덩어리 개수 비교).
            {
                var continentsGrid = new GridWorld(20, 20, 1f);
                TerrainGenerationSystem.Generate(continentsGrid, biomes, seed: 555, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Continents, targetWaterFraction: 0.55f);
                var archipelagoGrid = new GridWorld(20, 20, 1f);
                TerrainGenerationSystem.Generate(archipelagoGrid, biomes, seed: 555, wetnessMultiplier: 1f, shapeMode: TerrainGenerationSystem.MapShapeMode.Archipelago, targetWaterFraction: 0.70f);

                int continentsComponents = CountConnectedLandComponents(continentsGrid);
                int archipelagoComponents = CountConnectedLandComponents(archipelagoGrid);
                if (archipelagoComponents <= continentsComponents)
                {
                    Debug.LogError($"[TerrainGenerationVerification] Archipelago not more fragmented than Continents: archipelago={archipelagoComponents} continents={continentsComponents}");
                    ok = false;
                }
                else
                {
                    Debug.Log($"[TerrainGenerationVerification] Archipelago fragmentation PASS (archipelago components={archipelagoComponents}, continents components={continentsComponents})");
                }
            }

            return ok;
        }

        /// <summary>주어진 MapShapeMode로 생성해서 물 비율이 목표치 ±tolerance 안에 들고, 물 타일끼리
        /// 실제로 인접 가능한지(target이 0에 가깝지 않은 한) 확인하는 공통 검사.</summary>
        private static bool VerifyMapShapeWaterFraction(IReadOnlyList<BiomeCsvRow> biomes, TerrainGenerationSystem.MapShapeMode mode, float targetWaterFraction, float tolerance, int seed)
        {
            var grid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed: seed, wetnessMultiplier: 1f, shapeMode: mode, targetWaterFraction: targetWaterFraction);

            int waterCount = 0, totalCount = grid.Width * grid.Height;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetTerrain(new Vector2Int(x, y)) == TerrainType.Water) waterCount++;
            float waterFraction = (float)waterCount / totalCount;

            bool ok = true;
            if (Mathf.Abs(waterFraction - targetWaterFraction) > tolerance)
            {
                Debug.LogError($"[TerrainGenerationVerification] {mode} water fraction off target: {waterFraction:F2} (목표 {targetWaterFraction:F2} ±{tolerance:F2})");
                ok = false;
            }
            else
            {
                Debug.Log($"[TerrainGenerationVerification] {mode} water fraction PASS ({waterFraction:F2}, 목표 {targetWaterFraction:F2})");
            }
            return ok;
        }

        /// <summary>4방향 flood-fill로 연결된 육지(Land) 덩어리 개수를 센다 — Continents/Archipelago의
        /// 파편화 정도를 비교하는 데 쓴다.</summary>
        private static int CountConnectedLandComponents(GridWorld grid)
        {
            var visited = new bool[grid.Width * grid.Height];
            int components = 0;
            var stack = new Stack<Vector2Int>();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    int startIndex = grid.Index(start);
                    if (visited[startIndex] || grid.GetTerrain(start) != TerrainType.Land) continue;

                    components++;
                    stack.Push(start);
                    visited[startIndex] = true;
                    while (stack.Count > 0)
                    {
                        var cur = stack.Pop();
                        foreach (var n in grid.GetNeighbors(cur, allowDiagonal: false))
                        {
                            int nIndex = grid.Index(n);
                            if (visited[nIndex] || grid.GetTerrain(n) != TerrainType.Land) continue;
                            visited[nIndex] = true;
                            stack.Push(n);
                        }
                    }
                }
            }
            return components;
        }

        private static float SampleLandFraction(GridWorld grid, int cx, int cy, int radius)
        {
            int land = 0, total = 0;
            for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(grid.Height - 1, cy + radius); y++)
                for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(grid.Width - 1, cx + radius); x++)
                {
                    total++;
                    if (grid.GetTerrain(new Vector2Int(x, y)) == TerrainType.Land) land++;
                }
            return total > 0 ? (float)land / total : 0f;
        }

        /// <summary>7차 재정비 — docs/PolytopiaMapGeneration.md 7.1절 매트릭스대로, Suburb/Pre-terrain
        /// 마을이 맵 타입마다 있을 수도/없을 수도 있는지 확인한다. Drylands(Freeform)/Pangea/Continents는
        /// 둘 다 없어야 하고, Lakes/Archipelago/Waterworld는 Pre-terrain이 있어야 하며(Suburb는
        /// Waterworld만 없음), 있는 경우 서로/수도로부터 최소 거리 2, 가장자리 여백 1을 지켜야 한다.</summary>
        private static bool VerifyPreTerrainVillagePlanning()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            bool ok = true;

            ok &= VerifyNoPreTerrainVillages(biomes, TerrainGenerationSystem.MapShapeMode.Freeform, 0.05f, seed: 601);
            // Pangea/Continents는 Suburb/Pre-terrain이 없고, 대신 본토 마을이 사전 확정 마을로 반환된다(7.5절).
            ok &= VerifyMainlandVillages(biomes, TerrainGenerationSystem.MapShapeMode.Pangea, 0.5f, seed: 602);
            ok &= VerifyMainlandVillages(biomes, TerrainGenerationSystem.MapShapeMode.Continents, 0.55f, seed: 603);

            ok &= VerifyHasPreTerrainVillages(biomes, TerrainGenerationSystem.MapShapeMode.Lakes, 0.275f, seed: 604, expectSuburbs: true);
            ok &= VerifyHasPreTerrainVillages(biomes, TerrainGenerationSystem.MapShapeMode.Archipelago, 0.70f, seed: 605, expectSuburbs: true);
            ok &= VerifyHasPreTerrainVillages(biomes, TerrainGenerationSystem.MapShapeMode.Waterworld, 0.95f, seed: 606, expectSuburbs: false);

            return ok;
        }

        private static bool VerifyNoPreTerrainVillages(IReadOnlyList<BiomeCsvRow> biomes, TerrainGenerationSystem.MapShapeMode mode, float targetWaterFraction, int seed)
        {
            var grid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed, wetnessMultiplier: 1f, shapeMode: mode, targetWaterFraction: targetWaterFraction,
                out var suburbs, out var preTerrain);

            bool ok = suburbs.Length == 0 && preTerrain.Length == 0;
            if (!ok)
                Debug.LogError($"[TerrainGenerationVerification] {mode} should have no Suburb/Pre-terrain villages, got suburbs={suburbs.Length} preTerrain={preTerrain.Length}");
            else
                Debug.Log($"[TerrainGenerationVerification] {mode} pre-terrain village absence PASS");
            return ok;
        }

        /// <summary>Pangea/Continents 본토 마을(원문 7.5절) — Suburb 없음, 본토 마을이 존재하고 수도와 서로 모두
        /// Post-terrain 간격(3) 이상, 전부 육지 위.</summary>
        private static bool VerifyMainlandVillages(IReadOnlyList<BiomeCsvRow> biomes, TerrainGenerationSystem.MapShapeMode mode, float targetWaterFraction, int seed)
        {
            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, biomes, seed, wetnessMultiplier: 1f, shapeMode: mode, targetWaterFraction: targetWaterFraction,
                out var suburbs, out var mainland);

            bool ok = true;
            if (suburbs.Length != 0) { Debug.LogError($"[TerrainGenerationVerification] {mode} should have no suburbs, got {suburbs.Length}"); ok = false; }
            if (mainland.Length == 0) { Debug.LogError($"[TerrainGenerationVerification] {mode} produced no mainland villages"); ok = false; }
            var all = new List<Vector2Int>(anchors);
            all.AddRange(mainland);
            for (int i = 0; i < all.Count; i++)
            {
                if (grid.GetTerrain(all[i]) != TerrainType.Land) { Debug.LogError($"[TerrainGenerationVerification] {mode} city {all[i]} not on land"); ok = false; }
                for (int j = i + 1; j < all.Count; j++)
                    if (ProceduralGenerationUtil.ChebyshevDistance(all[i], all[j]) < TerrainGenerationSystem.PostTerrainCityMinDistance)
                    { Debug.LogError($"[TerrainGenerationVerification] {mode} cities {all[i]} and {all[j]} closer than {TerrainGenerationSystem.PostTerrainCityMinDistance}"); ok = false; }
            }
            if (ok) Debug.Log($"[TerrainGenerationVerification] {mode} mainland villages PASS ({mainland.Length} villages, {anchors.Length} capitals)");
            return ok;
        }

        private static bool VerifyHasPreTerrainVillages(IReadOnlyList<BiomeCsvRow> biomes, TerrainGenerationSystem.MapShapeMode mode, float targetWaterFraction, int seed, bool expectSuburbs)
        {
            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, biomes, seed, wetnessMultiplier: 1f, shapeMode: mode, targetWaterFraction: targetWaterFraction,
                out var suburbs, out var preTerrain);

            bool ok = true;
            if (expectSuburbs && suburbs.Length > anchors.Length * 2)
            {
                Debug.LogError($"[TerrainGenerationVerification] {mode} suburb count {suburbs.Length} exceeds max(2 per capital, {anchors.Length} capitals)");
                ok = false;
            }
            if (!expectSuburbs && suburbs.Length != 0)
            {
                Debug.LogError($"[TerrainGenerationVerification] {mode} should have no suburbs, got {suburbs.Length}");
                ok = false;
            }

            var allReserved = new List<Vector2Int>(anchors);
            allReserved.AddRange(suburbs);
            allReserved.AddRange(preTerrain);

            foreach (var pos in preTerrain)
            {
                if (ProceduralGenerationUtil.DistanceToEdge(grid, pos) < 1)
                {
                    Debug.LogError($"[TerrainGenerationVerification] {mode} pre-terrain village {pos} violates edge margin(1)");
                    ok = false;
                }
                foreach (var other in allReserved)
                {
                    if (other == pos) continue;
                    if (ProceduralGenerationUtil.ChebyshevDistance(other, pos) < 2)
                    {
                        Debug.LogError($"[TerrainGenerationVerification] {mode} pre-terrain village {pos} too close to {other}(min distance 2)");
                        ok = false;
                    }
                }
            }

            if (ok) Debug.Log($"[TerrainGenerationVerification] {mode} pre-terrain village planning PASS (suburbs={suburbs.Length}, preTerrain={preTerrain.Length})");
            return ok;
        }

        /// <summary>7차 재정비 핵심 — 수도/Suburb/Pre-terrain 마을 위치는 어떤 맵 타입이든 항상 육지여야
        /// 한다(마스크 경로는 GenerateMapShapeLandMask의 보호 보너스로, Freeform은 ForceLandAt으로 보장).</summary>
        private static bool VerifyGuaranteedLandAtReservedPositions()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var modes = new[]
            {
                (TerrainGenerationSystem.MapShapeMode.Freeform, 0.05f),
                (TerrainGenerationSystem.MapShapeMode.Pangea, 0.5f),
                (TerrainGenerationSystem.MapShapeMode.Lakes, 0.275f),
                (TerrainGenerationSystem.MapShapeMode.Continents, 0.55f),
                (TerrainGenerationSystem.MapShapeMode.Archipelago, 0.70f),
                (TerrainGenerationSystem.MapShapeMode.Waterworld, 0.95f),
            };

            bool ok = true;
            int seed = 701;
            foreach (var (mode, targetWaterFraction) in modes)
            {
                var grid = new GridWorld(20, 20, 1f);
                var anchors = TerrainGenerationSystem.Generate(grid, biomes, seed++, wetnessMultiplier: 1f, shapeMode: mode, targetWaterFraction: targetWaterFraction,
                    out var suburbs, out var preTerrain);

                foreach (var pos in anchors)
                    if (grid.GetTerrain(pos) != TerrainType.Land) { Debug.LogError($"[TerrainGenerationVerification] {mode}: capital anchor {pos} is not Land"); ok = false; }
                foreach (var pos in suburbs)
                    if (grid.GetTerrain(pos) != TerrainType.Land) { Debug.LogError($"[TerrainGenerationVerification] {mode}: suburb {pos} is not Land"); ok = false; }
                foreach (var pos in preTerrain)
                    if (grid.GetTerrain(pos) != TerrainType.Land) { Debug.LogError($"[TerrainGenerationVerification] {mode}: pre-terrain village {pos} is not Land"); ok = false; }
            }

            if (ok) Debug.Log("[TerrainGenerationVerification] guaranteed land at reserved positions PASS (all 6 modes)");
            return ok;
        }

        private static readonly (TerrainGenerationSystem.MapShapeMode Mode, float Water)[] AllModes =
        {
            (TerrainGenerationSystem.MapShapeMode.Freeform, 0.05f),
            (TerrainGenerationSystem.MapShapeMode.Pangea, 0.5f),
            (TerrainGenerationSystem.MapShapeMode.Lakes, 0.275f),
            (TerrainGenerationSystem.MapShapeMode.Continents, 0.55f),
            (TerrainGenerationSystem.MapShapeMode.Archipelago, 0.70f),
            (TerrainGenerationSystem.MapShapeMode.Waterworld, 0.95f),
        };

        private static readonly int[] MapSizes = { 11, 14, 16, 18, 20, 30 };

        /// <summary>sample_biomes.csv를 count개가 될 때까지 복제한다(Id만 바꿔서) — 수도 4/9개 케이스 검증용.</summary>
        private static List<BiomeCsvRow> SampleBiomesOfCount(int count)
        {
            var sample = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var result = new List<BiomeCsvRow>();
            for (int i = 0; i < count; i++)
            {
                var src = BiomeCsvSerializer.Parse(BiomeCsvSerializer.Write(new[] { sample[i % sample.Count] }))[0];
                src.Id = src.Id + "_" + i;
                result.Add(src);
            }
            return result;
        }

        /// <summary>8차 재정비(docs/PolytopiaMapGeneration.md 12절 보강 1~3) — 모든 맵 타입/크기/바이옴 수에서
        /// (1) 수도가 가장자리로부터 2칸 이상, (2) 수도가 9칸 이상 육지 덩어리 위(쿼드런트 맵은 3x3 전체가 육지),
        /// (3) 수도끼리 충분히 떨어져 있는지(쿼드런트 맵: 구역 한 변의 절반 이상, Pangea/Continents: 육지 면적/수도 수의 제곱근 x 0.9 이상).</summary>
        private static bool VerifyCapitalPlacementRules()
        {
            bool ok = true;
            int checkedMaps = 0;
            int seed = 801;
            foreach (int biomeCount in new[] { 2, 3, 4, 9 })
            {
                var biomes = SampleBiomesOfCount(biomeCount);
                foreach (var (mode, water) in AllModes)
                {
                    foreach (int size in MapSizes)
                    {
                        if (biomeCount == 9 && size < 16) continue; // 9명은 Tiny/Small에 안 맞음(원문: Tiny 최대 9명이지만 여기선 생략)
                        for (int rep = 0; rep < 3; rep++)
                        {
                            var grid = new GridWorld(size, size, 1f);
                            var anchors = TerrainGenerationSystem.Generate(grid, biomes, seed++, 1f, mode, water, out _, out _);
                            checkedMaps++;
                            string tag = $"{mode} {size}x{size} biomes={biomeCount} seed={seed - 1}";
                            bool quadrantMode = mode != TerrainGenerationSystem.MapShapeMode.Pangea && mode != TerrainGenerationSystem.MapShapeMode.Continents;

                            foreach (var a in anchors)
                            {
                                if (ProceduralGenerationUtil.DistanceToEdge(grid, a) < TerrainGenerationSystem.CapitalEdgeMargin)
                                { Debug.LogError($"[TerrainGenerationVerification] {tag}: capital {a} too close to map edge"); ok = false; }

                                int landmass = LandComponentSize(grid, a);
                                if (landmass < TerrainGenerationSystem.MinCapitalLandmassSize)
                                { Debug.LogError($"[TerrainGenerationVerification] {tag}: capital {a} on tiny landmass ({landmass} tiles)"); ok = false; }

                                if (quadrantMode)
                                    for (int dy = -1; dy <= 1; dy++)
                                        for (int dx = -1; dx <= 1; dx++)
                                        {
                                            var p = a + new Vector2Int(dx, dy);
                                            if (grid.InBounds(p) && grid.GetTerrain(p) != TerrainType.Land)
                                            { Debug.LogError($"[TerrainGenerationVerification] {tag}: capital {a} 3x3 has water at {p}"); ok = false; }
                                        }
                            }

                            int perSide = biomeCount <= 4 ? 2 : 3;
                            // Pangea/Continents는 땅(약 절반)에만 수도를 놓으므로, 땅 면적을 수도 수로 나눈 "1인당 정사각형"
                            // 한 변의 90%를 기대 간격으로 삼는다(9명 + 물 절반이면 물리적으로 맵 한 변의 1/4도 안 됨).
                            int landCount = 0;
                            for (int y = 0; y < size; y++)
                                for (int x = 0; x < size; x++)
                                    if (grid.GetTerrain(new Vector2Int(x, y)) == TerrainType.Land) landCount++;
                            int minExpected = quadrantMode
                                ? Mathf.Max(3, (size / perSide) / 2)
                                : Mathf.Max(3, Mathf.FloorToInt(Mathf.Sqrt(landCount / (float)anchors.Length) * 0.9f));
                            for (int i = 0; i < anchors.Length; i++)
                                for (int j = i + 1; j < anchors.Length; j++)
                                {
                                    int d = ProceduralGenerationUtil.ChebyshevDistance(anchors[i], anchors[j]);
                                    if (d < minExpected)
                                    { Debug.LogError($"[TerrainGenerationVerification] {tag}: capitals {anchors[i]} and {anchors[j]} only {d} apart (expected >= {minExpected})"); ok = false; }
                                }
                        }
                    }
                }
            }

            if (ok) Debug.Log($"[TerrainGenerationVerification] capital placement rules PASS ({checkedMaps} maps: edge margin, landmass, spacing)");
            return ok;
        }

        private static int LandComponentSize(GridWorld grid, Vector2Int start)
        {
            if (grid.GetTerrain(start) != TerrainType.Land) return 0;
            var seen = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var n in grid.GetNeighbors(p, allowDiagonal: false))
                    if (grid.GetTerrain(n) == TerrainType.Land && seen.Add(n)) stack.Push(n);
            }
            return seen.Count;
        }

        /// <summary>8차 재정비(12절 보강 4) — 지형+구조물까지 전부 생성한 뒤, 모든 수도/마을(Suburb/Pre-terrain/
        /// Post-terrain/외딴 섬 포함)이 서로 체비쇼프 거리 2 이상(인접 금지)인지 확인한다.</summary>
        private static bool VerifyCitiesNeverAdjacent()
        {
            bool ok = true;
            int seed = 901;
            int totalCities = 0;
            foreach (int biomeCount in new[] { 3, 4 })
            {
                var biomes = SampleBiomesOfCount(biomeCount);
                foreach (var (mode, water) in AllModes)
                    foreach (int size in MapSizes)
                        for (int rep = 0; rep < 2; rep++)
                        {
                            var grid = new GridWorld(size, size, 1f);
                            int s = seed++;
                            var anchors = TerrainGenerationSystem.Generate(grid, biomes, s, 1f, mode, water, out var suburbs, out var preTerrain);
                            StructureGenerationSystem.Generate(grid, biomes, anchors, s, suburbs, preTerrain, mode);

                            var cities = new List<Vector2Int>();
                            for (int y = 0; y < grid.Height; y++)
                                for (int x = 0; x < grid.Width; x++)
                                {
                                    var id = grid.GetStructure(new Vector2Int(x, y));
                                    if (id == StructureGenerationSystem.CapitalStructureId || id == StructureGenerationSystem.VillageStructureId)
                                        cities.Add(new Vector2Int(x, y));
                                }
                            totalCities += cities.Count;

                            for (int i = 0; i < cities.Count; i++)
                                for (int j = i + 1; j < cities.Count; j++)
                                    if (ProceduralGenerationUtil.ChebyshevDistance(cities[i], cities[j]) < TerrainGenerationSystem.CityMinDistance)
                                    {
                                        Debug.LogError($"[TerrainGenerationVerification] {mode} {size}x{size} seed={s}: cities {cities[i]}({grid.GetStructure(cities[i])}) and {cities[j]}({grid.GetStructure(cities[j])}) are adjacent");
                                        ok = false;
                                    }

                            foreach (var c in cities)
                                if (grid.GetTileType(c) == TerrainGenerationSystem.MountainTileId)
                                { Debug.LogError($"[TerrainGenerationVerification] {mode} {size}x{size} seed={s}: city {c} placed on a Mountain"); ok = false; }
                        }
            }

            if (ok) Debug.Log($"[TerrainGenerationVerification] cities never adjacent PASS ({totalCities} cities checked)");
            return ok;
        }

        /// <summary>숲/산 레이어(12.1절) — 합성 단일 바이옴(평지 하나)으로 산 14%/숲 38% 쿼터가 정확히 지켜지는지,
        /// 배수(1.5/0.5)의 비율 계산이 4절 순서(산 먼저 -> 숲 비례 보정)대로인지, 배수 0이면 레이어가 꺼지는지,
        /// sample_biomes.csv로도 실제로 숲/산이 생기는지 확인한다.</summary>
        private static bool VerifyForestMountainLayer()
        {
            bool ok = true;

            var biome = new BiomeCsvRow
            {
                Id = "Plain", Name = "Plain", Frequency = 0.15f, Octaves = 2, SeedOffset = 7, InnerRadius = 2, MountainRate = 1f, ForestRate = 1f,
                Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } }
            };
            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 42);
            int landCells = grid.Width * grid.Height - anchors.Length; // 전부 육지, 수도 칸만 제외
            int mountains = CountTileType(grid, TerrainGenerationSystem.MountainTileId);
            int forests = CountTileType(grid, TerrainGenerationSystem.ForestTileId);
            int expectedMountains = Mathf.RoundToInt(landCells * 0.14f);
            int expectedForests = Mathf.RoundToInt(landCells * 0.38f);
            if (mountains != expectedMountains || forests != expectedForests)
            {
                Debug.LogError($"[TerrainGenerationVerification] forest/mountain quota mismatch: mountain {mountains}/{expectedMountains}, forest {forests}/{expectedForests}");
                ok = false;
            }
            foreach (var a in anchors)
                if (grid.GetTileType(a) == TerrainGenerationSystem.MountainTileId || grid.GetTileType(a) == TerrainGenerationSystem.ForestTileId)
                { Debug.LogError($"[TerrainGenerationVerification] capital {a} was turned into {grid.GetTileType(a)}"); ok = false; }

            var (m, f) = TerrainGenerationSystem.ComputeFeatureFractions(1.5f, 0.5f);
            float expectedForest = 0.38f * (1f - 0.21f) / 0.86f * 0.5f;
            if (!Mathf.Approximately(m, 0.21f) || Mathf.Abs(f - expectedForest) > 0.0001f)
            { Debug.LogError($"[TerrainGenerationVerification] ComputeFeatureFractions(1.5,0.5) = ({m},{f}), expected (0.21,{expectedForest})"); ok = false; }

            biome.MountainRate = 0f;
            biome.ForestRate = 0f;
            var offGrid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(offGrid, new List<BiomeCsvRow> { biome }, seed: 42);
            if (CountTileType(offGrid, TerrainGenerationSystem.MountainTileId) + CountTileType(offGrid, TerrainGenerationSystem.ForestTileId) != 0)
            { Debug.LogError("[TerrainGenerationVerification] MountainRate/ForestRate=0 should disable the forest/mountain layer"); ok = false; }

            var sampleGrid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(sampleGrid, BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath)), seed: 43);
            int sampleMountains = CountTileType(sampleGrid, TerrainGenerationSystem.MountainTileId);
            int sampleForests = CountTileType(sampleGrid, TerrainGenerationSystem.ForestTileId);
            if (sampleMountains == 0 || sampleForests == 0)
            { Debug.LogError($"[TerrainGenerationVerification] sample_biomes.csv produced no forest/mountain (mountain={sampleMountains}, forest={sampleForests})"); ok = false; }

            if (ok) Debug.Log($"[TerrainGenerationVerification] forest/mountain layer PASS (quota mountain={mountains}, forest={forests} of {landCells}; sample mountain={sampleMountains}, forest={sampleForests})");
            return ok;
        }

        /// <summary>9차 재정비 — 모든 맵 타입에서 물 칸은 8방향에 육지가 있으면 얕은 물(바이옴 물 타일), 없으면
        /// 깊은 바다(Ocean)여야 한다(구조물 단계가 지형을 바꾼 뒤까지 포함).</summary>
        private static bool VerifyWaterDepthClassification()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            bool ok = true;
            int seed = 1001, ocean = 0, shallow = 0;
            foreach (var (mode, water) in AllModes)
            {
                var grid = new GridWorld(20, 20, 1f);
                int s = seed++;
                var anchors = TerrainGenerationSystem.Generate(grid, biomes, s, 1f, mode, water, out var suburbs, out var planned);
                StructureGenerationSystem.Generate(grid, biomes, anchors, s, suburbs, planned, mode);
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var pos = new Vector2Int(x, y);
                        if (grid.GetTerrain(pos) != TerrainType.Water) continue;
                        bool nearLand = false;
                        foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                            if (grid.GetTerrain(n) == TerrainType.Land) nearLand = true;
                        bool isOcean = grid.GetTileType(pos) == TerrainGenerationSystem.OceanTileId;
                        if (isOcean) ocean++; else shallow++;
                        if (isOcean == nearLand)
                        { Debug.LogError($"[TerrainGenerationVerification] {mode} seed={s}: water {pos} tile={grid.GetTileType(pos)} but nearLand={nearLand}"); ok = false; }
                    }
            }
            if (ocean == 0 || shallow == 0) { Debug.LogError($"[TerrainGenerationVerification] water depth: expected both kinds (ocean={ocean}, shallow={shallow})"); ok = false; }
            if (ok) Debug.Log($"[TerrainGenerationVerification] water depth classification PASS (shallow={shallow}, ocean={ocean})");
            return ok;
        }

        /// <summary>Continents(원문 7.5절) — 대륙끼리 대각선으로도 맞닿지 않고(8방향 덩어리 수 = 4방향 덩어리 수),
        /// 대륙 하나가 200칸을 넘지 않으며, 구조물까지 생성한 뒤 모든 대륙에 도시가 최소 1개 있어야 한다.</summary>
        private static bool VerifyContinentsShape()
        {
            bool ok = true;
            int seed = 1101, maps = 0;
            foreach (int biomeCount in new[] { 2, 4 })
            {
                var biomes = SampleBiomesOfCount(biomeCount);
                foreach (int size in MapSizes)
                    for (int rep = 0; rep < 2; rep++)
                    {
                        var grid = new GridWorld(size, size, 1f);
                        int s = seed++;
                        var anchors = TerrainGenerationSystem.Generate(grid, biomes, s, 1f, TerrainGenerationSystem.MapShapeMode.Continents, 0.55f, out var suburbs, out var planned);
                        StructureGenerationSystem.Generate(grid, biomes, anchors, s, suburbs, planned, TerrainGenerationSystem.MapShapeMode.Continents);
                        maps++;
                        string tag = $"Continents {size}x{size} biomes={biomeCount} seed={s}";

                        var (labels4, sizes4) = LabelComponents(grid, diagonal: false);
                        var (_, sizes8) = LabelComponents(grid, diagonal: true);
                        // 외딴 섬 마을(1칸 섬)은 8방향이 전부 물이라 이 비교에 영향이 없다.
                        if (sizes4.Count != sizes8.Count)
                        { Debug.LogError($"[TerrainGenerationVerification] {tag}: continents touch diagonally ({sizes4.Count} vs {sizes8.Count} components)"); ok = false; }
                        foreach (var continentSize in sizes4)
                            if (continentSize > 200) { Debug.LogError($"[TerrainGenerationVerification] {tag}: continent of {continentSize} tiles (>200)"); ok = false; }

                        var hasCity = new bool[sizes4.Count];
                        for (int y = 0; y < size; y++)
                            for (int x = 0; x < size; x++)
                            {
                                var p = new Vector2Int(x, y);
                                var id = grid.GetStructure(p);
                                if ((id == StructureGenerationSystem.CapitalStructureId || id == StructureGenerationSystem.VillageStructureId) && labels4[grid.Index(p)] >= 0)
                                    hasCity[labels4[grid.Index(p)]] = true;
                            }
                        for (int c = 0; c < hasCity.Length; c++)
                            if (!hasCity[c]) { Debug.LogError($"[TerrainGenerationVerification] {tag}: a continent of {sizes4[c]} tiles has no city"); ok = false; }
                    }
            }
            if (ok) Debug.Log($"[TerrainGenerationVerification] Continents shape PASS ({maps} maps: separated, <=200 tiles, every continent has a city)");
            return ok;
        }

        private static (int[] Labels, List<int> Sizes) LabelComponents(GridWorld grid, bool diagonal)
        {
            var labels = new int[grid.Width * grid.Height];
            for (int i = 0; i < labels.Length; i++) labels[i] = -1;
            var sizes = new List<int>();
            var stack = new Stack<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (grid.GetTerrain(start) != TerrainType.Land || labels[grid.Index(start)] >= 0) continue;
                    int id = sizes.Count, size = 0;
                    labels[grid.Index(start)] = id;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        size++;
                        foreach (var n in grid.GetNeighbors(p, diagonal))
                            if (grid.GetTerrain(n) == TerrainType.Land && labels[grid.Index(n)] < 0) { labels[grid.Index(n)] = id; stack.Push(n); }
                    }
                    sizes.Add(size);
                }
            return (labels, sizes);
        }
    }
}
