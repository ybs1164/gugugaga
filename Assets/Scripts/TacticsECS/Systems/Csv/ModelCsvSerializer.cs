using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모델 파츠 CSV(Assets/Resources/Models/*.csv) -&gt; ModelPartInfo. 자체 상태 없음. 형식은 docs/history/ModelingPlan.md.
    ///   - 컬럼: Model, Shape, X, Y, Z, SX, SY, SZ, RX, RY, RZ, Color, MinLevel, MaxLevel, Note (헤더 이름 기반 — CsvTableReader).
    ///   - Model 칸을 비우면 윗 행과 같은 모델(한 모델의 조각을 이어 적기 편하게).
    ///   - SZ를 비우면 SX와 같다(원기둥/원뿔/구처럼 가로가 같은 도형).
    ///   - Color: 팔레트 이름(ModelPalette.csv), #RRGGBB, 또는 Team(팀 색).
    /// </summary>
    public static class ModelCsvSerializer
    {
        public const string TeamColorName = "Team";

        private static readonly string[] Required = { "Shape" };
        private static readonly string[] Optional = { "Model", "X", "Y", "Z", "SX", "SY", "SZ", "RX", "RY", "RZ", "Color", "MinLevel", "MaxLevel", "Note" };

        public static Dictionary<string, Color> ParsePalette(string csvText, List<string> errors, string name = "ModelPalette.csv")
        {
            var t = CsvTableReader.Parse(name, csvText);
            CsvTableReader.CheckColumns(t, new[] { "Name", "Color" }, new[] { "Note" }, errors);
            var map = new Dictionary<string, Color>();
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string key = CsvTableReader.Get(t, r, "Name");
                string hex = CsvTableReader.Get(t, r, "Color");
                if (ColorUtility.TryParseHtmlString(hex, out var c)) map[key] = c;
                else CsvTableReader.Report(t, r, "Color", $"색이 아님 '{hex}' (#RRGGBB)", errors);
            }
            return map;
        }

        /// <summary>파츠를 models에 모델 Id별로 덧붙인다(여러 파일을 차례로 넣을 수 있게).</summary>
        public static void ParseInto(string csvText, IReadOnlyDictionary<string, Color> palette, Dictionary<string, List<ModelPartInfo>> models,
            List<string> errors, string name)
        {
            var t = CsvTableReader.Parse(name, csvText, keepBlankFirstCell: true);
            if (t.Rows.Count == 0) return;
            CsvTableReader.CheckColumns(t, Required, Optional, errors);
            string current = string.Empty;
            for (int r = 0; r < t.Rows.Count; r++)
            {
                string model = CsvTableReader.Get(t, r, "Model");
                if (model.Length > 0) current = model;
                if (current.Length == 0) { CsvTableReader.Report(t, r, "Model", "모델 Id가 없음", errors); continue; }
                float sx = CsvTableReader.GetFloat(t, r, "SX", 1f, errors);
                string colorText = CsvTableReader.Get(t, r, "Color", "#FFFFFF");
                var part = new ModelPartInfo
                {
                    Model = current,
                    Shape = CsvTableReader.GetEnum(t, r, "Shape", ModelShape.Box, errors),
                    Position = new Vector3(CsvTableReader.GetFloat(t, r, "X", 0f, errors), CsvTableReader.GetFloat(t, r, "Y", 0f, errors), CsvTableReader.GetFloat(t, r, "Z", 0f, errors)),
                    Size = new Vector3(sx, CsvTableReader.GetFloat(t, r, "SY", 1f, errors), CsvTableReader.GetFloat(t, r, "SZ", sx, errors)),
                    Rotation = new Vector3(CsvTableReader.GetFloat(t, r, "RX", 0f, errors), CsvTableReader.GetFloat(t, r, "RY", 0f, errors), CsvTableReader.GetFloat(t, r, "RZ", 0f, errors)),
                    MinLevel = CsvTableReader.GetInt(t, r, "MinLevel", 0, errors),
                    MaxLevel = CsvTableReader.GetInt(t, r, "MaxLevel", 0, errors),
                    TeamColor = colorText == TeamColorName,
                    Color = ResolveColor(colorText, palette, t, r, errors),
                };
                if (!models.TryGetValue(current, out var list)) models[current] = list = new List<ModelPartInfo>();
                list.Add(part);
            }
        }

        private static Color ResolveColor(string text, IReadOnlyDictionary<string, Color> palette, CsvTable t, int r, List<string> errors)
        {
            if (text == TeamColorName) return Color.white;
            if (palette != null && palette.TryGetValue(text, out var c)) return c;
            if (text.StartsWith("#") && ColorUtility.TryParseHtmlString(text, out c)) return c;
            CsvTableReader.Report(t, r, "Color", $"팔레트에 없는 색 '{text}'", errors);
            return Color.magenta;
        }
    }
}
