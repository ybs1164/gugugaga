using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>PC(Windows) 빌드: 샌드박스 씬 하나만 exe로 만든다 — 모바일 빌드(MobileBuild, Mobile.unity)와 씬을 나눈다.
    /// 빌드 설정(EditorBuildSettings)의 씬 목록은 쓰지 않는다.</summary>
    public static class Pixel2DBuild
    {
        public const string PcScenePath = "Assets/Scenes/Sandbox.unity";

        public static void Run()
        {
            var scenes=new[]{PcScenePath};
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=scenes,
                locationPathName="Builds/Pixel2D/gugugaga.exe",
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded)
                throw new InvalidOperationException("[Pixel2DBuild] " + report.summary.result);
            GameDataLoader.LoadAll();
            GameDataLoader.ExportTables(TableSource.Resources,"Builds/Pixel2D/SandboxTables"); // 샌드박스 "표 불러오기"로 고쳐 읽을 기본 표 사본
            Directory.CreateDirectory("Builds/Pixel2D/Licenses");
            foreach(string pack in new[]{"tiny-town","tiny-farm","tiny-dungeon","ui-pack-pixel-adventure"})
                File.Copy("Assets/Art/Pixel2D/ThirdParty/Kenney/"+pack+"/License.txt","Builds/Pixel2D/Licenses/"+pack+".txt",true);
            Debug.Log($"[Pixel2DBuild] PASS: {report.summary.totalSize} bytes, {report.summary.totalTime.TotalSeconds:F1}s");
        }
    }
}
