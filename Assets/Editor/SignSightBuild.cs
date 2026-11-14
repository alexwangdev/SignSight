using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the Quest APK (Android, the scenes enabled in Build Settings) to Builds/SignSightLearn.apk and writes a short
/// report to Logs/build-report.txt. Menu: SignSight/Build Quest APK.
/// </summary>
public static class SignSightBuild
{
    public const string ApkPath = "Builds/SignSightLearn.apk";
    public const string ReportPath = "Logs/build-report.txt";

    [MenuItem("SignSight/Build Quest APK")]
    public static void BuildApk()
    {
        Directory.CreateDirectory("Builds");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReportPath, $"started {System.DateTime.Now:HH:mm:ss}\n");
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        var text = $"result {s.result}\nfinished {System.DateTime.Now:HH:mm:ss}\nduration {s.totalTime}\n" +
                   $"size {s.totalSize / 1048576f:0.0} MB\nerrors {s.totalErrors} warnings {s.totalWarnings}\n" +
                   $"scenes {string.Join(", ", options.scenes)}\n";
        foreach (var step in report.steps)
        {
            foreach (var m in step.messages)
            {
                if (m.type == LogType.Error || m.type == LogType.Exception) text += $"ERROR [{step.name}] {m.content}\n";
            }
        }
        File.WriteAllText(ReportPath, text);
        Debug.Log("[SignSightBuild] " + text);
    }
}
