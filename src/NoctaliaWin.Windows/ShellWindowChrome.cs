// Physical-pixel placement and pointer position for the shell windows.
//
// WPF's Left/Top/Width/Height are device-independent units whose mapping to a specific
// monitor is ambiguous on a per-monitor-DPI desktop, so a bar computed in pixels by
// NoctaliaWin.Core is positioned with SetWindowPos instead. Both APIs are documented and
// neither one touches another process.
using NoctaliaWin.Core.Shell;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

public static class ShellWindowChrome
{
    /// Sent to a window whenever the display arrangement changes (winuser.h).
    public const int WmDisplayChange = 0x007E;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNonTopMost = new(-2);

    /// false when the OS refused the move; a shell window that cannot be placed must not
    /// take the desktop down with it.
    public static bool Place(IntPtr hwnd, PxRect rect, bool topMost)
    {
        if (hwnd == IntPtr.Zero) return false;

        bool ok = NativeMethods.SetWindowPos(
            hwnd,
            topMost ? HwndTopMost : HwndNonTopMost,
            rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOACTIVATE);

        return ok;
    }

    /// Cursor position in physical screen pixels, or null when the query fails (no
    /// pointer device, or no interactive desktop) — the caller then treats the pointer as
    /// "not on the bar".
    public static (int X, int Y)? Pointer() =>
        NativeMethods.GetCursorPos(out var point) ? (point.X, point.Y) : null;
}
