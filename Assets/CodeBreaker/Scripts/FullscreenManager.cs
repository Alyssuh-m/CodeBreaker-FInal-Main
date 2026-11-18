using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker: automatic fullscreen.
//
// The game always runs in borderless fullscreen (FullScreenMode.FullScreenWindow) at the monitor's
// native resolution, from the first frame of the Boot scene through MainMenu, Level 1, Level 2 and
// Level 3.
//
// How it starts: there is nothing to place in a scene. Before the first scene loads, Unity calls
// Bootstrap() below, which creates ONE "Fullscreen Manager" object and keeps it alive across scene
// loads (DontDestroyOnLoad). It works no matter which scene the game (or the Editor) starts in, and
// a second copy is never created: Bootstrap runs once per launch and Awake destroys any extra copy.
//
// Why it is needed even though Player Settings already say "Fullscreen Window": a Windows build
// remembers the last window mode/size per user (registry), so after Alt+Enter or an older windowed
// build the game could start windowed. This script applies fullscreen again at startup and after every
// scene load. It only touches the display (Screen.*): no input, camera, UI, audio or gameplay.
public class FullscreenManager : MonoBehaviour
{
    public static FullscreenManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("Fullscreen Manager");
        go.AddComponent<FullscreenManager>();   // Awake sets Instance and DontDestroyOnLoad
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // never two managers
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply("startup");
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply("scene " + scene.name);

    // The monitor's native resolution (falls back to the desktop resolution).
    static Vector2Int NativeResolution()
    {
        Display d = Display.main;
        if (d != null && d.systemWidth > 0 && d.systemHeight > 0) return new Vector2Int(d.systemWidth, d.systemHeight);
        Resolution r = Screen.currentResolution;
        return new Vector2Int(r.width, r.height);
    }

    // Borderless fullscreen at native resolution. Does nothing when that is already the case, so scene
    // changes never cause a flicker or a display reset.
    public static void Apply(string reason)
    {
#if UNITY_EDITOR
        // The Editor's Game view can't go fullscreen; the build is where this matters.
        Debug.Log("[Fullscreen] (" + reason + ") skipped in the Editor; the Windows build opens in fullscreen.");
#else
        Vector2Int native = NativeResolution();
        bool modeOk = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
        bool sizeOk = Screen.width == native.x && Screen.height == native.y;
        if (modeOk && sizeOk)
        {
            Debug.Log("[Fullscreen] (" + reason + ") already FullScreenWindow " + native.x + "x" + native.y);
            return;
        }
        string was = Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode;
        Screen.SetResolution(native.x, native.y, FullScreenMode.FullScreenWindow);
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Debug.Log("[Fullscreen] (" + reason + ") set FullScreenWindow " + native.x + "x" + native.y + " (was " + was + ")");
#endif
    }
}
