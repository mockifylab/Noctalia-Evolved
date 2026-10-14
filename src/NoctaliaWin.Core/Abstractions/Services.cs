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

public interface IHotkeyService
{
    /// Registers a global hotkey; returns false if the combination is taken.
    bool Register(string gesture, Action callback); // e.g. "CTRL+ALT+SPACE"
    void UnregisterAll();
}

public interface ITrayService : IDisposable
{
    event Action? IconActivated; // left click / double click: reopen settings
    event Action? ExitRequested;
    void SetTooltip(string text);
}
