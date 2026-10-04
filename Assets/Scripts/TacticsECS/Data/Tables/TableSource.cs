using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 게임 데이터 CSV를 어디서 읽을지(GameDataLoader에 넘기는 값). Folder가 비면 Assets/Resources만 쓴다.
    /// Folder가 있으면 표마다 그 폴더에 같은 이름의 파일이 있을 때만 그 파일을 쓰고, 없으면 Resources의 기본 표를 쓴다
    /// (샌드박스 "표 불러오기" — docs/spec/csv-common.md#파일). 값만 있다(CLAUDE.md 규칙 2).
    /// </summary>
    public struct TableSource
    {
        public string Folder;
        /// <summary>표별 개별 파일 선택(Resources 경로 → 외부 파일 경로). Folder보다 우선한다.</summary>
        public Dictionary<string, string> Files;

        /// <summary>폴더에서 실제로 읽은 표의 Resources 경로(예: "Tables/Units"). GameDataLoader가 채운다(null이면 기록하지 않음).</summary>
        public List<string> FromFolder;

        public static TableSource Resources => default;

        public static TableSource FromPath(string folder) =>
            new TableSource { Folder = string.IsNullOrEmpty(folder) ? null : folder, FromFolder = new List<string>() };
    }
}
