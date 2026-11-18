using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker: the first screen when the game launches (Boot scene).
// Plain black, the Code Breaker logo fades in, the main menu loads in the background,
// then the logo fades out and the main menu opens. Any key skips once loading is done.
public class StartupScreen : MonoBehaviour
{
    public CanvasGroup logoGroup;
    public RectTransform logo;
    public string nextScene = "MainMenu";
    public float fadeIn = 1.2f;
    [Tooltip("Minimum time the logo is fully visible (seconds).")]
    public float hold = 1.4f;
    public float fadeOut = 0.8f;

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        if (logoGroup != null) logoGroup.alpha = 0f;

        yield return null;   // one clean black frame first
        yield return Fade(0f, 1f, fadeIn, 0.94f, 1f);

        // Load the main menu in the background while the logo is fully visible
        // (started after the fade-in, so loading can never make the fade stutter).
        AsyncOperation op = Application.CanStreamedLevelBeLoaded(nextScene) ? SceneManager.LoadSceneAsync(nextScene) : null;
        if (op != null) op.allowSceneActivation = false;
        else Debug.LogError("[Code Breaker] '" + nextScene + "' is not in Build Settings.");

        float held = 0f;
        while (held < hold || (op != null && op.progress < 0.9f))
        {
            held += Time.unscaledDeltaTime;
            if (held > 0.4f && op != null && op.progress >= 0.9f && Input.anyKeyDown) break;   // skip
            yield return null;
        }

        yield return Fade(1f, 0f, fadeOut, 1f, 1.03f);
        Cursor.visible = true;
        if (op != null) op.allowSceneActivation = true;
    }

    IEnumerator Fade(float from, float to, float duration, float scaleFrom, float scaleTo)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            if (logoGroup != null) logoGroup.alpha = Mathf.Lerp(from, to, k);
            if (logo != null) logo.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, k);
            yield return null;
        }
        if (logoGroup != null) logoGroup.alpha = to;
        if (logo != null) logo.localScale = Vector3.one * scaleTo;
    }
}
