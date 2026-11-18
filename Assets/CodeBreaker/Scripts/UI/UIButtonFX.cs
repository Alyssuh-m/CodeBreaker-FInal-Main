using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Code Breaker: hover / pressed / selected feedback for menu buttons, sliders and toggles.
// Works with the mouse and with keyboard / controller navigation (selected = highlighted).
[RequireComponent(typeof(Selectable))]
public class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("Soft red glow behind the button, faded in on hover")]
    public Graphic glow;
    [Tooltip("Thin red bar on the left edge, shown on hover")]
    public Graphic accent;
    public TMP_Text label;
    public Color labelNormal = new Color(0.85f, 0.81f, 0.76f);
    public Color labelHighlighted = Color.white;
    public Color labelDisabled = new Color(0.42f, 0.39f, 0.37f);
    public float hoverScale = 1.035f;
    public float pressedScale = 0.97f;
    public float speed = 14f;
    [Tooltip("Also select the button when the mouse moves over it (keeps mouse and keyboard highlight in sync)")]
    public bool selectOnHover = true;

    Selectable selectable;
    bool hovered, pressed, selected;
    Vector3 baseScale = Vector3.one;
    float glowAlpha;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
        if (glow != null) SetAlpha(glow, 0f);
        if (accent != null) SetAlpha(accent, 0f);
    }

    void OnDisable()
    {
        hovered = pressed = selected = false;
        transform.localScale = baseScale;
        glowAlpha = 0f;
        if (glow != null) SetAlpha(glow, 0f);
        if (accent != null) SetAlpha(accent, 0f);
    }

    void Update()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        bool lit = interactable && (hovered || selected);
        float dt = Time.unscaledDeltaTime * speed;

        float s = !interactable ? 1f : pressed ? pressedScale : lit ? hoverScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * s, dt);

        glowAlpha = Mathf.Lerp(glowAlpha, lit ? (pressed ? 0.85f : 0.55f) : 0f, dt);
        if (glow != null) SetAlpha(glow, glowAlpha);
        if (accent != null) SetAlpha(accent, Mathf.Clamp01(glowAlpha * 1.8f));
        if (label != null)
            label.color = Color.Lerp(label.color, !interactable ? labelDisabled : lit ? labelHighlighted : labelNormal, dt);
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color; c.a = a; g.color = c;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        if (selectOnHover && EventSystem.current != null && selectable != null && selectable.IsInteractable())
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = false; }
}
