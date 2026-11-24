using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using UnityEditor;
using UnityEngine;

namespace CodeBreaker.Database.EditorTools
{
    // ---------------------------------------------------------------------------------------------
    // Database Viewer: shows what is inside codebreaker_game.db, right in the Unity Editor.
    //
    //   Menu:  Code Breaker > Database Viewer
    //
    //  - QUICK VIEWS: the save slot, puzzle attempts, collected items, keycards, completed levels
    //    and checkpoints, joined with level and puzzle names so they are easy to read.
    //  - TABLES: every table with its number of rows. Click one to see all its rows.
    //  - Click a row to see its full values at the bottom (long questions, hints ...).
    //  - SQL box: type your own SELECT query and press Run (only SELECT / PRAGMA are allowed,
    //    so the viewer can never change or delete data).
    //  - Auto refresh: while the game is playing, the view updates every second, so you can
    //    watch answers and items appear as you play.
    //
    // It only reads the database. The game writes it (DatabaseManager / GameDatabase).
    // ---------------------------------------------------------------------------------------------
    public class DatabaseViewerWindow : EditorWindow
    {
        const int MaxRows = 500;

        // ----------------------------------------------------------------- quick views
        static readonly string[] QuickNames =
        {
            "Save slot",
            "Puzzle attempts",
            "Collected items & hints",
            "Collected keycards",
            "Completed levels",
            "Checkpoints",
            "Checkpoint items",
        };

        static readonly string[] QuickSql =
        {
            @"SELECT p.player_name AS player, l.level_number AS level, l.level_name, pp.current_room AS room,
                     pp.timer_status AS timer, ROUND(pp.remaining_time) AS seconds_left, pp.health,
                     pp.wrong_attempts, ROUND(pp.total_play_time) AS play_seconds, pp.score, pp.game_status AS status,
                     COALESCE(datetime(pp.last_saved, 'localtime'), pp.last_saved) AS last_saved
              FROM player_progress pp
              JOIN player p ON p.player_id = pp.player_id
              JOIN level  l ON l.level_id  = pp.current_level_id",

            @"SELECT pa.attempt_id AS id, pz.puzzle_key AS puzzle, pz.room_name AS room, pz.question,
                     pa.submitted_answer AS answer,
                     CASE pa.is_correct WHEN 1 THEN 'CORRECT' ELSE 'WRONG' END AS result,
                     pz.correct_answer, COALESCE(datetime(pa.attempt_time, 'localtime'), pa.attempt_time) AS time
              FROM puzzle_attempt pa JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
              ORDER BY pa.attempt_id DESC",

            @"SELECT l.level_number AS level, c.clue_type AS type, c.clue_key AS item, c.clue_description AS description,
                     COALESCE(datetime(cc.date_collected, 'localtime'), cc.date_collected) AS collected
              FROM collected_clue cc
              JOIN clue c  ON c.clue_id  = cc.clue_id
              JOIN level l ON l.level_id = c.level_id
              ORDER BY cc.date_collected",

            @"SELECT k.keycard_name AS keycard,
                     CASE WHEN ck.keycard_id IS NULL THEN 'MISSING' ELSE 'COLLECTED' END AS status,
                     ck.access_code, COALESCE(datetime(ck.date_collected, 'localtime'), ck.date_collected) AS collected
              FROM keycard k
              LEFT JOIN collected_keycard ck ON ck.keycard_id = k.keycard_id
              ORDER BY k.keycard_id",

            @"SELECT l.level_number AS level, l.level_name, ROUND(lc.remaining_time) AS seconds_left, lc.level_score AS points,
                     COALESCE(datetime(lc.date_completed, 'localtime'), lc.date_completed) AS completed
              FROM level_completion lc JOIN level l ON l.level_id = lc.level_id
              ORDER BY l.level_number",

            @"SELECT l.level_number AS level, lc.checkpoint_label AS label,
                     CASE lc.is_current WHEN 1 THEN 'YES' ELSE '' END AS current,
                     lc.rooms_cleared, lc.time_remaining, lc.checkpoint_number, lc.held_item,
                     CASE lc.flashlight_on WHEN 1 THEN 'ON' WHEN 0 THEN 'OFF' END AS flashlight, lc.objective,
                     (SELECT COUNT(*) FROM checkpoint_item ci
                      WHERE ci.progress_id = lc.progress_id AND ci.level_id = lc.level_id) AS items
              FROM level_checkpoint lc JOIN level l ON l.level_id = lc.level_id
              ORDER BY l.level_number",

            @"SELECT l.level_number AS level, ci.item_type AS type, ci.item_name AS name, ci.value_1, ci.value_2
              FROM checkpoint_item ci JOIN level l ON l.level_id = ci.level_id
              ORDER BY l.level_number, ci.item_type, ci.sort_order",
        };

