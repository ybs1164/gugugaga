using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TacticsECS
{
    /// <summary>
    /// 번역 문구 속 이름 자리표시자를 CSV 값으로 채운다 — 문구에 숫자를 옮겨 적지 않기 위함(규칙: docs/spec/csv/strings.md#자리표시자).
    ///   {컬럼}               그 문구가 설명하는 행의 CSV 칸(self — 표마다 파서가 넘긴다). 예: 건물 설명의 {Cost}
    ///   {표.Id.컬럼}          다른 표의 행(CrossValue가 아는 표·컬럼). 예: {Building.Mine.Cost}, {Task.Pacifist.Threshold}
    ///   {Rule.도메인.이름}     GameRules.csv 값. 예: {Rule.Score.Monument}
    /// 숫자로 시작하는 {0}, {1}은 건드리지 않는다(string.Format 자리 — LocalizationSystem.F). 못 채운 자리표시자는 그대로 남아 화면에서 보인다.
    /// 다른 표 값은 이미 올라간 표를 보므로 GameDataLoader가 참조되는 표를 먼저 읽는다. 자체 상태는 없다.
    /// </summary>
    public static class TextPlaceholderSystem
    {
        public const string RulePrefix = "Rule";

        private static readonly Regex Pattern = new Regex(@"\{([A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)*)\}");

        public static string Fill(string text, Func<string, string> self = null)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return Pattern.Replace(text, m => Resolve(m.Groups[1].Value, self) ?? m.Value);
        }

        /// <summary>자리표시자 이름 하나의 값. 못 찾으면 null.</summary>
        public static string Resolve(string name, Func<string, string> self)
        {
            int dot = name.IndexOf('.');
            if (dot > 0)
            {
                string head = name.Substring(0, dot), rest = name.Substring(dot + 1);
                if (head == RulePrefix) return RuleValue(rest);
                int dot2 = rest.IndexOf('.');
                if (dot2 > 0)
                {
                    var cross = CrossValue(head, rest.Substring(0, dot2), rest.Substring(dot2 + 1));
                    if (cross != null) return cross;
                }
            }
            var own = self?.Invoke(name);
            return string.IsNullOrEmpty(own) ? null : own;
        }

        public static string RuleValue(string key) =>
            GameRulesCsvSerializer.AllFields().TryGetValue(key, out var field) ? Text(field.GetValue(null)) : null;

        /// <summary>다른 표 행의 값. 표 이름은 번역 키 앞부분과 같다(Unit은 유닛과 배 모두). 모르는 표·Id·컬럼이면 null.</summary>
        public static string CrossValue(string table, string id, string column)
        {
            switch (table)
            {
                case "Building":
                    foreach (var b in BuildingDefinition.All)
                        if (b.Id == id)
                            switch (column)
                            {
                                case "Cost": return Text(b.Cost);
                                case "Population": return Text(b.Population);
                                case "PopulationPerAdjacent": return Text(b.PopulationPerAdjacent);
                            }
                    return null;
                case "TileAction":
                    foreach (var a in TileActionDefinition.All)
                        if (a.Id == id)
                            switch (column)
                            {
                                case "Cost": return Text(a.Cost);
                                case "Population": return Text(a.Population);
                                case "StarsGain": return Text(a.StarsGain);
                            }
                    return null;
                case "Task":
                    foreach (var t in TaskDefinition.All)
                        if (t.Id == id && column == "Threshold") return Text(t.Threshold);
                    return null;
                case "CityReward":
                    foreach (var r in CityRewardDefinition.All)
                        if (r.Type.ToString() == id && column == "Amount") return Text(r.Amount);
                    return null;
                case "Unit":
                    foreach (var u in GameTables.Units) if (u.Id == id) return UnitValue(u, column);
                    foreach (var b in GameTables.Boats) if (b.Unit.Id == id) return UnitValue(b.Unit, column);
                    return null;
                case "Tech":
                    foreach (var t in GameTables.Techs)
                        if (t.Id == id)
                            switch (column)
                            {
                                case "CostBase": return Text(t.CostBase);
                                case "CostPerCity": return Text(t.CostPerCity);
                            }
                    return null;
                case "Tribe":
                    foreach (var t in GameTables.Tribes)
                        if (t.Id == id && column == "StartStars") return Text(t.StartStars);
                        else if (t.Id == id && column == "StartCapitalLevel") return Text(t.StartCapitalLevel);
                    return null;
                case "StartRule":
                    foreach (var r in GameTables.StartConditionRules)
                        if (r.Id == id)
                            switch (column)
                            {
                                case "Count": return Text(r.Count);
                                case "MinDistance": return Text(r.MinDistance);
                                case "MaxDistance": return Text(r.MaxDistance);
                            }
                    return null;
                default:
                    return null;
            }
        }

        private static string UnitValue(UnitCsvRow u, string column)
        {
            switch (column)
            {
                case "Cost": return Text(u.Cost);
                case "MaxHp": return Text(u.MaxHp);
                case "Defense": return Text(u.Defense);
                case "Move.Range": return Text(u.MoveRange);
                case "Attack.Attack": return Text(u.AttackAttack);
                case "Attack.Range": return Text(u.AttackRange);
                default: return null;
            }
        }

        private static string Text(object value) =>
            value is float f ? f.ToString("0.##", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
