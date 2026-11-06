using UnityEngine;
using UnityEngine.EventSystems;

// Code Breaker: small helpers shared by the menus.
public static class UIUtil
{
    // Menus need a free, visible cursor; gameplay (MouseMovement) locks it again on its own.
    public static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Buttons only work with an EventSystem in the scene; add one if a scene has none.
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Debug.Log("[Code Breaker] Added an EventSystem so the menu buttons can be clicked.");
        _ = es;
    }

    // True when the player presses a navigation / submit key while nothing is selected
    // (e.g. after clicking empty space), so the menu can put the highlight back on a button.
    public static bool NeedsKeyboardReselect()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return false;
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
               Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Tab) ||
               Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    public static void Select(GameObject go)
    {
        if (EventSystem.current == null || go == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(go);
    }
}
