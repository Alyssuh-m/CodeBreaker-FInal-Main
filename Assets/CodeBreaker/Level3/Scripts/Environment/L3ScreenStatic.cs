using UnityEngine;

namespace CodeBreaker.Level3
{
    // Decorative CCTV static: jumps the noise texture to a random offset many times a second.
    // Uses a property block, so the shared material is never changed.
    [RequireComponent(typeof(Renderer))]
    public class L3ScreenStatic : MonoBehaviour
    {
        public float rate = 24f;
        public Vector2 tiling = new Vector2(1.4f, 1.1f);
        Renderer rend; MaterialPropertyBlock block; float next;

        void Awake() { rend = GetComponent<Renderer>(); block = new MaterialPropertyBlock(); }

        void Update()
        {
            if (rend == null || !rend.isVisible || Time.time < next) return;
            next = Time.time + 1f / rate;
            rend.GetPropertyBlock(block);
            block.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, Random.value, Random.value));
            rend.SetPropertyBlock(block);
        }
    }
}
