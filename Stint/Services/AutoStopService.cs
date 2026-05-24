using Stint.Data;

namespace Stint.Services
{
    public class AutoStopService : IDisposable
    {
        private readonly EventRepository _repo;
        private readonly NotifyIcon _trayIcon;
        private System.Windows.Forms.Timer _timer;
        private readonly HashSet<int> _notifiedIds = new();

        public event Action? EventsChanged;

        public AutoStopService(EventRepository repo, NotifyIcon trayIcon)
        {
            _repo = repo;
            _trayIcon = trayIcon;
            _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _timer.Tick += CheckForOverdue;
            _timer.Start();
            CheckForOverdue(null, EventArgs.Empty);
        }

        private void CheckForOverdue(object? sender, EventArgs e)
        {
            if (AppConfig.MaxDurationMinutes == null) return;

            var max = AppConfig.MaxDurationMinutes.Value;
            var halfway = max / 2.0;
            var running = _repo.GetRunningEvents();
            bool any = false;

            foreach (var ev in running)
            {
                var elapsed = ev.Elapsed.TotalMinutes;

                // Auto-stop at 100%
                if (elapsed >= max)
                {
                    _repo.StopEvent(ev.Id, autoStopped: true);
                    _notifiedIds.Remove(ev.Id);
                    _trayIcon.ShowBalloonTip(5000, "Stint — Auto Stopped",
                        $"\"{ev.Title}\" was automatically stopped after {max} minutes.",
                        ToolTipIcon.Warning);
                    any = true;
                    continue;
                }

                // Notify at 50%
                if (elapsed >= halfway && !_notifiedIds.Contains(ev.Id))
                {
                    _notifiedIds.Add(ev.Id);
                    _trayIcon.ShowBalloonTip(5000, "Stint — Heads Up",
                        $"\"{ev.Title}\" has been running for {(int)elapsed} minutes — half your auto-stop limit.",
                        ToolTipIcon.Info);
                }
            }

            if (any) EventsChanged?.Invoke();
        }

        public void Dispose() => _timer.Dispose();
    }
}
