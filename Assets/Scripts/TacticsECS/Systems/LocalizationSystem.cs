using System;
using System.Globalization;

namespace TacticsECS
{
    /// <summary>
    /// 번역 표(StringTable) 조회. 화면에 보이는 문자열은 코드에 직접 적지 않고 여기서 키로 꺼낸다 — 키 규칙은 docs/spec/csv/strings.md.
    /// 없는 키는 fallback(기본은 키 자신)을 돌려줘 빠진 번역이 화면에서 바로 보이게 한다. 자체 상태는 없다.
    /// </summary>
    public static class LocalizationSystem
    {
        /// <summary>지금 언어의 문자열. 키가 없거나 칸이 비면 fallback(null이면 키). {Rule.*}, {표.Id.컬럼} 자리표시자는 채운다(TextPlaceholderSystem).</summary>
        public static string T(string key, string fallback = null)
        {
            if (!string.IsNullOrEmpty(key) && StringTable.Current.TryGetValue(key, out var value) && value.Length > 0) return TextPlaceholderSystem.Fill(value);
            return fallback ?? key;
        }

        /// <summary>string.Format 자리표시자({0}, {1} ...)를 채운 문자열. 형식이 깨졌으면 채우지 않은 문자열.</summary>
        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch (FormatException) { return format; }
        }

        public static bool Has(string key) => !string.IsNullOrEmpty(key) && StringTable.Current.ContainsKey(key);

        /// <summary>원문(StringTable.SourceLanguage) 문자열 — 프리팹에 구울 글자(UIPrefabSetup).</summary>
        public static string Source(string key) =>
            !string.IsNullOrEmpty(key) && StringTable.Source.TryGetValue(key, out var value) && value.Length > 0 ? value : key;

        /// <summary>데이터 표 행의 이름: "&lt;table&gt;.&lt;id&gt;.Name". 번역이 없으면 fallback, 그것도 비면 id.</summary>
        public static string Name(string table, string id, string fallback = null) =>
            T(table + "." + id + ".Name", string.IsNullOrEmpty(fallback) ? id : fallback);

        /// <summary>데이터 표 행의 설명: "&lt;table&gt;.&lt;id&gt;.Desc". 번역이 없으면 fallback(없으면 빈 문자열).
        /// self는 그 행의 CSV 칸(컬럼 이름 -&gt; 값) — 설명 속 {컬럼} 자리표시자를 채운다.</summary>
        public static string Desc(string table, string id, string fallback = null, Func<string, string> self = null) =>
            TextPlaceholderSystem.Fill(T(table + "." + id + ".Desc", fallback ?? string.Empty), self);

        /// <summary>언어 열 이름(ko/en ...)의 표시 이름 — 그 언어 열의 StringTable.LanguageNameKey 칸. 없으면 열 이름.</summary>
        public static string LanguageName(string language) =>
            language != null && StringTable.LanguageNames.TryGetValue(language, out var name) && name.Length > 0 ? name : language;

        /// <summary>언어 목록에서 language 다음 언어(끝이면 처음). 언어 선택 버튼이 누를 때마다 돌린다.</summary>
        public static string NextLanguage(string language)
        {
            var list = StringTable.Languages;
            if (list == null || list.Length == 0) return StringTable.SourceLanguage;
            int i = Array.IndexOf(list, language);
            return list[(i + 1) % list.Length];
        }
    }
}
