using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 타일 위 구조물(수도/유적/불가사리/자원) 생성 파이프라인을 자동으로 검증하는 배치모드 전용
    /// 스크립트. TerrainGenerationVerification과 같은 패턴 — Play 모드 없이 Console 로그의 PASS/FAIL만
    /// 확인하면 된다.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.StructureGenerationVerification.Run -quit
    /// </summary>
    public static class StructureGenerationVerification
    {
        public static void Run()
        {
            bool ok = VerifyCapitalsAtAnchors() &
                      VerifyPlacementRules() &
                      VerifyDeterministicSeed();
            Debug.Log(ok ? "[StructureGenerationVerification] ALL PASS" : "[StructureGenerationVerification] SOME CHECKS FAILED - see errors above");
        }

        private static BiomeCsvRow BuildTestBiome() => new BiomeCsvRow
        {
            Id = "Test", Name = "Test", NoiseType = "Perlin", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
            Tiles = new List<BiomeTileEntry>
            {
                new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 0.7f, OuterWeight = 0.7f },
                new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 0.3f, OuterWeight = 0.3f }
            },
            Structures = new List<BiomeStructureEntry>
            {
                // CountPerTiles=100 on a 256타일 단일 바이옴 -> 목표 3개. 넉넉한 여유를 둬서(Grass/Water
                // 타일이 노이즈로 갈리므로 정확히 몇 칸이 나올지는 시드에 달렸다) "최소 1개 이상 배치"만
                // 확인하고, 실제로 배치된 것들의 규칙 준수는 전부 검증한다.
                new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, CountPerTiles = 100f, MinDistance = 3, EdgeMargin = 1 },
                new BiomeStructureEntry { StructureId = "Starfish", AllowedTileTypes = new[] { "Water" }, Weight = 1f, CountPerTiles = 100f, MinDistance = 2 }
            }
        };

        /// <summary>바이옴 앵커마다(유닛 미점유 가정) 정확히 "Capital"이 놓이는지 확인한다.</summary>
        private static bool VerifyCapitalsAtAnchors()
        {
            var biome = BuildTestBiome();
            var grid = new GridWorld(16, 16, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 10);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 10);

            bool ok = true;
            foreach (var anchor in anchors)
            {
                if (grid.GetStructure(anchor) != StructureGenerationSystem.CapitalStructureId)
                {
                    Debug.LogError($"[StructureGenerationVerification] anchor {anchor} does not have Capital (got '{grid.GetStructure(anchor)}')");
                    ok = false;
                }
            }

            if (ok) Debug.Log($"[StructureGenerationVerification] capitals at anchors PASS ({anchors.Length} anchor(s))");
            return ok;
        }

        /// <summary>Ruin/Starfish가 각자 AllowedTileTypes 밖의 타일엔 절대 놓이지 않는지, MinDistance/
        /// EdgeMargin을 지키는지, 최소 하나 이상은 실제로 배치되는지 확인한다.</summary>
        private static bool VerifyPlacementRules()
        {
            var biome = BuildTestBiome();
            var grid = new GridWorld(16, 16, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 10);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 10);

            var ruinPositions = new List<Vector2Int>();
            var starfishPositions = new List<Vector2Int>();
            bool ok = true;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var structureId = grid.GetStructure(pos);
                    if (string.IsNullOrEmpty(structureId) || structureId == StructureGenerationSystem.CapitalStructureId) continue;

                    var tileType = grid.GetTileType(pos);
                    if (structureId == "Ruin")
                    {
                        if (tileType != "Grass")
                        {
                            Debug.LogError($"[StructureGenerationVerification] Ruin at {pos} sits on disallowed tile '{tileType}' (expected Grass)");
                            ok = false;
                        }
                        ruinPositions.Add(pos);
                    }
                    else if (structureId == "Starfish")
                    {
                        if (tileType != "Water")
                        {
                            Debug.LogError($"[StructureGenerationVerification] Starfish at {pos} sits on disallowed tile '{tileType}' (expected Water)");
                            ok = false;
                        }
                        starfishPositions.Add(pos);
                    }
                    else
                    {
                        Debug.LogError($"[StructureGenerationVerification] unexpected StructureId '{structureId}' at {pos}");
                        ok = false;
                    }
                }
            }

            ok &= CheckMinDistanceAndEdge(ruinPositions, grid, minDistance: 3, edgeMargin: 1, label: "Ruin");
            ok &= CheckMinDistanceAndEdge(starfishPositions, grid, minDistance: 2, edgeMargin: 0, label: "Starfish");

            if (ruinPositions.Count == 0) { Debug.LogError("[StructureGenerationVerification] no Ruin was placed at all"); ok = false; }
            if (starfishPositions.Count == 0) { Debug.LogError("[StructureGenerationVerification] no Starfish was placed at all"); ok = false; }

            if (ok) Debug.Log($"[StructureGenerationVerification] placement rules PASS (Ruin={ruinPositions.Count}, Starfish={starfishPositions.Count})");
            return ok;
        }

        private static bool CheckMinDistanceAndEdge(List<Vector2Int> positions, GridWorld grid, int minDistance, int edgeMargin, string label)
        {
            bool ok = true;
            for (int i = 0; i < positions.Count; i++)
            {
                if (edgeMargin > 0 && ProceduralGenerationUtil.DistanceToEdge(grid, positions[i]) < edgeMargin)
                {
                    Debug.LogError($"[StructureGenerationVerification] {label} {positions[i]} violates EdgeMargin({edgeMargin})");
                    ok = false;
                }
                for (int j = i + 1; j < positions.Count; j++)
                {
                    int dist = ProceduralGenerationUtil.ChebyshevDistance(positions[i], positions[j]);
                    if (dist < minDistance)
                    {
                        Debug.LogError($"[StructureGenerationVerification] {label} {positions[i]} and {positions[j]} violate MinDistance({minDistance}): dist={dist}");
                        ok = false;
                    }
                }
            }
            return ok;
        }

        /// <summary>같은 시드로 두 번 생성하면 완전히 같은 구조물 배치가 나오는지(결정론적) 확인한다.</summary>
        private static bool VerifyDeterministicSeed()
        {
            var biome = BuildTestBiome();

            var gridA = new GridWorld(16, 16, 1f);
            var anchorsA = TerrainGenerationSystem.Generate(gridA, new List<BiomeCsvRow> { biome }, seed: 321);
            StructureGenerationSystem.Generate(gridA, new List<BiomeCsvRow> { biome }, anchorsA, seed: 321);

            var gridB = new GridWorld(16, 16, 1f);
            var anchorsB = TerrainGenerationSystem.Generate(gridB, new List<BiomeCsvRow> { biome }, seed: 321);
            StructureGenerationSystem.Generate(gridB, new List<BiomeCsvRow> { biome }, anchorsB, seed: 321);

            bool ok = true;
            for (int y = 0; y < gridA.Height; y++)
                for (int x = 0; x < gridA.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (gridA.GetStructure(pos) != gridB.GetStructure(pos))
                    {
                        Debug.LogError($"[StructureGenerationVerification] seed determinism mismatch at {pos}");
                        ok = false;
                    }
                }

            if (ok) Debug.Log("[StructureGenerationVerification] deterministic seed PASS");
            return ok;
        }
    }
}
