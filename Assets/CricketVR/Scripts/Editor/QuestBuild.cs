using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CricketVR.Editor
{
    /// <summary>
    /// Builds the Quest APK from the enabled build scenes.
    /// Menu for the Editor; static Build() for batch mode:
    ///   Unity -batchmode -projectPath . -executeMethod CricketVR.Editor.QuestBuild.Build -quit
    /// </summary>
    public static class QuestBuild
    {
        public const string OutputPath = "Builds/CricketVR.apk";

        [MenuItem("CricketVR/Build/Quest APK (Development)")]
        public static void BuildDevelopmentMenu() => Run(development: true, autoRun: false);

        [MenuItem("CricketVR/Build/Quest APK (Development) and Run on Headset")]
        public static void BuildAndRunMenu() => Run(development: true, autoRun: true);

        /// <summary>Batch-mode entry. Exits with a non-zero code on failure.</summary>
        public static void Build()
        {
            bool development = Environment.GetCommandLineArgs().Contains("-cricketvrRelease") == false;
            var report = Run(development, autoRun: false);
            EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Run(bool development, bool autoRun)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[CricketVR] Active build target is not Android. Run CricketVR > Setup > 3. Apply Quest Player Settings first.");
                return null;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[CricketVR] No enabled build scenes. Run CricketVR > Setup > 6. Build or Repair Main Scene first.");
                return null;
            }

            if (XrProjectConfigurator.ReportValidation(fixAutomatic: false) > 0)
                Debug.LogWarning("[CricketVR] Building with OpenXR validation errors; the app may not start in VR.");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = BuildOptions.None;
            if (development) options |= BuildOptions.Development;
            if (autoRun) options |= BuildOptions.AutoRunPlayer;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = options,
            });

            var s = report.summary;
            var line = $"[CricketVR] Build {s.result}: {s.outputPath}, {s.totalSize / (1024f * 1024f):F1} MB, " +
                       $"{s.totalErrors} error(s), {s.totalWarnings} warning(s), {s.totalTime.TotalSeconds:F0} s.";
            if (s.result == BuildResult.Succeeded) Debug.Log(line); else Debug.LogError(line);
            return report;
        }
    }
}