        // ----------------------------------------------------------------- state
        string dbPath;
        string viewTitle = "";
        string currentSql = "";
        string customSql = "SELECT * FROM puzzle WHERE level_id = 1;";
        string error;
        readonly List<string> columns = new List<string>();
        readonly List<string[]> rows = new List<string[]>();
        readonly List<KeyValuePair<string, int>> tables = new List<KeyValuePair<string, int>>();
        float[] widths = new float[0];
        int selectedRow = -1;
        bool autoRefresh = true;
        double nextRefresh;
        Vector2 sideScroll, gridScroll, detailScroll;

        GUIStyle headerStyle, cellStyle, sectionStyle, bigStyle;

        [MenuItem("Code Breaker/Database Viewer", false, 0)]
        public static void Open()
        {
            DatabaseViewerWindow w = GetWindow<DatabaseViewerWindow>("Database Viewer");
            w.minSize = new Vector2(760, 420);
            w.Show();
        }

        bool needsReload, needsMeasure;

        void OnEnable()
        {
            dbPath = Path.Combine(Application.persistentDataPath, "codebreaker_game.db");
            viewTitle = QuickNames[0];
            currentSql = QuickSql[0];
            needsReload = true;   // loaded in the first OnGUI (Unity's editor styles are ready there)
        }

        // Live view while playing.
        void Update()
        {
            if (!autoRefresh || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 1.0;
            Reload();
            Repaint();
        }

        // ================================================================= drawing
        void OnGUI()
        {
            MakeStyles();
            if (needsReload && Event.current.type == EventType.Layout) { needsReload = false; Reload(); }
            if (needsMeasure && Event.current.type == EventType.Layout) { needsMeasure = false; MeasureColumns(); }
            DrawToolbar();

            if (!File.Exists(dbPath))
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("No database yet.\n\nPress Play once: the game creates codebreaker_game.db at\n" + dbPath, MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            EditorGUILayout.BeginVertical();
            DrawGrid();
            DrawDetails();
            DrawSqlBox();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) { Reload(); GUIUtility.ExitGUI(); }
            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto refresh while playing", EditorStyles.toolbarButton, GUILayout.Width(170));
            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(90)))
                EditorUtility.RevealInFinder(File.Exists(dbPath) ? dbPath : Application.persistentDataPath);
            GUILayout.Space(8);
            GUILayout.Label(dbPath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);

            GUILayout.Label("QUICK VIEWS", sectionStyle);
            for (int i = 0; i < QuickNames.Length; i++)
                if (GUILayout.Button(QuickNames[i], viewTitle == QuickNames[i] ? EditorStyles.toolbarButton : EditorStyles.miniButton))
                { RunQuick(i); GUIUtility.ExitGUI(); }

            GUILayout.Space(10);
            GUILayout.Label("TABLES  (rows)", sectionStyle);
            foreach (KeyValuePair<string, int> t in tables)
                if (GUILayout.Button(t.Key + "  (" + t.Value + ")", viewTitle == t.Key ? EditorStyles.toolbarButton : EditorStyles.miniButton))
                { Run(t.Key, "SELECT * FROM " + t.Key + " LIMIT " + MaxRows); GUIUtility.ExitGUI(); }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawGrid()
        {
            GUILayout.Label(viewTitle + "   ·   " + rows.Count + (rows.Count == 1 ? " row" : " rows") +
                            (rows.Count >= MaxRows ? " (first " + MaxRows + ")" : ""), bigStyle);

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(columns.Count == 0 ? "Nothing to show." : "This table is empty (play the game to fill it).", MessageType.None);
                return;
            }

