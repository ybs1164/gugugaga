using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// Assets/Resources의 게임 규칙 CSV를 읽어 Data 계층의 표(BuildingDefinition.All/TileActionDefinition.All ...)에
    /// 채워 넣는 진입점. 전투 화면(BattleController.Awake)과 에디터 검증/시뮬레이션이 시작할 때 한 번씩 부른다 —
    /// 다시 불러도 같은 결과(CSV를 고친 뒤 다시 부르면 새 값). 자체 상태는 없다: 결과는 Data의 정적 필드에만 남는다.
    /// 파일이 없거나 칸이 잘못되면 경고를 남기고 읽을 수 있는 만큼만 채운다(오류 목록을 돌려준다).
    /// </summary>
    public static class GameDataLoader
    {
        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/GameRules.csv(스칼라 규칙 값, Data/GameRules).</summary>
        public const string GameRulesCsvResourcePath = "GameRules";

        public static List<string> LoadAll()
        {
            var errors = new List<string>();
            LoadStrings(errors); // 다른 표의 이름/설명이 번역 표를 조회하므로 가장 먼저
            TileActionDefinition.All = GameTableCsvSerializer.ParseTileActions(Read(TileActionDefinition.CsvResourcePath, errors), errors);
            CityRewardDefinition.All = GameTableCsvSerializer.ParseCityRewards(Read(CityRewardDefinition.CsvResourcePath, errors), errors);
            TaskDefinition.All = GameTableCsvSerializer.ParseTasks(Read(TaskDefinition.CsvResourcePath, errors), errors);
            GameRulesCsvSerializer.Apply(Read(GameRulesCsvResourcePath, errors), errors);
            LoadModels(errors);
            LoadArrayTables(errors);
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return errors;
        }

        /// <summary>배열형 테이블(Assets/Resources/Tables, docs/spec/csv-common.md) -&gt; GameTables. 표 사이 Index 참조도 여기서 한 번 검사한다
        /// (바이옴 수는 기본 바이옴 표 기준). 배 → 유닛(BoatIndex) → 건물 → 해금 내역(Building/Unit/BoatIndex) 순서로 읽고 연결한다.</summary>
        private static void LoadArrayTables(List<string> errors)
        {
            GameTables.Boats = ArrayTableCsvSerializer.ParseBoats(Read(GameTables.BoatsPath, errors), errors);
            TableLinkSystem.SplitBoats(GameTables.Boats, out var raft, out var upgrades, out var special);
            if (raft != null) NavalUnitDefinition.Raft = raft;
            NavalUnitDefinition.Upgrades = upgrades;
            NavalUnitDefinition.Special = special;
            GameTables.Units = ArrayTableCsvSerializer.ParseUnits(Read(GameTables.UnitsPath, errors), GameTables.Boats, errors);
            BuildingDefinition.All = ArrayTableCsvSerializer.ParseBuildings(Read(GameTables.BuildingsPath, errors), errors);
            GameTables.TechUnlocks = ArrayTableCsvSerializer.ParseTechUnlocks(Read(GameTables.TechUnlocksPath, errors), errors);
            TableLinkSystem.ResolveUnlockTargets(GameTables.TechUnlocks, BuildingDefinition.All, GameTables.Units, GameTables.Boats);
            TableLinkSystem.ApplyBuildingUnlocks(BuildingDefinition.All, GameTables.TechUnlocks);
            GameTables.Techs = ArrayTableCsvSerializer.ParseTechs(Read(GameTables.TechsPath, errors), errors);
            GameTables.TechSlots = ArrayTableCsvSerializer.ParseTechSlots(Read(GameTables.TechSlotsPath, errors), errors);
            GameTables.TechTreeLayout = ArrayTableCsvSerializer.ParseTechTreeLayout(Read(GameTables.TechTreeLayoutPath, errors), errors);
            GameTables.TechGroups = ArrayTableCsvSerializer.ParseTechGroups(Read(GameTables.TechGroupsPath, errors), errors);
            GameTables.Tribes = ArrayTableCsvSerializer.ParseTribes(Read(GameTables.TribesPath, errors), errors);
            GameTables.StartConditions = ArrayTableCsvSerializer.ParseStartConditions(Read(GameTables.StartConditionsPath, errors), errors);
            GameTables.StartConditionRules = ArrayTableCsvSerializer.ParseStartConditionRules(Read(GameTables.StartConditionRulesPath, errors), errors);
            errors.AddRange(ArrayTableValidationSystem.ValidateLoaded(biomeCount: LoadDefaultBiomes().Count));
        }

        /// <summary>번역 표(Strings.csv)의 StringTable.Language 열을 올리고, 코드에 있는 구조물 표의 이름/설명을 채운다.
        /// 언어를 바꾸면 LoadAll을 다시 불러야 표의 이름이 바뀐다.</summary>
        public static void LoadStrings(List<string> errors)
        {
            var values = StringTableCsvSerializer.Parse(Read(StringTable.CsvResourcePath, errors), StringTable.Language, errors);
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

        /// <summary>기본 바이옴 표(Assets/Resources/Tables/Biomes.csv). 없으면 빈 목록.</summary>
        public static List<BiomeCsvRow> LoadDefaultBiomes()
        {
            var asset = Resources.Load<TextAsset>(GameTables.DefaultBiomesPath);
            return asset != null ? BiomeCsvSerializer.Parse(asset.text) : new List<BiomeCsvRow>();
        }

        /// <summary>모델 팔레트 + 파츠 CSV(Assets/Resources/Models) -&gt; ModelDefinition. 파일이 없으면 그 모델들만 비어 있다
        /// (그리는 쪽이 옛 표시로 대체한다).</summary>
        private static void LoadModels(List<string> errors)
        {
            ModelDefinition.Palette = ModelCsvSerializer.ParsePalette(Read(ModelDefinition.PaletteResourcePath, errors), errors);
            var models = new Dictionary<string, List<ModelPartInfo>>();
            foreach (var path in ModelDefinition.CsvResourcePaths)
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset != null) ModelCsvSerializer.ParseInto(asset.text, ModelDefinition.Palette, models, errors, System.IO.Path.GetFileName(path) + ".csv");
            }
            var result = new Dictionary<string, ModelPartInfo[]>();
            foreach (var kv in models) result[kv.Key] = kv.Value.ToArray();
            ModelDefinition.Models = result;
            ModelDefinition.Version++;
        }

        /// <summary>슬롯 구조 + 기술 배치 + 기술/해금 표를 조합한다. 매번 CSV를 다시 읽으므로 비용 수정도 반영된다.
        /// 해금 내역의 건물/유닛/배 Index는 LoadAll이 올린 표로 푼다.</summary>
        public static List<TechNodeData> LoadTechNodes()
        {
            var errors = new List<string>();
            var techs = ArrayTableCsvSerializer.ParseTechs(Read(GameTables.TechsPath, errors), errors);
            var unlocks = ArrayTableCsvSerializer.ParseTechUnlocks(Read(GameTables.TechUnlocksPath, errors), errors);
            TableLinkSystem.ResolveUnlockTargets(unlocks, BuildingDefinition.All, GameTables.Units, GameTables.Boats);
            var slots = ArrayTableCsvSerializer.ParseTechSlots(Read(GameTables.TechSlotsPath, errors), errors);
            var layout = ArrayTableCsvSerializer.ParseTechTreeLayout(Read(GameTables.TechTreeLayoutPath, errors), errors);
            var nodes = TechGroupSystem.BuildTechNodes(techs, unlocks, slots, layout, errors);
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return nodes;
        }

        private static string Read(string resourcePath, List<string> errors)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) return asset.text;
            errors.Add($"Resources/{resourcePath}.csv를 찾지 못함 — 그 표는 비어 있습니다.");
            return string.Empty;
        }
    }
}
