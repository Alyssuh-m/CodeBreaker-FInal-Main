// Test-only helper for FullscreenManager. It is compiled ONLY into Development builds
// (DEVELOPMENT_BUILD), so it never exists in a normal release build, and even there it only runs when
// the game is started with the command-line argument  -fullscreenSelfTest
// (Code Breaker > Build > Run Fullscreen Self-Test does that).
// It switches the game to a 1280x720 window (like pressing Alt+Enter), loads MainMenu, Level 1,
// Level 2 and Level 3 in turn, logs the screen state and the number of FullscreenManagers after each
// load to Player.log ("[FullscreenTest] ..."), then quits.
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FullscreenSelfTest : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fullscreenSelfTest") < 0) return;
        var go = new GameObject("Fullscreen Self-Test");
        DontDestroyOnLoad(go);
        go.AddComponent<FullscreenSelfTest>();
    }

    static void Log(string what) =>
        Debug.Log("[FullscreenTest] " + what + ": " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode +
                  "  native " + Display.main.systemWidth + "x" + Display.main.systemHeight +
                  "  managers=" + FindObjectsByType<FullscreenManager>(FindObjectsInactive.Include).Length);

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3f);
        Log("after launch (" + SceneManager.GetActiveScene().name + ")");
        foreach (string scene in new[] { "MainMenu", "Level 1", "Level 2", "Level 3", "MainMenu" })
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);   // simulate the player leaving fullscreen
            yield return new WaitForSecondsRealtime(1.5f);
            Log("windowed before loading " + scene);
            SceneManager.LoadScene(scene);
            yield return new WaitForSecondsRealtime(4f);
            Log("after loading " + scene);
        }
        Debug.Log("[FullscreenTest] done");
        Application.Quit();
    }
}
#endif
