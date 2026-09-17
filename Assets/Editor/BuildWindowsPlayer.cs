using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWindowsPlayer
{
    private const string OutputPath = "Builds/Windows/CAUGHT-GAME.exe";

    [MenuItem("Tools/Build/Windows Player")]
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes were found in Build Settings.");
        }

        string outputDirectory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Windows build failed with result {report.summary.result}.");
        }

        Debug.Log(
            $"Windows build completed: {OutputPath} " +
            $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)");
    }
}
