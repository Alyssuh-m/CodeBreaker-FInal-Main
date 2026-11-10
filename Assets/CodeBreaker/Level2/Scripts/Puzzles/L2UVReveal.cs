using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // UV reveal (vanity mirror)
    // MOON is invisible under any normal light. It shows only while the UV flashlight is collected,
    // equipped, switched on and aimed at the mirror. The Bedroom light switch has no effect on it.
    public class L2UVReveal : MonoBehaviour
    {
        public Renderer text;
        [Tooltip("Transparent glow material for the hidden text")]
        public Material revealMaterial;
        public float maxAngle = 17f;
        public float maxDistance = 32f;
        public float fadeSpeed = 4f;

        Material inst;
        Color baseColor;
        float alpha;
        bool announced;

        public bool Revealed => alpha > 0.85f;

        void Start()
        {
            if (text == null) return;
            if (revealMaterial != null)
            {
                inst = new Material(revealMaterial);
                text.sharedMaterial = inst;
                baseColor = inst.GetColor("_BaseColor");
            }
            text.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Apply();
        }

        void Update()
        {
            if (text == null) return;
            bool lit = IsLitByUV();
            alpha = Mathf.MoveTowards(alpha, lit ? 1f : 0f, Time.deltaTime * fadeSpeed);
            Apply();

            L2Game g = L2Game.Instance;
            if (lit && alpha >= 0.99f && g != null && !g.Flag("moon_seen"))
            {
                g.SetFlag("moon_seen");
                if (!announced) { announced = true; g.Notify("Glowing letters appear on the mirror...", L2NoteKind.Success); }
                g.RefreshObjective();
            }
        }

        void Apply()
        {
            text.enabled = alpha > 0.01f;
            if (inst != null)
            {
                Color c = baseColor;
                c.a = baseColor.a * alpha;
                inst.SetColor("_BaseColor", c);
            }
        }

        bool IsLitByUV()
        {
            L2Game g = L2Game.Instance;
            if (g == null || g.Held != L2Items.UVFlashlight || !g.FlashlightOn || g.playerCamera == null) return false;
            Transform c = g.playerCamera.transform;
            Vector3 target = text.bounds.center;
            Vector3 d = target - c.position;
            if (d.magnitude > maxDistance) return false;
            if (Vector3.Angle(c.forward, d) > maxAngle) return false;
            return L2Interactor.HasLineOfSight(c.position, target, g.player);
        }
    }
}
