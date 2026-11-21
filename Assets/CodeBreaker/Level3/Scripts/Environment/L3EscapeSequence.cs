using System.Collections;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Phase 9: the final escape. Runs once, when the central terminal accepts the exit code.
    //
    // It takes the terminal keypad's existing "on solved" reactions (the exit door, L3SlideDoor) and plays
    // them at the end of a short sequence instead of straight away:
    //   0.0 s  ACCESS GRANTED (terminal screen + message, from the existing keypad / L3Terminal)
    //   0.4 s  the siren cuts out (relay clunk), the distant alarm one-shots stop
    //   0.6 s  red emergency light fades to green over ~3 s, hall lights come back up
    //   1.6 s  steam vents either side of the exit door (hiss), the door's frame turns green
    //   2.6 s  the existing heavy door sound plays and the existing door slides open; the corridor lights up
    // The answer, the keypad, the door and the exit trigger (level complete) are the existing ones.
    // On a loaded game where the terminal is already solved it jumps to the end state silently.
    public class L3EscapeSequence : L3Reaction
    {
        public L3Keypad terminalKeypad;
        public L3ProgressEvents events;
        public ParticleSystem[] steam = new ParticleSystem[0];
        public AudioClip steamHiss, sirenCut, lockRelease;
        public Renderer[] exitFrame = new Renderer[0];
        public Light[] exitFrameLights = new Light[0];
        public Light[] greenFill = new Light[0];
        public Light corridorLight;
        public Color red = new Color(1f, 0.12f, 0.08f), green = new Color(0.2f, 1f, 0.4f);
        public float doorDelay = 2.6f;
        [Range(0f, 1f)] public float hallLevelAfter = 0.85f;

        L3Reaction[] doorReactions = new L3Reaction[0];
        bool ran;
        float[] fillTarget;
        float corridorTarget;
        MaterialPropertyBlock block;

        public bool Ran => ran;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (terminalKeypad != null)
            {
                // take over the door reaction(s) so the door opens at the end of the sequence (runs before L3Keypad.Start)
                var list = new System.Collections.Generic.List<L3Reaction>();
                foreach (L3Reaction r in terminalKeypad.onSolved) if (r != null && r != this) list.Add(r);
                doorReactions = list.ToArray();
                terminalKeypad.onSolved = new L3Reaction[] { this };
            }
            fillTarget = new float[greenFill.Length];
            for (int i = 0; i < greenFill.Length; i++) { if (greenFill[i] == null) continue; fillTarget[i] = greenFill[i].intensity; greenFill[i].intensity = 0f; greenFill[i].enabled = false; }
            if (corridorLight != null) { corridorTarget = corridorLight.intensity; corridorLight.intensity = 0f; corridorLight.enabled = false; }
            foreach (ParticleSystem p in steam) if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetFrame(red, 1f);
        }

        public override void Run(bool restoring)
        {
            if (ran) return;                       // never twice
            ran = true;
            if (restoring) { EndState(); L3Reaction.RunAll(doorReactions, true); return; }
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            L2Game g = L2Game.Instance;
            yield return new WaitForSeconds(0.4f);

            // siren stops
            if (events != null && events.siren != null) events.siren.Stop();
            StopDistantAlarm();
            Play(sirenCut, transform.position, 0.9f);

            // red -> green
            Light[] reds = events != null && events.emergencyRoot != null ? events.emergencyRoot.GetComponentsInChildren<Light>(true) : new Light[0];
            float[] redStart = new float[reds.Length];
            for (int i = 0; i < reds.Length; i++) redStart[i] = reds[i].intensity;
            bool emergency = events != null && events.EmergencyActive;
            float hallFrom = events != null ? events.failedLevel : 1f;
            foreach (Light l in greenFill) if (l != null) l.enabled = true;
            yield return new WaitForSeconds(0.2f);

            float dur = 3.0f;
            bool steamDone = false, doorDone = false;
            for (float t = 0f; t < Mathf.Max(dur, doorDelay - 0.6f + 0.1f); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                for (int i = 0; i < reds.Length; i++)
                {
                    if (reds[i] == null) continue;
                    reds[i].color = Color.Lerp(red, green, k);
                    reds[i].intensity = Mathf.Lerp(redStart[i], redStart[i] * 0.35f, k);
                }
                for (int i = 0; i < greenFill.Length; i++) if (greenFill[i] != null) greenFill[i].intensity = fillTarget[i] * k;
                if (emergency && events != null) events.RestoreHallLights(Mathf.Lerp(hallFrom, hallLevelAfter, k));
                SetFrame(Color.Lerp(red, green, k), 1f);

                if (!steamDone && t >= 1.0f)
                {
                    steamDone = true;
                    foreach (ParticleSystem p in steam) if (p != null) p.Play(true);
                    if (steam.Length > 0 && steam[0] != null) Play(steamHiss, steam[0].transform.position, 0.8f);
                    if (steam.Length > 1 && steam[1] != null) Play(steamHiss, steam[1].transform.position, 0.6f);
                }
                if (!doorDone && t >= doorDelay - 0.6f)
                {
                    doorDone = true;
                    Play(lockRelease, terminalKeypad != null ? terminalKeypad.transform.position : transform.position, 0.8f);
                    L3Reaction.RunAll(doorReactions, false);         // existing heavy door sound + slide
                    if (g != null) g.Notify("The lockdown is lifted. The exit is open: get out!", L2NoteKind.Success);
                    StartCoroutine(Corridor());
                }
                yield return null;
            }
            // leave the hall in its calm, powered state
            if (events != null) { events.EndEmergency(); if (emergency) events.RestoreHallLights(hallLevelAfter); }
            yield return new WaitForSeconds(2.5f);
            foreach (ParticleSystem p in steam) if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        IEnumerator Corridor()
        {
            if (corridorLight == null) yield break;
            corridorLight.enabled = true;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) { corridorLight.intensity = corridorTarget * (t / 1.5f); yield return null; }
            corridorLight.intensity = corridorTarget;
        }

        void EndState()
        {
            if (events != null)
            {
                if (events.siren != null) events.siren.Stop();
                bool emergency = events.EmergencyActive;
                events.EndEmergency();
                if (emergency) events.RestoreHallLights(hallLevelAfter);
            }
            StopDistantAlarm();
            for (int i = 0; i < greenFill.Length; i++) if (greenFill[i] != null) { greenFill[i].enabled = true; greenFill[i].intensity = fillTarget[i]; }
            if (corridorLight != null) { corridorLight.enabled = true; corridorLight.intensity = corridorTarget; }
            SetFrame(green, 1f);
        }

        // The distant-alarm one-shots from the Phase 5 ambience use the siren clip: silence them for good.
        void StopDistantAlarm()
        {
            AudioClip siren = events != null && events.siren != null ? events.siren.clip : null;
            if (siren == null) return;
            foreach (L3AmbientOneShots a in FindObjectsByType<L3AmbientOneShots>(FindObjectsSortMode.None))
                foreach (L3AmbientOneShots.Entry e in a.entries)
                    if (System.Array.IndexOf(e.clips, siren) >= 0)
                    {
                        e.next = float.MaxValue;
                        if (e.source != null) e.source.Stop();
                    }
        }

        void SetFrame(Color c, float strength)
        {
            foreach (Renderer r in exitFrame)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c * 1.6f * strength);
                r.SetPropertyBlock(block);
            }
            foreach (Light l in exitFrameLights) if (l != null) l.color = c;
        }

        static void Play(AudioClip clip, Vector3 at, float vol)
        {
            L2Game g = L2Game.Instance;
            if (clip == null || g == null || g.sfx == null) return;
            g.sfx.PlayAt(clip, at, vol);
        }
    }
}
