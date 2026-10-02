using System;
using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 번역 표(Strings.csv — docs/spec/csv/strings.md) CSV 텍스트 -&gt; StringTable 값. 헤더의 Key/Note가 아닌 열이 전부 언어 열이다.
    /// 칸 안의 "\n"(역슬래시+n)은 줄바꿈으로 바꾼다(CSV 한 줄 = 한 행). 고른 언어 칸이 비면 원문 열 값을 쓴다. 자체 상태는 없다.
    /// </summary>
    public static class StringTableCsvSerializer
    {
        public const string KeyColumn = "Key";
        private static readonly string[] MemoColumns = { "Note" };

        /// <summary>language가 표에 없으면 원문 열을 쓴다(StringTableValues.Language에 실제로 쓴 열).</summary>
        public static StringTableValues Parse(string text, string language, List<string> errors = null, string name = "Strings.csv")
        {
            var t = CsvTableReader.Parse(name, text);
            var languages = new List<string>();
            foreach (var h in t.Header ?? new string[0])
                if (h.Length > 0 && !string.Equals(h, KeyColumn, StringComparison.OrdinalIgnoreCase) &&
                    Array.FindIndex(MemoColumns, m => string.Equals(m, h, StringComparison.OrdinalIgnoreCase)) < 0)
                    languages.Add(h);

            var result = new StringTableValues
            {
                Languages = languages.ToArray(),
                Current = new Dictionary<string, string>(),
                Source = new Dictionary<string, string>(),
                LanguageNames = new Dictionary<string, string>(),
            };
            if (t.Header == null) return result;
            if (!CsvTableReader.HasColumn(t, KeyColumn)) errors?.Add($"{name}: 필수 컬럼 '{KeyColumn}'이(가) 없음");
            string source = languages.Contains(StringTable.SourceLanguage) ? StringTable.SourceLanguage : languages.Count > 0 ? languages[0] : null;
            if (source == null) { errors?.Add($"{name}: 언어 열이 없음"); return result; }
            if (source != StringTable.SourceLanguage) errors?.Add($"{name}: 원문 열 '{StringTable.SourceLanguage}'이(가) 없어 '{source}'을(를) 원문으로 씀");
            result.Language = languages.Contains(language) ? language : source;
            CsvTableReader.CheckUniqueIds(t, KeyColumn, errors);

            for (int r = 0; r < t.Rows.Count; r++)
            {
                string key = CsvTableReader.Get(t, r, KeyColumn);
                if (key.Length == 0) continue;
                string src = Unescape(CsvTableReader.Get(t, r, source));
                string cur = Unescape(CsvTableReader.Get(t, r, result.Language));
                if (src.Length == 0) CsvTableReader.Report(t, r, source, $"원문이 비어 있음 '{key}'", errors);
                result.Source[key] = src;
                result.Current[key] = cur.Length > 0 ? cur : src;
                if (key == StringTable.LanguageNameKey)
                    foreach (var lang in languages) result.LanguageNames[lang] = CsvTableReader.Get(t, r, lang);
            }
            return result;
        }

        private static string Unescape(string s) => s.Replace("\\n", "\n");
    }
}
