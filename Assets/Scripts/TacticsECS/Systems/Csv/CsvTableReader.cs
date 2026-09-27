using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 헤더 기반 CSV 읽기 공용 도구 — 모든 게임 데이터 표(기술/건물/타일 행동/규칙/유닛...)가 같은 규칙으로 읽힌다.
    ///   - 컬럼은 이름으로 찾는다(순서 무관). 없는 컬럼은 기본값, 모르는 컬럼은 오류 목록에 경고로 남긴다.
    ///   - 큰따옴표 인용("쉼표, 포함", "" = 따옴표 한 개)을 지원한다 — 엑셀/구글 시트가 저장한 파일을 그대로 읽기 위함.
    ///   - 한 칸에 여러 값은 세미콜론(;)으로 나열한다(Unlocks, Terrain, Flags ...).
    ///   - 숫자/열거형이 잘못되면 "파일:줄 컬럼: 값" 형식의 메시지를 errors에 넣고 기본값을 쓴다(게임은 멈추지 않는다).
    /// 자체 상태는 없다.
    /// </summary>
    public static class CsvTableReader
    {
        public const char ListSeparator = ';';

        /// <summary>BOM/CRLF를 정리하고 첫 줄을 헤더로 읽는다. 빈 줄과 첫 칸이 '#'으로 시작하는 줄(주석)은 건너뛴다. 첫 칸이 빈 줄도
        /// 건너뛰는데(Id 없는 행), keepBlankFirstCell이면 남긴다 — 모델 CSV처럼 첫 칸을 비워 "윗 행과 같음"을 뜻하는 표용.</summary>
        public static CsvTable Parse(string name, string text, bool keepBlankFirstCell = false)
        {
            var table = new CsvTable { Name = name };
            if (string.IsNullOrWhiteSpace(text)) return table;
            var lines = text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
            bool headerRead = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                var cells = SplitLine(line);
                for (int c = 0; c < cells.Count; c++) cells[c] = cells[c].Trim();
                if (!headerRead)
                {
                    table.Header = cells.ToArray();
                    headerRead = true;
                    continue;
                }
                if (cells[0].StartsWith("#") || cells.TrueForAll(c => c.Length == 0)) continue;
                if (cells[0].Length == 0 && !keepBlankFirstCell) continue;
                table.Rows.Add(cells.ToArray());
                table.LineNumbers.Add(i + 1);
            }
            return table;
        }

        public static int ColumnIndex(CsvTable t, string column)
        {
            for (int i = 0; i < t.Header.Length; i++)
                if (string.Equals(t.Header[i], column, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static bool HasColumn(CsvTable t, string column) => ColumnIndex(t, column) >= 0;

        public static string Get(CsvTable t, int row, string column, string fallback = "")
        {
            int c = ColumnIndex(t, column);
            if (c < 0 || row < 0 || row >= t.Rows.Count) return fallback;
            var cells = t.Rows[row];
            return c < cells.Length && cells[c].Length > 0 ? cells[c] : fallback;
        }

        public static int GetInt(CsvTable t, int row, string column, int fallback, List<string> errors)
        {
            string s = Get(t, row, column);
            if (s.Length == 0) return fallback;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            Report(t, row, column, $"정수가 아님 '{s}'", errors);
            return fallback;
        }

        public static float GetFloat(CsvTable t, int row, string column, float fallback, List<string> errors)
        {
            string s = Get(t, row, column);
            if (s.Length == 0) return fallback;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
            Report(t, row, column, $"숫자가 아님 '{s}'", errors);
            return fallback;
        }

        public static bool GetBool(CsvTable t, int row, string column, bool fallback, List<string> errors)
        {
            string s = Get(t, row, column);
            if (s.Length == 0) return fallback;
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) || s.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            Report(t, row, column, $"참/거짓이 아님 '{s}' (1/0/true/false)", errors);
            return fallback;
        }

        public static string[] GetList(CsvTable t, int row, string column) => SplitList(Get(t, row, column));

        public static string[] SplitList(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list.ToArray();
            foreach (var part in s.Split(ListSeparator))
            {
                var v = part.Trim();
                if (v.Length > 0) list.Add(v);
            }
            return list.ToArray();
        }

        public static TEnum GetEnum<TEnum>(CsvTable t, int row, string column, TEnum fallback, List<string> errors) where TEnum : struct
        {
            string s = Get(t, row, column);
            if (s.Length == 0) return fallback;
            if (Enum.TryParse(s, true, out TEnum v) && Enum.IsDefined(typeof(TEnum), v)) return v;
            Report(t, row, column, $"알 수 없는 값 '{s}' (가능: {string.Join("/", Enum.GetNames(typeof(TEnum)))})", errors);
            return fallback;
        }

        /// <summary>[Flags] 열거형을 "A;B;C" 목록으로 읽어 OR한다. 모르는 항목은 오류로 남기고 건너뛴다.</summary>
        public static TEnum GetFlags<TEnum>(CsvTable t, int row, string column, List<string> errors) where TEnum : struct
        {
            int bits = 0;
            foreach (var part in GetList(t, row, column))
            {
                if (Enum.TryParse(part, true, out TEnum v) && Enum.IsDefined(typeof(TEnum), v)) bits |= Convert.ToInt32(v);
                else Report(t, row, column, $"알 수 없는 값 '{part}' (가능: {string.Join("/", Enum.GetNames(typeof(TEnum)))})", errors);
            }
            return (TEnum)Enum.ToObject(typeof(TEnum), bits);
        }

        /// <summary>정해진 이름 목록(allowed) 중에서만 고를 수 있는 태그 목록 칸(예: 건물 Flags). 모르는 태그는 오류.</summary>
        public static HashSet<string> GetTags(CsvTable t, int row, string column, string[] allowed, List<string> errors)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in GetList(t, row, column))
            {
                if (Array.Exists(allowed, a => string.Equals(a, part, StringComparison.OrdinalIgnoreCase))) set.Add(part);
                else Report(t, row, column, $"알 수 없는 태그 '{part}' (가능: {string.Join("/", allowed)})", errors);
            }
            return set;
        }

        /// <summary>필수 컬럼이 빠졌거나 모르는 컬럼이 있으면 errors에 남긴다(모르는 컬럼은 무시되고 읽기는 계속된다).</summary>
        public static void CheckColumns(CsvTable t, string[] required, string[] optional, List<string> errors)
        {
            if (errors == null) return;
            foreach (var r in required)
                if (!HasColumn(t, r)) errors.Add($"{t.Name}: 필수 컬럼 '{r}'이(가) 없음");
            foreach (var h in t.Header)
            {
                if (h.Length == 0) continue;
                bool known = Array.Exists(required, r => string.Equals(r, h, StringComparison.OrdinalIgnoreCase)) ||
                             Array.Exists(optional, o => string.Equals(o, h, StringComparison.OrdinalIgnoreCase));
                if (!known) errors.Add($"{t.Name}: 모르는 컬럼 '{h}' (무시됨)");
            }
        }

        /// <summary>같은 Id가 두 번 나오면 오류(뒤의 행은 앞의 행과 구분할 수 없다).</summary>
        public static void CheckUniqueIds(CsvTable t, string column, List<string> errors)
        {
            var seen = new HashSet<string>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string id = Get(t, r, column);
                if (id.Length > 0 && !seen.Add(id)) Report(t, r, column, $"중복 Id '{id}'", errors);
            }
        }

        public static void Report(CsvTable t, int row, string column, string message, List<string> errors)
        {
            if (errors == null) return;
            int line = row >= 0 && row < t.LineNumbers.Count ? t.LineNumbers[row] : 0;
            errors.Add($"{t.Name}:{line} {column}: {message}");
        }

        /// <summary>큰따옴표 인용(안의 쉼표 허용, "" = 따옴표 한 개)을 지원하는 한 줄 분리기.</summary>
        public static List<string> SplitLine(string line)
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

        /// <summary>쉼표/따옴표가 든 값만 큰따옴표로 감싼다(작성기용).</summary>
        public static string Quote(string value)
        {
            value ??= string.Empty;
            if (value.IndexOf(',') < 0 && value.IndexOf('"') < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string Join(IEnumerable<string> values) => string.Join(ListSeparator.ToString(), values ?? new string[0]);
    }
}
