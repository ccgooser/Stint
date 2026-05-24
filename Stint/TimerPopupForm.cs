using DarkUI.Controls;
using DarkUI.Forms;
using Microsoft.Win32;
using Stint.Data;

namespace Stint
{
    public class TimerPopupForm : DarkForm
    {
        private ListView _eventList;
        private DarkTextBox _titleBox;
        private DarkTextBox _categoryBox;
        private ListBox _suggestions;
        private ListBox _categorySuggestions;
        private DarkLabel _activeLabel;
        private Panel _inputPanel;
        private EventRepository _repo;
        private System.Windows.Forms.Timer _refreshTimer;
        private bool _categoryHasFocus = false;
        private bool _populatingFromSuggestion = false;
        private readonly Stack<int> _undoStack = new();
        private Label _helpBtn;

        public event Action? UserClosed;
        public event Action? EventStarted;

        private const int LabelWidth = 90;
        private const int RowSpacing = 6;
        private const string RegKey = @"Software\Stint";

        // Dark theme colours
        private static readonly Color DarkBg = Color.FromArgb(43, 43, 43);
        private static readonly Color DarkPanel = Color.FromArgb(60, 63, 65);
        private static readonly Color DarkFg = Color.FromArgb(220, 220, 220);
        private static readonly Color DarkSelection = Color.FromArgb(75, 110, 175);

        public TimerPopupForm(EventRepository repo)
        {
            _repo = repo;

            var version = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version;
            var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "";
            Text = $"Stint  {versionStr}";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = true;
            ShowInTaskbar = false;
            TopMost = true;
            MinimumSize = new Size(360, 220);
            StartPosition = FormStartPosition.Manual;
            Size = LoadSize() ?? new Size(480, 380);

            // Force font — DarkUI 2.0.2 overrides the form font in its base constructor
            var appFont = new System.Drawing.Font("Segoe UI", 12f);
            Font = appFont;

            // ── Input panel — fixed height at bottom ─────────────────────────
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = DarkPanel
            };

            var titleLabel = new DarkLabel { Text = "Action:", Width = LabelWidth, TextAlign = ContentAlignment.MiddleRight };
            _titleBox = new DarkTextBox();
            _suggestions = new ListBox { Height = 72, Visible = false, TabStop = false, BackColor = DarkBg, ForeColor = DarkFg, BorderStyle = BorderStyle.None };
            var categoryLabel = new DarkLabel { Text = "Category:", Width = LabelWidth, TextAlign = ContentAlignment.MiddleRight };

            _categoryBox = new DarkTextBox();
            _categoryBox.Enter += (_, _) =>
            {
                _categoryHasFocus = true;
                foreach (ListViewItem item in _eventList.Items)
                    item.Selected = false;
            };
            _categoryBox.Leave += (_, _) => _categoryHasFocus = false;
            _categoryBox.TextChanged += OnCategoryChanged;
            _categoryBox.KeyDown += OnCategoryKeyDown;
            _categoryBox.KeyPress += (_, e) => { if (e.KeyChar == (char)Keys.Enter) e.Handled = true; };

            _categorySuggestions = new ListBox { Height = 72, Visible = false, TabStop = false, BackColor = DarkBg, ForeColor = DarkFg, BorderStyle = BorderStyle.None };
            _categorySuggestions.Click += (_, _) => SelectCategorySuggestion();
            _categorySuggestions.MouseClick += (_, e) =>
            {
                int idx = _categorySuggestions.IndexFromPoint(e.Location);
                if (idx >= 0) _categorySuggestions.SelectedIndex = idx;
                SelectCategorySuggestion();
            };
            _categorySuggestions.KeyDown += OnCategorySuggestionKeyDown;

            _helpBtn = new Label
            {
                Text = "?",
                Width = 22,
                Height = 22,
                TabStop = false,
                BackColor = Color.FromArgb(0x45, 0x49, 0x4A),
                ForeColor = Color.FromArgb(0x92, 0xAA, 0xD6),
                Font = new System.Drawing.Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _helpBtn.Click += (_, _) => CommandHintForm.ShowNear(_titleBox);
            _helpBtn.MouseEnter += (_, _) => _helpBtn.ForeColor = Color.White;
            _helpBtn.MouseLeave += (_, _) => _helpBtn.ForeColor = Color.FromArgb(0x92, 0xAA, 0xD6);

            _inputPanel.Controls.AddRange(new Control[] {
                titleLabel, _titleBox, _helpBtn, _suggestions, categoryLabel, _categoryBox, _categorySuggestions
            });
            _helpBtn.BringToFront();

            // ── Active events label ───────────────────────────────────────────
            _activeLabel = new DarkLabel
            {
                Text = "Active Events",
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 10, 0, 4),
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold)
            };

