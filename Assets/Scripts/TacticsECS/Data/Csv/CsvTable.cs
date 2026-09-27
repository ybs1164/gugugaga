using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 헤더가 있는 CSV 한 파일을 그대로 담은 값 — 헤더(컬럼 이름)와 행(칸 문자열 배열)뿐이다. 칸을 이름으로 찾거나
    /// 숫자/목록/열거형으로 읽는 일은 전부 Systems/Csv/CsvTableReader가 한다(CLAUDE.md 규칙 2).
    /// 컬럼을 위치가 아니라 이름으로 찾기 때문에 기획자가 스프레드시트에서 컬럼 순서를 바꾸거나 설명용 컬럼을
    /// 끼워 넣어도 그대로 읽힌다.
    /// </summary>
    public class CsvTable
    {
        /// <summary>오류 메시지에 쓰는 파일 이름(예: "Buildings.csv").</summary>
        public string Name;

        public string[] Header = new string[0];

        public readonly List<string[]> Rows = new List<string[]>();

        /// <summary>Rows[i]가 원본 파일의 몇 번째 줄이었는지(1부터, 헤더가 1번 줄) — 오류 메시지용.</summary>
        public readonly List<int> LineNumbers = new List<int>();
    }
}
