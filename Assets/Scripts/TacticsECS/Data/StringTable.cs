using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 번역 표(Assets/Resources/Strings.csv — docs/spec/csv/strings.md)를 담는 정적 저장소. 게임에 보이는 문자열은 전부 이 표에만 있다.
    /// GameDataLoader가 Language 열을 Current에, 원문 열(SourceLanguage)을 Source에 채운다. 조회는 LocalizationSystem. 값만 있다(CLAUDE.md 규칙 2).
    /// </summary>
    public static class StringTable
    {
        public const string CsvResourcePath = "Strings";

        /// <summary>원문 열. 다른 언어 칸이 비면 이 열 값을 쓴다. 프리팹에 구운 글자도 이 열 값이다(실행 중에는 각 HUD가 키로 다시 채운다).</summary>
        public const string SourceLanguage = "ko";

        /// <summary>지금 쓰는 언어 열 이름(헤더 그대로, 예: "ko", "en"). 표에 없는 언어면 원문.</summary>
        public static string Language = SourceLanguage;

        /// <summary>표 헤더에 있는 언어 열 이름들(Key/Note를 뺀 순서 그대로).</summary>
        public static string[] Languages = { SourceLanguage };

        /// <summary>언어 열 이름 -&gt; 그 열의 LanguageNameKey 칸(언어 선택 버튼에 그 언어 자신의 이름으로 보인다).</summary>
        public static Dictionary<string, string> LanguageNames = new Dictionary<string, string>();

        public const string LanguageNameKey = "Language.Name";

        /// <summary>Key -&gt; 지금 언어 문자열.</summary>
        public static Dictionary<string, string> Current = new Dictionary<string, string>();

        /// <summary>Key -&gt; 원문 문자열.</summary>
        public static Dictionary<string, string> Source = new Dictionary<string, string>();
    }
}
