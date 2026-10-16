using System;
using System.IO;

namespace TacticsECS
{
    /// <summary>
    /// 한 판 저장 파일 쓰기/읽기/지우기(이어하기). 내용은 SaveGameData 그래프 그대로(GraphSerializer). 쓰기는 임시 파일에 쓴 뒤 바꿔치기해서
    /// 쓰는 도중 앱이 꺼져도 이전 저장이 남는다. 언제 저장하는지는 BattleController가 정한다 — 규칙: docs/spec/game/session.md
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>데이터 타입의 필드가 바뀌어 옛 저장을 읽을 수 없게 되면 올린다.</summary>
        public const int Version = 1;

        public static void Write(string path, SaveGameData data)
        {
            data.Version = Version;
            var bytes = GraphSerializer.Serialize(data);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        /// <summary>읽을 수 없으면(없음·버전 다름·형식 오류) null과 그 이유.</summary>
        public static SaveGameData TryRead(string path, out string error)
        {
            error = null;
            if (!File.Exists(path)) { error = "missing"; return null; }
            try
            {
                var data = GraphSerializer.Deserialize<SaveGameData>(File.ReadAllBytes(path));
                if (data == null || data.Version != Version) { error = "version"; return null; }
                if (data.Grid == null || data.World == null || data.Economy == null) { error = "incomplete"; return null; }
                return data;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        public static bool Exists(string path) => File.Exists(path);

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