            // ── Event list — standard ListView manually dark-themed ───────────
            _eventList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                TabStop = false,
                BackColor = DarkBg,
                ForeColor = DarkFg,
                BorderStyle = BorderStyle.None,
                OwnerDraw = true
            };
            _eventList.Columns.Add("#", 34);
            _eventList.Columns.Add("Title", 210);
            _eventList.Columns.Add("Category", 100);
            _eventList.Columns.Add("Elapsed", 80);

            _eventList.DrawColumnHeader += (_, e) =>
            {
                using var brush = new SolidBrush(Color.White);
                e.Graphics.FillRectangle(brush, e.Bounds);
                using var pen = new Pen(Color.FromArgb(200, 200, 200));
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

                var textBounds = e.ColumnIndex == 0
                    ? new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height)
                    : e.Bounds;

                TextRenderer.DrawText(e.Graphics, e.Header.Text, _eventList.Font,
                    textBounds, Color.FromArgb(70, 100, 160), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            _eventList.DrawItem += (_, e) => e.DrawDefault = false;

            _eventList.DrawSubItem += (_, e) =>
            {
                bool selected = e.Item.Selected;
                Color bg = selected ? DarkSelection : (e.ItemIndex % 2 == 0 ? DarkBg : Color.FromArgb(48, 48, 48));
                using var brush = new SolidBrush(bg);
                e.Graphics.FillRectangle(brush, e.Bounds);

                // Add left padding to index column
                var textBounds = e.ColumnIndex == 0
                    ? new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height)
                    : e.Bounds;

                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, _eventList.Font,
                    textBounds, DarkFg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            // Mouse click returns focus to title box
            _eventList.MouseDown += (_, e) => _titleBox.Focus();

            _eventList.DoubleClick += (_, _) =>
            {
                if (_eventList.SelectedItems.Count > 0)
                {
                    var id = (int)_eventList.SelectedItems[0].Tag!;
                    var form = new EditEventForm(_repo, id);
                    form.FormClosed += (_, _) => RefreshEventList();
                    form.Show();
                }
            };

            Controls.Add(_eventList);
            Controls.Add(_activeLabel);
            Controls.Add(_inputPanel);

            // ── Wire events ──────────────────────────────────────────────────
            _titleBox.TextChanged += OnTitleChanged;
            _titleBox.KeyDown += OnTitleKeyDown;
            _titleBox.KeyPress += (_, e) => { if (e.KeyChar == (char)Keys.Enter) e.Handled = true; };
            _suggestions.Click += (_, _) => SelectSuggestion();
            _suggestions.KeyDown += OnSuggestionKeyDown;

            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
            HelpButtonClicked += (_, e) => { e.Cancel = true; ReportBuilder.GenerateHelp(); };
            KeyPress += (_, e) => { if (e.KeyChar == (char)Keys.Enter) e.Handled = true; };

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _refreshTimer.Tick += (_, _) => RefreshEventList();

            _inputPanel.Resize += (_, _) => LayoutInputPanel();
        }

        // ── ProcessCmdKey ────────────────────────────────────────────────────

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                OnEnter();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Z))
            {
                UndoStop();
                return true;
            }
            if (keyData == Keys.Tab)
            {
                if (_suggestions.Visible && _suggestions.SelectedIndex >= 0)
                {
                    SelectSuggestion();
                    _categoryBox.Focus();
                    return true;
                }
                if (_categorySuggestions.Visible && _categorySuggestions.SelectedIndex >= 0)
                {
                    SelectCategorySuggestion();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UndoStop()
        {
            if (_undoStack.Count == 0) return;
            var id = _undoStack.Pop();
            _repo.RestoreEvent(id);
            RefreshEventList();
        }

        // ── Input panel layout ───────────────────────────────────────────────

        private void LayoutInputPanel()
        {
            int p = _inputPanel.Padding.Left;
            int pt = _inputPanel.Padding.Top;
            int w = _inputPanel.ClientSize.Width - p * 2;
            int inputX = p + LabelWidth + 4;
            int inputW = w - LabelWidth - 4 - 12; // 12px right breathing room
            int rowH = _titleBox.PreferredHeight;
            int y = pt + 8;

            int btnW = 26;
            foreach (Control c in _inputPanel.Controls)
            {
                if (c is DarkLabel lbl && lbl.Text == "Action:")
                { c.SetBounds(p, y + 2, LabelWidth, rowH); continue; }
                if (c == _titleBox)
                { c.SetBounds(inputX, y, inputW, rowH); y += rowH + RowSpacing; continue; }
                if (c == _helpBtn)
                { continue; } // positioned after loop
                if (c == _suggestions)
                {
                    if (_suggestions.Visible) { c.SetBounds(inputX, y, inputW, 72); y += 72 + RowSpacing; }
                    continue;
                }
                if (c is DarkLabel lbl2 && lbl2.Text == "Category:")
                { c.SetBounds(p, y + 2, LabelWidth, rowH); continue; }
                if (c == _categoryBox)
                { c.SetBounds(inputX, y, inputW, rowH); y += rowH + RowSpacing; continue; }
                if (c == _categorySuggestions)
                {
                    if (_categorySuggestions.Visible) { c.SetBounds(inputX, y, inputW, 72); y += 72 + RowSpacing; }
                    continue;
                }
            }

            _inputPanel.Height = y + 10;

            // Overlay ? on right end of title box
            int helpLeft = _titleBox.Right - 26;
            _helpBtn.SetBounds(helpLeft, _titleBox.Top + 1, 24, _titleBox.Height - 2);
            _helpBtn.BringToFront();
        }

        // ── Registry persistence ─────────────────────────────────────────────

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            SaveRegistry();
        }

        private void SaveRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKey);
                key.SetValue("Width", Width);
                key.SetValue("Height", Height);
            }
            catch { }
        }

        public static void SaveWasOpen(bool wasOpen)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKey);
                key.SetValue("WasOpen", wasOpen ? 1 : 0);
            }
            catch { }
        }

        public static (Size? size, bool wasOpen) LoadRegistryState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKey);
                if (key == null) return (null, false);
                Size? size = null;
                if (key.GetValue("Width") is int w && key.GetValue("Height") is int h && w > 100 && h > 100)
                    size = new Size(w, h);
                bool wasOpen = key.GetValue("WasOpen") is int v && v == 1;
                return (size, wasOpen);
            }
            catch { }
            return (null, false);
        }

        private static Size? LoadSize() => LoadRegistryState().size;

        // ── Event list ───────────────────────────────────────────────────────

        public void RefreshEventList()
        {
            if (IsDisposed) return;

            int? selectedId = _eventList.SelectedItems.Count > 0
                ? (int)_eventList.SelectedItems[0].Tag!
                : null;

            _eventList.Items.Clear();
            int idx = 1;
            foreach (var ev in _repo.GetRunningEvents())
            {
                var item = new ListViewItem(idx.ToString()) { Tag = ev.Id };
                item.SubItems.Add(ev.Title);
                item.SubItems.Add(ev.Category ?? "");
                item.SubItems.Add(FormatElapsed(ev.Elapsed));
                _eventList.Items.Add(item);
                if (ev.Id == selectedId)
                    item.Selected = true;
                idx++;
            }
            StretchTitleColumn();

            var text = _titleBox.Text.Trim();
            if (text.StartsWith("#") && !_categoryHasFocus)
                UpdateGridHighlight(text);
        }

        private void StretchTitleColumn()
        {
            int fixedWidth = _eventList.Columns[0].Width
                           + _eventList.Columns[2].Width
                           + _eventList.Columns[3].Width
                           + SystemInformation.VerticalScrollBarWidth + 4;
            int titleWidth = _eventList.ClientSize.Width - fixedWidth;
            if (titleWidth > 60)
                _eventList.Columns[1].Width = titleWidth;
        }

        private static string FormatElapsed(TimeSpan t) =>
            t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes}m"
                : $"{t.Minutes}m {t.Seconds}s";

        // ── Grid highlight logic ─────────────────────────────────────────────

        private void UpdateGridHighlight(string text)
        {
            foreach (ListViewItem item in _eventList.Items)
                item.Selected = false;

            if (!text.StartsWith("#")) return;

            var numPart = text.Substring(1).TrimStart('0');
            if (int.TryParse(string.IsNullOrEmpty(numPart) ? "0" : numPart, out int idx) && idx >= 1)
            {
                var match = _eventList.Items.Count >= idx ? _eventList.Items[idx - 1] : null;
                if (match != null) { match.Selected = true; match.EnsureVisible(); }
            }
        }

        // ── Title input / suggestions ────────────────────────────────────────

        // Returns parsed retroactive start time if text begins with @HH:mm, else null.
        // Also outputs the title with the @time prefix stripped.
        private static (DateTime? startAt, string title) ParseRetroPrefix(string text)
        {
            if (!text.StartsWith("@")) return (null, text);
            var space = text.IndexOf(' ');
            var timePart = space > 0 ? text.Substring(1, space - 1) : text.Substring(1);
            var titlePart = space > 0 ? text.Substring(space + 1).Trim() : "";

            // Parse as time of day (HH:mm or H:mm)
            if (DateTime.TryParseExact(timePart, new[] { "H:mm", "HH:mm" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsedTime))
            {
                int hour = parsedTime.Hour;
                int minute = parsedTime.Minute;

                // Handle 12:xx meaning 00:xx in the midnight hour context
                // e.g. at 00:50, typing @12:40 means 00:40 not 12:40
                if (hour == 12 && DateTime.Now.Hour < 1)
                    hour = 0;

                var candidate = DateTime.SpecifyKind(
                    DateTime.Today.AddHours(hour).AddMinutes(minute),
                    DateTimeKind.Local);

                // Only roll back to yesterday if today's time is in the future
                if (candidate > DateTime.Now)
                    candidate = candidate.AddDays(-1);

                if (candidate <= DateTime.Now)
                    return (candidate, titlePart);
            }

            return (null, text);
        }

        private void OnTitleChanged(object? sender, EventArgs e)
        {
            if (_populatingFromSuggestion) return;
            var text = _titleBox.Text.Trim();

            // Always update grid highlight (only responds to #n now)
            UpdateGridHighlight(text);

            // ? @ # ! modes — suppress autocomplete
            if (text.StartsWith("?") || text.StartsWith("@") || text.StartsWith("#") || text.StartsWith("!"))
            {
                _suggestions.Items.Clear();
                _suggestions.Visible = false;
                LayoutInputPanel();
                return;
            }

            // Free text — always autocomplete for new event
            _suggestions.Items.Clear();
            if (text.Length >= 1)
            {
                var matches = _repo.GetCachedTitles(text);
                if (matches.Count > 0)
                {
                    foreach (var (title, _) in matches)
                        _suggestions.Items.Add(title);
                    _suggestions.Visible = true;
                    LayoutInputPanel();
                    return;
                }
            }

            _suggestions.Visible = false;
            LayoutInputPanel();
        }

        // ── Category suggestions ─────────────────────────────────────────────

        private void OnCategoryChanged(object? sender, EventArgs e)
        {
            var text = _categoryBox.Text.Trim();
            _categorySuggestions.Items.Clear();

            if (text.Length >= 1)
            {
                var matches = _repo.GetCachedCategories(text);
                if (matches.Count > 0)
                {
                    foreach (var cat in matches)
                        _categorySuggestions.Items.Add(cat);
                    _categorySuggestions.Visible = true;
                    LayoutInputPanel();
                    return;
                }
            }

            _categorySuggestions.Visible = false;
            LayoutInputPanel();
        }

        private void OnCategoryKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && _categorySuggestions.Visible && _categorySuggestions.Items.Count > 0)
            {
                _categorySuggestions.Focus();
                _categorySuggestions.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        private void OnCategorySuggestionKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { SelectCategorySuggestion(); e.Handled = true; }
            else if (e.KeyCode == Keys.Tab) { SelectCategorySuggestion(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { _categoryBox.Focus(); e.Handled = true; }
        }

        private void SelectCategorySuggestion()
        {
            if (_categorySuggestions.SelectedItem is string selected)
            {
                _categoryBox.Text = selected;
                _categorySuggestions.Visible = false;
                LayoutInputPanel();

                // If title is present, commit immediately — same as keyboard path
                if (!string.IsNullOrWhiteSpace(_titleBox.Text))
                {
                    var (catStartAt, catTitle) = ParseRetroPrefix(_titleBox.Text.Trim());
                    StartEvent(catStartAt, catTitle);
                }
                else
                {
                    _categoryBox.Focus();
                }
            }
        }

        // ── Keyboard handling ────────────────────────────────────────────────

        private void OnTitleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && _suggestions.Visible && _suggestions.Items.Count > 0)
            {
                _suggestions.Focus();
                _suggestions.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        private void OnEnter()
        {
            var text = _titleBox.Text.Trim();

            // ? report command
            if (text.StartsWith("?"))
            {
                ReportBuilder.Generate(text);
                _titleBox.Clear();
                return;
            }

            // If suggestions list has focus, select from it
            if (_suggestions.Visible && _suggestions.SelectedIndex >= 0)
            {
                SelectSuggestion();
                return;
            }

            // If category suggestions list is visible with selection — select and commit if title present
            if (_categorySuggestions.Visible && _categorySuggestions.SelectedIndex >= 0)
            {
                SelectCategorySuggestion();
                if (!string.IsNullOrWhiteSpace(_titleBox.Text))
                {
                    var (catStartAt, catTitle) = ParseRetroPrefix(_titleBox.Text.Trim());
                    StartEvent(catStartAt, catTitle);
                }
                return;
            }

            // ! command — open edit form for active event by index
            if (text.StartsWith("!"))
            {
                var numPart = text.Substring(1).TrimStart('0');
                if (int.TryParse(string.IsNullOrEmpty(numPart) ? "0" : numPart, out int idx)
                    && idx >= 1 && idx <= _eventList.Items.Count)
                {
                    var id = (int)_eventList.Items[idx - 1].Tag!;
                    var form = new EditEventForm(_repo, id);
                    form.FormClosed += (_, _) => RefreshEventList();
                    form.Show();
                    _titleBox.Clear();
                }
                return;
            }

            // #n command — stop highlighted event
            if (text.StartsWith("#") && _eventList.SelectedItems.Count > 0)
            {
                StopSelectedEvent();
                return;
            }

            // Everything else — new event
            var (startAt, title) = ParseRetroPrefix(text);
            StartEvent(startAt, title);
        }

        private void OnSuggestionKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { SelectSuggestion(); e.Handled = true; }
            else if (e.KeyCode == Keys.Tab) { SelectSuggestion(); _categoryBox.Focus(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { _titleBox.Focus(); e.Handled = true; }
        }

        private void SelectSuggestion()
        {
            if (_suggestions.SelectedItem is string selected)
            {
                _populatingFromSuggestion = true;
                _suggestions.Visible = false;
                LayoutInputPanel();
                _titleBox.Text = selected;
                _categoryBox.Text = _repo.GetMostUsedCategory(selected) ?? "";
                _titleBox.SelectionStart = _titleBox.Text.Length;
                _populatingFromSuggestion = false;
                _titleBox.Focus();
            }
        }

        // ── Stop event ───────────────────────────────────────────────────────

        private void StopSelectedEvent()
        {
            var item = _eventList.SelectedItems[0];
            var id = (int)item.Tag!;
            _repo.StopEvent(id);
            _undoStack.Push(id);
            _titleBox.Clear();
            _categoryBox.Clear();
            _suggestions.Visible = false;
            _categorySuggestions.Visible = false;
            LayoutInputPanel();
            RefreshEventList();
        }

        // ── Start event ──────────────────────────────────────────────────────

        private void StartEvent(DateTime? startAt = null, string? titleOverride = null)
        {
            var title = titleOverride ?? _titleBox.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;

            var category = _categoryBox.Text.Trim();
            if (!string.IsNullOrEmpty(category))
                category = _repo.GetCanonicalCategory(category) ?? category;

            _repo.StartEvent(title, string.IsNullOrEmpty(category) ? null : category, startAt);
            EventStarted?.Invoke();
            _categoryHasFocus = false;
            _titleBox.Clear();
            _categoryBox.Clear();
            _suggestions.Visible = false;
            _categorySuggestions.Visible = false;
            LayoutInputPanel();
            RefreshEventList();
            BeginInvoke(() => _titleBox.Focus());
        }

        // ── Form lifecycle ───────────────────────────────────────────────────

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _titleBox.Clear();
            _categoryBox.Clear();
            _suggestions.Items.Clear();
            _suggestions.Visible = false;
            _categorySuggestions.Items.Clear();
            _categorySuggestions.Visible = false;
            _undoStack.Clear();
            LayoutInputPanel();
            RefreshEventList();
            _refreshTimer.Start();
            _titleBox.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_eventList != null) StretchTitleColumn();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                RefreshEventList();
                _refreshTimer.Start();
                _titleBox.Focus();
            }
            else
            {
                _refreshTimer.Stop();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                UserClosed?.Invoke();
                return;
            }
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}