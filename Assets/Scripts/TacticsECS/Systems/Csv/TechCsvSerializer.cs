using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 기술트리 CSV(Assets/Resources/TechTree.csv) 텍스트 &lt;-&gt; TechNodeData 목록 변환만 담당하는 순수 파서/
    /// 작성기(UnitCsvSerializer와 같은 성격, 자체 상태 없음). 읽기는 헤더 이름 기준(CsvTableReader — 컬럼 순서 무관),
    /// 쓰기 컬럼 순서는 Header 한 곳에만 있다.
    /// Unlocks 칸은 여러 키를 담아야 해서 세미콜론으로 구분한다(UnitCsvSerializer의 Actions와 같은 이유).
    /// Effect 같은 설명 칸에 쉼표를 쓰고 싶으면 스프레드시트가 자동으로 붙이는 큰따옴표 인용("...,...")을
    /// 그대로 읽는다 — 엑셀/구글 시트에서 저장한 파일을 손대지 않고 넣을 수 있게 하기 위함이다.
    /// 컬럼 설명은 docs/TechTreeCsv.md.
    /// </summary>
    public static class TechCsvSerializer
    {
        private static readonly string[] Header =
        {
            "Id", "Name", "Branch", "Parent", "Tier", "Slot", "Icon", "CostBase", "CostPerCity", "Unlocks", "Effect"
        };

        /// <summary>헤더 이름으로 컬럼을 찾아(순서 무관 — CsvTableReader) 한 행씩 노드로 만든다. 빈 줄과 Id가 빈 줄은 무시한다.
        /// 숫자 칸이 비었거나 잘못됐으면 기본값(Tier 1, Slot 0, CostBase 4, CostPerCity = Tier)으로 채운다.
        /// Branch가 비어있으면 1티어는 자기 자신, 그 외는 Parent의 Branch를 따른다. errors를 주면 잘못된 칸을 기록한다.</summary>
        public static List<TechNodeData> Parse(string csvText, List<string> errors = null)
        {
            var nodes = new List<TechNodeData>();
            var t = CsvTableReader.Parse("TechTree.csv", csvText);
            if (t.Rows.Count == 0) return nodes;
            CsvTableReader.CheckColumns(t, new[] { "Id", "Unlocks" }, new[] { "Name", "Branch", "Parent", "Tier", "Slot", "Icon", "CostBase", "CostPerCity", "Effect", "Wiki", "Note" }, errors);
            CsvTableReader.CheckUniqueIds(t, "Id", errors);
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = CsvTableReader.Get(t, r, "Id");
                int tier = CsvTableReader.GetInt(t, r, "Tier", 1, errors);
                nodes.Add(new TechNodeData
                {
                    Id = id,
                    Name = CsvTableReader.Get(t, r, "Name", id),
                    Branch = CsvTableReader.Get(t, r, "Branch"),
                    ParentId = CsvTableReader.Get(t, r, "Parent"),
                    Tier = tier,
                    Slot = CsvTableReader.GetInt(t, r, "Slot", 0, errors),
                    Icon = CsvTableReader.Get(t, r, "Icon"),
                    CostBase = CsvTableReader.GetInt(t, r, "CostBase", GameRules.Tech.DefaultCostBase, errors),
                    CostPerCity = CsvTableReader.GetInt(t, r, "CostPerCity", tier, errors),
                    Unlocks = CsvTableReader.GetList(t, r, "Unlocks"),
                    Effect = CsvTableReader.Get(t, r, "Effect"),
                });
            }

            // Branch 비움 보정 — 부모가 앞에 있든 뒤에 있든 되도록 두 번 훑는다.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!string.IsNullOrEmpty(nodes[i].Branch)) continue;
                    var n = nodes[i];
                    if (string.IsNullOrEmpty(n.ParentId)) n.Branch = n.Id;
                    else
                    {
                        int p = nodes.FindIndex(x => x.Id == n.ParentId);
                        if (p >= 0 && !string.IsNullOrEmpty(nodes[p].Branch)) n.Branch = nodes[p].Branch;
                    }
                    nodes[i] = n;
                }
            }
            return nodes;
        }

        public static string Write(IReadOnlyList<TechNodeData> nodes)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header));
            foreach (var n in nodes)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Quote(n.Id), Quote(n.Name), Quote(n.Branch), Quote(n.ParentId),
                    n.Tier.ToString(CultureInfo.InvariantCulture), n.Slot.ToString(CultureInfo.InvariantCulture),
                    Quote(n.Icon), n.CostBase.ToString(CultureInfo.InvariantCulture), n.CostPerCity.ToString(CultureInfo.InvariantCulture),
                    Quote(CsvTableReader.Join(n.Unlocks)), Quote(n.Effect)
                }));
            }
            return sb.ToString();
        }

        private static string Quote(string value) => CsvTableReader.Quote(value);
    }
}
