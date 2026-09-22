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
                      VerifyDeterministicSeed() &
                      VerifyMaxDistanceFromAnchor() &
                      VerifyExcludeAdjacentStructures() &
                      VerifyFillRemaining() &
                      VerifyMaxWaterFraction() &
                      VerifyTinyIslandVillages() &
                      VerifyTinyIslandVillagesGatedByShapeMode();
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
                new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, MinCount = 1, CountPerTiles = 100f, MinDistance = 3, EdgeMargin = 1 },
                new BiomeStructureEntry { StructureId = "Starfish", AllowedTileTypes = new[] { "Water" }, Weight = 1f, MinCount = 1, CountPerTiles = 100f, MinDistance = 2 }
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
        /// EdgeMargin을 지키는지, 최소 하나 이상은 실제로 배치되는지 확인한다. TerrainGenerationSystem의
        /// 확률적 노이즈 결과에 기대지 않도록(특정 시드에서 Water 타일이 우연히 거의 안 나오면 테스트가
        /// 흔들릴 수 있음 — VerifyTinyIslandVillages와 같은 이유) 그리드를 절반씩 Grass/Water로 직접
        /// 구성한 뒤 StructureGenerationSystem.Generate만 바로 호출한다.</summary>
        private static bool VerifyPlacementRules()
        {
            var biome = BuildTestBiome();
            var grid = new GridWorld(16, 16, 1f);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    bool isWater = x >= grid.Width / 2;
                    grid.SetTerrain(pos, isWater ? TerrainType.Water : TerrainType.Land);
                    grid.SetTileType(pos, isWater ? "Water" : "Grass");
                }
            var anchors = new[] { new Vector2Int(2, 2) }; // Grass 쪽 구석 — 두 지형 모두에서 충분히 멀어 EdgeMargin/거리 판정에 간섭하지 않음
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
                    // Capital은 앵커 자동 배치, Village는 외딴 섬 마을(8절) 자동 배치 — 둘 다 이 바이옴의
                    // CSV Structures 목록과 무관하게 항상 실행되므로 이 테스트의 관심사가 아니다.
                    if (string.IsNullOrEmpty(structureId) || structureId == StructureGenerationSystem.CapitalStructureId ||
                        structureId == StructureGenerationSystem.VillageStructureId) continue;

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

        /// <summary>MaxDistanceFromAnchor(3절 "자원은 수도 2칸 이내에서만 스폰")를 지정한 엔트리가 실제로
        /// 앵커에서 그 거리 밖으로는 절대 배치되지 않는지 확인한다.</summary>
        private static bool VerifyMaxDistanceFromAnchor()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } },
                Structures = new List<BiomeStructureEntry>
                {
                    new BiomeStructureEntry { StructureId = "Resource_Food", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, CountPerTiles = 3f, MaxDistanceFromAnchor = 2 }
                }
            };

            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 5);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 5);

            bool ok = true;
            int placedCount = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetStructure(pos) != "Resource_Food") continue;
                    placedCount++;
                    int dist = ProceduralGenerationUtil.ChebyshevDistance(pos, anchors[0]);
                    if (dist > 2)
                    {
                        Debug.LogError($"[StructureGenerationVerification] Resource_Food at {pos} is {dist} tiles from anchor {anchors[0]} (MaxDistanceFromAnchor=2)");
                        ok = false;
                    }
                }

            if (placedCount == 0) { Debug.LogError("[StructureGenerationVerification] no Resource_Food was placed at all"); ok = false; }
            if (ok) Debug.Log($"[StructureGenerationVerification] MaxDistanceFromAnchor PASS ({placedCount} placed, all within 2 of anchor)");
            return ok;
        }

        /// <summary>ExcludeAdjacentStructures(9/10절 "다른 유적이나 마을과 바로 인접 불가")를 지정한
        /// Ruin이 실제로 Capital과 바로 인접하지 않는지 확인한다.</summary>
        private static bool VerifyExcludeAdjacentStructures()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } },
                Structures = new List<BiomeStructureEntry>
                {
                    // MinDistance=0(같은 타입끼리는 제약 없음)로 둬서, 인접 배제가 ExcludeAdjacentStructures
                    // 때문인지 MinDistance 때문인지 헷갈리지 않게 한다. FillRemaining으로 최대한 빽빽하게
                    // 채워서(앵커 주변까지) Capital 옆 칸도 후보에 오르게 만든다.
                    new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, FillRemaining = true, ExcludeAdjacentStructures = new[] { "Capital" } }
                }
            };

            var grid = new GridWorld(16, 16, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 7);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 7);

            bool ok = true;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetStructure(pos) != "Ruin") continue;
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: false))
                    {
                        if (grid.GetStructure(n) == StructureGenerationSystem.CapitalStructureId)
                        {
                            Debug.LogError($"[StructureGenerationVerification] Ruin at {pos} is adjacent to Capital at {n} (ExcludeAdjacentStructures violated)");
                            ok = false;
                        }
                    }
                }

            if (ok) Debug.Log("[StructureGenerationVerification] ExcludeAdjacentStructures PASS");
            return ok;
        }

        /// <summary>FillRemaining(7.4절 "더 이상 넣을 자리가 없을 때까지 채움")이 실제로 포화 상태까지
        /// 채우는지 확인한다 — 생성 후 아직 비어있는 모든 자격 있는 칸이 전부 제약(MinDistance) 때문에
        /// 막혀있는 게 맞는지(즉, 더 놓을 수 있는데 안 놓은 칸이 하나도 없는지) 직접 검사한다.</summary>
        private static bool VerifyFillRemaining()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } },
                Structures = new List<BiomeStructureEntry>
                {
                    new BiomeStructureEntry { StructureId = "Village", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, FillRemaining = true, MinDistance = 2 }
                }
            };

            var grid = new GridWorld(14, 14, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 3);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 3);

            var villagePositions = new List<Vector2Int>();
            var emptyGrassCells = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetStructure(pos) == "Village") villagePositions.Add(pos);
                    else if (grid.GetTileType(pos) == "Grass" && string.IsNullOrEmpty(grid.GetStructure(pos))) emptyGrassCells.Add(pos);
                }

            bool ok = true;
            if (villagePositions.Count == 0) { Debug.LogError("[StructureGenerationVerification] FillRemaining placed 0 Village"); ok = false; }

            // 남은 빈 Grass 칸은 전부 "여기 놓으면 MinDistance(2) 위반"이어야 한다 — 아니면 채울 수 있었는데
            // 안 채운 것이므로 포화가 덜 된 것.
            foreach (var empty in emptyGrassCells)
            {
                bool blocked = false;
                foreach (var v in villagePositions)
                    if (ProceduralGenerationUtil.ChebyshevDistance(empty, v) < 2) { blocked = true; break; }
                if (!blocked)
                {
                    Debug.LogError($"[StructureGenerationVerification] FillRemaining left {empty} empty even though no MinDistance conflict exists — not saturated");
                    ok = false;
                }
            }

            if (ok) Debug.Log($"[StructureGenerationVerification] FillRemaining PASS ({villagePositions.Count} placed, {emptyGrassCells.Count} correctly-blocked empty cells)");
            return ok;
        }

        /// <summary>MaxWaterFraction(9절 "Lakes 맵은 유적 최대 1/3만 물 위")이 실제로 물 위 배치 비율을
        /// 제한하는지 확인한다.</summary>
        private static bool VerifyMaxWaterFraction()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 0.5f, OuterWeight = 0.5f },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 0.5f, OuterWeight = 0.5f }
                },
                Structures = new List<BiomeStructureEntry>
                {
                    new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass", "Water" }, Weight = 1f, FillRemaining = true, MinDistance = 2, MaxWaterFraction = 0.34f }
                }
            };

            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 9);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 9);

            int total = 0, onWater = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetStructure(pos) != "Ruin") continue;
                    total++;
                    if (grid.GetTerrain(pos) == TerrainType.Water) onWater++;
                }

            bool ok = true;
            if (total == 0) { Debug.LogError("[StructureGenerationVerification] no Ruin was placed at all"); ok = false; }
            else
            {
                float fraction = (float)onWater / total;
                // 배치 순서에 따른 반올림 오차를 감안해 약간의 여유(+0.1)를 둔다.
                if (fraction > 0.34f + 0.1f)
                {
                    Debug.LogError($"[StructureGenerationVerification] MaxWaterFraction(0.34) violated: {onWater}/{total} = {fraction:F2} on water");
                    ok = false;
                }
            }

            if (ok) Debug.Log($"[StructureGenerationVerification] MaxWaterFraction PASS ({onWater}/{total} on water)");
            return ok;
        }

        /// <summary>외딴 섬 마을(8절)이 맵 크기별 개수표대로 배치되는지 확인한다. TerrainGenerationSystem
        /// 없이 그리드 전체를 직접 물로 채워(전부 "물로만 둘러싸인" 상태) StructureGenerationSystem.Generate를
        /// 바로 호출한다 — Generate는 이미 있는 GridWorld 상태만 보고 동작하므로 이렇게 확정적인 시나리오를
        /// 만들 수 있다.</summary>
        private static bool VerifyTinyIslandVillages()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 1f, OuterWeight = 1f }
                }
            };

            // 14x14 = Small 프리셋 -> 외딴 섬 마을 목표 1개(docs/PolytopiaMapGeneration.md 8절 표).
            var grid = new GridWorld(14, 14, 1f);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    grid.SetTerrain(pos, TerrainType.Water);
                    grid.SetTileType(pos, "Water");
                }
            var anchors = new[] { new Vector2Int(7, 7) };

            // shapeMode 기본값(Freeform)은 외딴 섬 마을을 꺼버리므로(7차 재정비 — Continents/Pangea
            // 전용이어야 함), 여기서는 명시적으로 Continents를 켜서 원래 의도(외딴 섬 마을 배치 검증)를 지킨다.
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 1,
                shapeMode: TerrainGenerationSystem.MapShapeMode.Continents);

            int villageCount = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (grid.GetStructure(pos) != StructureGenerationSystem.VillageStructureId) continue;
                    if (pos == anchors[0]) continue; // 앵커는 Capital이 이미 차지했을 수 있어 제외
                    villageCount++;
                    if (grid.GetTerrain(pos) != TerrainType.Land || grid.GetTileType(pos) != "Grass")
                    {
                        Debug.LogError($"[StructureGenerationVerification] tiny island village at {pos} did not convert terrain correctly (Terrain={grid.GetTerrain(pos)}, TileType={grid.GetTileType(pos)})");
                        return false;
                    }
                }

            bool ok = villageCount == 1;
            if (!ok) Debug.LogError($"[StructureGenerationVerification] expected 1 tiny island village on a 14x14 all-water grid, got {villageCount}");
            else Debug.Log("[StructureGenerationVerification] tiny island villages PASS (1 placed on all-water 14x14 grid)");
            return ok;
        }

        /// <summary>7차 재정비 버그 수정의 핵심 증거 — 외딴 섬 마을(8절)은 Continents/Pangea 전용이어야
        /// 하는데 예전엔 프리셋과 무관하게 항상 실행됐다. VerifyTinyIslandVillages와 동일한 전체-물
        /// 그리드로 shapeMode 기본값(Freeform)을 그대로 써서, 외딴 섬 마을이 단 하나도 배치되지 않는지
        /// 확인한다.</summary>
        private static bool VerifyTinyIslandVillagesGatedByShapeMode()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 2,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 1f, OuterWeight = 1f }
                }
            };

            var grid = new GridWorld(14, 14, 1f);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    grid.SetTerrain(pos, TerrainType.Water);
                    grid.SetTileType(pos, "Water");
                }
            var anchors = new[] { new Vector2Int(7, 7) };

            // shapeMode를 생략 -> 기본값 Freeform -> 외딴 섬 마을 비활성이어야 함.
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 1);

            int villageCount = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetStructure(new Vector2Int(x, y)) == StructureGenerationSystem.VillageStructureId) villageCount++;

            bool ok = villageCount == 0;
            if (!ok) Debug.LogError($"[StructureGenerationVerification] tiny island villages should be gated off for non-Continents/Pangea shapeMode, but {villageCount} were placed");
            else Debug.Log("[StructureGenerationVerification] tiny island villages gating PASS (0 placed with default Freeform shapeMode)");
            return ok;
        }
    }
}
