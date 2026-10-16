using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 안드로이드 빌드: 모바일 씬(MobileSceneSetup.MobileScenePath) 하나만 APK로 만든다. 빌드 설정(EditorBuildSettings)의 씬 목록은
    /// Windows 빌드(Pixel2DBuild)용이라 건드리지 않는다. 안드로이드용 PlayerSettings(패키지 이름, IL2CPP + ARM64, 자동 회전)는
    /// 빌드할 때마다 여기서 다시 적용해 ProjectSettings에 남긴다 — 에디터에서 손으로 바꿀 필요가 없게.
    /// 사용법(README "실행"): unity build --target Android --execute-method TacticsECS.EditorTools.MobileBuild.Run -o Builds/Android/gugugaga.apk
    /// 인자: -buildOutput &lt;경로&gt;(unity build의 -o가 넘긴다, 없으면 DefaultOutput), -release(개발 빌드 끄기).
    /// </summary>
    public static class MobileBuild
    {
        public const string PackageName = "com.ybs1164.gugugaga";
        private const string DefaultOutput = "Builds/Android/gugugaga.apk";

        public static void Run()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("[MobileBuild] Android Build Support 모듈이 이 에디터에 설치돼 있지 않다 — Unity Hub에서 " +
                    Application.unityVersion + "에 Android Build Support(OpenJDK, Android SDK & NDK 포함)를 추가한다.");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MobileSceneSetup.MobileScenePath) == null)
                throw new InvalidOperationException($"[MobileBuild] {MobileSceneSetup.MobileScenePath}가 없다 — 먼저 MobileSceneSetup.Generate를 실행한다.");

            string output = Arg("-buildOutput") ?? DefaultOutput;
            bool release = HasArg("-release");
            ApplyPlayerSettings();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MobileSceneSetup.MobileScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = release ? BuildOptions.None : BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("[MobileBuild] " + report.summary.result);
            Debug.Log($"[MobileBuild] PASS: {output} ({(release ? "release" : "development")}), {report.summary.totalSize} bytes, {report.summary.totalTime.TotalSeconds:F1}s");
        }

        /// <summary>안드로이드 전용 PlayerSettings. 최소 SDK·품질 단계(Android 기본 = Mobile → Mobile_RPAsset)는 ProjectSettings에 이미 있다.</summary>
        private static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // 가로·세로 모두(자동 회전, 거꾸로 세로만 뺀다). 화면은 폭에 따라 넓은/좁은 배치로 바뀐다(UiKit, docs/spec/game/session.md#화면-배치).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }
}
