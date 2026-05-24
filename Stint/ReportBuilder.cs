using System.Diagnostics;
using System.Net;
using Stint.Data;

namespace Stint
{
    public static class ReportBuilder
    {
        public static void Generate(string command)
        {
            var cmd = command.ToLowerInvariant().TrimStart('?').Trim();

            if (cmd == "open")
            {
                Directory.CreateDirectory(AppConfig.ReportPath);
                Process.Start(new ProcessStartInfo("explorer.exe", AppConfig.ReportPath) { UseShellExecute = true });
                return;
            }

            if (cmd == "help")
            {
                GenerateHelp();
                return;
            }

            if (cmd == "history")
            {
                GenerateHistory();
                return;
            }

            var (label, from, to) = ParseCommand(cmd);
            if (label == null) return;

            var (byEvent, byCategory) = QueryTotals(from, to);
            var html = BuildSummaryHtml(label, from, to, byEvent, byCategory);
            WriteAndOpen($"{cmd}", html);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void WriteAndOpen(string slug, string html)
        {
            Directory.CreateDirectory(AppConfig.ReportPath);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(AppConfig.ReportPath, $"{slug}_{stamp}.html");
            File.WriteAllText(path, html);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static string FormatDuration(TimeSpan t)
        {
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        // ── Date range parsing ────────────────────────────────────────────────

        private static (string? label, DateTime from, DateTime to) ParseCommand(string cmd)
        {
            var today = DateTime.Now.Date;
            return cmd switch
            {
                "today" => ("Today", today, today.AddDays(1).AddTicks(-1)),
                "yesterday" => ("Yesterday", today.AddDays(-1), today.AddTicks(-1)),
                "week" => ("This Week", StartOfWeek(today), today.AddDays(1).AddTicks(-1)),
                "month" => ("This Month", new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1)),
                _ => (null, today, today)
            };
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff);
        }

        // ── Summary report ────────────────────────────────────────────────────

        private static (List<(string Title, string? Category, TimeSpan Total)> byEvent,
                        List<(string Category, TimeSpan Total)> byCategory)
            QueryTotals(DateTime from, DateTime to)
        {
            var byEvent = new List<(string, string?, TimeSpan)>();
            var byCategory = new List<(string, TimeSpan)>();

            using var conn = Database.GetConnection();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT title, UPPER(COALESCE(category, '')),
                           SUM(
                               (julianday(COALESCE(stopped_at, datetime('now'))) -
                                julianday(started_at)) * 86400
                           ) AS seconds
                    FROM events
                    WHERE datetime(started_at) >= datetime($from)
                      AND datetime(started_at) <= datetime($to)
                    GROUP BY title, UPPER(COALESCE(category, ''))
                    ORDER BY seconds DESC;
                    """;
                cmd.Parameters.AddWithValue("$from", from.ToUniversalTime().ToString("o"));
                cmd.Parameters.AddWithValue("$to", to.ToUniversalTime().ToString("o"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var cat = r.GetString(1);
                    byEvent.Add((r.GetString(0), string.IsNullOrEmpty(cat) ? null : cat, TimeSpan.FromSeconds(r.GetDouble(2))));
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT COALESCE(UPPER(category), '(none)'),
                           SUM(
                               (julianday(COALESCE(stopped_at, datetime('now'))) -
                                julianday(started_at)) * 86400
                           ) AS seconds
                    FROM events
                    WHERE datetime(started_at) >= datetime($from)
                      AND datetime(started_at) <= datetime($to)
                    GROUP BY COALESCE(UPPER(category), '(none)')
                    ORDER BY seconds DESC;
                    """;
                cmd.Parameters.AddWithValue("$from", from.ToUniversalTime().ToString("o"));
                cmd.Parameters.AddWithValue("$to", to.ToUniversalTime().ToString("o"));
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    byCategory.Add((r.GetString(0), TimeSpan.FromSeconds(r.GetDouble(1))));
            }

            return (byEvent, byCategory);
        }

