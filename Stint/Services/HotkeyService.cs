using System.Runtime.InteropServices;

namespace Stint.Services
{
    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 0x4000;
        private const uint MOD_ALT   = 0x0001;
        private const uint MOD_CTRL  = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private IntPtr _handle;
        public event Action? HotkeyPressed;

        public void Register(IntPtr windowHandle)
        {
            _handle = windowHandle;
            var (modifiers, key) = ParseHotkey(AppConfig.Hotkey);
            RegisterHotKey(_handle, HOTKEY_ID, modifiers, (uint)key);
        }

        private static (uint modifiers, Keys key) ParseHotkey(string hotkey)
        {
            uint modifiers = 0;
            Keys key = Keys.T;

            var parts = hotkey.Split('+');
            foreach (var part in parts)
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "alt":   modifiers |= MOD_ALT;   break;
                    case "shift": modifiers |= MOD_SHIFT; break;
                    case "ctrl":
                    case "control": modifiers |= MOD_CTRL; break;
                    default:
                        if (Enum.TryParse<Keys>(part.Trim(), ignoreCase: true, out var parsed))
                            key = parsed;
                        break;
                }
            }

            return (modifiers, key);
        }

        public void ProcessMessage(Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY_ID)
                HotkeyPressed?.Invoke();
        }

        public void Dispose() => UnregisterHotKey(_handle, HOTKEY_ID);
    }
}
