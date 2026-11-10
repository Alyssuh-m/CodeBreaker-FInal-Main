using System;
using System.Collections.Generic;

namespace CodeBreaker.Level2
{
    [Serializable] public class L2IntEntry { public string key; public int value; }
    [Serializable] public class L2DoorEntry { public string name; public bool locked; public bool open; }
    [Serializable] public class L2LightEntry { public string group; public bool on; }

    // Everything Level 2 needs to continue from a checkpoint. Stored as JSON inside the normal
    // Code Breaker save file (SaveData.checkpointJson), so Level 1 data is never touched.
    [Serializable]
    public class L2State
    {
        public int version = 1;
        public int checkpoint;              // 1 = Hall B, 2 = Corridor, 3 = Hall A
        public string checkpointLabel;
        public List<string> inventory = new List<string>();
        public string held;
        public bool flashlightOn;
        public List<string> flags = new List<string>();
        public List<L2IntEntry> ints = new List<L2IntEntry>();
        public List<string> collected = new List<string>();
        public List<L2DoorEntry> doors = new List<L2DoorEntry>();
        public List<L2LightEntry> lights = new List<L2LightEntry>();
        public string objective;
    }
}
