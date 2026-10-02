using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 배열형 테이블(Assets/Resources/Tables — 기술 해금 내역/기술/기술 그룹/종족/시작 조건)과 습도 탭 2단계 지형 생성(1차 지형 아웃라인 →
    /// 바이옴 채우기), 종족 적용, 수도 주변 시작 조건을 GameObject 없이 검증한다. 다른 Verification과 같은 패턴(PASS/FAIL 로그).
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.ArrayTableVerification.Run
    /// </summary>
    public static class ArrayTableVerification
    {
        private static bool _ok;

        public static void Run()
        {
            _ok = true;
            var errors = GameDataLoader.LoadAll();
            Check(errors.Count == 0, "all tables load without errors/reference warnings: " + string.Join(" | ", errors));
            VerifyTableShapes();
            VerifyRoundTrips();
            VerifyTechLayout();
            VerifyTechReplacementAndBalance();
            VerifyInvalidTechLayout();
            VerifyArrayRulesReported();
            VerifyTribeApply();
            VerifyStartingUnits();
            VerifyStartConditions();
            VerifyOutlineRatios();
            VerifyGenerateFromOutline();
            VerifySandboxHudPrefab();
            Debug.Log(_ok ? "[ArrayTableVerification] ALL PASS" : "[ArrayTableVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[ArrayTableVerification] FAIL: " + message);
        }

        private static List<UnitCsvRow> SandboxUnits() =>
            UnitCsvSerializer.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "..", "SandboxUnits.csv")));

        private static void VerifyTableShapes()
        {
            Check(GameTables.Techs.Length == 25 && GameTables.TechUnlocks.Length == 50, $"25 techs / 50 unlocks (got {GameTables.Techs.Length}/{GameTables.TechUnlocks.Length})");
            Check(GameTables.Tribes.Length == 12 && GameTables.TechGroups.Length == 12, "12 regular tribes, one tech group each");
            Check(GameTables.Tribes.All(t => t.TechGroupIndex == t.Index), "each tribe points at its own group");
            var unitWarnings = ArrayTableValidationSystem.ValidateLoaded(GameDataLoader.LoadDefaultBiomes().Count, SandboxUnits().Count);
            Check(unitWarnings.Count == 0, "tribe start units are in range of SandboxUnits.csv: " + string.Join(" | ", unitWarnings));

            // 위키(Tribes) 시작 기술/별 대조.
            var expected = new Dictionary<string, (string Tech, int Stars)>
            {
                ["XinXi"] = ("Climbing", 7), ["Imperius"] = ("Organization", 5), ["Bardur"] = ("Hunting", 5), ["Oumaji"] = ("Riding", 6),
                ["Kickoo"] = ("Fishing", 5), ["Hoodrick"] = ("Archery", 7), ["Luxidoor"] = (null, 2), ["Vengir"] = ("Smithery", 5),
                ["Zebasi"] = ("Farming", 5), ["AiMo"] = ("Philosophy", 5), ["Quetzali"] = ("Strategy", 7), ["Yadakk"] = ("Roads", 7),
            };
            foreach (var t in GameTables.Tribes)
            {
                var e = expected[t.Id];
                string tech = t.StartTechs.Length > 0 ? GameTables.Techs[t.StartTechs[0]].Id : null;
                Check(tech == e.Tech && t.StartStars == e.Stars, $"{t.Id} start tech/stars matches wiki ({tech}/{t.StartStars})");
            }
        }

        private static void VerifyRoundTrips()
        {
            Check(ArrayTableCsvSerializer.WriteTechUnlocks(ArrayTableCsvSerializer.ParseTechUnlocks(ArrayTableCsvSerializer.WriteTechUnlocks(GameTables.TechUnlocks)))
                  == ArrayTableCsvSerializer.WriteTechUnlocks(GameTables.TechUnlocks), "TechUnlocks round-trip");
            Check(ArrayTableCsvSerializer.WriteTechs(ArrayTableCsvSerializer.ParseTechs(ArrayTableCsvSerializer.WriteTechs(GameTables.Techs)))
                  == ArrayTableCsvSerializer.WriteTechs(GameTables.Techs), "Techs round-trip");
            Check(ArrayTableCsvSerializer.WriteTechSlots(ArrayTableCsvSerializer.ParseTechSlots(ArrayTableCsvSerializer.WriteTechSlots(GameTables.TechSlots)))
                  == ArrayTableCsvSerializer.WriteTechSlots(GameTables.TechSlots), "TechSlots round-trip");
            Check(ArrayTableCsvSerializer.WriteTechTreeLayout(ArrayTableCsvSerializer.ParseTechTreeLayout(ArrayTableCsvSerializer.WriteTechTreeLayout(GameTables.TechTreeLayout)))
                  == ArrayTableCsvSerializer.WriteTechTreeLayout(GameTables.TechTreeLayout), "TechTreeLayout round-trip");
            Check(ArrayTableCsvSerializer.WriteTechGroups(ArrayTableCsvSerializer.ParseTechGroups(ArrayTableCsvSerializer.WriteTechGroups(GameTables.TechGroups)))
                  == ArrayTableCsvSerializer.WriteTechGroups(GameTables.TechGroups), "TechGroups round-trip");
            Check(ArrayTableCsvSerializer.WriteTribes(ArrayTableCsvSerializer.ParseTribes(ArrayTableCsvSerializer.WriteTribes(GameTables.Tribes)))
                  == ArrayTableCsvSerializer.WriteTribes(GameTables.Tribes), "Tribes round-trip");
            Check(ArrayTableCsvSerializer.WriteStartConditions(ArrayTableCsvSerializer.ParseStartConditions(ArrayTableCsvSerializer.WriteStartConditions(GameTables.StartConditions)))
                  == ArrayTableCsvSerializer.WriteStartConditions(GameTables.StartConditions), "StartConditions round-trip");
            Check(ArrayTableCsvSerializer.WriteStartConditionRules(ArrayTableCsvSerializer.ParseStartConditionRules(ArrayTableCsvSerializer.WriteStartConditionRules(GameTables.StartConditionRules)))
                  == ArrayTableCsvSerializer.WriteStartConditionRules(GameTables.StartConditionRules), "StartConditionRules round-trip");
        }

        /// <summary>기존 트리의 고정 구조는 유지한다. 기술 내용/비용은 이 비교에 넣지 않아 밸런스 변경을 허용한다.</summary>
        private static void VerifyTechLayout()
        {
            var legacy = TechCsvSerializer.Parse(Resources.Load<TextAsset>(TechTreeDefinition.CsvResourcePath).text);
            var built = TechGroupSystem.BuildTechNodes();
            Check(GameTables.TechSlots.Length == 25 && built.Count == 25, "fixed tree has 25 occupied slots");
            for (int i = 0; i < Mathf.Min(legacy.Count, built.Count); i++)
            {
                var a = legacy[i];
                var b = built[i];
                int oldParent = legacy.FindIndex(n => n.Id == a.ParentId);
                Check(GameTables.TechSlots[i].ParentIndex == oldParent && a.Tier == b.Tier && a.Slot == b.Slot,
                      $"slot {i} retains original position and prerequisite slot");
            }
            var loaded = GameDataLoader.LoadTechNodes();
            Check(TechCsvSerializer.Write(loaded) == TechCsvSerializer.Write(built), "default loader and tribe tree use the same separated tables");
        }

        private static void VerifyTechReplacementAndBalance()
        {
            var techs = GameTables.Techs.Concat(new[] { new TechRow
            {
                Index = GameTables.Techs.Length, Id = "Replacement", Name = "교체 기술", Icon = "mining",
                CostBase = 9, CostPerCity = 7, Unlocks = new[] { 13 }, Description = "교체 설명",
            } }).ToArray();
            var layout = (TechTreeLayoutRow[])GameTables.TechTreeLayout.Clone();
            int root = System.Array.FindIndex(GameTables.TechSlots, s => s.ParentIndex == -1);
            int placement = System.Array.FindIndex(layout, p => p.SlotIndex == root);
            string oldId = techs[layout[placement].TechIndex].Id;
            var p = layout[placement]; p.TechIndex = techs.Length - 1; layout[placement] = p;
            // CSV 행 순서와 슬롯 순서는 별개다.
            System.Array.Reverse(layout);
            var nodes = TechGroupSystem.BuildTechNodes(techs, GameTables.TechUnlocks, GameTables.TechSlots, layout);
            var tech = TechTreeData.CreateEmpty();
            var replacement = TechSystem.Find(nodes, "Replacement");
            Check(nodes.Count == 25 && replacement != null && TechSystem.Find(nodes, oldId) == null, "replacing root keeps slot count and removes old technology");
            if (replacement == null) return;
            Check(replacement.Value.Name == "교체 기술" && replacement.Value.Effect == "교체 설명" && replacement.Value.Icon == "mining", "replacement brings its own display data");
            var child = nodes[System.Array.FindIndex(GameTables.TechSlots, s => s.ParentIndex == root)];
            Check(child.ParentId == "Replacement" && !TechSystem.IsAvailable(nodes, tech, child.Id), "child requires technology currently in parent slot");
            Check(TechSystem.IsAvailable(nodes, tech, "Replacement") && !TechSystem.IsAvailable(nodes, tech, oldId), "replacement root is researchable; removed technology is not");
            Check(TechSystem.Cost(nodes, tech, replacement.Value, 2) == 23, "replacement uses its own cost independently of slot tier");
            var resources = new CityResourceData { Stars = 23 };
            Check(TechSystem.Unlock(nodes, tech, ref resources, 2, "Replacement") && resources.Stars == 0, "research consumes replacement cost");
            Check(TechSystem.IsAvailable(nodes, tech, child.Id) && TechSystem.HasUnlock(nodes, tech, "Unit.shield"), "replacement research opens child and its own unlocks");
            var changed = techs[techs.Length - 1]; changed.CostBase = 3; changed.CostPerCity = 2; techs[techs.Length - 1] = changed;
            var balanced = TechGroupSystem.BuildTechNodes(techs, GameTables.TechUnlocks, GameTables.TechSlots, layout);
            Check(TechSystem.Cost(balanced, TechTreeData.CreateEmpty(), TechSystem.Find(balanced, "Replacement").Value, 2) == 7,
                "editing only technology costs changes research cost");
            Check(nodes.Zip(balanced, (a, b) => a.Id == b.Id && a.ParentId == b.ParentId && a.Tier == b.Tier && a.Slot == b.Slot).All(x => x), "balance edit preserves positions and prerequisites");
            var kinds = new HashSet<TechUnlockKind>(GameTables.TechUnlocks.Select(u => u.Kind));
            Check(kinds.SetEquals((TechUnlockKind[])System.Enum.GetValues(typeof(TechUnlockKind))), "unlock table covers passive, active, unit, building and quest kinds");
        }

        private static void VerifyInvalidTechLayout()
        {
            var slots = GameTables.TechSlots;
            var layout = GameTables.TechTreeLayout;
            void Rejected(TechSlotRow[] testSlots, TechTreeLayoutRow[] testLayout, string reason)
            {
                var errors = new List<string>();
                var nodes = TechGroupSystem.BuildTechNodes(GameTables.Techs, GameTables.TechUnlocks, testSlots, testLayout, errors);
                Check(errors.Count > 0 && nodes.Count == 0, "invalid layout rejected: " + reason);
            }
            Rejected(slots, layout.Skip(1).ToArray(), "missing slot");
            Rejected(slots, layout.Concat(new[] { layout[0] }).ToArray(), "duplicate slot");
            var bad = (TechTreeLayoutRow[])layout.Clone();
            var p = bad[1]; p.TechIndex = bad[0].TechIndex; bad[1] = p;
            Rejected(slots, bad, "same technology in two slots");
            p.TechIndex = GameTables.Techs.Length; bad[1] = p;
            Rejected(slots, bad, "out-of-range technology");
            p = bad[1]; p.SlotIndex = slots.Length; bad[1] = p;
            Rejected(slots, bad, "out-of-range slot");
            var cycle = (TechSlotRow[])slots.Clone();
            var s = cycle[0]; s.ParentIndex = 1; cycle[0] = s;
            Rejected(cycle, layout, "cyclic prerequisites");
            var overlap = (TechSlotRow[])slots.Clone();
            s = overlap[2]; s.Slot = overlap[1].Slot; overlap[2] = s;
            Rejected(overlap, layout, "overlapping sibling positions");
        }

        private static void VerifyArrayRulesReported()
        {
            var errors = new List<string>();
            ArrayTableCsvSerializer.ParseTechGroups("Index,Id,Tech1\n0,A,1\n2,B,x\n", errors);
            Check(errors.Any(e => e.Contains("Index")) && errors.Any(e => e.Contains("'x'")), "index gap + non-integer index reported: " + string.Join(" | ", errors));

            var tribes = new[] { new TribeRow { Index = 0, Id = "T", BiomeIndex = 9, TechGroupIndex = 0, StartTechs = new[] { 3 }, StartUnits = new[] { 99 }, StartConditionIndex = 5 } };
            var groups = new[] { new TechGroupRow { Index = 0, Id = "G", Techs = new[] { 0, 1 } } };
            var w = ArrayTableValidationSystem.Validate(GameTables.TechUnlocks, GameTables.Techs, groups, tribes,
                GameTables.StartConditions, GameTables.StartConditionRules, biomeCount: 3, unitCount: 11);
            Check(w.Any(x => x.Contains("BiomeIndex")) && w.Any(x => x.Contains("StartUnit")) && w.Any(x => x.Contains("StartConditionIndex")) &&
                  w.Any(x => x.Contains("기술 그룹")), "out-of-range tribe references + start tech outside group reported: " + string.Join(" | ", w));
        }

        private static EconomyWorld NewEconomy()
        {
            var econ = new EconomyWorld { TechNodes = TechGroupSystem.BuildTechNodes(), UnitRows = SandboxUnits() };
            foreach (var team in CitySystem.Teams)
            {
                econ.Resources[team] = CityResourceData.Create(0, 0, false);
                econ.Tech[team] = TechTreeData.CreateEmpty();
            }
            return econ;
        }

        private static void VerifyTribeApply()
        {
            var econ = NewEconomy();
            var oumaji = GameTables.Tribes.First(t => t.Id == "Oumaji");
            TribeSystem.Apply(econ, Team.Player, oumaji, econ.UnitRows);
            Check(econ.Resources[Team.Player].Stars == 6, "Oumaji starts with 6 stars");
            Check(TechSystem.IsUnlocked(econ.Tech[Team.Player], "Riding"), "Oumaji starts with Riding");
            Check(TechSystem.HasUnlock(econ.TechNodes, econ.Tech[Team.Player], "Unit.cavalry"), "Riding unlock key reached through TechUnlocks");
            Check(econ.StartUnitIds[Team.Player].SequenceEqual(new[] { "cavalry" }), "Oumaji start unit = cavalry (rider)");
            Check(TechSystem.IsAvailable(econ.TechNodes, econ.Tech[Team.Player], "Climbing"), "full group allows Climbing");

            // 그룹에서 Climbing 갈래를 빼면 연구할 수 없다.
            var tech = econ.Tech[Team.Enemy];
            tech.Allowed = new HashSet<string>(GameTables.Techs.Where(t => t.Id != "Climbing").Select(t => t.Id));
            econ.Tech[Team.Enemy] = tech;
            Check(!TechSystem.IsAvailable(econ.TechNodes, econ.Tech[Team.Enemy], "Climbing"), "tech outside the group is not researchable");
            Check(TechSystem.IsAvailable(econ.TechNodes, econ.Tech[Team.Enemy], "Riding"), "tech inside the group stays researchable");
            Check(TechGroupSystem.Filter(econ.TechNodes, tech.Allowed).Count == 24, "tree view shows only group techs");

            var luxidoor = GameTables.Tribes.First(t => t.Id == "Luxidoor");
            var econ2 = NewEconomy();
            TribeSystem.Apply(econ2, Team.Player, luxidoor, econ2.UnitRows);
            Check(econ2.Tech[Team.Player].Unlocked.Count == 0 && econ2.Resources[Team.Player].Stars == 2, "Luxidoor: no start tech, 2 stars");
            Check(!string.IsNullOrEmpty(TribeSystem.Summary(oumaji, GameDataLoader.LoadDefaultBiomes(), econ.UnitRows)), "tribe summary text");
        }

        private static void VerifyStartingUnits()
        {
            var grid = new GridWorld(10, 10, 1f);
            var econ = NewEconomy();
            CitySystem.FoundCity(grid, econ, new Vector2Int(5, 5), Team.Player, true, "test");
            econ.StartUnitIds[Team.Player] = new[] { "cavalry", "archer" };
            var log = CitySystem.StartingUnitRequests(new EntityWorld(), econ);
            Check(log.Count == 2 && log[0].SpawnUnitId == "cavalry" && log[1].SpawnUnitId == "archer" && log.All(e => e.Team == Team.Player),
                "tribe start units become start-unit requests at the capital (enemy without capital gets none)");
        }

        private static GridWorld GrassGrid(int size, Vector2Int capital)
        {
            var grid = new GridWorld(size, size, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            grid.SetStructure(capital, StructureGenerationSystem.CapitalStructureId);
            return grid;
        }

        private static void VerifyStartConditions()
        {
            var capital = new Vector2Int(6, 6);
            var cond = GameTables.StartConditions.First(c => c.Id == "DrylandsCoast");
            var rules = GameTables.StartConditionRules;

            var grid = GrassGrid(12, capital);
            int changed = StartConditionSystem.Apply(grid, capital, cond, rules, "Drylands", 1);
            var ring = StartConditionSystem.Ring(grid, capital, 1, 1);
            int water = ring.Count(p => grid.GetTileType(p) == "Water" && grid.GetTerrain(p) == TerrainType.Water);
            int fish = ring.Count(p => grid.GetStructure(p) == "Resource_Fish" && grid.GetTerrain(p) == TerrainType.Water);
            Check(water == 2 && fish == 2 && changed == 4, $"Drylands coast capital: 2 water + 2 fish next to capital (water {water}, fish {fish}, changed {changed})");
            Check(grid.GetStructure(capital) == StructureGenerationSystem.CapitalStructureId && grid.GetTerrain(capital) == TerrainType.Land, "capital tile untouched");
            Check(StartConditionSystem.Apply(grid, capital, cond, rules, "Drylands", 2) == 0, "already satisfied -> no changes");

            var other = GrassGrid(12, capital);
            Check(StartConditionSystem.Apply(other, capital, cond, rules, "Continents", 1) == 0, "MapType filter: rule skipped on Continents");

            // 거리 고리 규칙(2칸)과 AllowedTiles 제한.
            var ringRule = new StartConditionRuleRow { Kind = StartRuleKind.Tile, Target = "Mountain", TerrainType = TerrainType.Land, Count = 3, MinDistance = 2, MaxDistance = 2, AllowedTiles = new[] { "Grass" } };
            var g3 = GrassGrid(12, capital);
            StartConditionSystem.ApplyRule(g3, capital, ringRule, "Lakes", new System.Random(3));
            Check(StartConditionSystem.Ring(g3, capital, 2, 2).Count(p => g3.GetTileType(p) == "Mountain") == 3 &&
                  StartConditionSystem.Ring(g3, capital, 1, 1).All(p => g3.GetTileType(p) == "Grass"), "ring rule places exactly in distance 2");
        }

        private static void VerifyOutlineRatios()
        {
            var modes = new[]
            {
                TerrainGenerationSystem.MapShapeMode.Freeform, TerrainGenerationSystem.MapShapeMode.Lakes, TerrainGenerationSystem.MapShapeMode.Pangea,
                TerrainGenerationSystem.MapShapeMode.Continents, TerrainGenerationSystem.MapShapeMode.Archipelago, TerrainGenerationSystem.MapShapeMode.Waterworld
            };
            foreach (var mode in modes)
                foreach (var target in new[] { 0.1f, 0.3f, 0.5f, 0.7f })
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var grid = new GridWorld(18, 18, 1f);
                        var o = TerrainGenerationSystem.PlanOutline(grid, 2, seed * 97, mode, target);
                        float actual = TerrainGenerationSystem.OutlineWaterFraction(o);
                        // Continents는 대륙 크기(30~200칸)/간격 규칙 때문에 목표를 정확히 못 맞출 수 있어 허용 폭을 넓힌다.
                        float tolerance = mode == TerrainGenerationSystem.MapShapeMode.Continents ? 0.2f : 0.08f;
                        Check(Mathf.Abs(actual - target) <= tolerance, $"{mode} outline water {actual:0.00} ~ target {target:0.00} (seed {seed})");
                        Check(o.Anchors.Length == 2 && o.Anchors.All(a => o.LandMask[grid.Index(a)]), $"{mode} outline: 2 capitals on land");
                        var again = TerrainGenerationSystem.PlanOutline(new GridWorld(18, 18, 1f), 2, seed * 97, mode, target);
                        Check(again.LandMask.SequenceEqual(o.LandMask) && again.Anchors.SequenceEqual(o.Anchors), $"{mode} outline deterministic");
                    }

            // 슬라이더: 물 비율을 올리면 물이 실제로 늘어난다.
            var low = TerrainGenerationSystem.PlanOutline(new GridWorld(16, 16, 1f), 2, 5, TerrainGenerationSystem.MapShapeMode.Lakes, 0.2f);
            var high = TerrainGenerationSystem.PlanOutline(new GridWorld(16, 16, 1f), 2, 5, TerrainGenerationSystem.MapShapeMode.Lakes, 0.6f);
            Check(TerrainGenerationSystem.OutlineWaterFraction(high) > TerrainGenerationSystem.OutlineWaterFraction(low) + 0.3f, "higher slider -> more water");
        }

        private static void VerifyGenerateFromOutline()
        {
            var biomes = GameDataLoader.LoadDefaultBiomes();
            Check(biomes.Count == 3, "default biome table has 3 biomes");
            if (biomes.Count == 0) return;
            var desert = biomes.First(b => b.Id == "Desert");
            var allowed = new HashSet<string>(desert.Tiles.Select(t => t.TileId))
                { TerrainGenerationSystem.ForestTileId, TerrainGenerationSystem.MountainTileId, TerrainGenerationSystem.OceanTileId };

            foreach (var mode in new[] { TerrainGenerationSystem.MapShapeMode.Freeform, TerrainGenerationSystem.MapShapeMode.Pangea, TerrainGenerationSystem.MapShapeMode.Archipelago })
            {
                var grid = new GridWorld(16, 16, 1f);
                var outline = TerrainGenerationSystem.PlanOutline(grid, 2, 11, mode, 0.4f);
                TerrainGenerationSystem.ApplyOutline(grid, outline);
                Check(Enumerable.Range(0, 256).All(i => grid.GetTerrain(new Vector2Int(i % 16, i / 16)) == (outline.LandMask[i] ? TerrainType.Land : TerrainType.Water)),
                    $"{mode}: ApplyOutline draws the mask");

                var anchors = TerrainGenerationSystem.GenerateFromOutline(grid, new List<BiomeCsvRow> { desert, desert }, outline, 123);
                bool sameShape = true, onlyDesert = true, filled = true;
                for (int i = 0; i < 256; i++)
                {
                    var p = new Vector2Int(i % 16, i / 16);
                    sameShape &= grid.GetTerrain(p) == (outline.LandMask[i] ? TerrainType.Land : TerrainType.Water);
                    onlyDesert &= allowed.Contains(grid.GetTileType(p));
                    filled &= !string.IsNullOrEmpty(grid.GetTileType(p));
                }
                Check(anchors.SequenceEqual(outline.Anchors), $"{mode}: fill keeps the outline capitals");
                Check(sameShape, $"{mode}: biome fill keeps the outline land/water shape");
                Check(onlyDesert && filled, $"{mode}: single-biome selection fills only that biome's tiles (+forest/mountain/ocean)");

                // 같은 아웃라인, 다른 바이옴 -> 모양은 그대로.
                var highland = biomes.First(b => b.Id == "Highland");
                TerrainGenerationSystem.GenerateFromOutline(grid, new List<BiomeCsvRow> { highland, desert }, outline, 456);
                Check(Enumerable.Range(0, 256).All(i => grid.GetTerrain(new Vector2Int(i % 16, i / 16)) == (outline.LandMask[i] ? TerrainType.Land : TerrainType.Water)),
                    $"{mode}: refilling with other biomes keeps the shape");
                StructureGenerationSystem.Generate(grid, new List<BiomeCsvRow> { highland, desert }, anchors, 456, outline.Suburbs, outline.PlannedVillages, mode);
                Check(anchors.All(a => grid.GetStructure(a) == StructureGenerationSystem.CapitalStructureId), $"{mode}: structures after outline fill put capitals on anchors");
            }
        }

        private static void VerifySandboxHudPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SandboxHud.prefab");
            var tab = prefab != null ? prefab.transform.Find("Canvas/GenerationTab") : null;
            Check(tab != null, "SandboxHud prefab has GenerationTab (습도 탭)");
            if (tab == null) return;
            foreach (var name in new[] { "MapTypeDropdown", "BiomeDropdown", "PlayerTribeDropdown", "EnemyTribeDropdown" })
                Check(tab.Find(name)?.GetComponent<Dropdown>() != null, $"GenerationTab/{name} is a Dropdown");
            Check(tab.Find("WaterSlider")?.GetComponent<Slider>() != null, "GenerationTab/WaterSlider is a Slider");
            Check(tab.Find("1차지형생성Button")?.GetComponent<Button>() != null, "GenerationTab outline button");
        }
    }
}
