using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Code Breaker: a human-shaped shadow for the first-person player.
//
// The player object only has a tall Cylinder, so it used to throw a long blob-shaped shadow. This
// component (added automatically to the object tagged "Player" in every level, nothing to set up)
//  - stops the Cylinder from casting a shadow (the Cylinder itself is left untouched otherwise), and
//  - builds an invisible "shadow body": head, neck, torso, hips, arms and legs made of simple shapes
//    that are rendered ONLY into shadow maps (ShadowCastingMode.ShadowsOnly). The player never sees
//    the body itself, only its shadow on the floor and walls.
// The body is sized from the player's CharacterController, turns with the player, and swings its arms
// and legs while walking (speed measured from the player's actual movement), so the shadow walks and
// stands still with you. It has no colliders and no scripts that touch input, camera or gameplay.
public class PlayerShadowBody : MonoBehaviour
{
    [Tooltip("Walking speed (units / second) at which the full stride is used")]
    public float fullStrideSpeed = 9f;
    [Tooltip("Length of one full walk cycle (two steps), as a multiple of the body height")]
    public float cycleLength = 1.4f;

    Transform root, hipL, hipR, kneeL, kneeR, shoulderL, shoulderR, elbowL, elbowR, torso;
    Vector3 lastPos;
    float phase, walk;
    float height;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        AttachToPlayer();
        SceneManager.sceneLoaded += (s, m) => AttachToPlayer();
    }

    static void AttachToPlayer()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null || p.GetComponent<CharacterController>() == null) return;
        if (p.GetComponent<PlayerShadowBody>() == null) p.AddComponent<PlayerShadowBody>();
    }

    void Start()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) { enabled = false; return; }

        // the old blob shadow
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r.transform != transform && r.GetComponentInParent<PlayerShadowBody>() == this && r.name == "Cylinder")
                r.shadowCastingMode = ShadowCastingMode.Off;

        // CharacterController size in the player's local space (the player object can be scaled)
        height = cc.height;                                   // local units
        float bottom = cc.center.y - cc.height * 0.5f;        // local y of the feet
        Build(bottom, cc.height, cc.radius);
        lastPos = transform.position;
    }

    Material ShadowMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        return new Material(sh) { name = "Shadow Body (runtime)" };
    }

    Transform Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        g.layer = gameObject.layer;
        return g.transform;
    }

    static Transform Joint(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    // Proportions are fractions of the body height H (feet at 0, top of the head at H).
    void Build(float bottom, float H, float radius)
    {
        Material m = ShadowMaterial();
        root = Joint("Shadow Body", transform, new Vector3(0f, bottom, 0f));

        float hipY = 0.50f * H, shoulderY = 0.81f * H;
        float legLen = 0.235f * H, shinLen = 0.245f * H;
        float armLen = 0.17f * H, foreLen = 0.16f * H;
        float limb = 0.065f * H;

        // head + neck
        Part("Head", PrimitiveType.Sphere, root, new Vector3(0f, 0.925f * H, 0.01f * H), new Vector3(0.12f, 0.14f, 0.13f) * H, m);
        Part("Neck", PrimitiveType.Cylinder, root, new Vector3(0f, 0.85f * H, 0f), new Vector3(0.055f * H, 0.03f * H, 0.055f * H), m);
        // torso: chest + waist, a little wider at the shoulders
        torso = Joint("Torso", root, new Vector3(0f, 0.68f * H, 0f));
        Part("Chest", PrimitiveType.Capsule, torso, new Vector3(0f, 0.04f * H, 0f), new Vector3(0.24f * H, 0.11f * H, 0.13f * H), m);
        Part("Waist", PrimitiveType.Capsule, torso, new Vector3(0f, -0.08f * H, 0f), new Vector3(0.2f * H, 0.09f * H, 0.12f * H), m);
        Part("Hips", PrimitiveType.Capsule, root, new Vector3(0f, 0.52f * H, 0f), new Vector3(0.21f * H, 0.06f * H, 0.13f * H), m);

        // legs: hip -> thigh -> knee -> shin -> foot
        hipL = Joint("Hip L", root, new Vector3(-0.055f * H, hipY, 0f));
        hipR = Joint("Hip R", root, new Vector3(0.055f * H, hipY, 0f));
        foreach (Transform hip in new[] { hipL, hipR })
        {
            Part("Thigh", PrimitiveType.Capsule, hip, new Vector3(0f, -legLen * 0.5f, 0f), new Vector3(limb * 1.25f, legLen * 0.55f, limb * 1.25f), m);
            Transform knee = Joint("Knee", hip, new Vector3(0f, -legLen, 0f));
            Part("Shin", PrimitiveType.Capsule, knee, new Vector3(0f, -shinLen * 0.5f, 0f), new Vector3(limb, shinLen * 0.55f, limb), m);
            Part("Foot", PrimitiveType.Cube, knee, new Vector3(0f, -shinLen + 0.012f * H, 0.035f * H), new Vector3(0.07f * H, 0.03f * H, 0.15f * H), m);
            if (hip == hipL) kneeL = knee; else kneeR = knee;
        }

        // arms: shoulder -> upper arm -> elbow -> forearm + hand
        shoulderL = Joint("Shoulder L", root, new Vector3(-0.15f * H, shoulderY, 0f));
        shoulderR = Joint("Shoulder R", root, new Vector3(0.15f * H, shoulderY, 0f));
        foreach (Transform sh in new[] { shoulderL, shoulderR })
        {
            Part("UpperArm", PrimitiveType.Capsule, sh, new Vector3(0f, -armLen * 0.5f, 0f), new Vector3(limb * 0.9f, armLen * 0.58f, limb * 0.9f), m);
            Transform elbow = Joint("Elbow", sh, new Vector3(0f, -armLen, 0f));
            Part("Forearm", PrimitiveType.Capsule, elbow, new Vector3(0f, -foreLen * 0.5f, 0f), new Vector3(limb * 0.75f, foreLen * 0.58f, limb * 0.75f), m);
            Part("Hand", PrimitiveType.Sphere, elbow, new Vector3(0f, -foreLen - 0.02f * H, 0f), new Vector3(0.045f, 0.06f, 0.035f) * H, m);
            if (sh == shoulderL) elbowL = elbow; else elbowR = elbow;
        }
        // relaxed arms hang slightly away from the body
        shoulderL.localRotation = Quaternion.Euler(0f, 0f, -6f);
        shoulderR.localRotation = Quaternion.Euler(0f, 0f, 6f);
    }

    void LateUpdate()
    {
        if (root == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // horizontal speed from the real movement (works with any movement script, teleports ignored)
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        float speed = d.magnitude / dt;
        if (speed > fullStrideSpeed * 4f) speed = 0f;          // a teleport, not walking

        float target = Mathf.Clamp01(speed / fullStrideSpeed);
        walk = Mathf.MoveTowards(walk, target, dt * 4f);
        // stride frequency follows the distance travelled; drift back to standing when stopped
        phase += speed * dt * Mathf.PI * 2f / Mathf.Max(0.2f, height * cycleLength);
        if (walk < 0.01f) phase = Mathf.MoveTowards(phase, Mathf.Round(phase / Mathf.PI) * Mathf.PI, dt * 3f);

        float sw = Mathf.Sin(phase);
        float legSwing = 30f * walk, armSwing = 24f * walk;
        hipL.localRotation = Quaternion.Euler(sw * legSwing, 0f, 0f);
        hipR.localRotation = Quaternion.Euler(-sw * legSwing, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, -sw) * 40f * walk, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, sw) * 40f * walk, 0f, 0f);
        shoulderL.localRotation = Quaternion.Euler(-sw * armSwing, 0f, -6f);
        shoulderR.localRotation = Quaternion.Euler(sw * armSwing, 0f, 6f);
        elbowL.localRotation = Quaternion.Euler(-12f * walk - 4f, 0f, 0f);
        elbowR.localRotation = Quaternion.Euler(-12f * walk - 4f, 0f, 0f);
        // small bob and lean while walking
        root.localPosition = new Vector3(0f, root.localPosition.y, 0f);
        torso.localRotation = Quaternion.Euler(4f * walk, Mathf.Sin(phase) * 4f * walk, 0f);
    }
}
