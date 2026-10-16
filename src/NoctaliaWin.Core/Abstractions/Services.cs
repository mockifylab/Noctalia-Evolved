// Platform service contracts: the WPF layer depends only on these; all Win32
// implementations live in NoctaliaWin.Windows.
namespace NoctaliaWin.Core.Abstractions;

public sealed record MonitorInfo(
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    int WorkLeft,
    int WorkTop,
    int WorkWidth,
    int WorkHeight,
    double ScaleX,
    double ScaleY,
    bool IsPrimary);

public interface IWindowPlacementService
{
    IReadOnlyList<MonitorInfo> Monitors { get; }
    event Action? DisplaysChanged;
    MonitorInfo MonitorAt(int x, int y);
    MonitorInfo PrimaryMonitor { get; }
}

public enum BatteryStatus
{
    NoBattery,
    Charging,
    Discharging,
    FullyCharged,
}

public sealed record SystemStatusSnapshot(
    int? BatteryPercent,
    BatteryStatus Battery,
    bool NetworkConnected,
    string? NetworkKind);

public interface ISystemStatusService
{
    SystemStatusSnapshot Snapshot { get; }
    event Action? Changed;
}

public interface IAudioService
{
    /// 0..100 master volume of the default output endpoint, or null if unavailable.
    int? MasterVolumePercent { get; }
    bool Muted { get; }
    void SetMasterVolume(int percent);
    void ToggleMute();
    event Action? Changed;
}

/// Where the system's current media session stands. `None` means there is no session at all,
/// which is a different thing from a player that is stopped.
public enum MediaPlaybackStatus
{
    None,
    Playing,
    Paused,
    Stopped,
}

/// What the operating system reports about whatever holds the media controls right now.
/// Every text field can be empty: a session with no metadata is normal — a browser tab that
/// has not filled its properties in, a radio stream, a podcast app mid-load — so the bar shows
/// what is there rather than making up what is not.
public sealed record MediaSnapshot(
    bool HasSession,
    string? Title,
    string? Artist,
    MediaPlaybackStatus Status,
    bool CanPlayPause,
    bool CanNext,
    bool CanPrevious,
    string? AppId)
{
    public static MediaSnapshot Empty { get; } =
        new(false, null, null, MediaPlaybackStatus.None, false, false, false, null);
}

/// The system-wide media controls — the same surface the OS volume overlay and a keyboard
/// play key drive. Reading it needs no cooperation from the player, which is why it shows
/// Spotify and a browser and a media app alike.
public interface IMediaService
{
    MediaSnapshot Snapshot { get; }
    event Action? Changed;

    /// These hand the request to whichever session Windows considers current and return;
    /// the result arrives back through `Changed`. A player that will not take the command
    /// simply leaves the snapshot where it was, so callers read `Can*` before offering one.
    void TogglePlayPause();
    void Next();
    void Previous();
}

public sealed record AppEntry(
    string Name,
    string Exec,
    string? Args,
    string? IconPath,
    string Kind) // "desktop" | "uwp" | "run"
{
    public string LaunchTarget => Args is { Length: > 0 } a ? $"{Exec} {a}" : Exec;
}

public interface IApplicationDiscoveryService
{
    IReadOnlyList<AppEntry> Applications { get; }
    event Action? Changed;
    void Refresh();
}

public interface IProcessLauncher
{
    /// Returns null on success, or an error message.
    string? Launch(AppEntry entry);
}

public interface IStartupService
{
    bool Enabled { get; }
    void SetEnabled(bool enabled);
}

/// <summary>
/// The operating system's own light/dark choice for application chrome, which is what
/// `theme.mode = "auto"` resolves against. Distinct from Noctalia's palette mode.
/// </summary>
public interface ISystemThemeService
{
    bool UsesLightTheme { get; }
    event Action? Changed;
}

public interface IHotkeyService
{
    /// Binds a global shortcut such as "CTRL+ALT+SPACE". Returns null once the key is live,
    /// or the reason it is not: a gesture this build cannot parse, or a combination another
    /// application already owns. An empty gesture asks for no binding and succeeds by doing
    /// nothing, so callers never have to branch on it.
    /// The callback runs on the thread that owns the service.
    string? Register(string gesture, Action callback);

    /// Drops every binding this process took, in reverse order.
    void UnregisterAll();
}

public interface ITrayService : IDisposable
{
    event Action? IconActivated; // left click: the launcher, the same door the bar's Search opens
    event Action? SettingsRequested; // the menu's Settings entry
    event Action? ExitRequested;
    void SetTooltip(string text);
}
