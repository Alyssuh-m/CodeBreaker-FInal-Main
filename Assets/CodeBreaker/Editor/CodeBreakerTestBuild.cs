using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Code Breaker > Build
//  - Windows Test Build:          normal build of every enabled scene (Boot, MainMenu, Level 1-3) into
//                                 <project>/Builds/CodeBreaker_Windows/CodeBreaker.exe
//  - Run Windows Test Build:      starts that .exe (same as double-clicking it)
//  - Fullscreen Self-Test Build:  Development build into Builds/CodeBreaker_Windows_DevTest
//  - Run Fullscreen Self-Test:    starts the Development build with -fullscreenSelfTest (see
//                                 FullscreenSelfTest.cs); the results go to Player.log
// Each build writes a summary to Logs/CB_TestBuild.txt. Player Settings are not changed.
public static class CodeBreakerTestBuild
{
    const string Folder = "Builds/CodeBreaker_Windows";
    const string DevFolder = "Builds/CodeBreaker_Windows_DevTest";

    [MenuItem("Code Breaker/Build/Windows Test Build", false, 500)]
    static void Build() => DoBuild(Folder, BuildOptions.None);

    [MenuItem("Code Breaker/Build/Run Windows Test Build", false, 501)]
    static void Run() => Launch(Folder, "");

    [MenuItem("Code Breaker/Build/Fullscreen Self-Test Build (Development)", false, 520)]
    static void BuildDev() => DoBuild(DevFolder, BuildOptions.Development);

    [MenuItem("Code Breaker/Build/Run Fullscreen Self-Test", false, 521)]
    static void RunDev() => Launch(DevFolder, "-fullscreenSelfTest");

    static void DoBuild(string folder, BuildOptions opts)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(folder);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = folder + "/CodeBreaker.exe",
            target = BuildTarget.StandaloneWindows64,
            options = opts,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        string text = "TEST BUILD " + System.DateTime.Now + "  (" + opts + ")\nresult: " + report.summary.result +
                      "\nerrors: " + report.summary.totalErrors + "  warnings: " + report.summary.totalWarnings +
                      "\ntime: " + report.summary.totalTime + "\noutput: " + Path.GetFullPath(options.locationPathName) +
                      "\nscenes: " + string.Join(", ", scenes) +
                      "\nPlayer Settings fullscreen mode: " + PlayerSettings.fullScreenMode +
                      "  native resolution: " + PlayerSettings.defaultIsNativeResolution + "\n";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_TestBuild.txt", text);
        Debug.Log(text);
    }

    static void Launch(string folder, string args)
    {
        string exe = Path.GetFullPath(folder + "/CodeBreaker.exe");
        if (!File.Exists(exe)) { Debug.LogWarning("No build yet: " + exe); return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args)
            { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
    }

    [MenuItem("Code Breaker/Build/Windows Test Build", true)]
    [MenuItem("Code Breaker/Build/Fullscreen Self-Test Build (Development)", true)]
    static bool NotPlaying() => !Application.isPlaying;
}