        private static string BuildSummaryHtml(
            string label, DateTime from, DateTime to,
            List<(string Title, string? Category, TimeSpan Total)> byEvent,
            List<(string Category, TimeSpan Total)> byCategory)
        {
            var totalTime = byEvent.Aggregate(TimeSpan.Zero, (acc, e) => acc + e.Total);
            var generated = DateTime.Now.ToString("dddd d MMMM yyyy, HH:mm");
            var dateRange = from.Date == to.Date
                ? from.ToString("dddd d MMMM yyyy")
                : $"{from:d MMM yyyy} – {to:d MMM yyyy}";

            var eventRows = new System.Text.StringBuilder();
            for (int i = 0; i < byEvent.Count; i++)
            {
                var e = byEvent[i];
                var pct = totalTime.TotalSeconds > 0 ? $"{e.Total.TotalSeconds / totalTime.TotalSeconds:P0}" : "—";
                eventRows.AppendLine($"<tr class=\"{(i % 2 == 0 ? "even" : "odd")}\">");
                eventRows.AppendLine($"  <td>{WebUtility.HtmlEncode(e.Title)}</td>");
                eventRows.AppendLine($"  <td>{WebUtility.HtmlEncode(e.Category ?? "")}</td>");
                eventRows.AppendLine($"  <td class=\"dur\">{FormatDuration(e.Total)}</td>");
                eventRows.AppendLine($"  <td class=\"dur\">{pct}</td>");
                eventRows.AppendLine("</tr>");
            }

            var catRows = new System.Text.StringBuilder();
            for (int i = 0; i < byCategory.Count; i++)
            {
                var c = byCategory[i];
                var pct = totalTime.TotalSeconds > 0 ? $"{c.Total.TotalSeconds / totalTime.TotalSeconds:P0}" : "—";
                catRows.AppendLine($"<tr class=\"{(i % 2 == 0 ? "even" : "odd")}\">");
                catRows.AppendLine($"  <td>{WebUtility.HtmlEncode(c.Category)}</td>");
                catRows.AppendLine($"  <td class=\"dur\">{FormatDuration(c.Total)}</td>");
                catRows.AppendLine($"  <td class=\"dur\">{pct}</td>");
                catRows.AppendLine("</tr>");
            }

            const string css = @"
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body { background: #1e1e1e; color: #d4d4d4; font-family: 'Segoe UI', sans-serif; font-size: 14px; padding: 32px; }
                h1 { color: #ffffff; font-size: 22px; margin-bottom: 4px; }
                .meta { color: #888; font-size: 12px; margin-bottom: 32px; }
                .summary { display: inline-block; background: #2a2a2a; border: 1px solid #3a3a3a; border-radius: 6px; padding: 12px 24px; margin-bottom: 32px; }
                .summary span { color: #6694d0; font-size: 20px; font-weight: bold; }
                h2 { color: #aaa; font-size: 13px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 10px; margin-top: 32px; }
                table { width: 100%; border-collapse: collapse; background: #2a2a2a; border-radius: 6px; overflow: hidden; }
                th { background: #ffffff; color: #466090; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; padding: 10px 14px; text-align: left; }
                td { padding: 9px 14px; border-bottom: 1px solid #333; }
                tr.even td { background: #2a2a2a; }
                tr.odd td { background: #272727; }
                tr:last-child td { border-bottom: none; }
                .dur { text-align: right; color: #aaa; font-variant-numeric: tabular-nums; }
                th.dur { text-align: right; }";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine($"  <title>Stint — {WebUtility.HtmlEncode(label)}</title>");
            sb.AppendLine($"  <style>{css}</style></head><body>");
            sb.AppendLine($"  <h1>Stint — {WebUtility.HtmlEncode(label)}</h1>");
            sb.AppendLine($"  <div class=\"meta\">{WebUtility.HtmlEncode(dateRange)} &nbsp;·&nbsp; Generated {generated}</div>");
            sb.AppendLine($"  <div class=\"summary\">Total tracked time: <span>{FormatDuration(totalTime)}</span></div>");
            sb.AppendLine("  <h2>By Event</h2><table><thead><tr>");
            sb.AppendLine("    <th>Event</th><th>Category</th><th class=\"dur\">Time</th><th class=\"dur\">% of Total</th>");
            sb.AppendLine("  </tr></thead><tbody>");
            sb.Append(eventRows);
            sb.AppendLine("  </tbody></table>");
            sb.AppendLine("  <h2>By Category</h2><table><thead><tr>");
            sb.AppendLine("    <th>Category</th><th class=\"dur\">Time</th><th class=\"dur\">% of Total</th>");
            sb.AppendLine("  </tr></thead><tbody>");
            sb.Append(catRows);
            sb.AppendLine("  </tbody></table></body></html>");
            return sb.ToString();
        }

        public static void GenerateHelp()
        {
            var helpPath = Path.Combine(AppContext.BaseDirectory, "help.html");
            string content;

            if (File.Exists(helpPath))
                content = File.ReadAllText(helpPath);
            else
                content = """
                    <h2>Getting Started</h2>
                    <p>Create a <strong>help.html</strong> file alongside the Stint executable to customise this page.</p>
                    <p>The file should contain HTML body content only — no &lt;html&gt;, &lt;head&gt;, or &lt;body&gt; tags.</p>
                    """;

            const string css = @"
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body { background: #1e1e1e; color: #d4d4d4; font-family: 'Segoe UI', sans-serif; font-size: 14px; padding: 32px; max-width: 860px; }
                h1 { color: #ffffff; font-size: 22px; margin-bottom: 4px; }
                .meta { color: #888; font-size: 12px; margin-bottom: 32px; }
                h2 { color: #6694d0; font-size: 15px; margin-top: 28px; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 1px; }
                h3 { color: #aaa; font-size: 13px; margin-top: 18px; margin-bottom: 6px; }
                p { line-height: 1.7; margin-bottom: 10px; color: #d4d4d4; }
                ul, ol { margin: 8px 0 12px 20px; line-height: 1.8; }
                li { margin-bottom: 4px; }
                code { background: #2a2a2a; color: #6694d0; padding: 2px 6px; border-radius: 3px; font-family: 'Consolas', monospace; font-size: 13px; }
                pre { background: #2a2a2a; padding: 14px; border-radius: 6px; margin: 12px 0; overflow-x: auto; }
                pre code { background: none; padding: 0; color: #d4d4d4; }
                strong { color: #ffffff; }
                hr { border: none; border-top: 1px solid #333; margin: 24px 0; }
                table { width: 100%; border-collapse: collapse; background: #2a2a2a; border-radius: 6px; overflow: hidden; margin: 12px 0; }
                th { background: #ffffff; color: #466090; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; padding: 10px 14px; text-align: left; }
                td { padding: 9px 14px; border-bottom: 1px solid #333; }
                tr:last-child td { border-bottom: none; }
                tr:nth-child(even) td { background: #272727; }";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <title>Stint — Help</title>");
            sb.AppendLine($"  <style>{css}</style></head><body>");
            sb.AppendLine("  <h1>Stint — Help</h1>");
            sb.AppendLine($"  <div class=\"meta\">Last updated: {(File.Exists(helpPath) ? File.GetLastWriteTime(helpPath).ToString("dddd d MMMM yyyy") : "—")}</div>");
            sb.AppendLine(content);
            sb.AppendLine("</body></html>");

            // Write to temp and open — help isn't saved to reports folder
            var path = Path.Combine(Path.GetTempPath(), "stint_help.html");
            File.WriteAllText(path, sb.ToString());
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static void GenerateHistory()
        {
            var events = QueryAllEvents();
            var html = BuildHistoryHtml(events);
            WriteAndOpen("history", html);
        }

        private static List<(int Id, string Title, string? Category, DateTime StartedAt, DateTime? StoppedAt, bool AutoStopped)> QueryAllEvents()
        {
            var results = new List<(int, string, string?, DateTime, DateTime?, bool)>();
            using var conn = Database.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, title, category, started_at, stopped_at, auto_stopped
                FROM events
                ORDER BY started_at DESC;
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                results.Add((
                    r.GetInt32(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    DateTime.Parse(r.GetString(3)).ToLocalTime(),
                    r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)).ToLocalTime(),
                    r.GetInt32(5) == 1
                ));
            }
            return results;
        }

        private static string BuildHistoryHtml(
            List<(int Id, string Title, string? Category, DateTime StartedAt, DateTime? StoppedAt, bool AutoStopped)> events)
        {
            var generated = DateTime.Now.ToString("dddd d MMMM yyyy, HH:mm");
            var running = events.Count(e => e.StoppedAt == null);

            var rows = new System.Text.StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                var duration = e.StoppedAt.HasValue
                    ? FormatDuration(e.StoppedAt.Value - e.StartedAt)
                    : "<span class=\"running\">running</span>";
                var autoTag = e.AutoStopped ? " <span class=\"auto\">(auto)</span>" : "";
                var stopped = e.StoppedAt.HasValue ? e.StoppedAt.Value.ToString("dd MMM HH:mm") : "—";

                rows.AppendLine($"<tr class=\"{(i % 2 == 0 ? "even" : "odd")}\">");
                rows.AppendLine($"  <td>{WebUtility.HtmlEncode(e.Title)}</td>");
                rows.AppendLine($"  <td>{WebUtility.HtmlEncode(e.Category?.ToUpperInvariant() ?? "")}</td>");
                rows.AppendLine($"  <td>{e.StartedAt:dd MMM yyyy}</td>");
                rows.AppendLine($"  <td>{e.StartedAt:HH:mm}</td>");
                rows.AppendLine($"  <td>{stopped}</td>");
                rows.AppendLine($"  <td class=\"dur\">{duration}{autoTag}</td>");
                rows.AppendLine($"  <td class=\"edit\"><a href=\"stint://edit?id={e.Id}\">edit</a></td>");
                rows.AppendLine("</tr>");
            }

            const string css = @"
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body { background: #1e1e1e; color: #d4d4d4; font-family: 'Segoe UI', sans-serif; font-size: 14px; padding: 32px; }
                h1 { color: #ffffff; font-size: 22px; margin-bottom: 4px; }
                .meta { color: #888; font-size: 12px; margin-bottom: 32px; }
                .summary { display: inline-block; background: #2a2a2a; border: 1px solid #3a3a3a; border-radius: 6px; padding: 12px 24px; margin-bottom: 32px; }
                .summary span { color: #6694d0; font-size: 20px; font-weight: bold; }
                table { width: 100%; border-collapse: collapse; background: #2a2a2a; border-radius: 6px; overflow: hidden; }
                th { background: #ffffff; color: #466090; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; padding: 10px 14px; text-align: left; }
                td { padding: 9px 14px; border-bottom: 1px solid #333; }
                tr.even td { background: #2a2a2a; }
                tr.odd td { background: #272727; }
                tr:last-child td { border-bottom: none; }
                .dur { text-align: right; color: #aaa; font-variant-numeric: tabular-nums; }
                th.dur { text-align: right; }
                .running { color: #6694d0; font-style: italic; }
                .auto { color: #888; font-size: 11px; }
                .edit { text-align: center; width: 40px; }
                .edit a { color: #6694d0; text-decoration: none; font-size: 11px; }
                .edit a:hover { color: #ffffff; }";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <title>Stint — History</title>");
            sb.AppendLine($"  <style>{css}</style></head><body>");
            sb.AppendLine("  <h1>Stint — History</h1>");
            sb.AppendLine($"  <div class=\"meta\">All events &nbsp;·&nbsp; Generated {generated}</div>");
            sb.AppendLine($"  <div class=\"summary\">{events.Count} events total &nbsp; <span>{running}</span> currently running</div>");
            sb.AppendLine("  <table><thead><tr>");
            sb.AppendLine("    <th>Event</th><th>Category</th><th>Date</th><th>Started</th><th>Stopped</th><th class=\"dur\">Duration</th><th class=\"edit\"></th>");
            sb.AppendLine("  </tr></thead><tbody>");
            sb.Append(rows);
            sb.AppendLine("  </tbody></table></body></html>");
            return sb.ToString();
        }
    }
}