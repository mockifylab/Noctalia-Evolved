// Bar widget names, matching Noctalia's widget registry (config `start`/`center`/`end`
// lists). Unsupported names are skipped during render rather than failing the bar.
namespace NoctaliaWin.Core.Shell;

public static class WidgetCatalog
{
    /// Widgets implemented in the Windows port as of Milestone 1.
    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "launcher", "clock", "control-center", "settings", "session",
            "network", "volume", "battery", "spacer",
        };

    /// Known upstream widget names not yet implemented on Windows. "text" is here
    /// because it needs a per-widget text value, which the config schema does not
    /// model yet, so rendering it would show an empty label rather than real content.
    /// "tray" and "notifications" are here because the only documented way to show them
    /// needs a panel of our own first: enumerating other processes' status-bar icons and
    /// reading another app's toast content has no supported API.
    public static readonly IReadOnlySet<string> Planned =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "text", "tray", "notifications",
            "wallpaper", "workspaces", "media", "clipboard", "bluetooth",
            "brightness", "keyboard-layout", "active-window", "taskbar",
            "screenshot", "weather", "sysmon", "audio-visualizer", "caffeine",
            "nightlight", "privacy", "power-profile", "theme-mode",
            "custom-button", "lock-keys", "idle-inhibitor",
        };

    public static bool IsSupported(string name) => Supported.Contains(name);
    public static bool IsKnown(string name) => Supported.Contains(name) || Planned.Contains(name);
}
