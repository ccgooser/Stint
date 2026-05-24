using DarkUI.Forms;

namespace Stint
{
    public class CommandHintForm : Form
    {
        private static CommandHintForm? _instance;

        private static readonly (string Command, string Description)[] Commands = new[]
        {
            ("@HH:mm title",   "Retroactive start"),
            ("#n",             "Stop event by index"),
            ("!n",             "Edit event by index"),
            ("?today",         "Today's report"),
            ("?yesterday",     "Yesterday's report"),
            ("?week",          "This week's report"),
            ("?month",         "This month's report"),
            ("?history",       "Full event log"),
            ("?open",          "Browse reports folder"),
            ("?help",          "Full help page"),
        };

        private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
        private static readonly Color BorderColor = Color.FromArgb(70, 70, 70);
        private static readonly Color CmdColor = Color.FromArgb(146, 170, 214);
        private static readonly Color DescColor = Color.FromArgb(200, 200, 200);
        private static readonly Color HeadColor = Color.FromArgb(120, 120, 120);

        private CommandHintForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = DarkBg;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12, 10, 16, 10);
            Font = new System.Drawing.Font("Segoe UI", 9.5f);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = DarkBg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Header
            var header = new Label
            {
                Text = "Command Reference",
                ForeColor = HeadColor,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 6),
                Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = DarkBg
            };
            layout.SetColumnSpan(header, 2);
            layout.Controls.Add(header);

            foreach (var (cmd, desc) in Commands)
            {
                layout.Controls.Add(new Label
                {
                    Text = cmd,
                    ForeColor = CmdColor,
                    AutoSize = true,
                    Padding = new Padding(0, 2, 16, 2),
                    Font = new System.Drawing.Font("Consolas", 9.5f),
                    BackColor = DarkBg
                });
                layout.Controls.Add(new Label
                {
                    Text = desc,
                    ForeColor = DescColor,
                    AutoSize = true,
                    Padding = new Padding(0, 2, 0, 2),
                    BackColor = DarkBg
                });
            }

            Controls.Add(layout);

            // Dismiss on click anywhere or loss of focus
            Click += (_, _) => Close();
            Deactivate += (_, _) => Close();
            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        public static void ShowNear(Control anchor)
        {
            _instance?.Close();
            _instance = new CommandHintForm();

            var screenPos = anchor.PointToScreen(new Point(0, 0));
            _instance.StartPosition = FormStartPosition.Manual;

            _instance.Load += (_, _) =>
            {
                _instance.Left = screenPos.X;
                _instance.Top = screenPos.Y - _instance.Height - 4;
            };

            _instance.FormClosed += (_, _) => _instance = null;
            _instance.Show(anchor.FindForm());
        }
    }
}