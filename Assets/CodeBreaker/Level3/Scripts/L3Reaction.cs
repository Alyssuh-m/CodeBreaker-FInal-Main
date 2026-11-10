using UnityEngine;

namespace CodeBreaker.Level3
{
    // Something that happens when a station is solved (open a door, print a keycard, lights on ...).
    // restoring = true when a saved game is loaded: jump straight to the end state, no sounds / effects.
    public abstract class L3Reaction : MonoBehaviour
    {
        public abstract void Run(bool restoring);

        public static void RunAll(L3Reaction[] list, bool restoring)
        {
            if (list == null) return;
            foreach (L3Reaction r in list)
                if (r != null) r.Run(restoring);
        }
    }
}
