using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the loading screen shown when entering a level (built by
// Code Breaker > UI > Build Menus and Loading Screens). Each level shows its own image and text
// from the Level Catalog. ShowPlain() is the simple black fade used for the main menu.
public class LevelLoadingScreen : MonoBehaviour
{
    public CanvasGroup group;
    [Tooltip("Full-screen level image")]
    public Image background;
    [Tooltip("Everything that belongs to the level version (title, description, artwork)")]
    public GameObject levelContent;
    public TMP_Text levelLabel;
    public TMP_Text levelTitle;
    public TMP_Text levelDescription;
    public TMP_Text tipText;
    [Header("Progress")]
    public GameObject progressRoot;
    [Tooltip("Coloured fill inside the bar; its right anchor follows the progress")]
    public RectTransform progressFill;
    public TMP_Text progressText;
    public TMP_Text loadingLabel;

    float shown, target;

    public void ShowLevel(LevelCatalog.LevelInfo info, string sceneName, string[] tips)
    {
        gameObject.SetActive(true);
        if (levelContent != null) levelContent.SetActive(true);
        if (progressRoot != null) progressRoot.SetActive(true);

        Sprite img = info != null ? info.loadingImage : null;
        if (background != null)
        {
            background.sprite = img;
            background.color = img != null ? Color.white : new Color(0.05f, 0.04f, 0.04f, 1f);
            AspectRatioFitter fit = background.GetComponent<AspectRatioFitter>();
            if (fit != null && img != null) fit.aspectRatio = img.rect.width / img.rect.height;
        }
        if (levelLabel != null) levelLabel.text = info != null && !string.IsNullOrEmpty(info.label) ? info.label : sceneName.ToUpper();
        if (levelTitle != null) levelTitle.text = info != null && !string.IsNullOrEmpty(info.title) ? info.title : sceneName.ToUpper();
        if (levelDescription != null) levelDescription.text = info != null ? info.description : "";
        if (tipText != null)
        {
            bool hasTips = tips != null && tips.Length > 0;
            tipText.gameObject.SetActive(hasTips);
            if (hasTips) tipText.text = tips[Random.Range(0, tips.Length)];
        }
        if (loadingLabel != null) loadingLabel.text = "LOADING";
    }

    public void ShowPlain()
    {
        gameObject.SetActive(true);
        if (levelContent != null) levelContent.SetActive(false);
        if (progressRoot != null) progressRoot.SetActive(false);
        if (background != null) { background.sprite = null; background.color = Color.black; }
    }

    public void SetProgress(float p, bool instant)
    {
        target = Mathf.Clamp01(p);
        if (instant) { shown = target; Apply(); }
    }

    void Update()
    {
        if (Mathf.Approximately(shown, target)) return;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2.5f);
        Apply();
    }

    void Apply()
    {
        if (progressFill != null) progressFill.anchorMax = new Vector2(Mathf.Max(0.001f, shown), progressFill.anchorMax.y);
        if (progressText != null) progressText.text = Mathf.RoundToInt(shown * 100f) + "%";
    }

    public IEnumerator Fade(float to, float duration)
    {
        if (group == null) yield break;
        group.blocksRaycasts = true;   // nothing underneath can be clicked while loading
        float from = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    public void HideImmediate()
    {
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        gameObject.SetActive(false);
    }

    // Plain black fade, used only if the prefab is missing from Resources.
    public static LevelLoadingScreen CreateFallback(Transform parent)
    {
        GameObject go = new GameObject("Loading Screen (fallback)", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 900;
        GameObject bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        RectTransform rt = (RectTransform)bg.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = Color.black;
        LevelLoadingScreen s = go.AddComponent<LevelLoadingScreen>();
        s.group = go.GetComponent<CanvasGroup>();
        s.background = bg.GetComponent<Image>();
        return s;
    }
}
