namespace CodeBreaker.Level2
{
    // One row of the score table (matches the planned SQLite table: player_id, level_number, score, time_remaining).
    [System.Serializable]
    public class ScoreRecord
    {
        public string player_id;
        public int level_number;
        public int score;
        public float time_remaining;
        public string saved_at;
    }

    // The progress row (matches the planned SQLite table: current_level, levels_completed, last_saved).
    [System.Serializable]
    public class ProgressRecord
    {
        public int current_level;
        public int levels_completed;
        public string last_saved;
    }

    // Where scores and progress are stored. The project has no SQLite setup yet, so the game uses
    // JsonScoreStore. To switch to SQLite later, write an SqliteScoreStore with the same two methods
    // and return it from ScoreStores.Current.
    public interface IScoreStore
    {
        void SaveScore(ScoreRecord record);
        void SaveProgress(ProgressRecord progress);
        ProgressRecord LoadProgress();
    }

    public static class ScoreStores
    {
        static IScoreStore current;
        public static IScoreStore Current
        {
            get { return current ?? (current = new JsonScoreStore()); }
            set { current = value; }
        }
    }
}
