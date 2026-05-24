using Stint.Data;

namespace Stint
{
    public class UrlDispatcher
    {
        private readonly EventRepository _repo;
        private readonly MainForm _mainForm;

        public UrlDispatcher(EventRepository repo, MainForm mainForm)
        {
            _repo = repo;
            _mainForm = mainForm;
        }

        public void Dispatch(string url)
        {
            // Always marshal back to UI thread
            if (_mainForm.InvokeRequired)
            {
                _mainForm.Invoke(() => Dispatch(url));
                return;
            }

            var parsed = ProtocolHandler.Parse(url);
            if (parsed == null) return;

            switch (parsed.Action)
            {
                case "start":
                    HandleStart(parsed.Params);
                    break;

                case "stop":
                    HandleStop(parsed.Params);
                    break;

                case "edit":
                    HandleEdit(parsed.Params);
                    break;

                case "report":
                    HandleReport(parsed.Params);
                    break;

                case "show":
                    _mainForm.OpenPopup();
                    break;
            }
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleStart(Dictionary<string, string> p)
        {
            p.TryGetValue("title", out var title);
            p.TryGetValue("category", out var category);
            p.TryGetValue("time", out var time);
            p.TryGetValue("guid", out var guid);

            if (string.IsNullOrWhiteSpace(title)) return;

            DateTime? startAt = null;
            if (!string.IsNullOrWhiteSpace(time) &&
                DateTime.TryParse(time, out var parsed))
                startAt = parsed;

            _repo.StartEvent(title, string.IsNullOrEmpty(category) ? null : category, startAt, guid);
            _mainForm.OpenPopup();
        }

        private void HandleStop(Dictionary<string, string> p)
        {
            // Support stop by internal id or external guid
            if (p.TryGetValue("guid", out var guid) && !string.IsNullOrWhiteSpace(guid))
                _repo.StopEventByGuid(guid);
            else if (p.TryGetValue("id", out var idStr) && int.TryParse(idStr, out var id))
                _repo.StopEvent(id);
        }

        private void HandleEdit(Dictionary<string, string> p)
        {
            if (!p.TryGetValue("id", out var idStr) || !int.TryParse(idStr, out var id))
                return;

            var form = new EditEventForm(_repo, id);
            form.Show();
            form.BringToFront();
        }

        private void HandleReport(Dictionary<string, string> p)
        {
            p.TryGetValue("type", out var type);
            if (!string.IsNullOrWhiteSpace(type))
                ReportBuilder.Generate($"?{type}");
        }
    }
}