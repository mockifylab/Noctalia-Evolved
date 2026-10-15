using System.Runtime.InteropServices;

namespace NoctaliaWin.Windows;

/// <summary>
/// Asks the desktop window manager to draw one window's caption in dark mode
/// (DWMWA_USE_IMMERSIVE_DARK_MODE). Per-window and documented: it is set again every time the
/// palette changes and never writes the registry or a system-wide setting, so a window that
/// stops asking goes back to the system default on its own.
/// </summary>
public static class WindowThemeChrome
{
    // 20 is the published attribute; 19 is the same attribute under the number Windows 10
    // 1809-1903 used, tried only when the modern one is refused.
    internal const int UseImmersiveDarkMode = 20;
    internal const int UseImmersiveDarkModeLegacy = 19;

    /// True when the caption took the requested state. A window whose build does not know the
    /// attribute keeps the system caption, which is a cosmetic miss, not a failure.
    public static bool UseDarkCaption(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return false;

        int value = dark ? 1 : 0;
        return Apply(hwnd, UseImmersiveDarkMode, value) == 0
            || Apply(hwnd, UseImmersiveDarkModeLegacy, value) == 0;
    }

    private static int Apply(IntPtr hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}
