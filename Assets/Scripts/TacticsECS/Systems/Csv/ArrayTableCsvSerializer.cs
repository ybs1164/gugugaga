using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 배열형 테이블(docs/spec/csv-common.md) CSV 텍스트 &lt;-&gt; 행 배열 변환. 읽기는 CsvTableReader(헤더 이름 기준, # 주석 행 허용)를 그대로
    /// 쓰고, 여기서는 배열형 공통 규칙만 더 확인한다:
    ///   - Index 컬럼이 0부터 1씩 늘어나는 행 순서와 같아야 한다(다르면 오류 — 다른 표가 이 번호로 가리키므로 빈 번호/중복은 곧 참조 오류).
    ///   - 참조 칸(…Index)은 정수, 비우면 -1(없음).
    ///   - 여러 값은 번호 붙은 반복 컬럼(Unlock1..N 등, 빈 칸은 건너뜀)을 정수/문자열 배열로.
    /// 표 사이 참조가 범위 안인지는 ArrayTableValidationSystem이 본다. 자체 상태는 없다.
    /// </summary>
    public static class ArrayTableCsvSerializer
    {
        // ---------- 읽기 ----------

        public static TechUnlockRow[] ParseTechUnlocks(string text, List<string> errors = null)
        {
            var t = Begin("TechUnlocks.csv", text, new[] { "Index", "Kind", "Category" },
                new[] { "Target", "BuildingIndex", "UnitIndex", "BoatIndex", "Name", "Description", "Note" }, null, errors);
            var rows = new TechUnlockRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new TechUnlockRow
                {
                    Index = r,
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TechUnlockKind.Passive, errors),
                    Category = CsvTableReader.Get(t, r, "Category"),
                    Target = CsvTableReader.Get(t, r, "Target"),
                    BuildingIndex = CsvTableReader.GetInt(t, r, "BuildingIndex", -1, errors),
                    UnitIndex = CsvTableReader.GetInt(t, r, "UnitIndex", -1, errors),
                    BoatIndex = CsvTableReader.GetInt(t, r, "BoatIndex", -1, errors),
                    Name = CsvTableReader.Get(t, r, "Name"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                };
            return rows;
        }

        // 번역 키 앞부분(docs/spec/csv/strings.md): 이 표들의 이름/설명은 Strings.csv의 "<앞부분>.<Id>.Name/Desc"에만 있다.
        public const string BuildingStringTable = "Building";
        public const string TechStringTable = "Tech";
        public const string TribeStringTable = "Tribe";
        public const string StartConditionStringTable = "StartCondition";

        /// <summary>건물 표(Buildings.csv). AdjacentBuildingIndex{n}은 같은 표의 Index — 여기서 Id로 바꿔 BuildingInfo.AdjacentBuildings에 담는다.
        /// UnlockKey는 비워 두고 TableLinkSystem.ApplyBuildingUnlocks가 TechUnlocks의 BuildingIndex로 채운다.</summary>
        public static BuildingInfo[] ParseBuildings(string text, List<string> errors = null, string name = "Buildings.csv")
        {
            var t = Begin(name, text, new[] { "Index", "Id", "Terrain" },
                new[] { "Cost", "Population", "RequiredStructure", "AdjacentBuildingIndex", "PopulationPerAdjacent", "Flag", "Task", "Wiki", "Note" },
                new[] { "Terrain", "RequiredStructure", "AdjacentBuildingIndex", "Flag" }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var rows = new BuildingInfo[t.Rows.Count];
            var adjacent = new int[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                var flags = CsvTableReader.GetTags(t, r, "Flag", GameTableCsvSerializer.BuildingFlags, errors);
                adjacent[r] = GetIndexList(t, r, "AdjacentBuildingIndex", errors);
                rows[r] = new BuildingInfo
                {
                    Id = id,
                    Name = LocalizationSystem.Name(BuildingStringTable, id),
                    UnlockKey = string.Empty,
                    Cost = CsvTableReader.GetInt(t, r, "Cost", 0, errors),
                    Population = CsvTableReader.GetInt(t, r, "Population", 0, errors),
                    Terrain = CsvTableReader.GetFlags<TileClass>(t, r, "Terrain", errors),
                    RequiredStructures = CsvTableReader.GetList(t, r, "RequiredStructure"),
                    PopulationPerAdjacent = CsvTableReader.GetInt(t, r, "PopulationPerAdjacent", 0, errors),
                    IsRoad = flags.Contains(GameTableCsvSerializer.FlagRoad),
                    AllowNeutral = flags.Contains(GameTableCsvSerializer.FlagNeutral),
                    RequiresOppositeLand = flags.Contains(GameTableCsvSerializer.FlagOppositeLand),
                    ActsAsRoad = flags.Contains(GameTableCsvSerializer.FlagActsAsRoad),
                    IsTemple = flags.Contains(GameTableCsvSerializer.FlagTemple),
                    ProducesStarsFromAdjacent = flags.Contains(GameTableCsvSerializer.FlagStarsFromAdjacent),
                    OnePerCity = flags.Contains(GameTableCsvSerializer.FlagOnePerCity),
                    TaskId = CsvTableReader.Get(t, r, "Task"),
                    Description = LocalizationSystem.Desc(BuildingStringTable, id),
                };
                if (rows[r].Terrain == TileClass.None) CsvTableReader.Report(t, r, "Terrain", "지을 수 있는 지형이 없음", errors);
            }
            for (int r = 0; r < rows.Length; r++)
            {
                var ids = new List<string>();
                foreach (int a in adjacent[r])
                {
                    if (a >= 0 && a < rows.Length) ids.Add(rows[a].Id);
                    else CsvTableReader.Report(t, r, "AdjacentBuildingIndex", $"Index {a}이(가) Buildings 범위 밖", errors);
                }
                rows[r].AdjacentBuildings = ids.ToArray();
            }
            return rows;
        }

        /// <summary>배 표(Boats.csv). 스탯 컬럼은 유닛 표와 같다(UnitCsvSerializer가 읽는다). Domain은 Water로 강제.</summary>
        public static BoatRow[] ParseBoats(string text, List<string> errors = null, string name = "Boats.csv")
        {
            var t = Begin(name, text, new[] { "Index", "Id", "Kind" }, UnitColumns.Concat(new[] { "Note" }).ToArray(),
                new[] { UnitCsvSerializer.ActionColumn }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var units = UnitCsvSerializer.Parse(text, errors, name);
            var rows = new BoatRow[Math.Min(t.Rows.Count, units.Count)];
            for (int r = 0; r < rows.Length; r++)
            {
                units[r].Domain = TerrainType.Water;
                rows[r] = new BoatRow { Index = r, Kind = CsvTableReader.GetEnum(t, r, "Kind", BoatKind.Special, errors), Unit = units[r] };
            }
            return rows;
        }

        /// <summary>유닛 표(Units.csv). BoatIndex(Boats Index, -1 = 뗏목)를 그 배의 Id로 바꿔 UnitCsvRow.Boat에 담는다.</summary>
        public static UnitCsvRow[] ParseUnits(string text, IReadOnlyList<BoatRow> boats, List<string> errors = null, string name = "Units.csv")
        {
            var t = Begin(name, text, new[] { "Index", "Id" }, UnitColumns.Concat(new[] { "BoatIndex", "Note" }).ToArray(),
                new[] { UnitCsvSerializer.ActionColumn }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            var units = UnitCsvSerializer.Parse(text, errors, name);
            for (int r = 0; r < units.Count && r < t.Rows.Count; r++)
            {
                int boat = CsvTableReader.GetInt(t, r, "BoatIndex", -1, errors);
                if (boat == -1) continue;
                if (boats != null && boat >= 0 && boat < boats.Count) units[r].Boat = boats[boat].Unit.Id;
                else CsvTableReader.Report(t, r, "BoatIndex", $"Index {boat}이(가) Boats 범위 밖", errors);
            }
            return units.ToArray();
        }

        /// <summary>유닛/배 표가 공통으로 갖는 컬럼(UnitCsvSerializer가 읽는 것).</summary>
        private static readonly string[] UnitColumns =
            { "MaxHp", "Defense", "BaseVisual", UnitCsvSerializer.ActionColumn, "Domain", "Move.Range", "Attack.Attack", "Attack.Range",
              "Heal.Amount", "Heal.Range", "Cost", "Trainable" };

        public static TechRow[] ParseTechs(string text, List<string> errors = null)
        {
            var t = Begin("Techs.csv", text, new[] { "Index", "Id", "CostBase", "CostPerCity" },
                new[] { "Icon", "Note" }, new[] { "Unlock" }, errors);
            var rows = new TechRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new TechRow
                {
                    Index = r,
                    Id = id,
                    Name = LocalizationSystem.Name(TechStringTable, id),
                    Icon = CsvTableReader.Get(t, r, "Icon"),
                    CostBase = CsvTableReader.GetInt(t, r, "CostBase", GameRules.Tech.DefaultCostBase, errors),
                    CostPerCity = CsvTableReader.GetInt(t, r, "CostPerCity", 0, errors),
                    Unlocks = GetIndexList(t, r, "Unlock", errors),
                    Description = LocalizationSystem.Desc(TechStringTable, id),
                };
            }
            return rows;
        }

        public static TechSlotRow[] ParseTechSlots(string text, List<string> errors = null)
        {
            var t = Begin("TechSlots.csv", text, new[] { "Index", "Id", "ParentIndex", "Tier", "Slot" }, new[] { "Note" }, null, errors);
            var rows = new TechSlotRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new TechSlotRow
                {
                    Index = r,
                    Id = CsvTableReader.Get(t, r, "Id"),
                    ParentIndex = CsvTableReader.GetInt(t, r, "ParentIndex", -1, errors),
                    Tier = CsvTableReader.GetInt(t, r, "Tier", 1, errors),
                    Slot = CsvTableReader.GetInt(t, r, "Slot", 0, errors),
                };
            return rows;
        }

        public static TechTreeLayoutRow[] ParseTechTreeLayout(string text, List<string> errors = null)
        {
            var t = Begin("TechTreeLayout.csv", text, new[] { "Index", "SlotIndex", "TechIndex" }, new[] { "Note" }, null, errors);
            var rows = new TechTreeLayoutRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new TechTreeLayoutRow
                {
                    Index = r,
                    SlotIndex = CsvTableReader.GetInt(t, r, "SlotIndex", -1, errors),
                    TechIndex = CsvTableReader.GetInt(t, r, "TechIndex", -1, errors),
                };
            return rows;
        }

        public static TechGroupRow[] ParseTechGroups(string text, List<string> errors = null)
        {
            var t = Begin("TechGroups.csv", text, new[] { "Index", "Id" }, new[] { "Name", "Description", "Note" }, new[] { "Tech" }, errors);
            var rows = new TechGroupRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new TechGroupRow
                {
                    Index = r,
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Description = CsvTableReader.Get(t, r, "Description"),
                    Techs = GetIndexList(t, r, "Tech", errors),
                };
            }
            return rows;
        }

        public static TribeRow[] ParseTribes(string text, List<string> errors = null)
        {
            var t = Begin("Tribes.csv", text, new[] { "Index", "Id" },
                new[] { "BiomeIndex", "TechGroupIndex", "StartStars", "StartConditionIndex", "Wiki", "Note" },
                new[] { "StartTech", "StartUnit" }, errors);
            var rows = new TribeRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new TribeRow
                {
                    Index = r,
                    Id = id,
                    Name = LocalizationSystem.Name(TribeStringTable, id),
                    Description = LocalizationSystem.Desc(TribeStringTable, id),
                    BiomeIndex = CsvTableReader.GetInt(t, r, "BiomeIndex", -1, errors),
                    TechGroupIndex = CsvTableReader.GetInt(t, r, "TechGroupIndex", -1, errors),
                    StartTechs = GetIndexList(t, r, "StartTech", errors),
                    StartStars = CsvTableReader.GetInt(t, r, "StartStars", GameRules.Economy.StartingStars, errors),
                    StartUnits = GetIndexList(t, r, "StartUnit", errors),
                    StartConditionIndex = CsvTableReader.GetInt(t, r, "StartConditionIndex", -1, errors),
                };
            }
            return rows;
        }

        public static StartConditionRow[] ParseStartConditions(string text, List<string> errors = null)
        {
            var t = Begin("StartConditions.csv", text, new[] { "Index", "Id" }, new[] { "Note" }, new[] { "Rule" }, errors);
            var rows = new StartConditionRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new StartConditionRow
                {
                    Index = r,
                    Id = id,
                    Name = LocalizationSystem.Name(StartConditionStringTable, id),
                    Description = LocalizationSystem.Desc(StartConditionStringTable, id),
                    Rules = GetIndexList(t, r, "Rule", errors),
                };
            }
            return rows;
        }

        public static StartConditionRuleRow[] ParseStartConditionRules(string text, List<string> errors = null)
        {
            var t = Begin("StartConditionRules.csv", text, new[] { "Index", "Kind", "Target" },
                new[] { "Id", "TerrainType", "Count", "MinDistance", "MaxDistance", "MapType", "Description", "Note" }, new[] { "AllowedTile" }, errors);
            var rows = new StartConditionRuleRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new StartConditionRuleRow
                {
                    Index = r,
                    Id = CsvTableReader.Get(t, r, "Id"),
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", StartRuleKind.Tile, errors),
                    Target = CsvTableReader.Get(t, r, "Target"),
                    TerrainType = CsvTableReader.GetEnum(t, r, "TerrainType", TerrainType.Land, errors),
                    Count = CsvTableReader.GetInt(t, r, "Count", 1, errors),
                    MinDistance = CsvTableReader.GetInt(t, r, "MinDistance", 1, errors),
                    MaxDistance = CsvTableReader.GetInt(t, r, "MaxDistance", 1, errors),
                    MapType = CsvTableReader.Get(t, r, "MapType"),
                    AllowedTiles = CsvTableReader.GetList(t, r, "AllowedTile"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                };
            return rows;
        }

        /// <summary>공통 시작: 컬럼 확인 + Index가 행 순서(0,1,2...)와 같은지 확인.</summary>
        private static CsvTable Begin(string name, string text, string[] required, string[] optional, string[] listColumns, List<string> errors)
        {
            var t = CsvTableReader.Parse(name, text);
            if (t.Rows.Count == 0) return t;
            CsvTableReader.CheckColumns(t, required, optional, errors, listColumns);
            for (int r = 0; r < t.Rows.Count; r++)
            {
                int index = CsvTableReader.GetInt(t, r, "Index", -1, errors);
                if (index != r) CsvTableReader.Report(t, r, "Index", $"Index {index}이(가) 행 순서 {r}와 다름 — 배열형 표는 0부터 빈틈없이 번호를 매긴다", errors);
            }
            return t;
        }

        /// <summary>반복 컬럼 baseName1..N의 정수 목록(빈 칸은 건너뜀).</summary>
        private static int[] GetIndexList(CsvTable t, int r, string baseName, List<string> errors)
        {
            var list = new List<int>();
            foreach (var s in CsvTableReader.GetList(t, r, baseName))
            {
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) list.Add(v);
                else CsvTableReader.Report(t, r, baseName, $"정수(Index)가 아님 '{s}'", errors);
            }
            return list.ToArray();
        }

        // ---------- 쓰기(왕복/템플릿 내보내기용) ----------

        public static string WriteTechUnlocks(IReadOnlyList<TechUnlockRow> rows) =>
            Write(new[] { "Index", "Kind", "Category", "Target", "BuildingIndex", "UnitIndex", "BoatIndex", "Name", "Description" }, rows, null,
                r => new[] { I(r.Index), r.Kind.ToString(), r.Category, r.Target, Opt(r.BuildingIndex), Opt(r.UnitIndex), Opt(r.BoatIndex), r.Name, r.Description },
                null, null);

        public static string WriteTechs(IReadOnlyList<TechRow> rows) =>
            Write(new[] { "Index", "Id", "Icon", "CostBase", "CostPerCity" }, rows, "Unlock",
                r => new[] { I(r.Index), r.Id, r.Icon, I(r.CostBase), I(r.CostPerCity) },
                r => Ints(r.Unlocks), null);

        public static string WriteTechSlots(IReadOnlyList<TechSlotRow> rows) =>
            Write(new[] { "Index", "Id", "ParentIndex", "Tier", "Slot" }, rows, null,
                r => new[] { I(r.Index), r.Id, I(r.ParentIndex), I(r.Tier), I(r.Slot) }, null, null);

        public static string WriteTechTreeLayout(IReadOnlyList<TechTreeLayoutRow> rows) =>
            Write(new[] { "Index", "SlotIndex", "TechIndex" }, rows, null,
                r => new[] { I(r.Index), I(r.SlotIndex), I(r.TechIndex) }, null, null);

        public static string WriteTechGroups(IReadOnlyList<TechGroupRow> rows) =>
            Write(new[] { "Index", "Id", "Name", "Description" }, rows, "Tech",
                r => new[] { I(r.Index), r.Id, r.Name, r.Description }, r => Ints(r.Techs), null);

        public static string WriteTribes(IReadOnlyList<TribeRow> rows)
        {
            int techCount = CsvTableReader.MaxCount(rows, r => r.StartTechs?.Length ?? 0);
            int unitCount = CsvTableReader.MaxCount(rows, r => r.StartUnits?.Length ?? 0);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "Index", "Id", "BiomeIndex", "TechGroupIndex" }
                .Concat(CsvTableReader.ListHeader("StartTech", techCount)).Append("StartStars")
                .Concat(CsvTableReader.ListHeader("StartUnit", unitCount)).Append("StartConditionIndex")));
            foreach (var r in rows)
            {
                var cells = new List<string> { I(r.Index), Q(r.Id), I(r.BiomeIndex), I(r.TechGroupIndex) };
                cells.AddRange(CsvTableReader.ListCells(Ints(r.StartTechs), techCount));
                cells.Add(I(r.StartStars));
                cells.AddRange(CsvTableReader.ListCells(Ints(r.StartUnits), unitCount));
                cells.Add(I(r.StartConditionIndex));
                sb.AppendLine(string.Join(",", cells));
            }
            return sb.ToString();
        }

        public static string WriteStartConditions(IReadOnlyList<StartConditionRow> rows) =>
            Write(new[] { "Index", "Id" }, rows, "Rule",
                r => new[] { I(r.Index), r.Id }, r => Ints(r.Rules), null);

        public static string WriteStartConditionRules(IReadOnlyList<StartConditionRuleRow> rows) =>
            Write(new[] { "Index", "Id", "Kind", "Target", "TerrainType", "Count", "MinDistance", "MaxDistance", "MapType" }, rows, "AllowedTile",
                r => new[] { I(r.Index), r.Id, r.Kind.ToString(), r.Target, r.TerrainType.ToString(), I(r.Count), I(r.MinDistance), I(r.MaxDistance), r.MapType },
                r => r.AllowedTiles, r => new[] { r.Description }, "Description");

        /// <summary>고정 컬럼 + 반복 컬럼 하나(listBase1..N) + 꼬리 컬럼(설명 등)으로 쓴다.</summary>
        private static string Write<T>(string[] header, IReadOnlyList<T> rows, string listBase, Func<T, string[]> head,
            Func<T, string[]> list, Func<T, string[]> tail, string tailHeader = null)
        {
            int count = listBase == null ? 0 : CsvTableReader.MaxCount(rows, r => list(r)?.Length ?? 0);
            var sb = new StringBuilder();
            IEnumerable<string> h = header;
            if (listBase != null) h = h.Concat(CsvTableReader.ListHeader(listBase, count));
            if (tailHeader != null) h = h.Append(tailHeader);
            sb.AppendLine(string.Join(",", h));
            foreach (var r in rows)
            {
                var cells = new List<string>(head(r).Select(Q));
                if (listBase != null) cells.AddRange(CsvTableReader.ListCells(list(r), count));
                if (tail != null) cells.AddRange(tail(r).Select(Q));
                sb.AppendLine(string.Join(",", cells));
            }
            return sb.ToString();
        }

        private static string[] Ints(int[] values) => values == null ? new string[0] : values.Select(I).ToArray();
        /// <summary>-1(없음)은 빈 칸으로.</summary>
        private static string Opt(int v) => v < 0 ? string.Empty : I(v);
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Q(string s) => CsvTableReader.Quote(s);
    }
}
