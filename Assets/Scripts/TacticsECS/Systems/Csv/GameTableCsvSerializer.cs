using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 게임 규칙 표 CSV(Assets/Resources/*.csv) -&gt; Core 값 타입 배열 변환. 표마다 컬럼 이름 목록(스키마)과 변환 한
    /// 함수만 있고, 칸 읽기/오류 보고는 CsvTableReader가 한다. 이름/설명은 번역 표(LocalizationSystem — "TileAction/CityReward/Task.&lt;Id&gt;.Name/Desc"). 자체 상태는 없다. 컬럼 설명은 docs/spec/csv/tile-actions.md, city-rewards.md, tasks.md.
    ///
    /// 표 형식(1차 비교분석 결과 — docs/history/GameDataCsv.md): "넓은 표 + Flags 목록" 하이브리드.
    ///   - 숫자/문자열 속성은 컬럼 하나씩(스프레드시트에서 정렬·필터·합계가 된다).
    ///   - 대부분의 행에 해당 없는 드문 불리언 속성은 태그로 나열한다(예: 다리 = Flag1 Neutral, Flag2 OppositeLand, Flag3 ActsAsRoad).
    ///     태그는 정해진 목록에서만 고를 수 있어 오타가 오류로 잡힌다.
    ///   - 여러 값을 갖는 속성(Terrain/RequiredStructure)은 한 칸에 묶지 않고 번호 붙은 반복 컬럼
    ///     (Terrain1, Terrain2 ...)으로 나열한다(CLAUDE.md 규칙 6 — CsvTableReader.GetList). 옛 한 칸 목록 컬럼
    ///     (RequiredStructures, "a;b")도 호환용으로 읽는다.
    /// </summary>
    public static class GameTableCsvSerializer
    {
        // ---------- 건물 Flag 태그 (Tables/Buildings.csv — 파서는 ArrayTableCsvSerializer.ParseBuildings) ----------

        public const string FlagRoad = "Road";
        public const string FlagNeutral = "Neutral";
        public const string FlagOppositeLand = "OppositeLand";
        public const string FlagActsAsRoad = "ActsAsRoad";
        public const string FlagTemple = "Temple";
        public const string FlagStarsFromAdjacent = "StarsFromAdjacent";
        public const string FlagOnePerCity = "OnePerCity";
        public const string FlagEmbassy = "Embassy";

        public static readonly string[] BuildingFlags = { FlagRoad, FlagNeutral, FlagOppositeLand, FlagActsAsRoad, FlagTemple, FlagStarsFromAdjacent, FlagOnePerCity, FlagEmbassy };

        // ---------- 타일 행동 (TileActions.csv) ----------

        private static readonly string[] TileActionRequired = { "Id", "Kind", "Terrain" };
        private static readonly string[] TileActionOptional =
            { "Unlock", "Cost", "RequiredStructure", "Population", "StarsGain", "Wiki", "Note" };
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
                    Name = LocalizationSystem.Name("TileAction", id),
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TileActionKind.Harvest, errors),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Cost = CsvTableReader.GetInt(t, r, "Cost", 0, errors),
                    Terrain = CsvTableReader.GetFlags<TileClass>(t, r, "Terrain", errors),
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructure"),
                    Population = CsvTableReader.GetInt(t, r, "Population", 0, errors),
                    StarsGain = CsvTableReader.GetInt(t, r, "StarsGain", 0, errors),
                    Description = LocalizationSystem.Desc("TileAction", id, null, c => CsvTableReader.Get(t, r, c)),
                });
            }
            return list.ToArray();
        }

        // ---------- 도시 보상 (CityRewards.csv) ----------

        public static CityRewardInfo[] ParseCityRewards(string csvText, List<string> errors, string name = "CityRewards.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Level", "Reward" }, new[] { "Amount", "Wiki", "Note" }, errors);
            var list = new List<CityRewardInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                var type = CsvTableReader.GetEnum(t, r, "Reward", CityRewardType.Resources, errors);
                list.Add(new CityRewardInfo
                {
                    Level = CsvTableReader.GetInt(t, r, "Level", 2, errors),
                    Type = type,
                    Name = LocalizationSystem.Name("CityReward", type.ToString()),
                    Amount = CsvTableReader.GetInt(t, r, "Amount", 0, errors),
                    Description = LocalizationSystem.Desc("CityReward", type.ToString(), null, c => CsvTableReader.Get(t, r, c)),
                });
            }
            return list.ToArray();
        }

        // ---------- 과업 (Tasks.csv) ----------

        public static TaskInfo[] ParseTasks(string csvText, List<string> errors, string name = "Tasks.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Id", "Kind" }, new[] { "Threshold", "Unlock", "Wiki", "Note" }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var list = new List<TaskInfo>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                list.Add(new TaskInfo
                {
                    Id = id,
                    Name = LocalizationSystem.Name("Task", id),
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TaskKind.Kills, errors),
                    Threshold = CsvTableReader.GetInt(t, r, "Threshold", 0, errors),
                    UnlockKey = CsvTableReader.Get(t, r, "Unlock"),
                    Description = LocalizationSystem.Desc("Task", id, null, c => CsvTableReader.Get(t, r, c)),
                });
            }
            return list.ToArray();
        }
    }
}
