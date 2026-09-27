using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 게임 규칙 표 CSV(Assets/Resources/*.csv) -&gt; Core 값 타입 배열 변환. 표마다 컬럼 이름 목록(스키마)과 변환 한
    /// 함수만 있고, 칸 읽기/오류 보고는 CsvTableReader가 한다. 자체 상태는 없다. 컬럼 설명은 docs/GameDataCsv.md.
    ///
    /// 표 형식(1차 비교분석 결과 — docs/GameDataCsv.md): "넓은 표 + Flags 목록" 하이브리드.
    ///   - 숫자/문자열 속성은 컬럼 하나씩(스프레드시트에서 정렬·필터·합계가 된다).
    ///   - 대부분의 행에 해당 없는 드문 불리언 속성은 컬럼을 늘리지 않고 Flags 칸 하나에 태그로 나열한다
    ///     (예: 다리 = "Neutral;OppositeLand;ActsAsRoad"). 태그는 정해진 목록에서만 고를 수 있어 오타가 오류로 잡힌다.
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

        public static readonly string[] BuildingFlags = { FlagRoad, FlagNeutral, FlagOppositeLand, FlagActsAsRoad, FlagTemple, FlagGoldFromAdjacent };

        private static readonly string[] BuildingRequired = { "Id", "Terrain" };
        private static readonly string[] BuildingOptional =
            { "Name", "Unlock", "Cost", "Population", "RequiredStructures", "AdjacentBuildings", "PopulationPerAdjacent", "Flags", "Task", "Description", "Wiki", "Note" };

        public static BuildingInfo[] ParseBuildings(string csvText, List<string> errors, string name = "Buildings.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, BuildingRequired, BuildingOptional, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var list = new List<BuildingInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                var flags = CsvTableReader.GetTags(t, r, "Flags", BuildingFlags, errors);
                var info = new BuildingInfo
                {
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Cost = CsvTableReader.GetInt(t, r, "Cost", 0, errors),
                    Population = CsvTableReader.GetInt(t, r, "Population", 0, errors),
                    Terrain = CsvTableReader.GetFlags<TileClass>(t, r, "Terrain", errors),
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructures"),
                    AdjacentBuildings = CsvTableReader.GetList(t, r, "AdjacentBuildings"),
                    PopulationPerAdjacent = CsvTableReader.GetInt(t, r, "PopulationPerAdjacent", 0, errors),
                    IsRoad = flags.Contains(FlagRoad),
                    AllowNeutral = flags.Contains(FlagNeutral),
                    RequiresOppositeLand = flags.Contains(FlagOppositeLand),
                    ActsAsRoad = flags.Contains(FlagActsAsRoad),
                    IsTemple = flags.Contains(FlagTemple),
                    ProducesGoldFromAdjacent = flags.Contains(FlagGoldFromAdjacent),
                    TaskId = CsvTableReader.Get(t, r, "Task"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                };
                if (info.Terrain == TileClass.None) CsvTableReader.Report(t, r, "Terrain", "지을 수 있는 지형이 없음", errors);
                list.Add(info);
            }

            // 인접 건물 칸은 같은 표의 Id를 가리켜야 한다(오타 방지).
            for (int r = 0; r < list.Count; r++)
                foreach (var adj in list[r].AdjacentBuildings)
                    if (!list.Exists(b => b.Id == adj)) CsvTableReader.Report(t, r, "AdjacentBuildings", $"표에 없는 건물 '{adj}'", errors);
            return list.ToArray();
        }

        // ---------- 타일 행동 (TileActions.csv) ----------

        private static readonly string[] TileActionRequired = { "Id", "Kind", "Terrain" };
        private static readonly string[] TileActionOptional =
            { "Name", "Unlock", "Cost", "RequiredStructures", "Population", "GoldGain", "Description", "Wiki", "Note" };

        public static TileActionInfo[] ParseTileActions(string csvText, List<string> errors, string name = "TileActions.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, TileActionRequired, TileActionOptional, errors);
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
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructures"),
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
