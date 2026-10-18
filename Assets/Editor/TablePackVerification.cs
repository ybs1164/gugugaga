using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 표 팩 왕복 검증(배치모드): Sandbox처럼 표 하나를 바꿔 낀 상태로 내보내기 → 가져오기 → 가져온 폴더로 읽은 표가 내보낼 때와
    /// 글자 하나까지 같은지, ID가 같은지, 표를 고친 zip은 거부하고 기존 팩을 건드리지 않는지 본다. 결과는 Console의 PASS/FAIL.
    /// 사용법: unity run . -- -logFile Logs/table_pack.log -executeMethod TacticsECS.EditorTools.TablePackVerification.Run
    /// </summary>
    public static class TablePackVerification
    {
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "TablePackVerification"));
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            bool ok = true;

            // Sandbox에서 GameRules.csv만 바꿔 낀 상태를 흉내 낸다(맨 끝에 주석 행 — 값은 같고 원문만 다르다).
            string custom = Path.Combine(root, "GameRules.csv");
            File.WriteAllText(custom, GameDataLoader.Read(GameDataLoader.GameRulesCsvResourcePath, null, TableSource.Resources).TrimStart('﻿') + "\n# table pack check\n");
            var sandbox = new TableSource { Files = new Dictionary<string, string> { { GameDataLoader.GameRulesCsvResourcePath, custom } } };

            string zip = Path.Combine(root, "Pack.zip");
            var exported = TablePackSystem.Export(sandbox, zip, "Pack", Application.version);
            string dest = Path.Combine(root, "Imported");
            var imported = TablePackSystem.Import(zip, dest, out string error);
            if (imported == null) { Debug.LogError("[TablePackVerification] import failed: " + error); ok = false; }
            else
            {
                ok &= Check(imported.Value.Id == exported.Id, $"id {exported.Id} == {imported.Value.Id}");
                ok &= Check(imported.Value.FileCount == GameDataLoader.SandboxCsvPaths.Length, $"file count {imported.Value.FileCount}");
                var folder = TableSource.FromPath(dest);
                foreach (var path in GameDataLoader.SandboxCsvPaths)
                    ok &= Check(Strip(GameDataLoader.Read(path, null, folder)) == Strip(GameDataLoader.Read(path, null, sandbox)), "same text " + path);
                ok &= Check(GameDataLoader.Read(GameDataLoader.GameRulesCsvResourcePath, null, folder).Contains("# table pack check"), "custom file carried");
                ok &= Check(TablePackSystem.ReadInfo(dest)?.Id == exported.Id, "ReadInfo id");
                ok &= Check(GameDataLoader.LoadAll(folder).Count == GameDataLoader.LoadAll(TableSource.Resources).Count, "same load warnings as defaults");
            }

            // 표 하나를 고친 zip: 거부하고 기존 팩은 그대로.
            string tampered = Path.Combine(root, "Tampered.zip");
            File.Copy(zip, tampered);
            using (var archive = ZipFile.Open(tampered, ZipArchiveMode.Update))
            {
                archive.GetEntry("Tables/Units.csv").Delete();
                using (var w = new StreamWriter(archive.CreateEntry("Tables/Units.csv").Open())) w.Write("Index,Id\n0,Broken\n");
            }
            ok &= Check(TablePackSystem.Import(tampered, dest, out error) == null, "tampered rejected: " + error);
            ok &= Check(TablePackSystem.ReadInfo(dest)?.Id == exported.Id, "previous pack kept");

            TablePackSystem.Clear(dest);
            ok &= Check(TablePackSystem.ReadInfo(dest) == null, "cleared");
            GameDataLoader.LoadAll();
            Debug.Log(ok ? "[TablePackVerification] ALL PASS" : "[TablePackVerification] SOME CHECKS FAILED - see errors above");
        }

        private static string Strip(string text) => text.TrimStart('﻿').Replace("\r\n", "\n");

        private static bool Check(bool condition, string what)
        {
            if (condition) Debug.Log("[TablePackVerification] PASS " + what);
            else Debug.LogError("[TablePackVerification] FAIL " + what);
            return condition;
        }
    }
}
