using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// CSV 텍스트 <-> UnitCsvRow 목록 변환만 담당하는 순수 파서/작성기. Unity 오브젝트(GameObject/프리팹)를
    /// 전혀 몰라서 배치모드 CLI에서도 그대로 왕복 검증할 수 있다(값을 읽고 쓰기만 할 뿐 자체 상태는 없다).
    /// 컬럼 순서/이름은 Header에 한 곳에만 정의되어 있다.
    /// 행 안의 Actions 컬럼은 여러 행동 이름을 담아야 해서(예: "Move;Attack;Counter"), CSV 컬럼 구분자인
    /// 쉼표와 겹치지 않도록 세미콜론을 구분자로 쓴다.
    /// </summary>
    public static class UnitCsvSerializer
    {
        private static readonly string[] Header =
        {
            "Name", "MaxHp", "Defense", "BaseVisual", "Actions", "Domain",
            "Move.Range",
            "Attack.Attack", "Attack.Range",
            "Heal.Amount", "Heal.Range",
            "Transport.Capacity"
        };

        private const char ActionsSeparator = ';';

        /// <summary>첫 줄(헤더)을 건너뛰고 나머지 줄을 각각 한 행으로 파싱한다. 비어있는 줄은 무시한다.
        /// 값이 비어있거나 형식이 잘못된 컬럼은 그 타입의 기본값(0/false/흰색/None)으로 채운다 —
        /// CSV를 손으로 편집하다 실수로 컬럼을 비워도 예외 없이 불러와지도록 하기 위함이다.</summary>
        public static List<UnitCsvRow> Parse(string csvText)
        {
            var rows = new List<UnitCsvRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            var lines = csvText.Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++) // 0번째 줄은 헤더
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                rows.Add(ParseRow(line.Split(',')));
            }
            return rows;
        }

        public static string Write(IReadOnlyList<UnitCsvRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header));
            foreach (var row in rows)
                sb.AppendLine(WriteRow(row));
            return sb.ToString();
        }

        private static UnitCsvRow ParseRow(string[] c) => new UnitCsvRow
        {
            Name = Col(c, 0),
            MaxHp = ParseInt(Col(c, 1)),
            Defense = ParseInt(Col(c, 2)),
            BaseVisual = Col(c, 3),
            Actions = ParseActions(Col(c, 4)),
            Domain = ParseDomain(Col(c, 5)),
            MoveRange = ParseInt(Col(c, 6)),
            AttackAttack = ParseInt(Col(c, 7)),
            AttackRange = ParseInt(Col(c, 8)),
            HealAmount = ParseInt(Col(c, 9)),
            HealRange = ParseInt(Col(c, 10)),
            TransportCapacity = ParseInt(Col(c, 11))
        };

        private static string WriteRow(UnitCsvRow row) => string.Join(",", new[]
        {
            row.Name ?? string.Empty,
            row.MaxHp.ToString(CultureInfo.InvariantCulture),
            row.Defense.ToString(CultureInfo.InvariantCulture),
            row.BaseVisual ?? string.Empty,
            WriteActions(row.Actions),
            row.Domain.ToString(),
            row.MoveRange.ToString(CultureInfo.InvariantCulture),
            row.AttackAttack.ToString(CultureInfo.InvariantCulture),
            row.AttackRange.ToString(CultureInfo.InvariantCulture),
            row.HealAmount.ToString(CultureInfo.InvariantCulture),
            row.HealRange.ToString(CultureInfo.InvariantCulture),
            row.TransportCapacity.ToString(CultureInfo.InvariantCulture)
        });

        private static string Col(string[] cols, int index) => index < cols.Length ? cols[index].Trim() : string.Empty;

        private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static TerrainType ParseDomain(string s) =>
            Enum.TryParse<TerrainType>(s, true, out var v) ? v : TerrainType.Land;

        private static ActionType ParseActions(string s)
        {
            var result = ActionType.None;
            if (string.IsNullOrEmpty(s)) return result;
            foreach (var token in s.Split(ActionsSeparator))
            {
                var name = token.Trim();
                if (name.Length > 0 && Enum.TryParse<ActionType>(name, true, out var flag)) result |= flag;
            }
            return result;
        }

        private static string WriteActions(ActionType actions)
        {
            var names = new List<string>();
            foreach (ActionType flag in Enum.GetValues(typeof(ActionType)))
            {
                if (flag != ActionType.None && (actions & flag) != 0) names.Add(flag.ToString());
            }
            return string.Join(ActionsSeparator.ToString(), names);
        }
    }
}
