using UnityEngine;

namespace CodeBreaker.Level2
{
    // Level 2 sound effects.
    // Clips that exist in the project are assigned by the setup tool (doors, success chime).
    // Any slot left empty gets a short sound generated in code, so nothing is silent and no
    // external audio is needed. Assign real clips to these fields later to replace them.
    public class L2Audio : MonoBehaviour
    {
        [Header("Assign clips here (empty = generated placeholder sound)")]
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public AudioClip doorLocked;
        public AudioClip unlock;
        public AudioClip pickup;
        public AudioClip buttonClick;
        public AudioClip lightSwitch;
        public AudioClip drawer;
        public AudioClip keypadBeep;
        public AudioClip success;
        public AudioClip failure;
        public AudioClip musicBox;

        [Range(0f, 1f)] public float volume = 0.85f;

        AudioSource ui, world;

        void Awake()
        {
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;

            var w = new GameObject("L2 World Audio");
            w.transform.SetParent(transform, false);
            world = w.AddComponent<AudioSource>();
            world.playOnAwake = false;
            world.spatialBlend = 1f;
            world.rolloffMode = AudioRolloffMode.Linear;
            world.minDistance = 6f;
            world.maxDistance = 60f;

            if (doorLocked == null) doorLocked = L2ToneGen.Rattle();
            if (unlock == null) unlock = L2ToneGen.Unlock();
            if (pickup == null) pickup = L2ToneGen.Pickup();
            if (buttonClick == null) buttonClick = L2ToneGen.Click(2400f, 0.035f);
            if (lightSwitch == null) lightSwitch = L2ToneGen.Click(1700f, 0.05f);
            if (drawer == null) drawer = L2ToneGen.Slide();
            if (keypadBeep == null) keypadBeep = L2ToneGen.Beep(1250f, 0.08f);
            if (success == null) success = L2ToneGen.Arpeggio();
            if (failure == null) failure = L2ToneGen.Buzz();
            if (musicBox == null) musicBox = L2ToneGen.MusicBox();
        }

        float Vol => volume * CodeBreakerSettings.SfxVolume;

        public void Play(AudioClip c, float v = 1f)
        {
            if (c != null && ui != null) ui.PlayOneShot(c, v * Vol);
        }

        public void PlayAt(AudioClip c, Vector3 pos, float v = 1f)
        {
            if (c == null || world == null) return;
            world.transform.position = pos;
            world.PlayOneShot(c, v * Vol);
        }

        public void Door(bool opening, Vector3 pos) => PlayAt(opening ? doorOpen : doorClose, pos, 0.8f);
        public void Locked(Vector3 pos) => PlayAt(doorLocked, pos, 0.8f);
        public void Unlock(Vector3 pos) => PlayAt(unlock, pos, 0.9f);
        public void Pickup() => Play(pickup, 0.6f);
        public void Click() => Play(buttonClick, 0.5f);
        public void Switch(Vector3 pos) => PlayAt(lightSwitch, pos, 0.7f);
        public void Drawer(Vector3 pos) => PlayAt(drawer, pos, 0.7f);
        public void Beep() => Play(keypadBeep, 0.45f);
        public void Success() => Play(success, 0.65f);
        public void Failure() => Play(failure, 0.5f);
        public void MusicBox(Vector3 pos) => PlayAt(musicBox, pos, 0.9f);
    }

    // Small generated placeholder sounds (no external assets).
    public static class L2ToneGen
    {
        const int Rate = 44100;

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            AudioClip c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Env(float t, float attack, float decay) => Mathf.Clamp01(t / attack) * Mathf.Exp(-t / decay);

        public static AudioClip Click(float freq, float len) => Make("L2 Click", len, t =>
        {
            float noise = Random.Range(-1f, 1f) * Mathf.Exp(-t / 0.004f);
            return (0.5f * noise + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * t)) * Env(t, 0.0005f, len * 0.3f);
        });

        public static AudioClip Beep(float freq, float len) => Make("L2 Beep", len, t =>
            0.6f * Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((len - t) / 0.02f));

        public static AudioClip Pickup() => Make("L2 Pickup", 0.22f, t =>
        {
            float f = Mathf.Lerp(620f, 1240f, t / 0.22f);
            return 0.45f * Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, 0.005f, 0.09f);
        });

        public static AudioClip Unlock() => Make("L2 Unlock", 0.32f, t =>
        {
            float a = Random.Range(-1f, 1f) * Mathf.Exp(-t / 0.006f);
            float t2 = t - 0.16f;
            float b = t2 > 0f ? Random.Range(-1f, 1f) * Mathf.Exp(-t2 / 0.008f) + 0.5f * Mathf.Sin(2f * Mathf.PI * 900f * t2) * Mathf.Exp(-t2 / 0.03f) : 0f;
            return 0.6f * (a + b);
        });

        public static AudioClip Rattle() => Make("L2 Locked", 0.35f, t =>
        {
            float k = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 14f * t));
            return 0.45f * Random.Range(-1f, 1f) * Mathf.Pow(k, 8f) * Mathf.Exp(-t / 0.2f);
        });

        public static AudioClip Slide()
        {
            float lp = 0f;
            return Make("L2 Drawer", 0.4f, t =>
            {
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.08f);   // low-passed noise = soft wood slide
                return 2.2f * lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f));
            });
        }

        public static AudioClip Buzz() => Make("L2 Wrong", 0.32f, t =>
        {
            float f = t < 0.15f ? 220f : 175f;
            float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
            return 0.18f * sq * Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.32f - t) / 0.03f);
        });

        public static AudioClip Arpeggio() => Make("L2 Success", 0.9f, t =>
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float ti = t - i * 0.11f;
                if (ti > 0f) s += 0.22f * Mathf.Sin(2f * Mathf.PI * notes[i] * ti) * Env(ti, 0.004f, 0.35f);
            }
            return s;
        });

        public static AudioClip MusicBox() => Make("L2 Music Box", 6.5f, t =>
        {
            float[] melody = { 1046.5f, 1318.5f, 1568f, 1318.5f, 1174.7f, 1396.9f, 1760f, 1396.9f,
                               1046.5f, 1318.5f, 1568f, 2093f, 1975.5f, 1568f, 1318.5f, 1046.5f };
            const float step = 0.38f;
            float s = 0f;
            for (int i = 0; i < melody.Length; i++)
            {
                float ti = t - i * step;
                if (ti < 0f || ti > 2f) continue;
                s += 0.2f * (Mathf.Sin(2f * Mathf.PI * melody[i] * ti) + 0.3f * Mathf.Sin(4f * Mathf.PI * melody[i] * ti)) * Env(ti, 0.002f, 0.45f);
            }
            return s;
        });
    }
}
