using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>번역 표 CSV 하나를 읽은 값(StringTableCsvSerializer.Parse 결과 — 필드 뜻은 StringTable과 같다). 값만 있다.</summary>
    public struct StringTableValues
    {
        public string[] Languages;

        /// <summary>실제로 Current를 채운 언어 열(요청한 언어가 표에 없으면 원문 열).</summary>
        public string Language;
        public Dictionary<string, string> Current;
        public Dictionary<string, string> Source;
        public Dictionary<string, string> LanguageNames;
    }
}
