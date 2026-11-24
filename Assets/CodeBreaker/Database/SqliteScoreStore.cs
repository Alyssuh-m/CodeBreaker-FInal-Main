using CodeBreaker.Level2;

namespace CodeBreaker.Database
{
    // ---------------------------------------------------------------------------------------------
    // SqliteScoreStore: plugs the database into the score system the game ALREADY has.
    //
    // IScoreStore.cs says: "To switch to SQLite later, write an SqliteScoreStore with the same two
    // methods and return it from ScoreStores.Current." This is that class. Level 2 (L2LevelSession)
    // calls SaveScore when the Study Room computer is unlocked, so the Level 2 score, time left and
    // completion then go into the database instead of codebreaker_scores.json.
    //
    // DatabaseManager.Connect() turns it on (ScoreStores.Current = new SqliteScoreStore()) once the
    // database has started. If SQLite can't start, the game keeps using the JSON file.
    // ---------------------------------------------------------------------------------------------
    public class SqliteScoreStore : IScoreStore
    {
        public void SaveScore(ScoreRecord record)
        {
            if (!DatabaseManager.IsReady || record == null) return;
            DatabaseManager m = DatabaseManager.Instance;
            int progressId = m.EnsureProgress();
            m.Db.CompleteLevel(progressId, record.level_number, record.time_remaining, record.score);
        }

        public void SaveProgress(ProgressRecord progress)
        {
            if (!DatabaseManager.IsReady || progress == null) return;
            if (progress.current_level < 1 || progress.current_level > GameDatabase.LastLevel) return;
            DatabaseManager m = DatabaseManager.Instance;
            m.Db.SetCurrentLevel(m.EnsureProgress(), progress.current_level);
        }

        public ProgressRecord LoadProgress()
        {
            if (!DatabaseManager.IsReady) return new ProgressRecord();
            DatabaseManager m = DatabaseManager.Instance;
            SaveGame save = m.LoadCurrentSave();
            if (save == null) return new ProgressRecord();
            return new ProgressRecord
            {
                current_level = save.LevelNumber,
                levels_completed = m.Db.GetCompletedLevels(save.ProgressId).Count,
                last_saved = save.LastSaved,
            };
        }
    }
}
