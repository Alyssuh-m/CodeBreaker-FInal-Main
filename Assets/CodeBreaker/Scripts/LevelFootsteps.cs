using UnityEngine;
using UnityEngine.Audio;

// Footsteps for Level 2 and Level 3, the same as Level 1.
//
// Level 1's footsteps come from the AudioManager on the Game Session. That manager mutes its
// gameplay sounds when a level ends, so it stays silent in Levels 2 and 3. This component plays
// the same step sounds (Level 1's clips, or the FootstepSet in Resources when the level is played
// on its own in the editor), with the same rules:
//  - steps come from how far the player actually walked on the ground, so they stop the moment
//    the player stops and never play while standing, in the air, during teleports or while the
//    controls are off (puzzle screens, pause);
//  - never the same sample twice in a row, with a slight random pitch;
//  - crouching gives quieter steps (and slower ones, because crouch walking is slower).
// While this component is active, the AudioManager skips its own footsteps (see Active), so
// steps are never doubled. Added automatically by L2Game (Levels 2 and 3 only).
public class LevelFootsteps : MonoBehaviour
{
    public float walkStride = 5f;          // same as Level 1
    [Range(0f, 1f)] public float volume = 0.55f;
    [Range(0f, 1f)] public float crouchVolume = 0.6f;   // multiplier while crouched

    AudioClip[] clips;
    AudioSource source;
    CharacterController cc;
    PlayerMovement mover;
    Vector3 lastPos;
    float strideProgress;
    int lastStep = -1;
    bool hasLast;

    public int StepCount { get; private set; }   // for testing

    // True while a level footstep player is running: the AudioManager then leaves footsteps to it.
    public static bool Active { get; private set; }
    void OnEnable() { if (source != null) Active = true; }
    void OnDisable() { Active = false; }
    void OnDestroy() { Active = false; }

    void Start()
    {
        AudioManager am = AudioManager.Instance;
        if (am != null && am.footstepsWalk != null && am.footstepsWalk.Length > 0) clips = am.footstepsWalk;
        else
        {
            FootstepSet set = Resources.Load<FootstepSet>("CodeBreaker/FootstepSet");
            if (set != null) clips = set.walk;
        }
        if (clips == null || clips.Length == 0) { enabled = false; return; }

        GameObject go = new GameObject("Audio Footsteps (level)");
        go.transform.SetParent(transform, false);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        if (am != null && am.sfxGroup != null) source.outputAudioMixerGroup = am.sfxGroup;
        Active = true;
    }

    void Update()
    {
        if (source == null) return;
        if (cc == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            cc = p.GetComponent<CharacterController>();
            mover = p.GetComponent<PlayerMovement>();
            if (cc == null) return;
        }

        Vector3 pos = cc.transform.position;
        if (!hasLast) { lastPos = pos; hasLast = true; return; }
        Vector3 delta = pos - lastPos; delta.y = 0f;
        lastPos = pos;
        float dist = delta.magnitude;

        AudioManager am = AudioManager.Instance;
        bool canStep = Time.deltaTime > 0f && dist < 3f &&
                       (mover == null || mover.enabled) && cc.enabled && cc.isGrounded;
        float speed = Time.deltaTime > 0f ? dist / Time.deltaTime : 0f;

        if (!canStep || speed < 1.5f)
        {
            strideProgress = walkStride * 0.75f;   // the first step comes quickly when moving again
            return;
        }

        strideProgress += dist;
        if (strideProgress < walkStride) return;
        strideProgress = 0f;

        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastStep) i = (i + 1) % clips.Length;
        lastStep = i;
        source.pitch = Random.Range(0.94f, 1.06f);
        float level = am != null ? am.SfxLevel : 0.9f * CodeBreakerSettings.SfxVolume;
        bool crouched = mover != null && mover.IsCrouching;
        source.PlayOneShot(clips[i], volume * level * (crouched ? crouchVolume : 1f));
        StepCount++;
    }
}
