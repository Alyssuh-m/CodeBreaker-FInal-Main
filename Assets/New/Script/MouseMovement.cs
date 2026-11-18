using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    private Transform playerBody;
    private float xRotation = 0f;

    // Code Breaker: steady mouse look (fix for sudden turns).
    // Causes found:
    //  1. Input.GetAxis("Mouse X/Y") is already "how far the mouse moved this frame". The old code
    //     also multiplied it by Time.deltaTime, so the same hand movement turned the camera more on a
    //     slow frame and less on a fast one. In the built game the frame rate is much higher and
    //     changes a lot (uncapped), so turning sped up and slowed down by itself and jumped whenever
    //     the frame rate dipped. The mouse value is now used as it is, with a fixed scale chosen so
    //     the speed equals the old speed at 60 FPS (the same Settings sensitivity feels the same).
    //  2. When the cursor is locked again (after a puzzle screen / pause menu) or the window gets
    //     focus back, the OS reports one large fake movement (the cursor jumping back to the
    //     centre). The first frames after that are ignored.
    //  3. Mouse look kept turning while the cursor was free or the game was paused (timeScale 0).
    const float ReferenceFrameTime = 1f / 60f;   // old feel at 60 FPS, now at every frame rate
    const int SettleFrames = 2;          // frames ignored after re-lock / focus
    const float MaxStepPerFrame = 60f;   // safety limit for a single frame's raw mouse value
    int settle = SettleFrames;
    CursorLockMode lastLock;

    void Start()
    {
        // Kusa nitong kukunin ang Player parent object para hindi mo na i-drag sa Inspector
        // The body is the player object itself (the one with PlayerMovement), not simply the
        // camera's parent: the camera now sits in a "View Height" holder used for crouching, and
        // turning that holder instead of the body made W walk the wrong way.
        PlayerMovement mover = GetComponentInParent<PlayerMovement>();
        playerBody = mover != null ? mover.transform : transform.parent;

        // Code Breaker: use the sensitivity chosen in Settings (keeps this value if never changed)
        mouseSensitivity = CodeBreakerSettings.GetMouseSensitivity(mouseSensitivity);
        CodeBreakerSettings.Changed += OnSettingsChanged;

        // I-lock ang cursor sa gitna ng screen
        Cursor.lockState = CursorLockMode.Locked;
        lastLock = Cursor.lockState;
        xRotation = NormalizePitch(transform.localEulerAngles.x);
    }

    void OnSettingsChanged() => mouseSensitivity = CodeBreakerSettings.GetMouseSensitivity(mouseSensitivity);

    void OnDestroy() => CodeBreakerSettings.Changed -= OnSettingsChanged;

    // Re-enabled after a menu / puzzle screen closed: let the cursor settle first.
    void OnEnable() => settle = SettleFrames;

    void OnApplicationFocus(bool focus)
    {
        if (focus) settle = SettleFrames;
    }

    static float NormalizePitch(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return Mathf.Clamp(a, -90f, 90f);
    }

    void Update()
    {
        // only steer while the cursor is locked to the game and the game isn't paused
        CursorLockMode lockNow = Cursor.lockState;
        if (lockNow != lastLock) { lastLock = lockNow; settle = SettleFrames; }
        if (lockNow != CursorLockMode.Locked || Time.timeScale <= 0f) return;

        float rawX = Input.GetAxis("Mouse X");
        float rawY = Input.GetAxis("Mouse Y");
        if (settle > 0) { settle--; return; }   // drop the fake jump after re-locking
        rawX = Mathf.Clamp(rawX, -MaxStepPerFrame, MaxStepPerFrame);
        rawY = Mathf.Clamp(rawY, -MaxStepPerFrame, MaxStepPerFrame);

        float mouseX = rawX * mouseSensitivity * ReferenceFrameTime;
        float mouseY = rawY * mouseSensitivity * ReferenceFrameTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Camera lang ang titingin pataas at pababa
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Ang mismong Player parent ang iikot pakaliwa at pakanan
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
