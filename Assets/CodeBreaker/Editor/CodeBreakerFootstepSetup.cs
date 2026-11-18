using System.IO;
using UnityEditor;
using UnityEngine;

// Code Breaker > Audio > Create Footstep Set (Levels 2-3)
// Creates Resources/CodeBreaker/FootstepSet.asset holding the same six walk sounds Level 1 uses
// (Footstep_Tile_Walk_1..6). LevelFootsteps uses it when Level 2 or 3 is played without the Game
// Session; in the normal game it takes the clips straight from Level 1's AudioManager.
// Does not open or change any scene.
public static class CodeBreakerFootstepSetup
{
    const string AssetPath = "Assets/CodeBreaker/Resources/CodeBreaker/FootstepSet.asset";

    [MenuItem("Code Breaker/Audio/Create Footstep Set (Levels 2-3)", false, 800)]
    public static void Create()
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= 6; i++)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Footstep_Tile_Walk_" + i + ".wav");
            if (c != null) clips.Add(c);
        }
        FootstepSet set = AssetDatabase.LoadAssetAtPath<FootstepSet>(AssetPath);
        if (set == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            set = ScriptableObject.CreateInstance<FootstepSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
        }
        set.walk = clips.ToArray();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("[Footsteps] " + AssetPath + " has " + clips.Count + " walk sounds");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_Footsteps.txt", "FootstepSet: " + clips.Count + " clips\n" + string.Join("\n", clips.ConvertAll(c => c.name)));
    }
}
