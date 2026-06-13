using System.Runtime.InteropServices;

namespace AuraScheduler.UI.Infrastructure
{
    /// <summary>
    /// Lightweight Win32 P/Invoke tray icon. No WinForms or third-party dependencies.
    /// Must be created and disposed on the UI thread.
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        // ── Win32 constants ──────────────────────────────────────────────────────
        private const int WM_USER = 0x0400;
        private const int WM_TRAY = WM_USER + 1;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_DESTROY = 0x0002;
        private const int NIM_ADD = 0;
        private const int NIM_DELETE = 2;
        private const int NIF_MESSAGE = 0x01;
        private const int NIF_ICON = 0x02;
        private const int NIF_TIP = 0x04;
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;

        // ── P/Invoke ─────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string? lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags,
            int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // ── State ────────────────────────────────────────────────────────────────
        private readonly IntPtr _hWnd;
        private readonly IntPtr _hIcon;
        private readonly WndProcDelegate _wndProc; // keep alive
        private readonly List<(string Text, Action Action)> _menuItems = new();
        private bool _disposed;

        public event Action? DoubleClicked;

        public TrayIcon(string tooltip)
        {
            var hInstance = GetModuleHandle(null);
            var className = $"TrayIconWnd_{Guid.NewGuid():N}";

            _wndProc = WndProc;

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                hInstance = hInstance,
                lpszClassName = className,
            };
            RegisterClassEx(ref wc);

            // HWND_MESSAGE = -3 creates a message-only window (no taskbar entry, no paint)
            _hWnd = CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

            _hIcon = AppIcon.ExtractSmallIcon();

            var data = BuildNid(tooltip);
            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        public void AddMenuItem(string text, Action action) => _menuItems.Add((text, action));

        public void AddSeparator() => _menuItems.Add((string.Empty, null!));

        private NOTIFYICONDATA BuildNid(string tooltip) => new()
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _hIcon,
            szTip = tooltip,
        };

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAY)
            {
                var mouseMsg = (int)lParam & 0xFFFF;

                if (mouseMsg == WM_LBUTTONDBLCLK)
                    DoubleClicked?.Invoke();

                if (mouseMsg == WM_RBUTTONUP)
                    ShowContextMenu();
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            if (_menuItems.Count == 0) return;

            SetForegroundWindow(_hWnd);
            GetCursorPos(out var pt);

            var hMenu = CreatePopupMenu();
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var (text, _) = _menuItems[i];
                if (string.IsNullOrEmpty(text))
                    AppendMenu(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
                else
                    AppendMenu(hMenu, MF_STRING, new IntPtr(i + 1), text);
            }

            var cmd = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_BOTTOMALIGN | TPM_RETURNCMD, pt.x, pt.y, 0, _hWnd, IntPtr.Zero);
            DestroyMenu(hMenu);

            if (cmd > 0 && cmd <= (uint)_menuItems.Count)
            {
                var (_, action) = _menuItems[(int)cmd - 1];
                action?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var data = BuildNid(string.Empty);
            Shell_NotifyIcon(NIM_DELETE, ref data);

            if (_hWnd != IntPtr.Zero) DestroyWindow(_hWnd);
            if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        }
    }
}
