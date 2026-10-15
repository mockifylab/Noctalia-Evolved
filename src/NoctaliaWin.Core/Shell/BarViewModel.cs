// Bar content: one label per widget, derived from the platform services and nothing else.
//
// A widget with no data (no battery in the machine, no audio endpoint) renders nothing at
// all rather than a filler value, and a widget whose Windows panel does not exist yet is
// not rendered either, so the bar never shows a control that cannot do what it looks like.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;

namespace NoctaliaWin.Core.Shell;

public sealed class BarViewModel : IDisposable
{
    /// Widgets this milestone can draw with real data. The rest of the supported names are
    /// buttons whose windows arrive with the panel and launcher stages.
    private static readonly IReadOnlySet<string> Rendered =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "clock", "network", "volume", "battery", "spacer" };

    private readonly ISystemStatusService _status;
    private readonly IAudioService _audio;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly Action<Action> _marshal;

    public event Action? Changed;

    public BarViewModel(
        ISystemStatusService status,
        IAudioService audio,
        ISettingsStore settings,
        Action<Action> marshal,
        TimeProvider? time = null)
    {
        _status = status;
        _audio = audio;
        _settings = settings;
        _marshal = marshal;
        _time = time ?? TimeProvider.System;

        _status.Changed += OnServiceChanged;
        _audio.Changed += OnServiceChanged;
        _settings.Changed += OnConfigChanged;
    }

    /// A supported widget this milestone can actually draw, for this machine, right now.
    public bool IsRendered(string widget) =>
        Rendered.Contains(widget) && (widget.Equals("battery", StringComparison.OrdinalIgnoreCase) == false || HasBattery);

    public bool HasBattery => _status.Snapshot.Battery != BatteryStatus.NoBattery;

    /// null means "no data to show", which hides the widget rather than labelling it fake.
    public string? Text(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => Strftime.Format(_settings.Current.Shell.TimeFormat, Now),
        "network" => NetworkText,
        "volume" => VolumeText,
        "battery" => HasBattery ? BatteryText : null,
        "spacer" => null,
        _ => null,
    };

    public string? Tooltip(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => Strftime.Format(_settings.Current.Shell.DateFormat, Now),
        "network" => _status.Snapshot.NetworkConnected
            ? $"Connected ({_status.Snapshot.NetworkKind ?? "network"})"
            : "Not connected",
        "volume" => _audio.Muted ? "Muted - click to unmute, scroll to change" : "Click to mute, scroll to change",
        "battery" => BatteryTooltip,
        _ => null,
    };

    public bool IsMuted => _audio.Muted;

    /// Dimmed while offline, like Noctalia's disabled bar text.
    public bool IsDim(string widget) => widget.ToLowerInvariant() switch
    {
        "network" => !_status.Snapshot.NetworkConnected,
        "volume" => _audio.Muted || _audio.MasterVolumePercent is null,
        "battery" => HasBattery && _status.Snapshot.BatteryPercent is null,
        _ => false,
    };

    public DateTimeOffset Now => _time.GetLocalNow();

    public void ToggleMute() => _audio.ToggleMute();

    public void AdjustVolume(int deltaPercent)
    {
        if (_audio.MasterVolumePercent is not { } current) return;
        _audio.SetMasterVolume(Math.Clamp(current + deltaPercent, 0, 100));
    }

    /// Called by the window's clock tick; the label changes on its own format, so the
    /// caller only repaints when the text actually moved.
    public void Refresh() => Changed?.Invoke();

    private string NetworkText
    {
        get
        {
            var snapshot = _status.Snapshot;
            if (!snapshot.NetworkConnected) return "Offline";

            return snapshot.NetworkKind switch
            {
                "wifi" => "Wi-Fi",
                "ethernet" => "Ethernet",
                "cellular" => "Cellular",
                _ => "Network",
            };
        }
    }

    private string? VolumeText => _audio.MasterVolumePercent is not { } percent
        ? "No output"
        : _audio.Muted ? "Muted" : $"{percent}%";

    private string? BatteryText => _status.Snapshot.BatteryPercent is { } percent
        ? $"{percent}%"
        : _status.Snapshot.Battery switch
        {
            BatteryStatus.Charging => "Charging",
            BatteryStatus.FullyCharged => "Full",
            _ => null,
        };

    private string? BatteryTooltip
    {
        get
        {
            var snapshot = _status.Snapshot;
            if (!HasBattery) return null;

            var status = snapshot.Battery switch
            {
                BatteryStatus.Charging => "Charging",
                BatteryStatus.FullyCharged => "Fully charged",
                BatteryStatus.Discharging => "On battery",
                _ => "Unknown",
            };

            return snapshot.BatteryPercent is { } percent ? $"{status}, {percent}%" : status;
        }
    }

    private void OnServiceChanged() => _marshal(() => Changed?.Invoke());

    // A format or scale change repaints the labels; geometry is the window's business.
    private void OnConfigChanged(AppConfig _) => OnServiceChanged();

    public void Dispose()
    {
        _status.Changed -= OnServiceChanged;
        _audio.Changed -= OnServiceChanged;
        _settings.Changed -= OnConfigChanged;
    }
}
