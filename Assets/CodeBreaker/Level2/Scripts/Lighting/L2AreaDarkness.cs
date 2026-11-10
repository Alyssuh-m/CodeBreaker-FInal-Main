using UnityEngine;

namespace CodeBreaker.Level2
{
    // The scene's flat ambient light keeps every room bright even with its lamps off. While the player
    // stands in a room / hallway whose switch is off, this fades the ambient and reflection light down
    // so the room really looks dark (and the glow stars stand out); it fades back when the player
    // leaves or switches the lights on. The original values are restored when the level unloads.
    // Nothing in the project's lighting or URP settings is changed.
    public class L2AreaDarkness : MonoBehaviour
    {
        [System.Serializable]
        public class Area
        {
            public string name;
            public L2LightSwitch lightSwitch;
            public Vector3 min;
            public Vector3 max;
        }

        public Area[] areas = new Area[0];
        [Range(0f, 1f)] public float darkAmbient = 0.08f;
        [Range(0f, 1f)] public float darkReflection = 0.08f;
        public float fadeSpeed = 2.5f;

        Color origAmbient, origSky, origEquator, origGround;
        float origReflection;
        float level;   // 0 = normal, 1 = dark
        bool captured;

        void Start()
        {
            origAmbient = RenderSettings.ambientLight;
            origSky = RenderSettings.ambientSkyColor;
            origEquator = RenderSettings.ambientEquatorColor;
            origGround = RenderSettings.ambientGroundColor;
            origReflection = RenderSettings.reflectionIntensity;
            captured = true;
        }

        void Update()
        {
            L2Game g = L2Game.Instance;
            if (!captured || g == null || g.player == null) return;
            float target = InDarkArea(g.player.position) ? 1f : 0f;
            if (Mathf.Approximately(level, target)) return;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * fadeSpeed);
            Apply();
        }

        bool InDarkArea(Vector3 p)
        {
            foreach (Area a in areas)
            {
                if (a == null || a.lightSwitch == null || a.lightSwitch.IsOn) continue;
                if (p.x >= a.min.x && p.x <= a.max.x && p.y >= a.min.y && p.y <= a.max.y && p.z >= a.min.z && p.z <= a.max.z)
                    return true;
            }
            return false;
        }

        void Apply()
        {
            float k = Mathf.Lerp(1f, darkAmbient, level);
            RenderSettings.ambientLight = origAmbient * k;
            RenderSettings.ambientSkyColor = origSky * k;
            RenderSettings.ambientEquatorColor = origEquator * k;
            RenderSettings.ambientGroundColor = origGround * k;
            RenderSettings.reflectionIntensity = Mathf.Lerp(origReflection, origReflection * darkReflection, level);
        }

        void OnDisable()
        {
            if (!captured) return;
            level = 0f;
            Apply();
        }
    }
}
