using UnityEngine;

// The footstep sounds used by LevelFootsteps when a level is played without the Game Session
// (for example pressing Play in Level 2 or 3 in the editor). Same clips as Level 1.
// Asset: Resources/CodeBreaker/FootstepSet (Code Breaker > Audio > Create Footstep Set).
public class FootstepSet : ScriptableObject
{
    public AudioClip[] walk;
}
