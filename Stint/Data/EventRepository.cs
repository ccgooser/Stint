using Microsoft.Data.Sqlite;
using Stint.Models;

namespace Stint.Data
{
    public class EventRepository
    {
        public int StartEvent(string title, string? category, DateTime? startAt = null, string? guid = null)
        {
            using var conn = Database.GetConnection();

            // Silent ignore if guid already exists
            if (guid != null)
            {
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM events WHERE guid = $guid;";
                checkCmd.Parameters.AddWithValue("$guid", guid);
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    return -1;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO events (title, category, started_at, guid)
                VALUES ($title, $category, $started_at, $guid);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$started_at", startAt.HasValue
                ? DateTime.SpecifyKind(startAt.Value, DateTimeKind.Local).ToString("o")
                : DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$guid", (object?)guid ?? DBNull.Value);
            int newId = Convert.ToInt32(cmd.ExecuteScalar());

            using var cacheCmd = conn.CreateCommand();
            cacheCmd.CommandText = """
                INSERT INTO titles_cache (title, category, last_used)
                VALUES ($title, $category, $last_used)
                ON CONFLICT(title) DO UPDATE SET
                    category = excluded.category,
                    last_used = excluded.last_used;
                """;
            cacheCmd.Parameters.AddWithValue("$title", title);
            cacheCmd.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
            cacheCmd.Parameters.AddWithValue("$last_used", DateTime.UtcNow.ToString("o"));
            cacheCmd.ExecuteNonQuery();

            return newId;
        }

        public void StopEventByGuid(string guid, bool autoStopped = false)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE events
                SET stopped_at = $stopped_at, auto_stopped = $auto_stopped
                WHERE guid = $guid AND stopped_at IS NULL;
                """;
            cmd.Parameters.AddWithValue("$stopped_at", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$auto_stopped", autoStopped ? 1 : 0);
            cmd.Parameters.AddWithValue("$guid", guid);
            cmd.ExecuteNonQuery();
        }

        public void StopEvent(int id, bool autoStopped = false)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE events
                SET stopped_at = $stopped_at, auto_stopped = $auto_stopped
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$stopped_at", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$auto_stopped", autoStopped ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public List<TimeEvent> GetRunningEvents()
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM events WHERE stopped_at IS NULL ORDER BY started_at DESC;";
            return ReadEvents(cmd);
        }

        public List<(string Title, string? Category)> GetCachedTitles(string prefix)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT title, category FROM titles_cache
                WHERE title LIKE $prefix
                ORDER BY last_used DESC
                LIMIT 20;
                """;
            cmd.Parameters.AddWithValue("$prefix", prefix + "%");

            var results = new List<(string, string?)>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
            return results;
        }

        public List<string> GetCachedCategories(string prefix)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT MIN(category) FROM titles_cache
                WHERE category IS NOT NULL AND UPPER(category) LIKE UPPER($prefix)
                GROUP BY UPPER(category)
                ORDER BY MAX(last_used) DESC
                LIMIT 10;
                """;
            cmd.Parameters.AddWithValue("$prefix", prefix + "%");

            var results = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(reader.GetString(0));
            return results;
        }
        public void RestoreEvent(int id)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE events
                SET stopped_at = NULL, auto_stopped = 0
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public string? GetMostUsedCategory(string title)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT category
                FROM events
                WHERE UPPER(title) = UPPER($title)
                  AND category IS NOT NULL
                ORDER BY started_at DESC
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$title", title);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        public Models.TimeEvent? GetEventById(int id)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM events WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            var results = ReadEvents(cmd);
            return results.Count > 0 ? results[0] : null;
        }

        public void UpdateEvent(int id, string title, string? category, DateTime startedAt, DateTime? stoppedAt)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE events
                SET title = $title,
                    category = $category,
                    started_at = $started_at,
                    stopped_at = $stopped_at
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$started_at", startedAt.ToUniversalTime().ToString("o"));
            cmd.Parameters.AddWithValue("$stopped_at", stoppedAt.HasValue
                ? stoppedAt.Value.ToUniversalTime().ToString("o")
                : DBNull.Value);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteEvent(int id)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM events WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public string? GetCanonicalCategory(string category)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT MIN(category) FROM titles_cache
                WHERE UPPER(category) = UPPER($category)
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$category", category);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        public string GetSetting(string key)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        public void SaveSetting(string key, string value)
        {
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO settings (key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        private static List<TimeEvent> ReadEvents(SqliteCommand cmd)
        {
            var list = new List<TimeEvent>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TimeEvent
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Category = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StartedAt = DateTime.Parse(reader.GetString(3)).ToLocalTime(),
                    StoppedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)).ToLocalTime(),
                    AutoStopped = reader.GetInt32(5) == 1
                });
            }
            return list;
        }
    }
}