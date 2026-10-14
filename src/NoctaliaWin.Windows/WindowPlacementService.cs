// Monitor geometry and per-monitor DPI, read through documented Win32 monitor APIs.
// Bounds are physical pixels; ScaleX/ScaleY come from MDT_EFFECTIVE_DPI so the WPF
// layer can convert logical Noctalia sizes (bar thickness, radii) per monitor.
using System.Runtime.InteropServices;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

public sealed class WindowPlacementService : IWindowPlacementService
{
    private readonly List<MonitorInfo> _monitors = [];
    private readonly Func<IReadOnlyList<MonitorInfo>>? _reader; // test seam

    public event Action? DisplaysChanged;

    public WindowPlacementService()
        : this(reader: null)
    {
    }

    internal WindowPlacementService(Func<IReadOnlyList<MonitorInfo>>? reader)
    {
        _reader = reader;
        Refresh();
    }

    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    public MonitorInfo PrimaryMonitor =>
        _monitors.FirstOrDefault(m => m.IsPrimary)
        ?? _monitors.FirstOrDefault()
        ?? throw new InvalidOperationException("no displays are available");

    /// The monitor whose bounds contain the point; falls back to the primary monitor
    /// so a stale coordinate can never crash the bar.
    public MonitorInfo MonitorAt(int x, int y)
    {
        foreach (var m in _monitors)
        {
            if (x >= m.Left && x < m.Left + m.Width && y >= m.Top && y < m.Top + m.Height)
                return m;
        }
        return PrimaryMonitor;
    }

    /// Re-enumerates displays. The WPF app calls this from WM_DISPLAYCHANGE.
    public void Refresh()
    {
        var fresh = _reader is { } injected ? injected() : ReadMonitors();
        if (fresh.Count == 0)
            fresh = [FallbackMonitor()];

        if (SameLayout(_monitors, fresh)) return;

        _monitors.Clear();
        _monitors.AddRange(fresh);
        DisplaysChanged?.Invoke();
    }

    private static bool SameLayout(List<MonitorInfo> a, IReadOnlyList<MonitorInfo> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static MonitorInfo FallbackMonitor() =>
        new("fallback", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, 1.0, IsPrimary: true);

    private static IReadOnlyList<MonitorInfo> ReadMonitors()
    {
        var found = new List<MonitorInfo>();

        NativeMethods.MonitorEnumProc onMonitor = (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEXW { cbSize = Marshal.SizeOf<MONITORINFOEXW>() };
            if (!NativeMethods.GetMonitorInfoW(hMonitor, ref info))
                return true;

            double scaleX = 1.0, scaleY = 1.0;
            if (NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY) == NativeMethods.S_OK
                && dpiX > 0 && dpiY > 0)
            {
                scaleX = dpiX / 96.0;
                scaleY = dpiY / 96.0;
            }

            found.Add(new MonitorInfo(
                DeviceName: info.szDevice,
                Left: info.rcMonitor.Left,
                Top: info.rcMonitor.Top,
                Width: info.rcMonitor.Width,
                Height: info.rcMonitor.Height,
                WorkLeft: info.rcWork.Left,
                WorkTop: info.rcWork.Top,
                WorkWidth: info.rcWork.Width,
                WorkHeight: info.rcWork.Height,
                ScaleX: scaleX,
                ScaleY: scaleY,
                IsPrimary: (info.dwFlags & MONITORINFOEXW.PrimaryFlag) != 0));

            return true; // keep enumerating
        };

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, onMonitor, IntPtr.Zero))
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

        // Order by X then Y so index 0 is the left-most display, like Noctalia's monitor list.
        return found
            .OrderBy(m => m.Left)
            .ThenBy(m => m.Top)
            .ToList();
    }
}
