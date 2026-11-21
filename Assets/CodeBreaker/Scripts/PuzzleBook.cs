using System;
using System.Collections.Generic;
using UnityEngine;

// Code Breaker: the list of puzzles in every level, in play order, each with a unique ID
// ("<scene>/<key>", e.g. "Level 3/s2"). Used to record solved puzzles in the save file and to
// pick the first unsolved puzzle for hints. A puzzle counts as solved when the level's own
// progress data (flags / numbers) says so, so nothing about how the puzzles work changes here.
public static class PuzzleBook
{
    public class Puzzle
    {
        public string key;
        public string room;   // Level 2: living / bedroom / kitchen / study.  Level 3: maint / sec / ctrl / main
        public Func<Func<string, bool>, Func<string, int, int>, bool> done;
        public Puzzle(string key, string room, Func<Func<string, bool>, Func<string, int, int>, bool> done)
        { this.key = key; this.room = room; this.done = done; }
    }

    public const string Level1 = "Level 1";
    public const string Level2 = "Level 2";
    public const string Level3 = "Level 3";

    // Level 1: one door question per room. Room count matches the Level 1 scene (RoomManager.rooms).
    public static int Level1Rooms = 3;

    static Puzzle F(string key, string room, string flag) => new Puzzle(key, room, (f, i) => f(flag));

    static readonly Puzzle[] Level2Puzzles =
    {
        F("living_photo",     "living",  "photo_assembled"),
        F("living_starlamp",  "living",  "star_shadow_seen"),
        F("living_star",      "living",  "star_placed"),
        F("living_exit",      "living",  "exit_living"),
        F("bedroom_uv",       "bedroom", "moon_seen"),
        F("bedroom_moonbox",  "bedroom", "moonbox_open"),
        F("bedroom_stars",    "bedroom", "stars_seen"),
        F("bedroom_exit",     "bedroom", "exit_bedroom"),
        F("kitchen_recipe",   "kitchen", "recipe_complete"),
        F("kitchen_oven",     "kitchen", "oven_open"),
        F("kitchen_exit",     "kitchen", "exit_kitchen"),
        new Puzzle("study_checks", "study", (f, i) => i("terminal_checks", 0) >= 2 || f("picture_decoded") || f("level_complete")),
        F("study_picture",    "study",   "picture_decoded"),
        F("study_login",      "study",   "level_complete"),
    };

    static readonly Puzzle[] Level3Puzzles =
    {
        F("m1",  "maint", "solved_m1"),
        F("m2",  "maint", "solved_m2"),
        F("m3",  "maint", "solved_m3"),
        F("gen", "maint", "solved_gen"),
        F("s1",  "sec",   "solved_s1"),
        F("s2",  "sec",   "solved_s2"),
        F("s3",  "sec",   "solved_s3"),
        F("c2",  "ctrl",  "solved_c2"),
        new Puzzle("fan", "ctrl", (f, i) => i("fan", 1) == 3 || f("solved_c3")),
        F("c3",  "ctrl",  "solved_c3"),
        F("term","main",  "solved_term"),
    };

    public static string Id(string scene, string key) => scene + "/" + key;

    public static IList<Puzzle> For(string scene)
    {
        if (scene == Level2) return Level2Puzzles;
        if (scene == Level3) return Level3Puzzles;
        if (scene == Level1)
        {
            var list = new List<Puzzle>();
            for (int r = 1; r <= Level1Rooms; r++)
            {
                int room = r;
                list.Add(new Puzzle("room" + room, "room" + room, (f, i) => i("rooms_cleared", 0) >= room));
            }
            return list;
        }
        return new Puzzle[0];
    }

    // The IDs of the puzzles that are solved in this level, judged from its progress data.
    public static List<string> Solved(string scene, Func<string, bool> flag, Func<string, int, int> getInt)
    {
        var ids = new List<string>();
        foreach (Puzzle p in For(scene))
            if (p.done(flag, getInt)) ids.Add(Id(scene, p.key));
        return ids;
    }

    // The first puzzle in play order that is not solved yet (null when all are).
    public static Puzzle FirstUnsolved(string scene, Func<string, bool> flag, Func<string, int, int> getInt)
    {
        foreach (Puzzle p in For(scene))
            if (!p.done(flag, getInt)) return p;
        return null;
    }

    public static List<string> AllFor(string scene)
    {
        var ids = new List<string>();
        foreach (Puzzle p in For(scene)) ids.Add(Id(scene, p.key));
        return ids;
    }

    public static bool IsPuzzleOf(string id, string scene) =>
        !string.IsNullOrEmpty(id) && id.StartsWith(scene + "/", StringComparison.Ordinal);

    // Reads a level's saved checkpoint data (used when upgrading an older save file).
    public static List<string> SolvedFromCheckpoint(string scene, string json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try
        {
            if (scene == Level1)
            {
                Level1Progress l1 = JsonUtility.FromJson<Level1Progress>(json);
                int rooms = l1 != null ? l1.roomsCleared : 0;
                return Solved(scene, f => false, (k, fb) => k == "rooms_cleared" ? rooms : fb);
            }
            CodeBreaker.Level2.L2State s = JsonUtility.FromJson<CodeBreaker.Level2.L2State>(json);
            if (s == null) return new List<string>();
            var flags = new HashSet<string>(s.flags);
            var ints = new Dictionary<string, int>();
            foreach (var e in s.ints) ints[e.key] = e.value;
            return Solved(scene, flags.Contains, (k, fb) => ints.TryGetValue(k, out int v) ? v : fb);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Code Breaker] Could not read the " + scene + " checkpoint: " + e.Message);
            return new List<string>();
        }
    }
}

// Level 1's checkpoint data (stored in the save like Level 2 / 3's): how many rooms are cleared.
[Serializable]
public class Level1Progress
{
    public int version = 1;
    public int roomsCleared;          // rooms whose correct door was chosen (the next room is where you continue)
    public float timeRemaining = -1f; // Level 1 clock when the room was cleared (-1 = full time)
}
