using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 게임 규칙 CSV(기본은 Assets/Resources, 샌드박스는 TableSource.Folder가 덮어씀)를 읽어 Data 계층의 표
    /// (BuildingDefinition.All/TileActionDefinition.All ...)에 채워 넣는 진입점. 전투 화면(BattleController.Awake), 샌드박스
    /// "표 불러오기", 에디터 검증/시뮬레이션이 부른다 — 다시 불러도 같은 결과(CSV를 고친 뒤 다시 부르면 새 값).
    /// 자체 상태는 없다: 결과는 Data의 정적 필드에만 남는다. 파일이 없거나 칸이 잘못되면 경고를 남기고 읽을 수 있는 만큼만 채운다
    /// (오류 목록을 돌려준다).
    /// </summary>
    public static class GameDataLoader
    {
        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/GameRules.csv(스칼라 규칙 값, Data/GameRules).</summary>
        public const string GameRulesCsvResourcePath = "GameRules";

        /// <summary>LoadAll이 읽는 게임 데이터 표 전부(Resources 경로, 확장자 없음). 표 폴더 불러오기/내보내기의 대상이다.
        /// 모델 CSV(Models/)와 샌드박스 예제(TechTree.csv)는 들지 않는다.</summary>
        public static readonly string[] TableCsvPaths =
        {
            StringTable.CsvResourcePath, GameRulesCsvResourcePath,
            TileActionDefinition.CsvResourcePath, CityRewardDefinition.CsvResourcePath, TaskDefinition.CsvResourcePath,
            GameTables.UnitsPath, GameTables.BoatsPath, GameTables.BuildingsPath,
            GameTables.TechUnlocksPath, GameTables.TechsPath, GameTables.TechSlotsPath, GameTables.TechTreeLayoutPath, GameTables.TechGroupsPath,
            GameTables.TribesPath, GameTables.StartConditionsPath, GameTables.StartConditionRulesPath, GameTables.DefaultBiomesPath,
        };

        public static string[] SandboxCsvPaths => new List<string>(TableCsvPaths)
        {
            "Pixel2D/SpriteCatalog"
        }.ToArray();

        public static List<string> LoadAll() => LoadAll(TableSource.Resources);

        public static List<string> LoadAll(TableSource source)
        {
            var errors = new List<string>();
            // 순서: 번역 표(다른 표의 이름/설명이 조회) → 규칙({Rule.*}) → 키형 표 → 배열형 표. 설명 속 {표.Id.컬럼}은 먼저 읽은 표만 볼 수 있다.
            LoadStrings(errors, source);
            GameRulesCsvSerializer.Apply(Read(GameRulesCsvResourcePath, errors, source), errors);
            TileActionDefinition.All = GameTableCsvSerializer.ParseTileActions(Read(TileActionDefinition.CsvResourcePath, errors, source), errors);
            CityRewardDefinition.All = GameTableCsvSerializer.ParseCityRewards(Read(CityRewardDefinition.CsvResourcePath, errors, source), errors);
            TaskDefinition.All = GameTableCsvSerializer.ParseTasks(Read(TaskDefinition.CsvResourcePath, errors, source), errors);
            LoadArrayTables(errors, source);
            PixelSpriteCatalog.SetSource(source);
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return errors;
        }

        /// <summary>배열형 테이블(Tables/, docs/spec/csv-common.md) -&gt; GameTables. 표 사이 Index 참조도 여기서 한 번 검사한다
        /// (바이옴 수는 기본 바이옴 표 기준). 배 → 유닛(BoatIndex) → 건물 → 해금 내역(Building/Unit/BoatIndex) 순서로 읽고 연결한다.</summary>
        private static void LoadArrayTables(List<string> errors, TableSource source)
        {
            GameTables.Boats = ArrayTableCsvSerializer.ParseBoats(Read(GameTables.BoatsPath, errors, source), errors);
            TableLinkSystem.SplitBoats(GameTables.Boats, out var raft, out var upgrades, out var special);
            if (raft != null) NavalUnitDefinition.Raft = raft;
            NavalUnitDefinition.Upgrades = upgrades;
            NavalUnitDefinition.Special = special;
            GameTables.Units = ArrayTableCsvSerializer.ParseUnits(Read(GameTables.UnitsPath, errors, source), GameTables.Boats, errors);
            BuildingDefinition.All = ArrayTableCsvSerializer.ParseBuildings(Read(GameTables.BuildingsPath, errors, source), errors);
            GameTables.TechUnlocks = ArrayTableCsvSerializer.ParseTechUnlocks(Read(GameTables.TechUnlocksPath, errors, source), errors);
            TableLinkSystem.ResolveUnlockTargets(GameTables.TechUnlocks, BuildingDefinition.All, GameTables.Units, GameTables.Boats);
            TableLinkSystem.ApplyBuildingUnlocks(BuildingDefinition.All, GameTables.TechUnlocks);
            GameTables.Techs = ArrayTableCsvSerializer.ParseTechs(Read(GameTables.TechsPath, errors, source), errors);
            GameTables.TechSlots = ArrayTableCsvSerializer.ParseTechSlots(Read(GameTables.TechSlotsPath, errors, source), errors);
            GameTables.TechTreeLayout = ArrayTableCsvSerializer.ParseTechTreeLayout(Read(GameTables.TechTreeLayoutPath, errors, source), errors);
            GameTables.TechGroups = ArrayTableCsvSerializer.ParseTechGroups(Read(GameTables.TechGroupsPath, errors, source), errors);
            GameTables.Tribes = ArrayTableCsvSerializer.ParseTribes(Read(GameTables.TribesPath, errors, source), errors);
            GameTables.StartConditionRules = ArrayTableCsvSerializer.ParseStartConditionRules(Read(GameTables.StartConditionRulesPath, errors, source), errors);
            GameTables.StartConditions = ArrayTableCsvSerializer.ParseStartConditions(Read(GameTables.StartConditionsPath, errors, source), errors);
            errors.AddRange(ArrayTableValidationSystem.ValidateLoaded(biomeCount: LoadDefaultBiomes(source).Count));
        }

        public static void LoadStrings(List<string> errors) => LoadStrings(errors, TableSource.Resources);

        /// <summary>번역 표(Strings.csv)의 StringTable.Language 열을 올리고, 코드에 있는 구조물 표의 이름/설명을 채운다.
        /// 언어를 바꾸면 LoadAll을 다시 불러야 표의 이름이 바뀐다.</summary>
        public static void LoadStrings(List<string> errors, TableSource source)
        {
            var values = StringTableCsvSerializer.Parse(Read(StringTable.CsvResourcePath, errors, source), StringTable.Language, errors);
            if (values.Language != null) StringTable.Language = values.Language;
            StringTable.Languages = values.Languages.Length > 0 ? values.Languages : new[] { StringTable.SourceLanguage };
            StringTable.Current = values.Current;
            StringTable.Source = values.Source;
            StringTable.LanguageNames = values.LanguageNames;
            for (int i = 0; i < StructureDefinition.All.Length; i++)
            {
                string id = StructureDefinition.All[i].Id;
                StructureDefinition.All[i].Name = LocalizationSystem.Name(StructureDefinition.StringTable, id);
                StructureDefinition.All[i].Description = LocalizationSystem.Desc(StructureDefinition.StringTable, id);
            }
        }

        /// <summary>기본 바이옴 표(Tables/Biomes.csv — 표 폴더에 있으면 그 파일). 없으면 빈 목록.</summary>
        public static List<BiomeCsvRow> LoadDefaultBiomes(TableSource source = default)
        {
            var text = Read(GameTables.DefaultBiomesPath, null, source);
            return text.Length > 0 ? BiomeCsvSerializer.Parse(text) : new List<BiomeCsvRow>();
        }

        /// <summary>모델 팔레트 + 파츠 CSV(Assets/Resources/Models) -&gt; ModelDefinition. 파일이 없으면 그 모델들만 비어 있다
        /// (그리는 쪽이 옛 표시로 대체한다). 표 폴더로 덮어쓰지 않는다.</summary>


        /// <summary>슬롯 구조 + 기술 배치 + 기술/해금 표를 조합한다. 매번 CSV를 다시 읽으므로 비용 수정도 반영된다.
        /// 해금 내역의 건물/유닛/배 Index는 LoadAll이 올린 표로 푼다.</summary>
        public static List<TechNodeData> LoadTechNodes(TableSource source = default)
        {
            var errors = new List<string>();
            var techs = ArrayTableCsvSerializer.ParseTechs(Read(GameTables.TechsPath, errors, source), errors);
            var unlocks = ArrayTableCsvSerializer.ParseTechUnlocks(Read(GameTables.TechUnlocksPath, errors, source), errors);
            TableLinkSystem.ResolveUnlockTargets(unlocks, BuildingDefinition.All, GameTables.Units, GameTables.Boats);
            var slots = ArrayTableCsvSerializer.ParseTechSlots(Read(GameTables.TechSlotsPath, errors, source), errors);
            var layout = ArrayTableCsvSerializer.ParseTechTreeLayout(Read(GameTables.TechTreeLayoutPath, errors, source), errors);
            var nodes = TechGroupSystem.BuildTechNodes(techs, unlocks, slots, layout, errors);
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return nodes;
        }

        // ---------- 표 폴더 (샌드박스) ----------

        /// <summary>파일 하나를 골랐을 때 그 파일이 든 표 폴더의 뿌리. 파일 이름이 TableCsvPaths의 표 이름이 아니면 null(옛 단일 파일).
        /// Resources와 같은 구조(뿌리/Tables/Units.csv)면 Tables의 부모를, 평평하게 둔 폴더면 그 폴더를 돌려준다.</summary>
        public static string TableFolderOf(string pickedFile)
        {
            if (string.IsNullOrEmpty(pickedFile)) return null;
            string name = Path.GetFileNameWithoutExtension(pickedFile);
            foreach (var path in TableCsvPaths)
            {
                if (!string.Equals(Path.GetFileName(path), name, System.StringComparison.OrdinalIgnoreCase)) continue;
                var dir = Path.GetDirectoryName(Path.GetFullPath(pickedFile));
                string sub = Path.GetDirectoryName(path); // "Tables" 또는 ""
                if (!string.IsNullOrEmpty(sub) && string.Equals(Path.GetFileName(dir), sub, System.StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(dir);
                return dir;
            }
            return null;
        }

        /// <summary>source에서 지금 쓰는 표(TableCsvPaths)를 destFolder에 Resources와 같은 구조(Tables/ 포함)로 그대로 복사한다.
        /// 원문 텍스트를 옮기므로 주석 행·메모 칸도 남는다. 쓴 파일 수를 돌려준다.</summary>
        public static int ExportTables(TableSource source, string destFolder)
        {
            int count = 0;
            foreach (var path in TableCsvPaths)
            {
                var text = Read(path, null, source);
                if (text.Length == 0) continue;
                var file = Path.Combine(destFolder, path + ".csv");
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, text, new System.Text.UTF8Encoding(false));
                count++;
            }
            return count;
        }

        /// <summary>개별 선택 파일 → 호환용 표 폴더 → Resources 순서로 텍스트를 읽는다.</summary>
        public static string Read(string resourcePath, List<string> errors, TableSource source)
        {
            if (source.Files != null && source.Files.TryGetValue(resourcePath, out var selected))
            {
                try { return File.ReadAllText(selected); }
                catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
                { errors?.Add($"{selected}: {e.Message} — 기본 표를 씁니다."); }
            }
            if (!string.IsNullOrEmpty(source.Folder))
            {
                foreach (var file in new[] { Path.Combine(source.Folder, resourcePath + ".csv"), Path.Combine(source.Folder, Path.GetFileName(resourcePath) + ".csv") })
                {
                    if (!File.Exists(file)) continue;
                    try
                    {
                        var text = File.ReadAllText(file);
                        if (source.FromFolder != null && !source.FromFolder.Contains(resourcePath)) source.FromFolder.Add(resourcePath);
                        return text;
                    }
                    catch (IOException e) { errors?.Add($"{file}을(를) 읽지 못함({e.Message}) — 기본 표를 씁니다."); }
                }
            }
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) return asset.text;
            errors?.Add($"Resources/{resourcePath}.csv를 찾지 못함 — 그 표는 비어 있습니다.");
            return string.Empty;
        }
    }
}
