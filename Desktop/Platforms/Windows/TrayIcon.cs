#if WINDOWS

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

/// <summary>
/// Minimal Shell_NotifyIcon wrapper, so we don't have to drag WinForms into the app just for a tray icon.
/// Owns the STA thread that creates and pumps its message-only window; GetMessage only returns messages queued to the
/// calling thread, so creating and pumping must happen on the same one.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    public sealed record MenuItem(string Text, Action? OnClick = null)
    {
        public static readonly MenuItem Separator = new(string.Empty);
    }

    private const int WmDestroy = 0x0002;
    private const int WmClose = 0x0010;
    private const int WmNull = 0x0000;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const int WmTrayCallback = 0x8000 + 1; // WM_APP + 1

    private const int NimAdd = 0x0000;
    private const int NimModify = 0x0001;
    private const int NimDelete = 0x0002;

    private const int NifMessage = 0x0001;
    private const int NifIcon = 0x0002;
    private const int NifTip = 0x0004;

    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;

    private const int SmCxSmIcon = 49;
    private const int SmCySmIcon = 50;

    private const uint MfString = 0x0000;
    private const uint MfSeparator = 0x0800;
    private const uint MfGrayed = 0x0001;

    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const uint TpmNonNotify = 0x0080;

    private static readonly IntPtr HwndMessage = new(-3);

    private readonly WndProc _wndProc;
    private readonly string _className;
    private readonly Thread _thread;
    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private readonly Action? _onLeftClick;

    /// <summary>
    /// Menu shown on right click. Read on the owning thread when the menu pops up, so it may be swapped from any thread.
    /// </summary>
    public IReadOnlyList<MenuItem> MenuItems { get; set; } = [];

    /// <param name="tooltip">Hover text, truncated to 127 characters by the shell.</param>
    /// <param name="iconPath">Path to an .ico file.</param>
    /// <param name="onLeftClick">Invoked on the owning thread when the icon is left clicked.</param>
    public TrayIcon(string tooltip, string iconPath, Action? onLeftClick = null)
    {
        _onLeftClick = onLeftClick;
        _wndProc = WindowProc;
        _className = $"OpenShockTray_{Guid.NewGuid():N}";

        using var ready = new ManualResetEventSlim(false);
        Exception? failure = null;

        _thread = new Thread(() =>
        {
            try
            {
                Create(tooltip, iconPath);
            }
            catch (Exception e)
            {
                failure = e;
                ready.Set();
                return;
            }

            ready.Set();
            RunMessageLoop();

            // UnregisterClass fails while a window of the class exists, so it can only happen once the loop returns.
            if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
            UnregisterClassW(_className, GetModuleHandleW(null));
        })
        {
            IsBackground = true,
            Name = "OpenShock tray"
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        ready.Wait();
        if (failure != null) throw failure;
    }

    private void Create(string tooltip, string iconPath)
    {
        var moduleHandle = GetModuleHandleW(null);

        var windowClass = new WndClassEx
        {
            cbSize = Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = moduleHandle,
            lpszClassName = _className
        };

        if (RegisterClassExW(ref windowClass) == 0)
            throw new InvalidOperationException("Failed to register tray window class.",
                new Win32Exception(Marshal.GetLastWin32Error()));

        _hwnd = CreateWindowExW(0, _className, string.Empty, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, moduleHandle,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create tray message window.",
                new Win32Exception(Marshal.GetLastWin32Error()));

        _hIcon = LoadImageW(IntPtr.Zero, iconPath, ImageIcon, GetSystemMetrics(SmCxSmIcon),
            GetSystemMetrics(SmCySmIcon), LrLoadFromFile);

        var data = CreateIconData();
        data.uFlags = NifMessage | NifIcon | NifTip;
        data.uCallbackMessage = WmTrayCallback;
        data.hIcon = _hIcon;
        data.szTip = tooltip;

        if (!Shell_NotifyIconW(NimAdd, ref data))
            throw new InvalidOperationException("Failed to add tray icon.",
                new Win32Exception(Marshal.GetLastWin32Error()));
    }

    /// <summary>
    /// Updates the hover text of the icon.
    /// </summary>
    public void SetTooltip(string tooltip)
    {
        var data = CreateIconData();
        data.uFlags = NifTip;
        data.szTip = tooltip;

        Shell_NotifyIconW(NimModify, ref data);
    }

    /// <summary>
    /// Pumps messages until the owning thread receives WM_QUIT.
    /// </summary>
    private static void RunMessageLoop()
    {
        int result;
        while ((result = GetMessageW(out var message, IntPtr.Zero, 0, 0)) != 0)
        {
            if (result == -1) return;

            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private NotifyIconData CreateIconData() => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = _hwnd,
        uID = 1,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WmTrayCallback:
                switch ((int)(lParam.ToInt64() & 0xFFFF))
                {
                    case WmLButtonUp:
                        _onLeftClick?.Invoke();
                        break;
                    case WmRButtonUp:
                    case WmContextMenu:
                        ShowContextMenu();
                        break;
                }

                return IntPtr.Zero;

            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var items = MenuItems;
        if (items.Count == 0) return;

        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (ReferenceEquals(item, MenuItem.Separator))
                {
                    AppendMenuW(menu, MfSeparator, UIntPtr.Zero, null);
                    continue;
                }

                // Command ids are 1-based, TrackPopupMenuEx returns 0 when the menu is dismissed.
                AppendMenuW(menu, item.OnClick == null ? MfString | MfGrayed : MfString, (UIntPtr)(i + 1), item.Text);
            }

            GetCursorPos(out var cursor);

            // Required so the menu closes when the user clicks elsewhere, see TrackPopupMenuEx remarks.
            SetForegroundWindow(_hwnd);

            var command = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCmd | TpmNonNotify, cursor.X, cursor.Y,
                _hwnd, IntPtr.Zero);

            PostMessageW(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);

            if (command > 0 && command <= items.Count) items[command - 1].OnClick?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var data = CreateIconData();
        Shell_NotifyIconW(NimDelete, ref data);

        // A window may only be destroyed from the thread that created it.
        if (_hwnd != IntPtr.Zero) PostMessageW(_hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);

        // Reached from a menu action (Quit) the pump thread is still inside, which cannot join itself.
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(2));
    }

    private delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public int message;
        public IntPtr wParam;
        public IntPtr lParam;
        public int time;
        public Point pt;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool Shell_NotifyIconW(int message, ref NotifyIconData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WndClassEx windowClass);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterClassW([MarshalAs(UnmanagedType.LPWStr)] string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int exStyle, [MarshalAs(UnmanagedType.LPWStr)] string className,
        [MarshalAs(UnmanagedType.LPWStr)] string windowName, int style, int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessageW(out Msg message, IntPtr hwnd, int filterMin, int filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref Msg message);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr instance, [MarshalAs(UnmanagedType.LPWStr)] string name, uint type,
        int cx, int cy, uint load);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id,
        [MarshalAs(UnmanagedType.LPWStr)] string? item);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr @params);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);
}

#endif
