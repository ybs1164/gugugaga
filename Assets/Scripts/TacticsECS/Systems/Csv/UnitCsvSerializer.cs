using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// CSV 텍스트 <-> UnitCsvRow 목록 변환만 담당하는 순수 파서/작성기. Unity 오브젝트(GameObject/프리팹)를
    /// 전혀 몰라서 배치모드 CLI에서도 그대로 왕복 검증할 수 있다(값을 읽고 쓰기만 할 뿐 자체 상태는 없다).
    /// 읽기는 헤더 이름 기준(순서 무관), 쓰기 컬럼 순서는 Header 한 곳에만 정의되어 있다.
    /// 유닛의 행동은 여러 개라서 한 칸에 묶지 않고 반복 컬럼 Action1..N에 하나씩 나열한다(CLAUDE.md 규칙 6 —
    /// CsvTableReader.GetList). 옛 파일의 "Actions" 한 칸 세미콜론 목록("Move;Attack")도 호환용으로 읽는다.
    /// </summary>
    public static class UnitCsvSerializer
    {
        private static readonly string[] HeaderBeforeActions = { "Id", "Name", "MaxHp", "Defense", "BaseVisual" };

        public const string ActionColumn = "Action";
        private const string LegacyActionsColumn = "Actions";

        private static readonly string[] HeaderAfterActions =
        {
            "Domain",
            "Move.Range",
            "Attack.Attack", "Attack.Range",
            "Heal.Amount", "Heal.Range",
            "Transport.Capacity",
            "Cost"
        };


        /// <summary>헤더 이름으로 컬럼을 찾아(CsvTableReader — 순서 무관, 메모 컬럼/`#` 주석 행 허용) 한 행씩 파싱한다.
        /// 값이 비어있거나 형식이 잘못된 컬럼은 그 타입의 기본값(0/None/Land)으로 채운다 — CSV를 손으로 편집하다 실수로
        /// 컬럼을 비워도 예외 없이 불러와지도록 하기 위함이다(잘못된 칸은 errors를 주면 기록된다). Defense/Attack.Attack은
        /// 위키 원값처럼 소수(0.5, 3.5)를 허용한다.</summary>
        public static List<UnitCsvRow> Parse(string csvText, List<string> errors = null, string name = "units.csv")
        {
            var rows = new List<UnitCsvRow>();
            var t = CsvTableReader.Parse(name, csvText);
            for (int r = 0; r < t.Rows.Count; r++)
            {
                rows.Add(new UnitCsvRow
                {
                    Id = CsvTableReader.Get(t, r, "Id"),
                    Name = CsvTableReader.Get(t, r, "Name"),
                    MaxHp = CsvTableReader.GetInt(t, r, "MaxHp", 0, errors),
                    Defense = CsvTableReader.GetFloat(t, r, "Defense", 0f, errors),
                    BaseVisual = CsvTableReader.Get(t, r, "BaseVisual"),
                    Actions = ParseActions(t, r, errors),
                    Domain = CsvTableReader.GetEnum(t, r, "Domain", TerrainType.Land, errors),
                    MoveRange = CsvTableReader.GetInt(t, r, "Move.Range", 0, errors),
                    AttackAttack = CsvTableReader.GetFloat(t, r, "Attack.Attack", 0f, errors),
                    AttackRange = CsvTableReader.GetInt(t, r, "Attack.Range", 0, errors),
                    HealAmount = CsvTableReader.GetInt(t, r, "Heal.Amount", 0, errors),
                    HealRange = CsvTableReader.GetInt(t, r, "Heal.Range", 0, errors),
                    TransportCapacity = CsvTableReader.GetInt(t, r, "Transport.Capacity", 0, errors),
                    Cost = CsvTableReader.GetInt(t, r, "Cost", UnitCsvRow.DefaultCost, errors),
                });
            }
            return rows;
        }

        public static string Write(IReadOnlyList<UnitCsvRow> rows)
        {
            var sb = new StringBuilder();
            int actionCount = CsvTableReader.MaxCount(rows, r => ActionNames(r.Actions).Count);
            sb.AppendLine(string.Join(",", HeaderBeforeActions.Concat(CsvTableReader.ListHeader(ActionColumn, actionCount)).Concat(HeaderAfterActions)));
            foreach (var row in rows)
                sb.AppendLine(WriteRow(row, actionCount));
            return sb.ToString();
        }

        private static string WriteRow(UnitCsvRow row, int actionCount) => string.Join(",", new[]
        {
            row.Id ?? string.Empty,
            row.Name ?? string.Empty,
            row.MaxHp.ToString(CultureInfo.InvariantCulture),
            row.Defense.ToString(CultureInfo.InvariantCulture),
            row.BaseVisual ?? string.Empty,
        }.Concat(CsvTableReader.ListCells(ActionNames(row.Actions), actionCount)).Concat(new[]
        {
            row.Domain.ToString(),
            row.MoveRange.ToString(CultureInfo.InvariantCulture),
            row.AttackAttack.ToString(CultureInfo.InvariantCulture),
            row.AttackRange.ToString(CultureInfo.InvariantCulture),
            row.HealAmount.ToString(CultureInfo.InvariantCulture),
            row.HealRange.ToString(CultureInfo.InvariantCulture),
            row.TransportCapacity.ToString(CultureInfo.InvariantCulture),
            row.Cost.ToString(CultureInfo.InvariantCulture)
        }));

        /// <summary>Action1..N(+ 옛 Actions 칸)의 행동 이름을 ActionType 플래그로 OR한다. 모르는 이름은 errors에 남기고 건너뛴다.</summary>
        private static ActionType ParseActions(CsvTable t, int row, List<string> errors)
        {
            var result = ActionType.None;
            foreach (var name in CsvTableReader.GetList(t, row, ActionColumn, LegacyActionsColumn))
            {
                if (Enum.TryParse<ActionType>(name, true, out var flag) && Enum.IsDefined(typeof(ActionType), flag)) result |= flag;
                else CsvTableReader.Report(t, row, ActionColumn, $"알 수 없는 행동 '{name}'", errors);
            }
            return result;
        }

        private static List<string> ActionNames(ActionType actions)
        {
            var names = new List<string>();
            foreach (ActionType flag in Enum.GetValues(typeof(ActionType)))
            {
                if (flag != ActionType.None && (actions & flag) != 0) names.Add(flag.ToString());
            }
            return names;
        }
    }
}
