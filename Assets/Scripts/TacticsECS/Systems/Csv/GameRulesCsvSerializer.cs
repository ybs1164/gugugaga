using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace TacticsECS
{
    /// <summary>
    /// 스칼라 규칙 CSV(Assets/Resources/GameRules.csv, "Key,Value,Wiki,Description,Note") -&gt; Data/GameRules의 정적 필드.
    /// Key "도메인.이름"을 GameRules의 중첩 클래스(도메인)와 그 public static 필드(이름)로 리플렉션해서 찾으므로, 규칙을
    /// 추가할 때 이 파일은 고칠 필요가 없다. 자체 상태는 없다.
    ///   - 모르는 Key, 읽을 수 없는 Value, CSV에 행이 없는 규칙(코드 기본값 사용)은 errors에 남긴다.
    ///   - Wiki 칸이 있고 Value와 다르면 "원문과 다른 점"이므로 Note(이유)가 있어야 한다 — 없으면 errors(CLAUDE.md 규칙 4).
    ///     Wiki 칸이 빈 규칙은 프로젝트 고유 규칙이며 역시 Note로 이유를 적는다.
    /// </summary>
    public static class GameRulesCsvSerializer
    {
        public static List<GameRuleCsvRow> ParseRows(string csvText, List<string> errors, string name = "GameRules.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Key", "Value" }, new[] { "Wiki", "Description", "Note" }, errors);
            CsvTableReader.CheckUniqueIds(t, "Key", errors);
            var rows = new List<GameRuleCsvRow>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                var row = new GameRuleCsvRow
                {
                    Key = CsvTableReader.Get(t, r, "Key"),
                    Value = CsvTableReader.Get(t, r, "Value"),
                    Wiki = CsvTableReader.Get(t, r, "Wiki"),
                    Note = CsvTableReader.Get(t, r, "Note"),
                };
                if (row.Note.Length == 0 && (row.Wiki.Length == 0 || row.Wiki != row.Value))
                    CsvTableReader.Report(t, r, "Note", row.Wiki.Length == 0 ? "프로젝트 고유 규칙(Wiki 빈칸)은 Note에 이유를 적어야 함"
                                                                           : $"위키 값({row.Wiki})과 달라 Note에 이유를 적어야 함", errors);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>CSV 값을 GameRules 필드에 쓴다. 적용한 규칙 수를 돌려준다.</summary>
        public static int Apply(string csvText, List<string> errors, string name = "GameRules.csv")
        {
            var rows = ParseRows(csvText, errors, name);
            var fields = AllFields();
            var seen = new HashSet<string>();
            int applied = 0;
            foreach (var row in rows)
            {
                if (!fields.TryGetValue(row.Key, out var field)) { errors?.Add($"{name}: 모르는 규칙 '{row.Key}' (무시됨)"); continue; }
                seen.Add(row.Key);
                if (TryConvert(row.Value, field.FieldType, out object value)) { field.SetValue(null, value); applied++; }
                else errors?.Add($"{name}: '{row.Key}' 값 '{row.Value}'을(를) {field.FieldType.Name}(으)로 읽을 수 없음");
            }
            foreach (var key in fields.Keys)
                if (!seen.Contains(key)) errors?.Add($"{name}: '{key}' 행이 없음 — 코드 기본값 {fields[key].GetValue(null)} 사용");
            return applied;
        }

        /// <summary>"도메인.이름" -&gt; GameRules 필드. 검증에도 쓴다.</summary>
        public static Dictionary<string, FieldInfo> AllFields()
        {
            var map = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (var domain in typeof(GameRules).GetNestedTypes(BindingFlags.Public))
                foreach (var f in domain.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (!f.IsLiteral && !f.IsInitOnly) map[domain.Name + "." + f.Name] = f;
            return map;
        }

        private static bool TryConvert(string s, Type type, out object value)
        {
            value = null;
            if (type == typeof(int)) { bool ok = int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v); value = v; return ok; }
            if (type == typeof(float)) { bool ok = float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v); value = v; return ok; }
            if (type == typeof(bool))
            {
                if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
                return false;
            }
            if (type == typeof(string)) { value = s; return true; }
            if (type.IsEnum)
            {
                try { value = Enum.Parse(type, s, true); return Enum.IsDefined(type, value); }
                catch (ArgumentException) { return false; }
            }
            return false;
        }
    }
}
