using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // A wall light switch. Turns only its own room's / hallway's lights on and off.
    // "On" restores each light to how it was set up in the scene, so the original lighting is kept.
    // The state is remembered in checkpoints and never changes by itself when the player walks around.
    public class L2LightSwitch : L2Interactable
    {
        [Tooltip("Unique id of the area, e.g. bedroom, hall_b")]
        public string group;
        [Tooltip("Shown in notifications, e.g. Bedroom")]
        public string areaName;
        public Light[] lights = new Light[0];
        [Tooltip("The rocker that flips when pressed")]
        public Transform rocker;
        public float rockerAngle = 14f;

        public bool IsOn { get; private set; } = true;
        public event System.Action<bool> Toggled;

        bool[] original;
        Coroutine anim;

        void Awake()
        {
            original = new bool[lights.Length];
            for (int i = 0; i < lights.Length; i++) original[i] = lights[i] != null && lights[i].enabled;
        }

        void Start()
        {
            bool on = true;
            if (Game != null && Game.TryGetLightState(group, out bool saved)) on = saved;
            Apply(on, false);
        }

        public override string Prompt => "Press E to Interact";

        public override void Interact() => Apply(!IsOn, true);

        void Apply(bool on, bool fx)
        {
            IsOn = on;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].enabled = on && original[i];

            if (rocker != null)
            {
                if (anim != null) StopCoroutine(anim);
                if (fx && isActiveAndEnabled) anim = StartCoroutine(Flip(on));
                else rocker.localRotation = Quaternion.Euler(on ? -rockerAngle : rockerAngle, 0f, 0f);
            }

            if (Game != null)
            {
                Game.RecordLight(group, on);
                if (fx)
                {
                    if (Game.sfx != null) Game.sfx.Switch(transform.position);
                    Game.Notify(areaName + ": lights turned " + (on ? "on." : "off."), L2NoteKind.Info);
                }
            }
            Toggled?.Invoke(on);
        }

        IEnumerator Flip(bool on)
        {
            Quaternion from = rocker.localRotation;
            Quaternion to = Quaternion.Euler(on ? -rockerAngle : rockerAngle, 0f, 0f);
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                rocker.localRotation = Quaternion.Slerp(from, to, t / 0.08f);
                yield return null;
            }
            rocker.localRotation = to;
            anim = null;
        }
    }
}
