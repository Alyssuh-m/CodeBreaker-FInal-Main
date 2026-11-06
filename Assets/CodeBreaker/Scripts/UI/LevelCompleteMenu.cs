using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the three buttons on the Congratulations screen.
//   TRY AGAIN    -> restart this level from the beginning (HP as it was when the level started)
//   NEXT LEVEL   -> loading screen of the next level (disabled with "More levels coming soon" if there is none)
//   EXIT TO HOME -> back to the main menu
// The actual work is done by GameFlowManager; a click disables all three, so a load can only start once.
public class LevelCompleteMenu : MonoBehaviour
{
    public CanvasGroup group;
    public Button tryAgainButton;
    public Button nextLevelButton;
    public Button exitHomeButton;
    public TMP_Text nextLevelLabel;
    public string nextLevelText = "NEXT LEVEL";
    public string noNextLevelText = "MORE LEVELS COMING SOON";
    public float fadeTime = 0.35f;

    bool canGoNext;
    Coroutine fade;

    void Awake()
    {
        if (tryAgainButton != null) tryAgainButton.onClick.AddListener(() => Choose(0));
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(() => Choose(1));
        if (exitHomeButton != null) exitHomeButton.onClick.AddListener(() => Choose(2));
    }

    public bool IsOpen => gameObject.activeSelf;

    void Update()
    {
        if (UIUtil.NeedsKeyboardReselect())
            UIUtil.Select(canGoNext && nextLevelButton != null && nextLevelButton.interactable ? nextLevelButton.gameObject
                        : tryAgainButton != null ? tryAgainButton.gameObject : null);
    }

    public void Show(bool hasNextLevel, bool showNextButton = true)
    {
        canGoNext = hasNextLevel;
        UIUtil.EnsureEventSystem();
        UIUtil.ShowCursor();
        gameObject.SetActive(true);

        SetInteractable(true);
        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(showNextButton);
            nextLevelButton.interactable = hasNextLevel;
        }
        if (nextLevelLabel != null) nextLevelLabel.text = hasNextLevel ? nextLevelText : noNextLevelText;

        if (fade != null) StopCoroutine(fade);
        if (group != null) { group.alpha = 0f; fade = StartCoroutine(FadeIn()); }
        UIUtil.Select(hasNextLevel && nextLevelButton != null ? nextLevelButton.gameObject
                                                              : tryAgainButton != null ? tryAgainButton.gameObject : null);
    }

    public void Hide()
    {
        if (fade != null) { StopCoroutine(fade); fade = null; }
        gameObject.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        yield return new WaitForSecondsRealtime(0.25f);   // let the title land first
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeTime;
            yield return null;
        }
        group.alpha = 1f;
        fade = null;
    }

    void SetInteractable(bool on)
    {
        if (tryAgainButton != null) tryAgainButton.interactable = on;
        if (nextLevelButton != null) nextLevelButton.interactable = on && canGoNext;
        if (exitHomeButton != null) exitHomeButton.interactable = on;
    }

    void Choose(int option)
    {
        GameFlowManager gm = GameFlowManager.Instance;
        if (gm == null || SceneLoader.IsLoading) return;
        if (option == 1 && !canGoNext) return;

        SetInteractable(false);   // one click only: no duplicate scene loads
        bool started = option == 0 ? gm.RetryLevel() : option == 1 ? gm.GoToNextLevel() : gm.ExitToHome();
        if (!started) SetInteractable(true);   // e.g. the scene is missing from Build Settings
    }
}
