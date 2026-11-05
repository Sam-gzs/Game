using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click shareable builds. Use the Build menu in the Unity editor:
///   Build > Windows x64 (zip)
///   Build > WebGL (zip)
/// The zips land in Builds/ and can be uploaded / sent directly.
/// </summary>
public static class GameBuild
{
    const string WindowsDir = "Builds/Windows";
    const string WindowsExe = "Builds/Windows/Stickman.exe";
    const string WindowsZip = "Builds/Stickman-Windows.zip";
    const string WebGLDir = "Builds/WebGL";
    const string WebGLZip = "Builds/Stickman-WebGL.zip";

    static string[] EnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var list = new System.Collections.Generic.List<string>();
        for (int i = 0; i < scenes.Length; i++)
            if (scenes[i].enabled) list.Add(scenes[i].path);
        return list.ToArray();
    }

    static readonly string[] Levels1to6 =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Level1.unity",
        "Assets/Scenes/Level2.unity",
        "Assets/Scenes/Level3.unity",
        "Assets/Scenes/Level4.unity",
        "Assets/Scenes/Level5.unity",
        "Assets/Scenes/Level6.unity",
    };

    [MenuItem("Build/Windows x64 Levels 1-6 (zip)")]
    public static void BuildWindowsLevels1to6()
    {
        const string dir = "Builds/Levels1-6";
        const string exe = "Builds/Levels1-6/Stickman.exe";
        const string zip = "Builds/Stickman-Levels1-6-Windows.zip";

        Directory.CreateDirectory(dir);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Levels1to6,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Levels 1-6 Windows build failed: " + report.summary.result);
            return;
        }

        ZipFolder(dir, zip);
        Debug.Log("Levels 1-6 Windows build ready: " + Path.GetFullPath(zip));
        EditorUtility.RevealInFinder(zip);
    }

    [MenuItem("Build/Windows x64 (zip)")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory(WindowsDir);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = WindowsExe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Windows build failed: " + report.summary.result);
            return;
        }

        ZipFolder(WindowsDir, WindowsZip);
        Debug.Log("Windows build ready: " + Path.GetFullPath(WindowsZip));
        EditorUtility.RevealInFinder(WindowsZip);
    }

    [MenuItem("Build/WebGL (zip)")]
    public static void BuildWebGL()
    {
        // Disabled compression keeps a plain static host simple; itch.io / Unity Play
        // also accept this build.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        Directory.CreateDirectory(WebGLDir);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = WebGLDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("WebGL build failed: " + report.summary.result);
            return;
        }

        ZipFolder(WebGLDir, WebGLZip);
        Debug.Log("WebGL build ready: " + Path.GetFullPath(WebGLZip));
        EditorUtility.RevealInFinder(WebGLZip);
    }

    static void ZipFolder(string folder, string zipPath)
    {
        // Unity can leave a debug backup folder in the build; do not ship it.
        string backup = Path.Combine(folder, "Stickman_BackUpThisFolder_ButDontShipItWithYourGame");
        if (Directory.Exists(backup)) Directory.Delete(backup, true);

        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(folder, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
    }
}
