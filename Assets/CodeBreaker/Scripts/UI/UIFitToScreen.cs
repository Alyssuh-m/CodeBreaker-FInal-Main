using UnityEngine;

// Code Breaker: keeps a fixed-layout UI element (e.g. the lectern question page) as large as
// possible on any screen without stretching it. The element keeps its design size and is
// scaled uniformly, so the text and margins inside it keep exactly the same proportions.
[RequireComponent(typeof(RectTransform))]
public class UIFitToScreen : MonoBehaviour
{
    [Tooltip("Width x height the layout was designed at")]
    public Vector2 designSize = new Vector2(540f, 720f);
    [Tooltip("How much of the available screen (width or height, whichever is tighter) it may use")]
    [Range(0.3f, 1f)] public float fill = 0.92f;
    [Tooltip("Never scale beyond this (keeps text crisp on huge monitors)")]
    public float maxScale = 3f;
    [Tooltip("Part of the screen height kept free at the top (e.g. for the HUD timer); the element is centred below it")]
    [Range(0f, 0.4f)] public float topReserve = 0f;

    RectTransform rt, parent;
    Vector2 lastParentSize;

    void OnEnable()
    {
        rt = (RectTransform)transform;
        parent = rt.parent as RectTransform;
        Apply();
    }

    void LateUpdate()
    {
        if (parent != null && parent.rect.size != lastParentSize) Apply();
    }

    public void Apply()
    {
        if (rt == null) rt = (RectTransform)transform;
        if (parent == null) parent = rt.parent as RectTransform;
        if (parent == null || designSize.x <= 0f || designSize.y <= 0f) return;

        Vector2 avail = parent.rect.size;
        lastParentSize = avail;
        if (avail.x <= 0f || avail.y <= 0f) return;

        float freeHeight = avail.y * (1f - topReserve);
        float s = Mathf.Min(avail.x * fill / designSize.x, freeHeight * fill / designSize.y);
        s = Mathf.Min(s, maxScale);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -avail.y * topReserve * 0.5f);
        rt.sizeDelta = designSize;
        rt.localScale = new Vector3(s, s, 1f);
    }
}
