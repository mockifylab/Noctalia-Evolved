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

internal static class NativeMethods
{
    public const int S_OK = 0;

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
}
