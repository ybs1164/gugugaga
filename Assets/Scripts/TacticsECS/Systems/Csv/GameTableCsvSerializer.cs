using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 게임 규칙 표 CSV(Assets/Resources/*.csv) -&gt; Core 값 타입 배열 변환. 표마다 컬럼 이름 목록(스키마)과 변환 한
    /// 함수만 있고, 칸 읽기/오류 보고는 CsvTableReader가 한다. 자체 상태는 없다. 컬럼 설명은 docs/GameDataCsv.md.
    ///
    /// 표 형식(1차 비교분석 결과 — docs/GameDataCsv.md): "넓은 표 + Flags 목록" 하이브리드.
    ///   - 숫자/문자열 속성은 컬럼 하나씩(스프레드시트에서 정렬·필터·합계가 된다).
    ///   - 대부분의 행에 해당 없는 드문 불리언 속성은 태그로 나열한다(예: 다리 = Flag1 Neutral, Flag2 OppositeLand, Flag3 ActsAsRoad).
    ///     태그는 정해진 목록에서만 고를 수 있어 오타가 오류로 잡힌다.
    ///   - 여러 값을 갖는 속성(Terrain/RequiredStructure/AdjacentBuilding/Flag)은 한 칸에 묶지 않고 번호 붙은 반복 컬럼
    ///     (Terrain1, Terrain2 ...)으로 나열한다(CLAUDE.md 규칙 6 — CsvTableReader.GetList). 옛 한 칸 목록 컬럼
    ///     (RequiredStructures/AdjacentBuildings/Flags, "a;b")도 호환용으로 읽는다.
    /// </summary>
    public static class GameTableCsvSerializer
    {
        // ---------- 건물 (Buildings.csv) ----------

        public const string FlagRoad = "Road";
        public const string FlagNeutral = "Neutral";
        public const string FlagOppositeLand = "OppositeLand";
        public const string FlagActsAsRoad = "ActsAsRoad";
        public const string FlagTemple = "Temple";
        public const string FlagGoldFromAdjacent = "GoldFromAdjacent";
        public const string FlagOnePerCity = "OnePerCity";

        public static readonly string[] BuildingFlags = { FlagRoad, FlagNeutral, FlagOppositeLand, FlagActsAsRoad, FlagTemple, FlagGoldFromAdjacent, FlagOnePerCity };

        private static readonly string[] BuildingRequired = { "Id", "Terrain" };
        private static readonly string[] BuildingOptional =
            { "Name", "Unlock", "Cost", "Population", "RequiredStructure", "AdjacentBuilding", "PopulationPerAdjacent", "Flag", "Task", "Description", "Wiki", "Note",
              "RequiredStructures", "AdjacentBuildings", "Flags" };
        private static readonly string[] BuildingLists = { "Terrain", "RequiredStructure", "AdjacentBuilding", "Flag" };

        public static BuildingInfo[] ParseBuildings(string csvText, List<string> errors, string name = "Buildings.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, BuildingRequired, BuildingOptional, errors, BuildingLists);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var list = new List<BuildingInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                var flags = CsvTableReader.GetTags(t, r, "Flag", BuildingFlags, errors, "Flags");
                var info = new BuildingInfo
                {
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Cost = CsvTableReader.GetInt(t, r, "Cost", 0, errors),
                    Population = CsvTableReader.GetInt(t, r, "Population", 0, errors),
                    Terrain = CsvTableReader.GetFlags<TileClass>(t, r, "Terrain", errors),
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructure", "RequiredStructures"),
                    AdjacentBuildings = CsvTableReader.GetList(t, r, "AdjacentBuilding", "AdjacentBuildings"),
                    PopulationPerAdjacent = CsvTableReader.GetInt(t, r, "PopulationPerAdjacent", 0, errors),
                    IsRoad = flags.Contains(FlagRoad),
                    AllowNeutral = flags.Contains(FlagNeutral),
                    RequiresOppositeLand = flags.Contains(FlagOppositeLand),
                    ActsAsRoad = flags.Contains(FlagActsAsRoad),
                    IsTemple = flags.Contains(FlagTemple),
                    ProducesGoldFromAdjacent = flags.Contains(FlagGoldFromAdjacent),
                    OnePerCity = flags.Contains(FlagOnePerCity),
                    TaskId = CsvTableReader.Get(t, r, "Task"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                };
                if (info.Terrain == TileClass.None) CsvTableReader.Report(t, r, "Terrain", "지을 수 있는 지형이 없음", errors);
                list.Add(info);
            }

            // 인접 건물 칸은 같은 표의 Id를 가리켜야 한다(오타 방지).
            for (int r = 0; r < list.Count; r++)
                foreach (var adj in list[r].AdjacentBuildings)
                    if (!list.Exists(b => b.Id == adj)) CsvTableReader.Report(t, r, "AdjacentBuilding", $"표에 없는 건물 '{adj}'", errors);
            return list.ToArray();
        }

        // ---------- 타일 행동 (TileActions.csv) ----------

        private static readonly string[] TileActionRequired = { "Id", "Kind", "Terrain" };
        private static readonly string[] TileActionOptional =
            { "Name", "Unlock", "Cost", "RequiredStructure", "Population", "GoldGain", "Description", "Wiki", "Note", "RequiredStructures" };
        private static readonly string[] TileActionLists = { "Terrain", "RequiredStructure" };

        public static TileActionInfo[] ParseTileActions(string csvText, List<string> errors, string name = "TileActions.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, TileActionRequired, TileActionOptional, errors, TileActionLists);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var list = new List<TileActionInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                list.Add(new TileActionInfo
                {
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TileActionKind.Harvest, errors),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Cost = CsvTableReader.GetInt(t, r, "Cost", 0, errors),
                    Terrain = CsvTableReader.GetFlags<TileClass>(t, r, "Terrain", errors),
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructure", "RequiredStructures"),
                    Population = CsvTableReader.GetInt(t, r, "Population", 0, errors),
                    GoldGain = CsvTableReader.GetInt(t, r, "GoldGain", 0, errors),
                    Description = CsvTableReader.Get(t, r, "Description"),
                });
            }
            return list.ToArray();
        }

        // ---------- 도시 보상 (CityRewards.csv) ----------

        public static CityRewardInfo[] ParseCityRewards(string csvText, List<string> errors, string name = "CityRewards.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Level", "Reward" }, new[] { "Name", "Amount", "Description", "Wiki", "Note" }, errors);
            var list = new List<CityRewardInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                var type = CsvTableReader.GetEnum(t, r, "Reward", CityRewardType.Resources, errors);
                list.Add(new CityRewardInfo
                {
                    Level = CsvTableReader.GetInt(t, r, "Level", 2, errors),
                    Type = type,
                    Name = CsvTableReader.Get(t, r, "Name", type.ToString()),
                    Amount = CsvTableReader.GetInt(t, r, "Amount", 0, errors),
                    Description = CsvTableReader.Get(t, r, "Description"),
                });
            }
            return list.ToArray();
        }

        // ---------- 배 유닛 (NavalUnits.csv) ----------

        /// <summary>유닛 CSV와 같은 컬럼(UnitCsvSerializer) + Unlock(업그레이드 해금 키). Id가 raft인 행이 뗏목, Unlock이 있는 행은 뗏목
        /// 업그레이드, Unlock이 빈 행은 특수 배(위키 Dinghy/Pirate — 유닛 CSV Boat 칸이 가리킨다, 업그레이드 불가).</summary>
        public static void ParseNavalUnits(string csvText, List<string> errors, out UnitCsvRow raft, out (UnitCsvRow Row, string UnlockKey)[] upgrades,
            out UnitCsvRow[] special, string name = "NavalUnits.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            var rows = UnitCsvSerializer.Parse(csvText, errors, name);
            raft = null;
            var list = new List<(UnitCsvRow, string)>();
            var specialList = new List<UnitCsvRow>();
            for (int r = 0; r < rows.Count && r < t.Rows.Count; r++)
            {
                rows[r].Domain = TerrainType.Water;
                string unlock = CsvTableReader.Get(t, r, "Unlock");
                if (rows[r].Id == NavalUnitDefinition.RaftId) raft = rows[r];
                else if (unlock.Length > 0) list.Add((rows[r], unlock));
                else specialList.Add(rows[r]);
            }
            if (raft == null) errors?.Add($"{name}: '{NavalUnitDefinition.RaftId}' 행이 없음 — 코드 기본 뗏목 사용");
            upgrades = list.ToArray();
            special = specialList.ToArray();
        }

        // ---------- 과업 (Tasks.csv) ----------

        public static TaskInfo[] ParseTasks(string csvText, List<string> errors, string name = "Tasks.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Id", "Kind" }, new[] { "Name", "Threshold", "Unlock", "Description", "Wiki", "Note" }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var list = new List<TaskInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                list.Add(new TaskInfo
                {
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TaskKind.Kills, errors),
                    Threshold = CsvTableReader.GetInt(t, r, "Threshold", 0, errors),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                });
            }
            return list.ToArray();
        }
    }
}
