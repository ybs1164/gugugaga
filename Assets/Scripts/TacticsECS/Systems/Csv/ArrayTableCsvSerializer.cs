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
            var t = Begin("TechUnlocks.csv", text, new[] { "Index", "Kind", "Category" }, new[] { "Target", "Name", "Description", "Note" }, null, errors);
            var rows = new TechUnlockRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new TechUnlockRow
                {
                    Index = r,
                    Kind = CsvTableReader.GetEnum(t, r, "Kind", TechUnlockKind.Passive, errors),
                    Category = CsvTableReader.Get(t, r, "Category"),
                    Target = CsvTableReader.Get(t, r, "Target"),
                    Name = CsvTableReader.Get(t, r, "Name"),
                    Description = CsvTableReader.Get(t, r, "Description"),
                };
            return rows;
        }

        public static TechRow[] ParseTechs(string text, List<string> errors = null)
        {
            var t = Begin("Techs.csv", text, new[] { "Index", "Id", "CostBase", "CostPerCity" },
                new[] { "Name", "Icon", "Description", "Note" }, new[] { "Unlock" }, errors);
            var rows = new TechRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new TechRow
                {
                    Index = r,
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Icon = CsvTableReader.Get(t, r, "Icon"),
                    CostBase = CsvTableReader.GetInt(t, r, "CostBase", GameRules.Tech.DefaultCostBase, errors),
                    CostPerCity = CsvTableReader.GetInt(t, r, "CostPerCity", 0, errors),
                    Unlocks = GetIndexList(t, r, "Unlock", errors),
                    Description = CsvTableReader.Get(t, r, "Description"),
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
                new[] { "Name", "Description", "BiomeIndex", "TechGroupIndex", "StartStars", "StartConditionIndex", "Wiki", "Note" },
                new[] { "StartTech", "StartUnit" }, errors);
            var rows = new TribeRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new TribeRow
                {
                    Index = r,
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Description = CsvTableReader.Get(t, r, "Description"),
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
            var t = Begin("StartConditions.csv", text, new[] { "Index", "Id" }, new[] { "Name", "Description", "Note" }, new[] { "Rule" }, errors);
            var rows = new StartConditionRow[t.Rows.Count];
            for (int r = 0; r < rows.Length; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                rows[r] = new StartConditionRow
                {
                    Index = r,
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Description = CsvTableReader.Get(t, r, "Description"),
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
            Write(new[] { "Index", "Kind", "Category", "Target", "Name", "Description" }, rows, null,
                r => new[] { I(r.Index), r.Kind.ToString(), r.Category, r.Target, r.Name, r.Description }, null, null);

        public static string WriteTechs(IReadOnlyList<TechRow> rows) =>
            Write(new[] { "Index", "Id", "Name", "Icon", "CostBase", "CostPerCity" }, rows, "Unlock",
                r => new[] { I(r.Index), r.Id, r.Name, r.Icon, I(r.CostBase), I(r.CostPerCity) },
                r => Ints(r.Unlocks), r => new[] { r.Description }, "Description");

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
            sb.AppendLine(string.Join(",", new[] { "Index", "Id", "Name", "Description", "BiomeIndex", "TechGroupIndex" }
                .Concat(CsvTableReader.ListHeader("StartTech", techCount)).Append("StartStars")
                .Concat(CsvTableReader.ListHeader("StartUnit", unitCount)).Append("StartConditionIndex")));
            foreach (var r in rows)
            {
                var cells = new List<string> { I(r.Index), Q(r.Id), Q(r.Name), Q(r.Description), I(r.BiomeIndex), I(r.TechGroupIndex) };
                cells.AddRange(CsvTableReader.ListCells(Ints(r.StartTechs), techCount));
                cells.Add(I(r.StartStars));
                cells.AddRange(CsvTableReader.ListCells(Ints(r.StartUnits), unitCount));
                cells.Add(I(r.StartConditionIndex));
                sb.AppendLine(string.Join(",", cells));
            }
            return sb.ToString();
        }

        public static string WriteStartConditions(IReadOnlyList<StartConditionRow> rows) =>
            Write(new[] { "Index", "Id", "Name", "Description" }, rows, "Rule",
                r => new[] { I(r.Index), r.Id, r.Name, r.Description }, r => Ints(r.Rules), null);

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
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Q(string s) => CsvTableReader.Quote(s);
    }
}
