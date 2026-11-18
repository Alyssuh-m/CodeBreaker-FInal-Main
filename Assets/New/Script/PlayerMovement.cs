using UnityEngine;

// First-person movement for every level (Level 1, 2 and 3 all use this component).
//
//  Walk     WASD / arrows, relative to where the player faces.
//  Jump     Space. Only when the feet are on the ground: no jumping again in the air, a short grace
//           period after walking off an edge, and a press just before landing is remembered for a
//           moment so jumps feel responsive. Hitting a ceiling stops the rise. Falls are a little
//           faster than rises, and the view dips slightly on a hard landing.
//  Crouch   Hold Left Ctrl. The capsule shrinks (feet stay on the floor), the view lowers smoothly
//           and walking is slower. Releasing Ctrl stands up again, but only when there is room:
//           under a low obstacle the player stays crouched until it is clear.
//
// Ground detection uses the CharacterController itself. The old ground-check sphere sat in the
// middle of the body on the player's own layer, so it always touched the player's own collider and
// reported "grounded" in mid-air, which allowed endless jumps.
public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;

    public float speed = 12f;
    public float gravity = -19.62f;

    [Header("Jump")]
    [Tooltip("Jump height in world units (the player is about 3 units tall).")]
    public float jumpApexHeight = 1.2f;
    [Tooltip("Gravity multiplier while falling, so landings are firm instead of floaty.")]
    public float fallGravityMultiplier = 1.6f;
    [Tooltip("Seconds after leaving an edge during which a jump is still allowed.")]
    public float coyoteTime = 0.1f;
    [Tooltip("Seconds a Space press is remembered before landing.")]
    public float jumpBufferTime = 0.1f;
    public float maxFallSpeed = 40f;

    [Header("Crouch (hold Left Ctrl)")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    [Tooltip("Crouched height as a fraction of the standing height.")]
    [Range(0.4f, 0.9f)] public float crouchHeightFactor = 0.6f;
    [Tooltip("Walking speed while crouched, as a fraction of normal speed.")]
    [Range(0.2f, 1f)] public float crouchSpeedFactor = 0.5f;
    [Tooltip("How fast the body and view go down / up (fraction of the change per second).")]
    public float crouchTransitionSpeed = 10f;

    [Header("Landing")]
    [Tooltip("How far the view dips on a hard landing, in world units.")]
    public float landingDip = 0.12f;

    // Kept so existing scenes load without warnings; ground detection no longer uses them.
    [HideInInspector] public float jumpHeight = 3f;
    [HideInInspector] public Transform groundCheck;
    [HideInInspector] public float groundDistance = 0.4f;
    [HideInInspector] public LayerMask groundMask;

    public bool IsGrounded { get; private set; }
    public bool IsCrouching { get; private set; }

    Vector3 velocity;
    float lastGroundedTime = -10f;
    float jumpPressedTime = -10f;
    bool wasGrounded = true;
    float airFallSpeed;

    // capsule (player-local units)
    float standHeight, standCenterY, currentHeight;
    float eyeAboveFeet;           // camera height above the feet when standing (player-local units)
    // view: the camera sits inside a small holder that moves for crouch / landing, so effects that
    // move the camera itself (the burn shake in Level 1) keep working unchanged.
    Transform viewHolder;
    float viewOffset;             // current crouch offset applied to the holder (player-local units)
    float landOffset, landKick;   // landing dip (world units)
    Transform shadowBody;
    static readonly Collider[] overlap = new Collider[16];

    void Start()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null) { enabled = false; return; }
        standHeight = controller.height;
        standCenterY = controller.center.y;
        currentHeight = standHeight;
        SetupViewHolder();
        Camera cam = GetComponentInChildren<Camera>(true);
        float feetLocal = standCenterY - standHeight * 0.5f;
        float camLocalY = cam != null ? transform.InverseTransformPoint(cam.transform.position).y : standCenterY + standHeight * 0.5f;
        eyeAboveFeet = Mathf.Max(standHeight, camLocalY - feetLocal);
    }

    float Ratio => standHeight > 0f ? currentHeight / standHeight : 1f;

    void SetupViewHolder()
    {
        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam == null) return;
        Transform camT = cam.transform;
        if (camT.parent != null && camT.parent.name == "View Height") { viewHolder = camT.parent; return; }
        if (camT.parent != transform) return;   // unexpected rig: leave it alone (crouch still works, view stays)
        viewHolder = new GameObject("View Height").transform;
        viewHolder.SetParent(transform, false);
        viewHolder.localPosition = Vector3.zero;
        viewHolder.localRotation = Quaternion.identity;
        viewHolder.localScale = Vector3.one;
        camT.SetParent(viewHolder, false);       // same local pose as before, now under the holder
    }

    void OnDisable()
    {
        jumpPressedTime = -10f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || controller == null || !controller.enabled) return;

        UpdateCrouch(dt);

        // ---- ground state (from the last Move)
        bool grounded = controller.isGrounded;
        if (grounded)
        {
            if (!wasGrounded) OnLanded(airFallSpeed);
            lastGroundedTime = Time.time;
            airFallSpeed = 0f;
            if (velocity.y < 0f) velocity.y = -2f;   // keep the capsule pressed onto the floor / stairs
        }
        else if (velocity.y < 0f) airFallSpeed = Mathf.Max(airFallSpeed, -velocity.y);
        wasGrounded = grounded;
        IsGrounded = grounded;

        // ---- walking
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();   // diagonals are not faster
        float moveSpeed = speed * (IsCrouching ? crouchSpeedFactor : 1f);

        // ---- jump: only from the ground (or just after leaving it), never again in the air
        if (Input.GetButtonDown("Jump")) jumpPressedTime = Time.time;
        bool wantsJump = Time.time - jumpPressedTime <= jumpBufferTime;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime && velocity.y <= 0f && !IsCrouching;
        if (wantsJump && canJump)
        {
            velocity.y = Mathf.Sqrt(2f * jumpApexHeight * -gravity);
            jumpPressedTime = -10f;
            lastGroundedTime = -10f;   // the grace period is used up: no second jump
            wasGrounded = false;
        }

        // ---- gravity (a little stronger on the way down)
        float g = gravity * (velocity.y < 0f ? fallGravityMultiplier : 1f);
        velocity.y = Mathf.Max(velocity.y + g * dt, -maxFallSpeed);

        CollisionFlags flags = controller.Move((move * moveSpeed + Vector3.up * velocity.y) * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;   // bumped the head

        UpdateView(dt);
    }

    // ------------------------------------------------------------------ crouch
    void UpdateCrouch(float dt)
    {
        bool wantCrouch = Input.GetKey(crouchKey);
        if (wantCrouch) IsCrouching = true;
        else if (IsCrouching && HasHeadroom()) IsCrouching = false;

        float target = IsCrouching ? standHeight * crouchHeightFactor : standHeight;
        if (Mathf.Abs(currentHeight - target) < 0.0005f) { currentHeight = target; ApplyHeight(); return; }

        float next = Mathf.Lerp(currentHeight, target, 1f - Mathf.Exp(-crouchTransitionSpeed * dt));
        if (Mathf.Abs(next - target) < 0.002f) next = target;
        if (next > currentHeight && !FitsAtHeight(next)) return;   // something moved in above: wait
        currentHeight = next;
        ApplyHeight();
    }

    // Grow / shrink from the feet: the bottom of the capsule never moves, so the player never sinks
    // into the floor or pops up.
    void ApplyHeight()
    {
        float minH = controller.radius * 2f + 0.01f;
        float h = Mathf.Max(currentHeight, minH);
        if (Mathf.Approximately(controller.height, h)) return;
        controller.height = h;
        controller.center = new Vector3(controller.center.x, standCenterY - (standHeight - h) * 0.5f, controller.center.z);

        if (shadowBody == null) shadowBody = transform.Find("Shadow Body");
        if (shadowBody != null) shadowBody.localScale = new Vector3(1f, h / standHeight, 1f);   // shadow crouches too
    }

    bool HasHeadroom() => FitsAtHeight(standHeight);

    // Top of the player's head above the feet (player-local): the view sits above the capsule in
    // these levels, so the head is the higher of the two, plus a small margin.
    float HeadLocal(float ratio)
    {
        float sy = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        return Mathf.Max(standHeight, eyeAboveFeet + 0.25f / sy) * ratio;
    }

    // Is there room to grow to this height (feet where they are now)? Checks the space between the
    // current head and the new head for solid colliders, ignoring the player's own and triggers.
    bool FitsAtHeight(float h)
    {
        float target = standHeight > 0f ? h / standHeight : 1f;
        if (target <= Ratio + 0.0001f) return true;
        Vector3 scale = transform.lossyScale;
        float r = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.92f;
        float sy = Mathf.Abs(scale.y);
        float bottomLocal = controller.center.y - controller.height * 0.5f;
        Vector3 up = transform.up;
        Vector3 feet = transform.TransformPoint(new Vector3(controller.center.x, bottomLocal, controller.center.z));
        Vector3 from = feet + up * Mathf.Max(r, HeadLocal(Ratio) * sy - r);
        Vector3 to = feet + up * Mathf.Max(r, HeadLocal(target) * sy - r);
        if ((to - from).sqrMagnitude < 1e-6f) to = from + up * 0.001f;
        int n = Physics.OverlapCapsuleNonAlloc(from, to, r, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = overlap[i];
            if (c == null || c == controller) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ view (crouch height + landing dip)
    void OnLanded(float fallSpeed)
    {
        if (fallSpeed < 4f) return;   // stepping off a stair is not a landing
        landKick = -Mathf.Clamp((fallSpeed - 4f) * 0.02f, 0f, landingDip);
    }

    void UpdateView(float dt)
    {
        if (viewHolder == null) return;
        float sy = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        // the eye comes down with the body: same fraction as the capsule
        float crouch = -eyeAboveFeet * (1f - Ratio);
        // landing: ease down quickly, then recover
        landOffset = Mathf.Lerp(landOffset, landKick, 1f - Mathf.Exp(-28f * dt));
        landKick = Mathf.Lerp(landKick, 0f, 1f - Mathf.Exp(-9f * dt));
        viewOffset = crouch + landOffset / sy;
        viewHolder.localPosition = new Vector3(0f, viewOffset, 0f);
        viewHolder.localRotation = Quaternion.identity;   // the holder only moves up/down, never turns
    }
}
