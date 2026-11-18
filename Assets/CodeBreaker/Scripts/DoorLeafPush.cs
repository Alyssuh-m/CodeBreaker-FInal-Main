using System.Collections.Generic;
using UnityEngine;

// Code Breaker: solid swinging doors.
// Used by DoorInteraction (Level 1) and L2Door (Level 2) while a door leaf rotates.
// The leaf stays solid the whole time. Each frame, after the leaf has moved, the player's
// CharacterController is pushed out of it the shortest way (CharacterController.Move, so walls and
// furniture still stop the player). If the player can't be pushed clear (pinned between the leaf
// and a wall), Resolve returns false and the door keeps its previous angle for that frame, so it
// never swings through the player.
public static class DoorLeafPush
{
    const float Skin = 0.03f;

    static readonly List<Collider> tmp = new List<Collider>();

    // True when the player overlaps any of the leaf colliders.
    public static bool Overlaps(IList<Collider> leaves, CharacterController cc)
    {
        if (cc == null || !cc.enabled || leaves == null) return false;
        Transform t = cc.transform;
        foreach (Collider c in leaves)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (!c.bounds.Intersects(cc.bounds)) continue;
            if (Physics.ComputePenetration(cc, t.position, t.rotation, c, c.transform.position, c.transform.rotation, out _, out float d) && d > 0.001f)
                return true;
        }
        return false;
    }

    // Pushes the player out of the leaf colliders. Returns false if the player is still inside one.
    public static bool Resolve(IList<Collider> leaves, CharacterController cc)
    {
        if (cc == null || !cc.enabled || leaves == null) return true;
        Physics.SyncTransforms();
        Transform t = cc.transform;
        for (int iter = 0; iter < 4; iter++)
        {
            bool moved = false;
            foreach (Collider c in leaves)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!c.bounds.Intersects(cc.bounds)) continue;
                if (!Physics.ComputePenetration(cc, t.position, t.rotation, c, c.transform.position, c.transform.rotation, out Vector3 dir, out float dist)) continue;
                if (dist <= 0.001f) continue;
                dir.y = 0f;                                   // push sideways only
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                cc.Move(dir * (dist + Skin));
                moved = true;
            }
            if (!moved) break;
            Physics.SyncTransforms();
        }
        return !Overlaps(leaves, cc);
    }

    // All solid colliders on a door leaf.
    public static List<Collider> SolidColliders(Transform leafRoot)
    {
        var list = new List<Collider>();
        if (leafRoot == null) return list;
        leafRoot.GetComponentsInChildren(true, tmp);
        foreach (Collider c in tmp) if (c != null && !c.isTrigger) list.Add(c);
        return list;
    }
}
