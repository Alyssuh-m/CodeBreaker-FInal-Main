using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Stand-in for the SQLite database: keeps the score table and the progress row in
    // <persistentDataPath>/codebreaker_scores.json.
    public class JsonScoreStore : IScoreStore
    {
        [System.Serializable]
        class Data
        {
            public List<ScoreRecord> scores = new List<ScoreRecord>();
            public ProgressRecord progress = new ProgressRecord();
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "codebreaker_scores.json");

        Data Read()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Data d = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                    if (d != null) return d;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Scores] Could not read " + FilePath + ": " + e.Message); }
            return new Data();
        }

        void Write(Data d)
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(d, true)); }
            catch (System.Exception e) { Debug.LogWarning("[Scores] Could not write " + FilePath + ": " + e.Message); }
        }

        public void SaveScore(ScoreRecord record)
        {
            Data d = Read();
            d.scores.Add(record);
            Write(d);
        }

        public void SaveProgress(ProgressRecord progress)
        {
            Data d = Read();
            d.progress = progress;
            Write(d);
        }

        public ProgressRecord LoadProgress() => Read().progress;
    }
}
