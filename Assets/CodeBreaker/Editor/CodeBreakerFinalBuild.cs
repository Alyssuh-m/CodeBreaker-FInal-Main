using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Code Breaker > Build > Final PC Application
//  1. Pre-flight check:  every scene in Build Settings exists and is enabled, and each scene is opened
//                        and checked for missing scripts / missing prefabs. Nothing is changed.
//  2. Build:             Windows 64-bit release build of Boot, MainMenu, Level 1, Level 2, Level 3 (in that
//                        order) into  <Downloads>/Code Breaker - Final PC Application/Code Breaker.exe
//                        The only setting changed is the product name ("My project (1)" -> "Code Breaker"),
//                        which is the window title. Quality, graphics, input and audio settings are not touched.
//  3. Launch test:       starts the .exe with a log file, waits, records what the log says, then closes it.
// Everything is written to Logs/CB_FinalBuild.txt.
public static class CodeBreakerFinalBuild
{
    const string LogPath = "Logs/CB_FinalBuild.txt";
    const string FolderName = "Code Breaker - Final PC Application";
    const string ExeName = "Code Breaker.exe";
    static StringBuilder log;

    // next to the project folders, outside the Unity project (short path, the project is not touched)
    public static string OutputDir
    {
        get
        {
            string project = Path.GetFullPath(".");
            DirectoryInfo d = new DirectoryInfo(project);
            while (d != null && d.Name != "Downloads") d = d.Parent;
            string root = d != null ? d.FullName : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
            return Path.Combine(root, FolderName);
        }
    }

    static void Begin(string title) { log = new StringBuilder("\n==== " + title + "  " + System.DateTime.Now + "\n"); }
    static void End() { Directory.CreateDirectory("Logs"); File.AppendAllText(LogPath, log.ToString()); Debug.Log("[FinalBuild] done -> " + LogPath); }

