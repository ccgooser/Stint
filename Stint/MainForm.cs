using DarkUI.Forms;
using Stint.Data;
using Stint.Services;

namespace Stint
{
    public class MainForm : DarkForm
    {
        private NotifyIcon _trayIcon;
        private HotkeyService _hotkey;
        private AutoStopService _autoStop;
        private EventRepository _repo = new();
        private TimerPopupForm? _popup;
        private bool _userOpenedPopup = false;

        public MainForm()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;
            Size = new Size(1, 1);


            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
            var icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

            _trayIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "Stint"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("New Event", null, (_, _) => OpenPopup());
            menu.Items.Add("-");
            menu.Items.Add("Exit", null, (_, _) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.Click += (_, e) => { if (e is MouseEventArgs me && me.Button == MouseButtons.Left) OpenPopup(); };

            _hotkey = new HotkeyService();
            _hotkey.HotkeyPressed += OpenPopup;

            _autoStop = new AutoStopService(_repo, _trayIcon);
            _autoStop.EventsChanged += () => _popup?.RefreshEventList();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _hotkey.Register(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            _hotkey.ProcessMessage(m);
            base.WndProc(ref m);
        }

        public void OpenPopup()
        {
            if (_popup != null && !_popup.IsDisposed && _popup.Visible)
            {
                _popup.Hide();
                return;
            }

            if (_popup == null || _popup.IsDisposed)
            {
                _popup = new TimerPopupForm(_repo);
                _popup.UserClosed += () => _userOpenedPopup = false;
            }

            PositionNearTray(_popup);
            _popup.Show();
            _popup.BringToFront();
            _popup.Activate();
            _userOpenedPopup = true;
        }

        private void PositionNearTray(Form form)
        {
            var screen = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
            form.Left = screen.Right - form.Width - 16;
            form.Top = screen.Bottom - form.Height - 16;
        }

        private void ExitApp()
        {
            TimerPopupForm.SaveWasOpen(_userOpenedPopup);
            _trayIcon.Visible = false;
            _hotkey.Dispose();
            _autoStop.Dispose();
            _popup?.Dispose();
            Application.Exit();
            Environment.Exit(0);
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
