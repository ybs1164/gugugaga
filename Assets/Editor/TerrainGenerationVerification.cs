using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 바이옴 지형 생성 파이프라인(CSV 왕복, 노이즈/가중치/인접배제/최소개수 규칙 준수, 시드 재현성)을
    /// 자동으로 검증하는 배치모드 전용 스크립트. UnitCsvVerification과 같은 패턴 — Play 모드 없이
    /// Console 로그의 PASS/FAIL만 확인하면 된다.
    /// 사용법: unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.TerrainGenerationVerification.Run -quit
    /// </summary>
    public static class TerrainGenerationVerification
    {
        private const string SampleCsvRelativePath = "docs/sample_biomes.csv";

        public static void Run()
        {
            bool ok = VerifyRoundTrip() &
                      VerifySingleBiomeRules() &
                      VerifyDeterministicSeed() &
                      VerifyMultiBiomeSmoke();
            Debug.Log(ok ? "[TerrainGenerationVerification] ALL PASS" : "[TerrainGenerationVerification] SOME CHECKS FAILED - see errors above");
        }

        private static string ReadCsv(string relativePath) => File.ReadAllText(Path.Combine(Application.dataPath, "..", relativePath));

        /// <summary>Parse -> Write -> 재파싱해서 값이 그대로 보존되는지 확인한다(UnitCsvVerification.VerifyRoundTrip과 같은 방식).</summary>
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
                    a.Tiles.Count == b.Tiles.Count;

                if (same)
                {
                    for (int t = 0; t < a.Tiles.Count; t++)
                    {
                        var ta = a.Tiles[t];
                        var tb = b.Tiles[t];
                        bool tileSame = ta.TileId == tb.TileId && ta.TerrainType == tb.TerrainType &&
                            Mathf.Approximately(ta.Weight, tb.Weight) && ta.MinCount == tb.MinCount &&
                            string.Join("|", ta.ExcludeAdjacent) == string.Join("|", tb.ExcludeAdjacent);
                        if (!tileSame) { same = false; break; }
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

        /// <summary>바이옴 1개짜리 그리드(Voronoi 분할이 개입하지 않아 인접 배제 판정이 모호하지 않음)로
        /// MinCount/ExcludeAdjacent가 실제로 지켜지는지 정확히 검증한다.</summary>
        private static bool VerifySingleBiomeRules()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Grassland", Name = "평원", NoiseType = "Perlin", Frequency = 0.15f, Octaves = 3, SeedOffset = 101,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, Weight = 0.6f, MinCount = 0, ExcludeAdjacent = new string[0] },
                    new BiomeTileEntry { TileId = "Forest", TerrainType = TerrainType.Land, Weight = 0.3f, MinCount = 3, ExcludeAdjacent = new[] { "Water" } },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, Weight = 0.1f, MinCount = 2, ExcludeAdjacent = new[] { "Forest" } },
                }
            };

            var grid = new GridWorld(10, 10, 1f);
            TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 42);

            var counts = new Dictionary<string, int>();
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

            if (ok) Debug.Log($"[TerrainGenerationVerification] single-biome rules PASS (Forest={forestCount}, Water={waterCount})");
            return ok;
        }

        /// <summary>같은 시드로 두 번 생성하면 완전히 같은 결과가 나오는지(결정론적) 확인한다.</summary>
        private static bool VerifyDeterministicSeed()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var gridA = new GridWorld(12, 12, 1f);
            var gridB = new GridWorld(12, 12, 1f);
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
        /// 모든 칸이 채워지는지, 각 타일 타입의 전역 등장 수가 선언된 MinCount 합보다 작지 않은지 확인하는
        /// 통합 스모크 테스트(바이옴 경계에서의 교차 인접은 바이옴별로 다른 규칙이라 여기서는 검증하지
        /// 않는다 — 그 부분은 VerifySingleBiomeRules가 모호함 없이 다룬다).</summary>
        private static bool VerifyMultiBiomeSmoke()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            var grid = new GridWorld(20, 20, 1f);
            TerrainGenerationSystem.Generate(grid, biomes, seed: 2024);

            var minCountByTile = new Dictionary<string, int>();
            foreach (var biome in biomes)
                foreach (var entry in biome.Tiles)
                    minCountByTile[entry.TileId] = minCountByTile.TryGetValue(entry.TileId, out var v) ? v + entry.MinCount : entry.MinCount;

            var counts = new Dictionary<string, int>();
            bool ok = true;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var tileType = grid.GetTileType(new Vector2Int(x, y));
                    if (string.IsNullOrEmpty(tileType))
                    {
                        Debug.LogError($"[TerrainGenerationVerification] multi-biome: cell ({x},{y}) left empty");
                        ok = false;
                        continue;
                    }
                    counts[tileType] = counts.TryGetValue(tileType, out var c) ? c + 1 : 1;
                }

            foreach (var kv in minCountByTile)
            {
                int actual = counts.TryGetValue(kv.Key, out var c) ? c : 0;
                if (actual < kv.Value)
                {
                    Debug.LogError($"[TerrainGenerationVerification] multi-biome: tile '{kv.Key}' total MinCount {kv.Value} not met, got {actual}");
                    ok = false;
                }
            }

            if (ok) Debug.Log("[TerrainGenerationVerification] multi-biome smoke PASS");
            return ok;
        }
    }
}
