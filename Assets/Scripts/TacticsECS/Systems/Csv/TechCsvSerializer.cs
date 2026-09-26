using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 기술트리 CSV(Assets/Resources/TechTree.csv) 텍스트 &lt;-&gt; TechNodeData 목록 변환만 담당하는 순수 파서/
    /// 작성기(UnitCsvSerializer와 같은 성격, 자체 상태 없음). 컬럼 순서/이름은 Header 한 곳에만 있다.
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

        private const char ListSeparator = ';';

        /// <summary>첫 줄(헤더)을 건너뛰고 나머지 줄을 한 노드씩 파싱한다. 빈 줄과 Id가 빈 줄은 무시한다.
        /// 숫자 칸이 비었거나 잘못됐으면 기본값(Tier 1, Slot 0, CostBase 4, CostPerCity = Tier)으로 채운다.
        /// Branch가 비어있으면 1티어는 자기 자신, 그 외는 Parent의 Branch를 따른다.</summary>
        public static List<TechNodeData> Parse(string csvText)
        {
            var nodes = new List<TechNodeData>();
            if (string.IsNullOrWhiteSpace(csvText)) return nodes;

            var lines = csvText.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                var c = SplitCsvLine(line);
                string id = Col(c, 0);
                if (id.Length == 0) continue;

                int tier = ParseInt(Col(c, 4), 1);
                nodes.Add(new TechNodeData
                {
                    Id = id,
                    Name = Col(c, 1).Length > 0 ? Col(c, 1) : id,
                    Branch = Col(c, 2),
                    ParentId = Col(c, 3),
                    Tier = tier,
                    Slot = ParseInt(Col(c, 5), 0),
                    Icon = Col(c, 6),
                    CostBase = ParseInt(Col(c, 7), TechTreeDefinition.DefaultCostBase),
                    CostPerCity = ParseInt(Col(c, 8), tier),
                    Unlocks = ParseList(Col(c, 9)),
                    Effect = Col(c, 10),
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
                    Quote(string.Join(ListSeparator.ToString(), n.Unlocks ?? new string[0])), Quote(n.Effect)
                }));
            }
            return sb.ToString();
        }

        /// <summary>큰따옴표 인용(안의 쉼표 허용, "" = 따옴표 한 개)을 지원하는 한 줄 분리기.</summary>
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result;
        }

        private static string Quote(string value)
        {
            value ??= string.Empty;
            if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Col(List<string> c, int i) => i < c.Count ? c[i].Trim() : string.Empty;

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        private static string[] ParseList(string s)
        {
            var list = new List<string>();
            foreach (var part in s.Split(ListSeparator))
            {
                var t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }
    }
}