            float total = 30f;
            foreach (float w in widths) total += w;
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));

            Rect header = GUILayoutUtility.GetRect(total, 20f);
            EditorGUI.DrawRect(header, new Color(0.2f, 0.2f, 0.2f));
            float x = header.x + 30f;
            GUI.Label(new Rect(header.x + 4, header.y, 26, 20), "#", headerStyle);
            for (int c = 0; c < columns.Count; c++)
            {
                GUI.Label(new Rect(x + 4, header.y, widths[c] - 6, 20), columns[c], headerStyle);
                x += widths[c];
            }

            for (int r = 0; r < rows.Count; r++)
            {
                Rect line = GUILayoutUtility.GetRect(total, 18f);
                if (r == selectedRow) EditorGUI.DrawRect(line, new Color(0.17f, 0.36f, 0.53f, 0.8f));
                else if (r % 2 == 1) EditorGUI.DrawRect(line, new Color(1f, 1f, 1f, 0.04f));

                GUI.Label(new Rect(line.x + 4, line.y, 26, 18), (r + 1).ToString(), EditorStyles.miniLabel);
                x = line.x + 30f;
                for (int c = 0; c < columns.Count; c++)
                {
                    string v = rows[r][c];
                    GUI.Label(new Rect(x + 4, line.y, widths[c] - 6, 18), new GUIContent(Short(v, widths[c]), v), cellStyle);
                    x += widths[c];
                }

                if (Event.current.type == EventType.MouseDown && line.Contains(Event.current.mousePosition))
                {
                    selectedRow = r;
                    Event.current.Use();
                    Repaint();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawDetails()
        {
            if (selectedRow < 0 || selectedRow >= rows.Count) return;
            GUILayout.Label("ROW " + (selectedRow + 1), sectionStyle);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.Height(110));
            for (int c = 0; c < columns.Count; c++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(columns[c], EditorStyles.boldLabel, GUILayout.Width(150));
                EditorGUILayout.SelectableLabel(rows[selectedRow][c] ?? "NULL", EditorStyles.wordWrappedLabel, GUILayout.Height(16));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawSqlBox()
        {
            GUILayout.Label("YOUR OWN QUERY  (SELECT only)", sectionStyle);
            EditorGUILayout.BeginHorizontal();
            customSql = EditorGUILayout.TextArea(customSql, GUILayout.Height(38));
            if (GUILayout.Button("Run", GUILayout.Width(60), GUILayout.Height(38))) { RunCustom(); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }

        // ================================================================= queries
        void RunQuick(int i) => Run(QuickNames[i], QuickSql[i]);

        void RunCustom()
        {
            string s = customSql.Trim();
            string first = s.Split(new[] { ' ', '\n', '\r', '\t' }, 2)[0].ToUpperInvariant();
            if (first != "SELECT" && first != "WITH" && first != "PRAGMA")
            {
                viewTitle = "Your query";
                error = "Only SELECT, WITH and PRAGMA queries can run here. The viewer never changes the database.";
                columns.Clear(); rows.Clear();
                return;
            }
            Run("Your query", s);
        }

        void Run(string newTitle, string sql)
        {
            viewTitle = newTitle;
            currentSql = sql;
            selectedRow = -1;
            Reload();
        }

        // Runs the current query again (and refreshes the table list).
        void Reload()
        {
            error = null;
            columns.Clear();
            rows.Clear();
            if (!File.Exists(dbPath)) return;

            try
            {
                using (var con = new SqliteConnection("URI=file:" + dbPath))
                {
                    con.Open();
                    LoadTables(con);
                    if (string.IsNullOrEmpty(currentSql)) return;
                    using (SqliteCommand cmd = con.CreateCommand())
                    {
                        cmd.CommandText = currentSql;
                        using (SqliteDataReader r = cmd.ExecuteReader())
                        {
                            for (int c = 0; c < r.FieldCount; c++) columns.Add(r.GetName(c));
                            while (r.Read() && rows.Count < MaxRows)
                            {
                                var row = new string[r.FieldCount];
                                for (int c = 0; c < r.FieldCount; c++)
                                    row[c] = r.IsDBNull(c) ? null : Convert.ToString(r.GetValue(c), System.Globalization.CultureInfo.InvariantCulture);
                                rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            if (selectedRow >= rows.Count) selectedRow = -1;
            needsMeasure = true;   // column widths are measured in OnGUI
            if (widths.Length != columns.Count) widths = new float[columns.Count];
            for (int c = 0; c < widths.Length; c++) if (widths[c] <= 0f) widths[c] = 120f;
        }

        void LoadTables(SqliteConnection con)
        {
            tables.Clear();
            var names = new List<string>();
            using (SqliteCommand cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY rowid;";
                using (SqliteDataReader r = cmd.ExecuteReader())
                    while (r.Read()) names.Add(r.GetString(0));
            }
            foreach (string n in names)
                using (SqliteCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM \"" + n + "\";";
                    tables.Add(new KeyValuePair<string, int>(n, Convert.ToInt32(cmd.ExecuteScalar())));
                }
        }

        // ================================================================= helpers
        void MeasureColumns()
        {
            widths = new float[columns.Count];
            GUIStyle s = cellStyle ?? EditorStyles.label;
            for (int c = 0; c < columns.Count; c++)
            {
                float w = s.CalcSize(new GUIContent(columns[c])).x + 16f;
                int sample = Math.Min(rows.Count, 60);
                for (int r = 0; r < sample; r++)
                    if (rows[r][c] != null) w = Math.Max(w, s.CalcSize(new GUIContent(rows[r][c])).x + 12f);
                widths[c] = Mathf.Clamp(w, 50f, 320f);
            }
        }

        static string Short(string v, float width)
        {
            if (v == null) return "NULL";
            v = v.Replace("\n", " ").Replace("\r", "");
            int max = Mathf.Max(4, (int)(width / 6.5f));
            return v.Length <= max ? v : v.Substring(0, max - 1) + "…";
        }

        void MakeStyles()
        {
            if (headerStyle != null) return;
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.8f, 0.4f) } };
            cellStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
            sectionStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.9f, 0.35f, 0.3f) } };
            bigStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        }
    }
}
