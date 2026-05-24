using DarkUI.Controls;
using DarkUI.Forms;
using Stint.Data;

namespace Stint
{
    public class EditEventForm : DarkForm
    {
        private readonly EventRepository _repo;
        private readonly int _eventId;

        private DarkTextBox _titleBox;
        private DarkTextBox _categoryBox;
        private DarkTextBox _startedBox;
        private DarkTextBox _stoppedBox;
        private DarkButton _saveBtn;
        private DarkButton _stopBtn;
        private DarkButton _deleteBtn;
        private DarkButton _cancelBtn;
        private DarkLabel _statusLabel;

        private static readonly Color DarkBg = Color.FromArgb(43, 43, 43);

        public EditEventForm(EventRepository repo, int eventId)
        {
            _repo = repo;
            _eventId = eventId;

            Text = "Stint — Edit Event";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 420;
            Font = new System.Drawing.Font("Segoe UI", 10.5f);
            TopMost = true;

            BuildUI();
            LoadEvent();

            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private void BuildUI()
        {
            const int labelW = 80;
            const int inputX = 95;
            const int rowH = 26;
            const int rowSpacing = 8;
            const int margin = 16;
            int y = margin;

            Controls.Add(MakeLabel("Title:", margin, y, labelW));
            _titleBox = new DarkTextBox { Left = inputX, Top = y, Width = Width - inputX - margin - 16 };
            Controls.Add(_titleBox);
            y += rowH + rowSpacing;

            Controls.Add(MakeLabel("Category:", margin, y, labelW));
            _categoryBox = new DarkTextBox { Left = inputX, Top = y, Width = Width - inputX - margin - 16 };
            Controls.Add(_categoryBox);
            y += rowH + rowSpacing;

            Controls.Add(MakeLabel("Started:", margin, y, labelW));
            _startedBox = new DarkTextBox { Left = inputX, Top = y, Width = Width - inputX - margin - 16 };
            Controls.Add(_startedBox);
            y += rowH + rowSpacing;

            Controls.Add(MakeLabel("Stopped:", margin, y, labelW));
            _stoppedBox = new DarkTextBox { Left = inputX, Top = y, Width = Width - inputX - margin - 16 };
            Controls.Add(_stoppedBox);
            y += rowH + rowSpacing + 4;

            // Status label for validation feedback
            _statusLabel = new DarkLabel
            {
                Left = margin,
                Top = y,
                Width = Width - margin * 2 - 16,
                Height = 20,
                ForeColor = Color.FromArgb(210, 100, 100),
                Text = ""
            };
            Controls.Add(_statusLabel);
            y += 24;

            // Buttons — Stop | Save | Delete | Cancel
            int btnW = 80;
            int btnH = 30;
            int totalBtns = 4;
            int btnAreaWidth = (btnW * totalBtns) + (8 * (totalBtns - 1));
            int btnLeft = Width - margin - 16 - btnAreaWidth;

            _stopBtn = new DarkButton
            {
                Text = "Stop",
                Left = btnLeft,
                Top = y,
                Width = btnW,
                Height = btnH,
                ForeColor = Color.FromArgb(210, 160, 60)
            };

            _saveBtn = new DarkButton
            {
                Text = "Save",
                Left = btnLeft + btnW + 8,
                Top = y,
                Width = btnW,
                Height = btnH
            };

            _deleteBtn = new DarkButton
            {
                Text = "Delete",
                Left = btnLeft + (btnW + 8) * 2,
                Top = y,
                Width = btnW,
                Height = btnH,
                ForeColor = Color.FromArgb(210, 100, 100)
            };

            _cancelBtn = new DarkButton
            {
                Text = "Cancel",
                Left = btnLeft + (btnW + 8) * 3,
                Top = y,
                Width = btnW,
                Height = btnH
            };

            Controls.Add(_stopBtn);
            Controls.Add(_saveBtn);
            Controls.Add(_deleteBtn);
            Controls.Add(_cancelBtn);

            _stopBtn.Click += OnStop;
            _saveBtn.Click += OnSave;
            _deleteBtn.Click += OnDelete;
            _cancelBtn.Click += (_, _) => Close();

            var noteLabel = new DarkLabel
            {
                Text = "Note: changes will be reflected in subsequent report views.",
                Left = 0,
                Top = y + btnH + 10,
                Width = Width - 16,
                Height = 20,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(noteLabel);

            // Size form to fit content exactly
            ClientSize = new Size(ClientSize.Width, noteLabel.Bottom + margin);
        }

        private static DarkLabel MakeLabel(string text, int x, int y, int w) => new DarkLabel
        {
            Text = text,
            Left = x,
            Top = y + 4,
            Width = w,
            TextAlign = ContentAlignment.MiddleRight
        };

        private void LoadEvent()
        {
            var ev = _repo.GetEventById(_eventId);
            if (ev == null) { Close(); return; }

            _titleBox.Text = ev.Title;
            _categoryBox.Text = ev.Category ?? "";
            _startedBox.Text = ev.StartedAt.ToString("dd/MM/yyyy HH:mm");

            bool isRunning = !ev.StoppedAt.HasValue;
            _stopBtn.Enabled = isRunning;
            _stoppedBox.Enabled = !isRunning;

            if (isRunning)
            {
                _stoppedBox.Text = "(still running)";
                _stoppedBox.ForeColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                _stoppedBox.Text = ev.StoppedAt!.Value.ToString("dd/MM/yyyy HH:mm");
            }
        }

        private void OnStop(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Stop this event now?",
                "Stint — Stop Event",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Yes)
            {
                _repo.StopEvent(_eventId);
                Close();
            }
        }

        private void OnSave(object? sender, EventArgs e)
        {
            var title = _titleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                _statusLabel.Text = "Title cannot be empty.";
                return;
            }

            if (!DateTime.TryParseExact(_startedBox.Text.Trim(),
                new[] { "dd/MM/yyyy HH:mm", "d/MM/yyyy HH:mm", "dd/MM/yyyy H:mm" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var startedAt))
            {
                _statusLabel.Text = "Invalid start time. Use dd/MM/yyyy HH:mm";
                return;
            }

            DateTime? stoppedAt = null;
            if (_stoppedBox.Enabled && !string.IsNullOrWhiteSpace(_stoppedBox.Text))
            {
                if (!DateTime.TryParseExact(_stoppedBox.Text.Trim(),
                    new[] { "dd/MM/yyyy HH:mm", "d/MM/yyyy HH:mm", "dd/MM/yyyy H:mm" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var st))
                {
                    _statusLabel.Text = "Invalid stop time. Use dd/MM/yyyy HH:mm";
                    return;
                }
                stoppedAt = st;
            }

            if (stoppedAt.HasValue && stoppedAt <= startedAt)
            {
                _statusLabel.Text = "Stop time must be after start time.";
                return;
            }

            var category = _categoryBox.Text.Trim();
            _repo.UpdateEvent(_eventId, title,
                string.IsNullOrEmpty(category) ? null : category,
                startedAt, stoppedAt);

            Close();
        }

        private void OnDelete(object? sender, EventArgs e)
        {
            var ev = _repo.GetEventById(_eventId);
            if (ev == null) { Close(); return; }

            var result = MessageBox.Show(
                $"Permanently delete \"{ev.Title}\"?\n\nThis cannot be undone.",
                "Stint — Delete Event",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                _repo.DeleteEvent(_eventId);
                Close();
            }
        }
    }
}