    // ================================================================== 1. pre-flight
    [MenuItem("Code Breaker/Build/Final PC Application/1. Pre-flight check", false, 600)]
    static void Preflight()
    {
        Begin("PRE-FLIGHT");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { log.AppendLine("cancelled"); End(); return; }
        string active = SceneManager.GetActiveScene().path;
        log.AppendLine("editor version " + Application.unityVersion + ", active build target " + EditorUserBuildSettings.activeBuildTarget);
        log.AppendLine("Windows 64-bit player support installed: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
        log.AppendLine("scripting backend: " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) +
                       ", product name '" + PlayerSettings.productName + "', fullscreen " + PlayerSettings.fullScreenMode +
                       ", quality level " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " (" + string.Join(", ", QualitySettings.names) + ")" +
                       ", colour space " + PlayerSettings.colorSpace);
        int i = 0;
        foreach (var s in EditorBuildSettings.scenes)
        {
            bool exists = File.Exists(s.path);
            log.AppendLine("scene " + i++ + ": " + s.path + " enabled " + s.enabled + " exists " + exists);
            if (!exists || !s.enabled) continue;
            Scene sc = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int missingScripts = 0, missingPrefabs = 0, objects = 0;
            foreach (GameObject root in sc.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    objects++;
                    foreach (var c in t.GetComponents<Component>()) if (c == null) { missingScripts++; if (missingScripts <= 5) log.AppendLine("     missing script on " + PathOf(t)); }
                    if (PrefabUtility.IsPrefabAssetMissing(t.gameObject)) { missingPrefabs++; if (missingPrefabs <= 5) log.AppendLine("     missing prefab " + PathOf(t)); }
                }
            log.AppendLine("     objects " + objects + ", missing scripts " + missingScripts + ", missing prefabs " + missingPrefabs);
        }
        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active);
        log.AppendLine("output folder: " + OutputDir);
        End();
    }

    // ================================================================== 2. build
    [MenuItem("Code Breaker/Build/Final PC Application/2. Build Windows 64-bit", false, 601)]
    static void Build()
    {
        Begin("FINAL BUILD");
        if (Application.isPlaying) { log.AppendLine("leave Play mode first"); End(); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { log.AppendLine("cancelled"); End(); return; }
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        log.AppendLine("scenes (in order): " + string.Join(", ", scenes));

        if (PlayerSettings.productName != "Code Breaker")
        {
            log.AppendLine("product name '" + PlayerSettings.productName + "' -> 'Code Breaker' (window title)");
            PlayerSettings.productName = "Code Breaker";
            AssetDatabase.SaveAssets();
        }

        string dir = OutputDir;
        Directory.CreateDirectory(dir);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(dir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var sum = report.summary;
        log.AppendLine("result " + sum.result + ", errors " + sum.totalErrors + ", warnings " + sum.totalWarnings +
                       ", time " + sum.totalTime + ", size " + (sum.totalSize / (1024f * 1024f)).ToString("F1") + " MB");
        log.AppendLine("output " + sum.outputPath);
        foreach (var step in report.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception)
                    log.AppendLine("  ERROR [" + step.name + "] " + m.content.Split('\n')[0]);
        int w = 0;
        foreach (var step in report.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Warning && w++ < 30) log.AppendLine("  warning [" + step.name + "] " + m.content.Split('\n')[0]);
        if (sum.result == BuildResult.Succeeded) ListOutput(dir);
        End();
    }

    static void ListOutput(string dir)
    {
        log.AppendLine("folder contents:");
        foreach (string f in Directory.GetFileSystemEntries(dir).OrderBy(x => x))
        {
            bool isDir = Directory.Exists(f);
            long size = isDir ? Directory.GetFiles(f, "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length) : new FileInfo(f).Length;
            log.AppendLine("  " + Path.GetFileName(f) + (isDir ? "/" : "") + "  " + (size / 1024f / 1024f).ToString("F1") + " MB");
        }
        string data = Path.Combine(dir, "Code Breaker_Data");
        if (Directory.Exists(data))
            log.AppendLine("Code Breaker_Data: " + string.Join(", ", Directory.GetFileSystemEntries(data).Select(Path.GetFileName).OrderBy(x => x)));
    }

    // ================================================================== 3. launch test
    static Process testProcess;
    static double testStart;
    static double TestSeconds = 75;

    [MenuItem("Code Breaker/Build/Final PC Application/3. Launch test (75 s, then closes)", false, 602)]
    static void LaunchTest() => StartTest(75);

    [MenuItem("Code Breaker/Build/Final PC Application/5. Launch for a longer check (closes after 6 min)", false, 603)]
    static void LaunchLong() => StartTest(360);

    static void StartTest(double seconds)
    {
        TestSeconds = seconds;
        Begin("LAUNCH TEST");
        string exe = Path.Combine(OutputDir, ExeName);
        if (!File.Exists(exe)) { log.AppendLine("no build at " + exe); End(); return; }
        string testLog = Path.GetFullPath("Logs/CB_FinalBuild_Player.log");
        if (File.Exists(testLog)) File.Delete(testLog);
        testProcess = Process.Start(new ProcessStartInfo(exe, "-logFile \"" + testLog + "\"") { WorkingDirectory = OutputDir, UseShellExecute = false });
        testStart = EditorApplication.timeSinceStartup;
        log.AppendLine("started " + exe + " (pid " + testProcess.Id + "), log " + testLog);
        EditorApplication.update -= WatchTest; EditorApplication.update += WatchTest;
    }

    static void WatchTest()
    {
        if (testProcess == null) { EditorApplication.update -= WatchTest; return; }
        bool exited = testProcess.HasExited;
        if (!exited && EditorApplication.timeSinceStartup - testStart < TestSeconds) return;
        EditorApplication.update -= WatchTest;
        if (exited) log.AppendLine("the game closed by itself after " + (EditorApplication.timeSinceStartup - testStart).ToString("F0") + " s, exit code " + testProcess.ExitCode);
        else
        {
            log.AppendLine("still running after " + TestSeconds + " s (responding " + testProcess.Responding + ") -> closing it");
            try { testProcess.CloseMainWindow(); if (!testProcess.WaitForExit(5000)) testProcess.Kill(); } catch (System.Exception e) { log.AppendLine("close: " + e.Message); }
        }
        testProcess = null;
        string testLog = Path.GetFullPath("Logs/CB_FinalBuild_Player.log");
        System.Threading.Thread.Sleep(500);
        if (File.Exists(testLog))
        {
            string[] lines = File.ReadAllLines(testLog);
            log.AppendLine("player log: " + lines.Length + " lines");
            foreach (string l in lines.Where(l => l.Contains("Loaded scene") || l.Contains("UnloadTime") || l.Contains("Direct3D") || l.StartsWith("    Renderer") ||
                                                  l.Contains("Exception") || l.Contains("Error") || l.Contains("error") || l.Contains("Missing") || l.Contains("[Fullscreen]") ||
                                                  l.Contains("The referenced script") || l.Contains("Fallback handler")).Take(60))
                log.AppendLine("  | " + l);
        }
        else log.AppendLine("no player log was written");
        End();
    }

    // ================================================================== 4. short-path copy + build
    // The project folder path is ~170 characters, so files Unity writes deep inside Library/ and Temp/
    // (Burst temp files, some package files) go past Windows' 260-character limit and the build's last
    // step fails. This copies Assets, Packages and ProjectSettings to a short folder (the original project
    // is only read, never changed) and builds that copy with a second Unity in batch mode.
    public const string ShortCopy = @"C:\Users\jonar\CB\CodeBreaker";
    static Process batch;
    static string batchLog;

    [MenuItem("Code Breaker/Build/Final PC Application/4. Copy to short path + build there", false, 610)]
    static void CopyAndBuild()
    {
        Begin("SHORT-PATH COPY + BUILD");
        if (Application.isPlaying) { log.AppendLine("leave Play mode first"); End(); return; }
        AssetDatabase.SaveAssets();
        string src = Path.GetFullPath(".");
        string dst = ShortCopy;
        if (Directory.Exists(dst)) { log.AppendLine("removing old copy " + dst); DeleteLong(dst); }
        int files = 0; long bytes = 0; var failed = new List<string>();
        foreach (string top in new[] { "Assets", "Packages", "ProjectSettings" })
            CopyTree(Path.Combine(src, top), Path.Combine(dst, top), ref files, ref bytes, failed);
        log.AppendLine("copied " + files + " files, " + (bytes / 1024f / 1024f).ToString("F0") + " MB from " + src + " to " + dst + ", failed " + failed.Count);
        foreach (string f in failed.Take(20)) log.AppendLine("   copy failed: " + f);
        if (failed.Count > 0) { End(); return; }

        string unity = EditorApplication.applicationPath;
        batchLog = Path.Combine(Path.GetDirectoryName(dst), "batch_build_log.txt");
        if (File.Exists(batchLog)) File.Delete(batchLog);
        string args = "-batchmode -quit -projectPath \"" + dst + "\" -buildTarget Win64 -executeMethod CodeBreakerFinalBuild.BuildFromCommandLine" +
                      " -cbOutput \"" + OutputDir + "\" -logFile \"" + batchLog + "\"";
        batch = Process.Start(new ProcessStartInfo(unity, args) { UseShellExecute = false, CreateNoWindow = true });
        log.AppendLine("started batch build (pid " + batch.Id + "): " + unity + " " + args);
        End();
        EditorApplication.update -= WatchBatch; EditorApplication.update += WatchBatch;
    }

    static double lastCheck;
    static void WatchBatch()
    {
        if (batch == null) { EditorApplication.update -= WatchBatch; return; }
        if (EditorApplication.timeSinceStartup - lastCheck < 5) return;
        lastCheck = EditorApplication.timeSinceStartup;
        if (!batch.HasExited) return;
        EditorApplication.update -= WatchBatch;
        Begin("BATCH BUILD FINISHED");
        log.AppendLine("exit code " + batch.ExitCode + " (0 = success)");
        string summary = Path.Combine(ShortCopy, "Logs", "CB_FinalBuild.txt");
        if (File.Exists(summary)) log.AppendLine(File.ReadAllText(summary));
        batch = null;
        End();
    }

    // called by the batch-mode Unity in the short-path copy
    public static void BuildFromCommandLine()
    {
        string[] a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, "-cbOutput");
        string dir = i >= 0 && i + 1 < a.Length ? a[i + 1] : OutputDir;
        Begin("BATCH FINAL BUILD (" + Path.GetFullPath(".") + ")");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        log.AppendLine("scenes (in order): " + string.Join(", ", scenes));
        PlayerSettings.productName = "Code Breaker";
        if (Directory.Exists(dir)) { log.AppendLine("clearing old output " + dir); Directory.Delete(dir, true); }
        Directory.CreateDirectory(dir);
        var options = new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = Path.Combine(dir, ExeName),
            target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var sum = report.summary;
        log.AppendLine("result " + sum.result + ", errors " + sum.totalErrors + ", warnings " + sum.totalWarnings +
                       ", time " + sum.totalTime + ", size " + (sum.totalSize / (1024f * 1024f)).ToString("F1") + " MB");
        foreach (var step in report.steps)
            foreach (var m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception)
                    log.AppendLine("  ERROR [" + step.name + "] " + m.content.Split('\n')[0]);
        if (sum.result == BuildResult.Succeeded)
        {
            // debug symbols for crash reports; Unity names it "DoNotShip"
            string burstDebug = Path.Combine(dir, "Code Breaker_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(burstDebug)) { Directory.Delete(burstDebug, true); log.AppendLine("removed Code Breaker_BurstDebugInformation_DoNotShip (debug symbols, not part of the game)"); }
            ListOutput(dir);
        }
        End();
        EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string Long(string p) => p.StartsWith(@"\\?\") ? p : @"\\?\" + Path.GetFullPath(p);

    static void CopyTree(string from, string to, ref int files, ref long bytes, List<string> failed)
    {
        Directory.CreateDirectory(Long(to));
        foreach (string f in Directory.GetFiles(Long(from)))
        {
            string name = Path.GetFileName(f);
            try { File.Copy(f, Long(Path.Combine(to, name)), true); files++; bytes += new FileInfo(f).Length; }
            catch (System.Exception e) { failed.Add(f + " : " + e.Message); }
        }
        foreach (string d in Directory.GetDirectories(Long(from)))
        {
            string name = Path.GetFileName(d);
            if (name == ".git") continue;
            CopyTree(d.Substring(4), Path.Combine(to, name), ref files, ref bytes, failed);
        }
    }

    static void DeleteLong(string dir)
    {
        foreach (string f in Directory.GetFiles(Long(dir), "*", SearchOption.AllDirectories)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); }
        Directory.Delete(Long(dir), true);
    }

    static string PathOf(Transform t) { string s = t.name; while ((t = t.parent) != null) s = t.name + "/" + s; return s; }

    [MenuItem("Code Breaker/Build/Final PC Application/2. Build Windows 64-bit", true)]
    [MenuItem("Code Breaker/Build/Final PC Application/1. Pre-flight check", true)]
    static bool NotPlaying() => !Application.isPlaying;
}
