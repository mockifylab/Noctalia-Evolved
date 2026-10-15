// Documented Win32 entry points used by the Windows service layer. Only the
// APIs this project needs are declared; nothing here patches or hooks anything.
using System.Runtime.InteropServices;
using System.Text;

namespace NoctaliaWin.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MONITORINFOEXW
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice = string.Empty;

    public MONITORINFOEXW()
    {
        cbSize = 0;
        rcMonitor = default;
        rcWork = default;
        dwFlags = 0;
        szDevice = string.Empty;
    }

    public const uint PrimaryFlag = 1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus; // 0 offline, 1 online, 255 unknown
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public int BatteryLifeTime;
    public int BatteryFullLifeTime;
}

internal struct POINT
{
    public int X;
    public int Y;
}

internal static class NativeMethods
{
    public const int S_OK = 0;

    // SetWindowPos flags (winuser.h).
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    // The LPRECT is passed as a pointer we never read; the monitor handle is the
    // input to GetMonitorInfoW.
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect,
        MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEXW lpmi);

    // MDT_EFFECTIVE_DPI = 0: the DPI a window on this monitor is laid out with.
    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    // Physical-pixel window placement: WPF's Left/Top/Width/Height are DIPs of an
    // ambiguous monitor on per-monitor-DPI desktops, so the shell windows are positioned
    // through SetWindowPos, which is documented to take virtual-screen pixels.
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // Global hotkeys. RegisterHotKey binds to a window: WM_HOTKEY is posted to that window's
    // thread, so the shell uses a message-only window rather than a bar or panel one - a
    // binding has to outlive the window that happens to be on screen.
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // Windows 7 and later: the key fires once per press instead of again on every auto-repeat,
    // which would otherwise re-toggle the launcher while a key is held down.
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    // The parent value that turns a window into a message-only one: it is never visible,
    // never in the tab order and receives nothing but posted messages.
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
