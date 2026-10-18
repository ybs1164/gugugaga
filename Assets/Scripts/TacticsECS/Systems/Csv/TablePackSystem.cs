using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TacticsECS
{
    /// <summary>
    /// 표 팩: Sandbox에서 쓰던 표 전부(GameDataLoader.SandboxCsvPaths)를 zip 하나로 내보내고, 이미 설치된 앱(휴대폰)이 그 zip을
    /// 검증해 표 폴더로 푼다. 푼 폴더는 TableSource.FromPath로 읽는다. 형식: docs/spec/csv/table-pack.md, 흐름: docs/spec/game/session.md
    /// 자체 상태는 없다.
    /// </summary>
    public static class TablePackSystem
    {
        public const string ManifestFile = "Pack.csv";
        public const int Format = 1;
        private const int IdLength = 8;
        private static readonly UTF8Encoding Utf8Bom = new UTF8Encoding(true);

        /// <summary>source에서 지금 쓰는 표를 zipPath에 쓴다(Resources와 같은 경로 + Pack.csv). 원문 텍스트를 그대로 옮긴다.</summary>
        public static TablePackInfo Export(TableSource source, string zipPath, string name, string buildVersion)
        {
            var files = new List<KeyValuePair<string, byte[]>>();
            foreach (var path in GameDataLoader.SandboxCsvPaths)
            {
                var text = GameDataLoader.Read(path, null, source);
                if (text.Length == 0) continue;
                files.Add(new KeyValuePair<string, byte[]>(path, Encode(text.TrimStart('﻿'))));
            }
            var hashes = files.Select(f => new KeyValuePair<string, string>(f.Key, Hash(f.Value))).ToList();
            var manifest = new StringBuilder("Kind,Key,Value\n");
            manifest.Append("Meta,Format,").Append(Format).Append('\n');
            manifest.Append("Meta,Name,").Append(Quote(name)).Append('\n');
            manifest.Append("Meta,BuildVersion,").Append(Quote(buildVersion)).Append('\n');
            foreach (var h in hashes) manifest.Append("File,").Append(h.Key).Append(',').Append(h.Value).Append('\n');

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath)));
            string temp = zipPath + ".tmp";
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, ManifestFile, Encode(manifest.ToString()));
                foreach (var f in files) WriteEntry(zip, f.Key + ".csv", f.Value);
            }
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(temp, zipPath);
            return new TablePackInfo { Name = name, Id = PackId(hashes), FileCount = files.Count, BuildVersion = buildVersion };
        }

        /// <summary>zip을 검증하고(Pack.csv, 표마다 해시) destFolder에 푼다 — 하나라도 틀리면 아무것도 바꾸지 않고 null과 이유.
        /// 이 빌드가 모르는 표는 건너뛴다(그 표는 읽히지 않는다). 기존 폴더는 다 푼 뒤에 바꿔치기한다.</summary>
        public static TablePackInfo? Import(string zipPath, string destFolder, out string error)
        {
            error = null;
            string temp = destFolder + ".tmp";
            try
            {
                using (var zip = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
                {
                    var manifestEntry = zip.GetEntry(ManifestFile);
                    if (manifestEntry == null) { error = ManifestFile + " 없음"; return null; }
                    var manifestBytes = ReadEntry(manifestEntry);
                    var info = ParseManifest(Utf8Bom.GetString(manifestBytes), out var hashes, out error);
                    if (info == null) return null;

                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                    Directory.CreateDirectory(temp);
                    foreach (var h in hashes)
                    {
                        if (!GameDataLoader.SandboxCsvPaths.Contains(h.Key)) continue;
                        var entry = zip.GetEntry(h.Key + ".csv");
                        if (entry == null) { error = h.Key + ".csv 없음"; return null; }
                        var bytes = ReadEntry(entry);
                        if (!string.Equals(Hash(bytes), h.Value, StringComparison.OrdinalIgnoreCase)) { error = h.Key + ".csv 해시가 Pack.csv와 다름"; return null; }
                        var file = Path.Combine(temp, h.Key + ".csv");
                        Directory.CreateDirectory(Path.GetDirectoryName(file));
                        File.WriteAllBytes(file, bytes);
                    }
                    File.WriteAllBytes(Path.Combine(temp, ManifestFile), manifestBytes);
                    if (Directory.Exists(destFolder)) Directory.Delete(destFolder, true);
                    Directory.Move(temp, destFolder);
                    return info;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return null;
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch (IOException) { }
            }
        }

        /// <summary>가져온 폴더의 팩 요약. 팩이 없거나 Pack.csv를 읽을 수 없으면 null.</summary>
        public static TablePackInfo? ReadInfo(string folder)
        {
            var file = Path.Combine(folder, ManifestFile);
            if (!File.Exists(file)) return null;
            try { return ParseManifest(File.ReadAllText(file), out _, out _); }
            catch (IOException) { return null; }
        }

        /// <summary>가져온 팩을 지운다 — 다음에 읽을 때부터 기본 표.</summary>
        public static void Clear(string folder)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        private static TablePackInfo? ParseManifest(string text, out List<KeyValuePair<string, string>> hashes, out string error)
        {
            hashes = new List<KeyValuePair<string, string>>();
            error = null;
            var table = CsvTableReader.Parse(ManifestFile, text);
            var meta = new Dictionary<string, string>();
            for (int r = 0; r < table.Rows.Count; r++)
            {
                string kind = CsvTableReader.Get(table, r, "Kind"), key = CsvTableReader.Get(table, r, "Key"), value = CsvTableReader.Get(table, r, "Value");
                if (kind == "Meta") meta[key] = value;
                else if (kind == "File" && key.Length > 0) hashes.Add(new KeyValuePair<string, string>(key, value));
            }
            if (!meta.TryGetValue("Format", out var format) || !int.TryParse(format, out int f) || f > Format) { error = "Format " + format + " — 이 빌드는 " + Format + "까지 읽음"; return null; }
            if (hashes.Count == 0) { error = ManifestFile + "에 File 행이 없음"; return null; }
            meta.TryGetValue("Name", out var name);
            meta.TryGetValue("BuildVersion", out var build);
            return new TablePackInfo { Name = name ?? "", Id = PackId(hashes), FileCount = hashes.Count, BuildVersion = build ?? "" };
        }

        /// <summary>표 경로·해시 목록 전체의 해시 앞자리(경로 순서와 무관).</summary>
        private static string PackId(List<KeyValuePair<string, string>> hashes)
        {
            var sb = new StringBuilder();
            foreach (var h in hashes.OrderBy(h => h.Key, StringComparer.Ordinal)) sb.Append(h.Key).Append('\n').Append(h.Value.ToLowerInvariant()).Append('\n');
            return Hash(Encoding.UTF8.GetBytes(sb.ToString())).Substring(0, IdLength);
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private static byte[] Encode(string text) => Utf8Bom.GetPreamble().Concat(Utf8Bom.GetBytes(text)).ToArray();

        private static string Quote(string value) =>
            value != null && (value.Contains(",") || value.Contains("\"")) ? "\"" + value.Replace("\"", "\"\"") + "\"" : value ?? "";

        private static void WriteEntry(ZipArchive zip, string path, byte[] bytes)
        {
            using (var s = zip.CreateEntry(path).Open()) s.Write(bytes, 0, bytes.Length);
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            using (var s = entry.Open())
            using (var m = new MemoryStream())
            {
                s.CopyTo(m);
                return m.ToArray();
            }
        }
    }
}
