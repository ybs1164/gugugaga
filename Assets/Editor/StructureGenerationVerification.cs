using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 타일 위 구조물(수도/마을/자원/등대/유적/불가사리) 생성 파이프라인을 자동으로 검증하는 배치모드 전용
    /// 스크립트. TerrainGenerationVerification과 같은 패턴 — Play 모드 없이 Console 로그의 PASS/FAIL만
    /// 확인하면 된다. 9차 재정비(원문 순서대로 단계별 배치) 반영.
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.StructureGenerationVerification.Run -quit
    /// </summary>
    public static class StructureGenerationVerification
    {
        private const string SampleCsvRelativePath = "docs/sample_biomes.csv";

        public static void Run()
        {
            bool ok = VerifyCapitalsAtAnchors() &
                      VerifyPlacementRules() &
                      VerifyDeterministicSeed() &
                      VerifyResourcesAroundAllCities() &
                      VerifyResourceInnerOuterRates() &
                      VerifyExcludeAdjacentStructures() &
                      VerifyFillRemaining() &
                      VerifyMaxWaterFractionOnLakes() &
                      VerifyRuinCountTable() &
                      VerifyLighthousesAtCorners() &
                      VerifySampleStarfishAndRuinRules() &
                      VerifyTinyIslandVillages() &
                      VerifyTinyIslandVillagesGatedByShapeMode() &
                      VerifyLakesVillageConnections();
            Debug.Log(ok ? "[StructureGenerationVerification] ALL PASS" : "[StructureGenerationVerification] SOME CHECKS FAILED - see errors above");
        }

        private static string ReadCsv(string relativePath) => File.ReadAllText(Path.Combine(Application.dataPath, "..", relativePath));

        private static BiomeCsvRow BuildTestBiome() => new BiomeCsvRow
        {
            Id = "Test", Name = "Test", NoiseType = "Perlin", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 1,
            Tiles = new List<BiomeTileEntry>
            {
                new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 0.7f, OuterWeight = 0.7f },
                new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 0.3f, OuterWeight = 0.3f }
            },
            Structures = new List<BiomeStructureEntry>
            {
                // Ruin 개수는 맵 크기별 고정표(16x16 -> 7)를 쓰고, Starfish는 물 칸 100칸당 1개(+최소 1).
                new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, MinDistance = 3, EdgeMargin = 1 },
                new BiomeStructureEntry { StructureId = "Starfish", AllowedTileTypes = new[] { "Water", TerrainGenerationSystem.OceanTileId }, Weight = 1f, MinCount = 1, CountPerTiles = 100f, MinDistance = 2 }
            }
        };

        private static BiomeCsvRow GrassOnlyBiome(params BiomeStructureEntry[] structures) => new BiomeCsvRow
        {
            Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 1,
            Tiles = new List<BiomeTileEntry> { new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f } },
            Structures = new List<BiomeStructureEntry>(structures)
        };

        private static GridWorld FilledGrid(int size, System.Func<Vector2Int, bool> isWater)
        {
            var grid = new GridWorld(size, size, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var pos = new Vector2Int(x, y);
                    bool water = isWater(pos);
                    grid.SetTerrain(pos, water ? TerrainType.Water : TerrainType.Land);
                    grid.SetTileType(pos, water ? "Water" : "Grass");
                }
            return grid;
        }

        private static List<Vector2Int> Positions(GridWorld grid, string structureId)
        {
            var result = new List<Vector2Int>();
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.GetStructure(new Vector2Int(x, y)) == structureId) result.Add(new Vector2Int(x, y));
            return result;
        }

        private static List<Vector2Int> Cities(GridWorld grid)
        {
            var result = Positions(grid, StructureGenerationSystem.CapitalStructureId);
            result.AddRange(Positions(grid, StructureGenerationSystem.VillageStructureId));
            return result;
        }

        private static int NearestDistance(List<Vector2Int> positions, Vector2Int pos)
        {
            int best = int.MaxValue;
            foreach (var p in positions) best = Mathf.Min(best, ProceduralGenerationUtil.ChebyshevDistance(p, pos));
            return best;
        }

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
        /// EdgeMargin을 지키는지, 최소 하나 이상은 실제로 배치되는지 확인한다. 노이즈 결과에 기대지 않도록
        /// 그리드를 절반씩 Grass/Water로 직접 구성한다(바다 쪽 대부분은 ClassifyWaterDepth로 Ocean이 된다).</summary>
        private static bool VerifyPlacementRules()
        {
            var biome = BuildTestBiome();
            var grid = FilledGrid(16, p => p.x >= 8);
            var anchors = new[] { new Vector2Int(3, 3) };
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
                    if (string.IsNullOrEmpty(structureId) || structureId == StructureGenerationSystem.CapitalStructureId ||
                        structureId == StructureGenerationSystem.LighthouseStructureId) continue;

                    var tileType = grid.GetTileType(pos);
                    if (structureId == "Ruin")
                    {
                        if (tileType != "Grass") { Debug.LogError($"[StructureGenerationVerification] Ruin at {pos} sits on disallowed tile '{tileType}'"); ok = false; }
                        ruinPositions.Add(pos);
                    }
                    else if (structureId == "Starfish")
                    {
                        if (tileType != "Water" && tileType != TerrainGenerationSystem.OceanTileId)
                        { Debug.LogError($"[StructureGenerationVerification] Starfish at {pos} sits on disallowed tile '{tileType}'"); ok = false; }
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
            if (grid.GetTileType(new Vector2Int(14, 8)) != TerrainGenerationSystem.OceanTileId || grid.GetTileType(new Vector2Int(8, 8)) != "Water")
            { Debug.LogError("[StructureGenerationVerification] shallow/deep water classification wrong on the half-water grid"); ok = false; }

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

        /// <summary>같은 시드로 두 번 생성하면 완전히 같은 구조물/지형이 나오는지(결정론적) 확인한다 — 모든 맵 타입.</summary>
        private static bool VerifyDeterministicSeed()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            bool ok = true;
            foreach (TerrainGenerationSystem.MapShapeMode mode in System.Enum.GetValues(typeof(TerrainGenerationSystem.MapShapeMode)))
            {
                var gridA = new GridWorld(16, 16, 1f);
                var gridB = new GridWorld(16, 16, 1f);
                foreach (var grid in new[] { gridA, gridB })
                {
                    var anchors = TerrainGenerationSystem.Generate(grid, biomes, 321, 1f, mode, 0.5f, out var suburbs, out var planned);
                    StructureGenerationSystem.Generate(grid, biomes, anchors, 321, suburbs, planned, mode);
                }

                for (int y = 0; y < gridA.Height; y++)
                    for (int x = 0; x < gridA.Width; x++)
                    {
                        var pos = new Vector2Int(x, y);
                        if (gridA.GetStructure(pos) != gridB.GetStructure(pos) || gridA.GetTileType(pos) != gridB.GetTileType(pos))
                        {
                            Debug.LogError($"[StructureGenerationVerification] {mode} seed determinism mismatch at {pos}");
                            ok = false;
                        }
                    }
            }

            if (ok) Debug.Log("[StructureGenerationVerification] deterministic seed PASS (all modes)");
            return ok;
        }

        /// <summary>원문 3절 "자원은 도시/마을 2칸 이내" — 자원이 수도뿐 아니라 마을 주변에도 생기고, 수도 바로 옆
        /// (Inner)에도 생기며, 어떤 도시로부터도 2칸 밖에는 절대 안 생기는지 확인한다(예전엔 수도 기준 + 수도 인접 금지).</summary>
        private static bool VerifyResourcesAroundAllCities()
        {
            var biome = GrassOnlyBiome(
                new BiomeStructureEntry { StructureId = "Village", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, FillRemaining = true, MinDistance = 3, EdgeMargin = 2 },
                new BiomeStructureEntry { StructureId = "Resource_Fruit", AllowedTileTypes = new[] { "Grass" }, InnerRate = 0.5f, OuterRate = 0.5f });

            var grid = new GridWorld(20, 20, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 5);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 5);

            var cities = Cities(grid);
            var villages = Positions(grid, StructureGenerationSystem.VillageStructureId);
            var fruits = Positions(grid, "Resource_Fruit");
            bool ok = true, nextToVillage = false, nextToCapital = false;
            foreach (var f in fruits)
            {
                int d = NearestDistance(cities, f);
                if (d > StructureGenerationSystem.ResourceCityRadius)
                { Debug.LogError($"[StructureGenerationVerification] Resource_Fruit at {f} is {d} tiles from the nearest city"); ok = false; }
                if (NearestDistance(villages, f) <= 2) nextToVillage = true;
                if (ProceduralGenerationUtil.ChebyshevDistance(f, anchors[0]) == 1) nextToCapital = true;
            }

            if (fruits.Count == 0) { Debug.LogError("[StructureGenerationVerification] no Resource_Fruit was placed at all"); ok = false; }
            if (villages.Count == 0) { Debug.LogError("[StructureGenerationVerification] expected post-terrain villages on an all-grass 20x20 map"); ok = false; }
            if (!nextToVillage) { Debug.LogError("[StructureGenerationVerification] no resource near any village (resources must use every city, not just the capital)"); ok = false; }
            if (!nextToCapital) { Debug.LogError("[StructureGenerationVerification] no resource directly adjacent to the capital (Inner City must allow resources)"); ok = false; }

            if (ok) Debug.Log($"[StructureGenerationVerification] resources around all cities PASS ({fruits.Count} fruit, {villages.Count} villages)");
            return ok;
        }

        /// <summary>InnerRate/OuterRate 쿼터 — Inner 1.0/Outer 0.0이면 도시 인접 칸은 전부 자원, 거리 2 칸은 하나도
        /// 없어야 하고, 두 자원이 같은 타일을 공유하면 CSV 순서대로 쿼터를 나눠 가져야 한다.</summary>
        private static bool VerifyResourceInnerOuterRates()
        {
            var biome = GrassOnlyBiome(
                new BiomeStructureEntry { StructureId = "Resource_Fruit", AllowedTileTypes = new[] { "Grass" }, InnerRate = 0.5f, OuterRate = 0f },
                new BiomeStructureEntry { StructureId = "Resource_Crop", AllowedTileTypes = new[] { "Grass" }, InnerRate = 0.5f, OuterRate = 0f });
            var grid = FilledGrid(12, _ => false);
            var anchors = new[] { new Vector2Int(5, 5) };
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 12);

            bool ok = true;
            int fruit = 0, crop = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    var id = grid.GetStructure(pos);
                    int d = ProceduralGenerationUtil.ChebyshevDistance(pos, anchors[0]);
                    if (id == "Resource_Fruit") fruit++;
                    if (id == "Resource_Crop") crop++;
                    if (d == 1 && id != "Resource_Fruit" && id != "Resource_Crop")
                    { Debug.LogError($"[StructureGenerationVerification] inner cell {pos} left without resource (Inner 0.5+0.5=100%)"); ok = false; }
                    if (d == 2 && (id == "Resource_Fruit" || id == "Resource_Crop"))
                    { Debug.LogError($"[StructureGenerationVerification] outer cell {pos} got {id} with OuterRate=0"); ok = false; }
                }
            // 8칸 x 0.5 = 정확히 4개씩(소수부 없음 -> 확률적 반올림 개입 없음).
            if (fruit != 4 || crop != 4) { Debug.LogError($"[StructureGenerationVerification] inner quota split wrong: fruit={fruit}, crop={crop} (expected 4/4)"); ok = false; }

            if (ok) Debug.Log("[StructureGenerationVerification] resource inner/outer rates PASS (fruit=4, crop=4, outer=0)");
            return ok;
        }

        /// <summary>ExcludeAdjacentStructures가 8방향(대각선 포함)으로 지켜지는지 확인한다.</summary>
        private static bool VerifyExcludeAdjacentStructures()
        {
            var biome = GrassOnlyBiome(
                new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, FillRemaining = true, ExcludeAdjacentStructures = new[] { "Capital" } });

            var grid = new GridWorld(16, 16, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 7);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 7);

            bool ok = true;
            foreach (var pos in Positions(grid, "Ruin"))
                foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                    if (grid.GetStructure(n) == StructureGenerationSystem.CapitalStructureId)
                    { Debug.LogError($"[StructureGenerationVerification] Ruin at {pos} is adjacent (8-dir) to Capital at {n}"); ok = false; }

            if (ok) Debug.Log("[StructureGenerationVerification] ExcludeAdjacentStructures (8-dir) PASS");
            return ok;
        }

        /// <summary>Post-terrain 마을이 포화 상태까지 채워지는지 — 남은 빈 Grass 칸은 전부 가장자리 여백이나 도시 간격
        /// (Post-terrain 3칸, 수도 포함) 때문에 막혀 있어야 한다.</summary>
        private static bool VerifyFillRemaining()
        {
            var biome = GrassOnlyBiome(
                new BiomeStructureEntry { StructureId = "Village", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, FillRemaining = true, MinDistance = 2 });

            var grid = new GridWorld(14, 14, 1f);
            var anchors = TerrainGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, seed: 3);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 3);

            var villages = Positions(grid, StructureGenerationSystem.VillageStructureId);
            var cities = Cities(grid);
            bool ok = true;
            if (villages.Count == 0) { Debug.LogError("[StructureGenerationVerification] FillRemaining placed 0 Village"); ok = false; }

            int blockedEmpty = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!string.IsNullOrEmpty(grid.GetStructure(pos)) && grid.GetStructure(pos) != "Resource_Fruit") continue;
                    if (grid.GetTileType(pos) != "Grass") continue;
                    if (NearestDistance(cities, pos) < TerrainGenerationSystem.PostTerrainCityMinDistance) { blockedEmpty++; continue; }
                    Debug.LogError($"[StructureGenerationVerification] FillRemaining left {pos} empty even though no spacing conflict exists — not saturated");
                    ok = false;
                }
            for (int i = 0; i < cities.Count; i++)
                for (int j = i + 1; j < cities.Count; j++)
                    if (ProceduralGenerationUtil.ChebyshevDistance(cities[i], cities[j]) < TerrainGenerationSystem.PostTerrainCityMinDistance)
                    { Debug.LogError($"[StructureGenerationVerification] post-terrain cities {cities[i]} and {cities[j]} closer than {TerrainGenerationSystem.PostTerrainCityMinDistance}"); ok = false; }

            if (ok) Debug.Log($"[StructureGenerationVerification] FillRemaining PASS ({villages.Count} villages, {blockedEmpty} correctly-blocked cells)");
            return ok;
        }

        /// <summary>MaxWaterFractionOnLakes — Lakes 맵에서는 물 위 유적이 목표 개수의 1/3을 넘지 않아야 한다.</summary>
        private static bool VerifyMaxWaterFractionOnLakes()
        {
            var biome = new BiomeCsvRow
            {
                Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 1,
                Tiles = new List<BiomeTileEntry>
                {
                    new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f },
                    new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 1f, OuterWeight = 1f }
                },
                Structures = new List<BiomeStructureEntry>
                {
                    new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass", "Water", TerrainGenerationSystem.OceanTileId }, Weight = 1f, MinDistance = 2, MaxWaterFractionOnLakes = 0.34f }
                }
            };

            // 물이 대부분인 20x20(유적 목표 11) — 제한이 없으면 물 위 유적이 다수가 된다.
            var grid = FilledGrid(20, p => p.x > 3);
            var anchors = new[] { new Vector2Int(2, 10) };
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 9, shapeMode: TerrainGenerationSystem.MapShapeMode.Lakes);

            int total = 0, onWater = 0;
            foreach (var pos in Positions(grid, "Ruin"))
            {
                total++;
                if (grid.GetTerrain(pos) == TerrainType.Water) onWater++;
            }

            bool ok = true;
            if (total == 0) { Debug.LogError("[StructureGenerationVerification] no Ruin was placed at all"); ok = false; }
            else if (onWater > Mathf.FloorToInt(11 * 0.34f))
            { Debug.LogError($"[StructureGenerationVerification] Lakes water cap violated: {onWater}/{total} ruins on water (max {Mathf.FloorToInt(11 * 0.34f)})"); ok = false; }

            if (ok) Debug.Log($"[StructureGenerationVerification] MaxWaterFractionOnLakes PASS ({onWater}/{total} on water)");
            return ok;
        }

        /// <summary>유적 개수가 원문 9절의 맵 크기별 고정표를 따르는지(공간이 충분한 전부-평지 맵).</summary>
        private static bool VerifyRuinCountTable()
        {
            var biome = GrassOnlyBiome(
                new BiomeStructureEntry { StructureId = "Ruin", AllowedTileTypes = new[] { "Grass" }, Weight = 1f, MinDistance = 2, ExcludeAdjacentStructures = new[] { "Capital", "Village" } });
            bool ok = true;
            foreach (var (size, expected) in new[] { (11, 4), (14, 5), (16, 7), (18, 9), (20, 11), (30, 23) })
            {
                var grid = FilledGrid(size, _ => false);
                StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, new[] { new Vector2Int(size / 2, size / 2) }, seed: size);
                int count = Positions(grid, "Ruin").Count;
                if (count != expected) { Debug.LogError($"[StructureGenerationVerification] {size}x{size}: {count} ruins, expected {expected}"); ok = false; }
            }
            if (ok) Debug.Log("[StructureGenerationVerification] ruin count table PASS (4/5/7/9/11/23)");
            return ok;
        }

        private static bool VerifyLighthousesAtCorners()
        {
            var grid = FilledGrid(14, _ => false);
            StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { GrassOnlyBiome() }, new[] { new Vector2Int(7, 7) }, seed: 1);
            bool ok = Positions(grid, StructureGenerationSystem.LighthouseStructureId).Count == 4;
            foreach (var c in new[] { new Vector2Int(0, 0), new Vector2Int(13, 0), new Vector2Int(0, 13), new Vector2Int(13, 13) })
                if (grid.GetStructure(c) != StructureGenerationSystem.LighthouseStructureId) ok = false;
            if (!ok) Debug.LogError("[StructureGenerationVerification] lighthouses must sit on exactly the 4 map corners");
            else Debug.Log("[StructureGenerationVerification] lighthouses at corners PASS");
            return ok;
        }

        /// <summary>sample_biomes.csv로 모든 맵 타입을 생성해 원문 9/10절을 확인한다 — 불가사리는 도시/등대/다른
        /// 불가사리와 8방향 인접 불가, 유적은 도시/다른 유적과 인접 불가 + 물 위라면 깊은 바다에만, 자원은 도시 2칸
        /// 이내, 물고기는 얕은 물에만, 등대는 항상 4개.</summary>
        private static bool VerifySampleStarfishAndRuinRules()
        {
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            bool ok = true;
            int seed = 1300, starfish = 0, ruins = 0, resources = 0;
            foreach (TerrainGenerationSystem.MapShapeMode mode in System.Enum.GetValues(typeof(TerrainGenerationSystem.MapShapeMode)))
                foreach (int size in new[] { 14, 20 })
                {
                    var grid = new GridWorld(size, size, 1f);
                    int s = seed++;
                    float water = mode == TerrainGenerationSystem.MapShapeMode.Waterworld ? 0.95f : mode == TerrainGenerationSystem.MapShapeMode.Lakes ? 0.275f : 0.55f;
                    var anchors = TerrainGenerationSystem.Generate(grid, biomes, s, 1f, mode, water, out var suburbs, out var planned);
                    StructureGenerationSystem.Generate(grid, biomes, anchors, s, suburbs, planned, mode);
                    var cities = Cities(grid);
                    string tag = $"{mode} {size}x{size} seed={s}";
                    int lighthouses = Positions(grid, StructureGenerationSystem.LighthouseStructureId).Count;
                    if (lighthouses != 4) { Debug.LogError($"[StructureGenerationVerification] {tag}: {lighthouses} lighthouses (expected 4)"); ok = false; }

                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            var pos = new Vector2Int(x, y);
                            var id = grid.GetStructure(pos);
                            if (id == "Starfish")
                            {
                                starfish++;
                                foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                                {
                                    var nid = grid.GetStructure(n);
                                    if (nid == "Starfish" || nid == "Capital" || nid == "Village" || nid == "Lighthouse")
                                    { Debug.LogError($"[StructureGenerationVerification] {tag}: Starfish {pos} adjacent to {nid} {n}"); ok = false; }
                                }
                            }
                            else if (id == "Ruin")
                            {
                                ruins++;
                                if (grid.GetTerrain(pos) == TerrainType.Water && grid.GetTileType(pos) != TerrainGenerationSystem.OceanTileId)
                                { Debug.LogError($"[StructureGenerationVerification] {tag}: Ruin {pos} on shallow water"); ok = false; }
                                foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                                {
                                    var nid = grid.GetStructure(n);
                                    if (nid == "Ruin" || nid == "Capital" || nid == "Village")
                                    { Debug.LogError($"[StructureGenerationVerification] {tag}: Ruin {pos} adjacent to {nid} {n}"); ok = false; }
                                }
                            }
                            else if (id != null && id.StartsWith("Resource_"))
                            {
                                resources++;
                                if (NearestDistance(cities, pos) > StructureGenerationSystem.ResourceCityRadius)
                                { Debug.LogError($"[StructureGenerationVerification] {tag}: {id} {pos} farther than 2 from every city"); ok = false; }
                                if (id == "Resource_Fish" && grid.GetTileType(pos) != "Water")
                                { Debug.LogError($"[StructureGenerationVerification] {tag}: fish {pos} not on shallow water ({grid.GetTileType(pos)})"); ok = false; }
                            }
                        }
                }

            if (starfish == 0 || ruins == 0 || resources == 0) { Debug.LogError($"[StructureGenerationVerification] sample maps produced starfish={starfish}, ruins={ruins}, resources={resources}"); ok = false; }
            if (ok) Debug.Log($"[StructureGenerationVerification] sample starfish/ruin/resource rules PASS (starfish={starfish}, ruins={ruins}, resources={resources})");
            return ok;
        }

        /// <summary>외딴 섬 마을(8절)이 맵 크기별 개수표대로, 8방향이 전부 물인 칸에 배치되는지 확인한다 —
        /// Continents와 Waterworld(원문 7.1절 매트릭스) 둘 다.</summary>
        private static bool VerifyTinyIslandVillages()
        {
            bool ok = true;
            foreach (var mode in new[] { TerrainGenerationSystem.MapShapeMode.Continents, TerrainGenerationSystem.MapShapeMode.Waterworld })
            {
                var biome = new BiomeCsvRow
                {
                    Id = "Test", Name = "Test", Frequency = 0.15f, Octaves = 2, SeedOffset = 1, InnerRadius = 1,
                    Tiles = new List<BiomeTileEntry>
                    {
                        new BiomeTileEntry { TileId = "Grass", TerrainType = TerrainType.Land, InnerWeight = 1f, OuterWeight = 1f },
                        new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 1f, OuterWeight = 1f }
                    }
                };

                // 14x14 = Small 프리셋 -> 외딴 섬 마을 목표 1개(8절 표).
                var grid = FilledGrid(14, _ => true);
                var anchors = new[] { new Vector2Int(7, 7) };
                StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 1, shapeMode: mode);

                var villages = Positions(grid, StructureGenerationSystem.VillageStructureId);
                foreach (var pos in villages)
                {
                    if (grid.GetTerrain(pos) != TerrainType.Land || grid.GetTileType(pos) != "Grass")
                    { Debug.LogError($"[StructureGenerationVerification] {mode}: tiny island village at {pos} did not convert terrain"); ok = false; }
                    foreach (var n in grid.GetNeighbors(pos, allowDiagonal: true))
                        if (grid.GetTerrain(n) != TerrainType.Water)
                        { Debug.LogError($"[StructureGenerationVerification] {mode}: tiny island village {pos} touches land {n}"); ok = false; }
                }
                if (villages.Count != 1) { Debug.LogError($"[StructureGenerationVerification] {mode}: expected 1 tiny island village on 14x14, got {villages.Count}"); ok = false; }
            }

            if (ok) Debug.Log("[StructureGenerationVerification] tiny island villages PASS (Continents, Waterworld)");
            return ok;
        }

        /// <summary>외딴 섬 마을은 Continents/Pangea/Waterworld 전용 — Freeform/Lakes/Archipelago에서는 0개여야 한다.</summary>
        private static bool VerifyTinyIslandVillagesGatedByShapeMode()
        {
            bool ok = true;
            foreach (var mode in new[] { TerrainGenerationSystem.MapShapeMode.Freeform, TerrainGenerationSystem.MapShapeMode.Lakes, TerrainGenerationSystem.MapShapeMode.Archipelago })
            {
                var grid = FilledGrid(14, _ => true);
                StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { BuildTestBiome() }, new[] { new Vector2Int(7, 7) }, seed: 1, shapeMode: mode);
                int villageCount = Positions(grid, StructureGenerationSystem.VillageStructureId).Count;
                if (villageCount != 0) { Debug.LogError($"[StructureGenerationVerification] tiny island villages should be off for {mode}, but {villageCount} were placed"); ok = false; }
            }
            if (ok) Debug.Log("[StructureGenerationVerification] tiny island villages gating PASS");
            return ok;
        }

        /// <summary>Lakes(원문 2절) — 모든 수도가 4방향 육로로 최소 2개 마을과 이어져 있어야 한다(맵 전체 마을이 2개
        /// 이상일 때). 강제 시나리오: 수도가 물로 완전히 둘러싸인 섬에 있어도 육지 다리가 놓여야 한다.</summary>
        private static bool VerifyLakesVillageConnections()
        {
            bool ok = true;
            var biomes = BiomeCsvSerializer.Parse(ReadCsv(SampleCsvRelativePath));
            int seed = 1400, maps = 0;
            foreach (int size in new[] { 11, 14, 16, 20 })
                for (int rep = 0; rep < 3; rep++)
                {
                    var grid = new GridWorld(size, size, 1f);
                    int s = seed++;
                    var anchors = TerrainGenerationSystem.Generate(grid, biomes, s, 1f, TerrainGenerationSystem.MapShapeMode.Lakes, 0.275f, out var suburbs, out var planned);
                    StructureGenerationSystem.Generate(grid, biomes, anchors, s, suburbs, planned, TerrainGenerationSystem.MapShapeMode.Lakes);
                    maps++;
                    ok &= CheckCapitalsReachTwoVillages(grid, anchors, $"Lakes {size}x{size} seed={s}");
                }

            // 강제 시나리오: 섬(5x5) 위 수도 + 바다 건너 마을 두 개.
            {
                var grid = FilledGrid(16, p => !(p.x >= 1 && p.x <= 5 && p.y >= 1 && p.y <= 5) && !(p.x >= 10 && p.y >= 10));
                var anchors = new[] { new Vector2Int(3, 3) };
                var biome = GrassOnlyBiome();
                biome.Tiles.Add(new BiomeTileEntry { TileId = "Water", TerrainType = TerrainType.Water, InnerWeight = 1f, OuterWeight = 1f });
                StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { biome }, anchors, seed: 3,
                    plannedVillagePositions: new[] { new Vector2Int(11, 11), new Vector2Int(14, 14) }, shapeMode: TerrainGenerationSystem.MapShapeMode.Lakes);
                ok &= CheckCapitalsReachTwoVillages(grid, anchors, "Lakes forced island");
            }

            if (ok) Debug.Log($"[StructureGenerationVerification] Lakes village connections PASS ({maps} generated maps + forced island)");
            return ok;
        }

        private static bool CheckCapitalsReachTwoVillages(GridWorld grid, Vector2Int[] anchors, string tag)
        {
            int totalVillages = Positions(grid, StructureGenerationSystem.VillageStructureId).Count;
            if (totalVillages < 2) return true; // 연결할 마을 자체가 부족하면 규칙 대상 아님
            bool ok = true;
            foreach (var capital in anchors)
            {
                var seen = new HashSet<Vector2Int> { capital };
                var stack = new Stack<Vector2Int>();
                stack.Push(capital);
                int villages = 0;
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    if (grid.GetStructure(p) == StructureGenerationSystem.VillageStructureId) villages++;
                    foreach (var n in grid.GetNeighbors(p, allowDiagonal: false))
                        if (grid.GetTerrain(n) == TerrainType.Land && seen.Add(n)) stack.Push(n);
                }
                if (villages < 2) { Debug.LogError($"[StructureGenerationVerification] {tag}: capital {capital} reaches only {villages} village(s) by land"); ok = false; }
            }
            return ok;
        }
    }
}
