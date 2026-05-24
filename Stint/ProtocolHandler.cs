using Microsoft.Win32;

namespace Stint
{
    public static class ProtocolHandler
    {
        private const string Scheme = "stint";

        // ── Registration ─────────────────────────────────────────────────────

        public static void Register()
        {
            try
            {
                var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Scheme}");
                key.SetValue("", $"URL:{Scheme} Protocol");
                key.SetValue("URL Protocol", "");

                using var iconKey = key.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", $"{exePath},0");

                using var cmdKey = key.CreateSubKey(@"shell\open\command");
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }
            catch { /* non-critical — may not have registry access */ }
        }

        // ── URL parsing ───────────────────────────────────────────────────────

        public record ParsedUrl(string Action, Dictionary<string, string> Params);

        public static ParsedUrl? Parse(string url)
        {
            try
            {
                // Expected format: stint://action?key=value&key=value
                if (!url.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase))
                    return null;

                var withoutScheme = url.Substring($"{Scheme}://".Length);
                var qMark = withoutScheme.IndexOf('?');

                var action = qMark >= 0
                    ? withoutScheme.Substring(0, qMark).Trim('/')
                    : withoutScheme.Trim('/');

                var parms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (qMark >= 0)
                {
                    var query = withoutScheme.Substring(qMark + 1);
                    foreach (var part in query.Split('&'))
                    {
                        var eq = part.IndexOf('=');
                        if (eq > 0)
                        {
                            var k = Uri.UnescapeDataString(part.Substring(0, eq));
                            var v = Uri.UnescapeDataString(part.Substring(eq + 1));
                            parms[k] = v;
                        }
                    }
                }

                return new ParsedUrl(action.ToLowerInvariant(), parms);
            }
            catch { return null; }
        }
    }
}
