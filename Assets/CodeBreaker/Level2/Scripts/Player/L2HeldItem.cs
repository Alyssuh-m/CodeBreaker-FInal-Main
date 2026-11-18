using UnityEngine;

namespace CodeBreaker.Level2
{
    // Shows the equipped item in first-person view at the bottom-right of the screen, built from the
    // item's own model in the level. The UV flashlight also gets a spot light that follows the camera.
    // The view object is not parented to the camera (the player has a non-uniform scale), it just
    // follows it every frame.
    public class L2HeldItem : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Position in front of the camera: right, up, forward (Unity units)")]
        public Vector3 offset = new Vector3(0.42f, -0.34f, 0.95f);
        public Color uvColor = new Color(0.5f, 0.25f, 1f);
        public float uvIntensity = 22f;
        public float uvRange = 45f;
        public float uvAngle = 34f;

        Transform root;
        Transform modelRoot;
        Light beam;
        Renderer[] lensGlow = new Renderer[0];
        string current;
        Vector3 sway;

        public Light Beam => beam;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            root = new GameObject("L2 Held Item View").transform;
            root.SetParent(transform, false);

            var b = new GameObject("UV Beam");
            b.transform.SetParent(root, false);
            beam = b.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = uvColor;
            beam.intensity = uvIntensity;
            beam.range = uvRange;
            beam.spotAngle = uvAngle;
            beam.innerSpotAngle = uvAngle * 0.55f;
            beam.shadows = LightShadows.None;
            beam.enabled = false;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Transform c = cam.transform;
            float t = Time.time;
            Vector3 target = new Vector3(Mathf.Sin(t * 1.3f) * 0.008f, Mathf.Sin(t * 2.1f) * 0.006f, 0f);
            sway = Vector3.Lerp(sway, target, 0.2f);
            root.SetPositionAndRotation(c.position + c.rotation * (offset + sway), c.rotation);
            beam.transform.SetPositionAndRotation(c.position + c.forward * 0.6f, c.rotation);
        }

        [Tooltip("Beam of an ordinary (white) flashlight item")]
        public Color whiteColor = new Color(1f, 0.95f, 0.85f);
        public float whiteIntensity = 30f;

        public void SetLight(bool on)
        {
            if (beam != null)
            {
                bool uv = current == L2Items.UVFlashlight;
                beam.color = uv ? uvColor : whiteColor;
                beam.intensity = uv ? uvIntensity : whiteIntensity;
                beam.enabled = on;
            }
            foreach (Renderer r in lensGlow)
                if (r != null) r.enabled = on;
        }

        public void Show(string itemId)
        {
            if (current == itemId && modelRoot != null) return;
            current = itemId;
            if (modelRoot != null) Destroy(modelRoot.gameObject);
            modelRoot = null;
            lensGlow = new Renderer[0];
            if (beam != null) beam.enabled = false;
            if (string.IsNullOrEmpty(itemId)) return;

            L2ItemDef def = L2Items.Get(itemId);
            Transform proto = L2Game.Instance != null ? L2Game.Instance.GetPrototype(itemId) : null;
            if (def == null || proto == null) return;

            modelRoot = BuildVisual(proto, def.viewSize, "Held " + def.name).transform;
            modelRoot.SetParent(root, false);
            modelRoot.localRotation = Quaternion.Euler(def.viewEuler);

        }

        // Copies the meshes of a level object (no colliders, no scripts), centred and scaled so its
        // longest side is 'size'. Also used to show items placed into puzzles.
        public static GameObject BuildVisual(Transform proto, float size, string name)
        {
            var holder = new GameObject(name);
            var inner = new GameObject("Model");
            inner.transform.SetParent(holder.transform, false);

            Matrix4x4 toLocal = proto.worldToLocalMatrix;
            Bounds b = new Bounds();
            bool hasBounds = false;
            foreach (MeshRenderer r in proto.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(r.name);
                part.transform.SetParent(inner.transform, false);
                Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var nr = part.AddComponent<MeshRenderer>();
                nr.sharedMaterials = r.sharedMaterials;
                nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nr.receiveShadows = false;

                // bounds of this part in the model's local space
                Bounds mb = mf.sharedMesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!hasBounds) { b = new Bounds(p, Vector3.zero); hasBounds = true; }
                    else b.Encapsulate(p);
                }
            }

            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float k = longest > 0.0001f ? size / longest : 1f;
            inner.transform.localScale = Vector3.one * k;
            inner.transform.localPosition = -b.center * k;
            return holder;
        }
    }
}
