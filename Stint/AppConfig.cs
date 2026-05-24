namespace Stint
{
    public static class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            AppContext.BaseDirectory, "config.txt");

        public static string Hotkey { get; private set; } = "Alt+Shift+T";
        public static int? MaxDurationMinutes { get; private set; } = 480;
        public static string ReportPath { get; private set; } = Path.Combine(AppContext.BaseDirectory, "Reports");

        static AppConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                File.WriteAllLines(ConfigPath, new[]
                {
                    "# Stint configuration",
                    "# Hotkey format: modifiers+Key e.g. Alt+Shift+T, Ctrl+Alt+T",
                    "# Supported modifiers: Alt, Shift, Ctrl",
                    "hotkey=Alt+Shift+T",
                    "",
                    "# Maximum event duration in minutes before auto-stop. Set to 'off' to disable.",
                    "max_duration_minutes=480",
                    "",
                    "# Path where HTML reports are saved. Defaults to a Reports folder next to the exe.",
                    $"report_path={Path.Combine(AppContext.BaseDirectory, "Reports")}"
                });
                return;
            }

            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                if (line.StartsWith("#") || !line.Contains('=')) continue;
                var parts = line.Split('=', 2);
                var key = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "hotkey": Hotkey = value; break;
                    case "report_path": if (!string.IsNullOrWhiteSpace(value)) ReportPath = value; break;
                    case "max_duration_minutes":
                        if (value.ToLowerInvariant() == "off")
                            MaxDurationMinutes = null;
                        else if (int.TryParse(value, out int mins) && mins > 0)
                            MaxDurationMinutes = mins;
                        break;
                }
            }
        }
    }
}
