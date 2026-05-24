using Microsoft.Data.Sqlite;

namespace Stint.Data
{
    public static class Database
    {
        private static string _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Stint", "stint.db");

        public static SqliteConnection GetConnection()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        public static void Initialize()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    title TEXT NOT NULL,
                    category TEXT,
                    started_at TEXT NOT NULL,
                    stopped_at TEXT,
                    auto_stopped INTEGER NOT NULL DEFAULT 0,
                    guid TEXT UNIQUE
                );

                CREATE TABLE IF NOT EXISTS titles_cache (
                    title TEXT NOT NULL,
                    category TEXT,
                    last_used TEXT NOT NULL,
                    PRIMARY KEY (title)
                );

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                INSERT OR IGNORE INTO settings (key, value) VALUES ('max_duration_minutes', '480');
                """;
            cmd.ExecuteNonQuery();

            // Migrate existing databases that don't have the guid column yet
            try
            {
                using var migCmd = conn.CreateCommand();
                migCmd.CommandText = "ALTER TABLE events ADD COLUMN guid TEXT UNIQUE;";
                migCmd.ExecuteNonQuery();
            }
            catch { /* column already exists — safe to ignore */ }
        }
    }
}