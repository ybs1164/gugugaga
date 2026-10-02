using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 플레이어가 고른 언어(번역 표 Strings.csv의 언어 열 이름)를 PlayerPrefs에 저장/복원한다 — docs/spec/csv/strings.md#언어-고르기.
    /// 처음 실행이면 시스템 언어가 한국어일 때 원문 열, 아니면 "en". GameDataLoader.LoadAll 전에 Apply를 불러야 표 이름까지 그 언어로 읽힌다.
    /// </summary>
    public static class LanguagePreference
    {
        private const string PrefKey = "Language";
        private const string NonKoreanDefault = "en";

        public static void Apply()
        {
            string fallback = Application.systemLanguage == SystemLanguage.Korean ? StringTable.SourceLanguage : NonKoreanDefault;
            StringTable.Language = PlayerPrefs.GetString(PrefKey, fallback);
        }

        public static void Save(string language)
        {
            PlayerPrefs.SetString(PrefKey, language);
            PlayerPrefs.Save();
        }
    }
}
