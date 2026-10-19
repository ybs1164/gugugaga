using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>PC(Windows) 빌드: 샌드박스 씬 하나만 exe로 만든다 — 모바일 빌드(MobileBuild, Mobile.unity)와 씬을 나눈다.
    /// 빌드 설정(EditorBuildSettings)의 씬 목록은 쓰지 않는다. 빌드 뒤 배포 zip(BundleZip) 하나로 묶는다:
    /// 플레이어 + 기본 표 사본 + 라이선스 + README.md·docs/(CSV 사용법 docs/spec/csv-howto.md). 묶음 구성: README "실행".</summary>
    public static class Pixel2DBuild
    {
        public const string PcScenePath = "Assets/Scenes/Sandbox.unity";
        private const string OutputDir = "Builds/Pixel2D";
        private const string BundleZip = "Builds/gugugaga-pc.zip";
        private const string BundleRoot = "gugugaga-pc"; // zip 안 최상위 폴더

        public static void Run()
        {
            var scenes=new[]{PcScenePath};
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=scenes,
                locationPathName=OutputDir+"/gugugaga.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException("[Pixel2DBuild] " + report.summary.result);
            GameDataLoader.LoadAll();
            GameDataLoader.ExportTables(TableSource.Resources,OutputDir+"/SandboxTables"); // 샌드박스 "표 불러오기"로 고쳐 읽을 기본 표 사본
            Directory.CreateDirectory(OutputDir+"/Licenses");
            foreach(string pack in new[]{"tiny-town","tiny-farm","tiny-dungeon","ui-pack-pixel-adventure"})
                File.Copy("Assets/Art/Pixel2D/ThirdParty/Kenney/"+pack+"/License.txt",OutputDir+"/Licenses/"+pack+".txt",true);
            int entries=WriteBundle(report);
            Debug.Log($"[Pixel2DBuild] PASS: {report.summary.totalSize} bytes, {report.summary.totalTime.TotalSeconds:F1}s, {BundleZip} {entries} files");
        }

        /// <summary>OutputDir에는 이전에 손으로 넣은 파일이 남아 있을 수 있어 통째로 묶지 않는다 —
        /// 이번 빌드가 쓴 파일의 최상위 항목 + SandboxTables·Licenses만 넣는다. *DoNotShip / *DontShip* 폴더는 뺀다.</summary>
        private static int WriteBundle(BuildReport report)
        {
            string outFull=Path.GetFullPath(OutputDir);
            var top=report.GetFiles()
                .Select(f=>Path.GetFullPath(f.path))
                .Where(p=>p.StartsWith(outFull+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                .Select(p=>p.Substring(outFull.Length+1).Split('/','\\')[0])
                .Concat(new[]{"SandboxTables","Licenses"})
                .Where(n=>!IsDoNotShip(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n=>n,StringComparer.Ordinal)
                .ToList();

            if(File.Exists(BundleZip)) File.Delete(BundleZip);
            int count=0;
            using(var zip=ZipFile.Open(BundleZip,ZipArchiveMode.Create))
            {
                foreach(string name in top)
                    count+=AddPath(zip,Path.Combine(OutputDir,name),BundleRoot+"/"+name);
                // 문서: 저장소와 같은 상대 위치에 둬서 README·docs 안 링크가 zip을 푼 뒤에도 그대로 열린다.
                count+=AddPath(zip,"README.md",BundleRoot+"/README.md");
                count+=AddPath(zip,"docs",BundleRoot+"/docs");
            }
            return count;
        }

        private static bool IsDoNotShip(string name)=>
            name.IndexOf("DoNotShip",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("DontShip",StringComparison.OrdinalIgnoreCase)>=0;

        private static int AddPath(ZipArchive zip,string source,string entry)
        {
            if(File.Exists(source))
            {
                zip.CreateEntryFromFile(source,entry,System.IO.Compression.CompressionLevel.Optimal);
                return 1;
            }
            if(!Directory.Exists(source))
                throw new FileNotFoundException("[Pixel2DBuild] 묶을 항목이 없다: "+source);
            int count=0;
            foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories).OrderBy(f=>f,StringComparer.Ordinal))
            {
                string rel=file.Substring(source.Length+1).Replace('\\','/');
                if(rel.Split('/').Any(IsDoNotShip)) continue;
                zip.CreateEntryFromFile(file,entry+"/"+rel,System.IO.Compression.CompressionLevel.Optimal);
                count++;
            }
            return count;
        }
    }
}
