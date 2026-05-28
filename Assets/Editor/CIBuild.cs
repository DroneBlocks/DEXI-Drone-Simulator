using System.Linq;
using UnityEditor;
using UnityEngine;

// One-off headless build helper for verifying the editor-only free-flight gate.
// Invoke: Unity -batchmode -quit -executeMethod CIBuild.WebGL -buildOutput <dir>
public static class CIBuild
{
    public static void WebGL()
    {
        string output = "Builds/WebGL";
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOutput") output = args[i + 1];

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        Debug.Log($"[CIBuild] Building WebGL to '{output}' with scenes: {string.Join(", ", scenes)}");

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;
        Debug.Log($"[CIBuild] Result={summary.result} totalErrors={summary.totalErrors} totalTime={summary.totalTime} sizeBytes={summary.totalSize}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
