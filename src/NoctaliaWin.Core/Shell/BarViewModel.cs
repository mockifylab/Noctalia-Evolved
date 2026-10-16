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
    ///
    /// `media` is here rather than gated on a session existing: playback starts and stops many
    /// times over a bar's life, and the slot collapses to nothing while there is no track to
    /// show, which is cheaper than rebuilding the bar on every change.
    private static readonly IReadOnlySet<string> Rendered =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "clock", "network", "volume", "battery", "spacer", "media" };

    /// Widgets that are a button rather than a readout: nothing about the machine decides
    /// their text, because their job is to open a surface. Upstream draws these as icon-font
    /// glyphs, which this port cannot ship, so each gets a word instead.
    private static readonly IReadOnlyDictionary<string, string> ButtonLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["launcher"] = "Search",
            ["control-center"] = "Quick settings",
            ["settings"] = "Settings",
        };

    private static readonly IReadOnlyDictionary<string, string> ButtonTooltips =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["launcher"] = "Search installed applications",
            ["control-center"] = "Open quick settings",
            ["settings"] = "Open the Noctalia settings window",
        };

    private readonly ISystemStatusService _status;
    private readonly IAudioService _audio;
    private readonly IMediaService _media;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly Action<Action> _marshal;

    public event Action? Changed;

    public BarViewModel(
        ISystemStatusService status,
        IAudioService audio,
        IMediaService media,
        ISettingsStore settings,
        Action<Action> marshal,
        TimeProvider? time = null)
    {
        _status = status;
        _audio = audio;
        _media = media;
        _settings = settings;
        _marshal = marshal;
        _time = time ?? TimeProvider.System;

        _status.Changed += OnServiceChanged;
        _audio.Changed += OnServiceChanged;
        _media.Changed += OnServiceChanged;
        _settings.Changed += OnConfigChanged;
    }

    /// A supported widget this milestone can actually draw, for this machine, right now.
    public bool IsRendered(string widget) =>
        (Rendered.Contains(widget) || ButtonLabels.ContainsKey(widget))
        && (widget.Equals("battery", StringComparison.OrdinalIgnoreCase) == false || HasBattery);

    public bool HasBattery => _status.Snapshot.Battery != BatteryStatus.NoBattery;

    /// null means "no data to show", which hides the widget rather than labelling it fake.
    public string? Text(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => Strftime.Format(_settings.Current.Shell.TimeFormat, Now),
        "network" => NetworkText,
        "volume" => VolumeText,
        "battery" => HasBattery ? BatteryText : null,
        "media" => MediaText,
        "spacer" => null,
        _ => ButtonLabels.TryGetValue(widget, out var button) ? button : null,
    };

    public string? Tooltip(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => Strftime.Format(_settings.Current.Shell.DateFormat, Now),
        "network" => _status.Snapshot.NetworkConnected
            ? $"Connected ({_status.Snapshot.NetworkKind ?? "network"})"
            : "Not connected",
        "volume" => _audio.Muted
            ? "Muted - the panel has the unmute control, scroll to change level"
            : "Click for quick settings, scroll to change level",
        "battery" => BatteryTooltip,
        "media" => MediaTooltip,
        _ => ButtonTooltips.TryGetValue(widget, out var tip) ? tip : null,
    };

    public bool IsMuted => _audio.Muted;

    /// Dimmed while offline, like Noctalia's disabled bar text.
    public bool IsDim(string widget) => widget.ToLowerInvariant() switch
    {
        "network" => !_status.Snapshot.NetworkConnected,
        "volume" => _audio.Muted || _audio.MasterVolumePercent is null,
        "battery" => HasBattery && _status.Snapshot.BatteryPercent is null,
        "media" => _media.Snapshot is { HasSession: true, Status: not MediaPlaybackStatus.Playing },
        _ => false,
    };

    public DateTimeOffset Now => _time.GetLocalNow();

    /// The panel's headline date, from `shell.date_format`.
    public string DateText => Strftime.Format(_settings.Current.Shell.DateFormat, Now);

    public string ClockText => Strftime.Format(_settings.Current.Shell.TimeFormat, Now);

    /// null when the machine has no output endpoint, which the panel shows as a disabled
    /// control rather than a slider wired to nothing.
    public int? VolumePercent => _audio.MasterVolumePercent;

    public string NetworkSummary => _status.Snapshot.NetworkConnected
        ? $"Connected ({_status.Snapshot.NetworkKind ?? "network"})"
        : "Not connected";

    public string BatterySummary => HasBattery ? BatteryTooltip ?? "Battery" : "No battery in this machine";

    public void ToggleMute() => _audio.ToggleMute();

    public void SetVolume(int percent) => _audio.SetMasterVolume(Math.Clamp(percent, 0, 100));

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

    /// How far the bar will let a track line stretch before it is cut. Titles are as long as
    /// they are, and the bar's own width is not negotiable, so the label gives up room to the
    /// rest of the lane and the tooltip carries the whole line.
    public const int MediaLabelLimit = 42;

    private string? MediaText
    {
        get
        {
            var media = _media.Snapshot;
            if (!media.HasSession) return null;

            string label = TrackLine(media) ?? media.Status switch
            {
                MediaPlaybackStatus.Playing => "Playing",
                MediaPlaybackStatus.Paused => "Paused",
                _ => "Media",
            };

            if (label.Length <= MediaLabelLimit) return label;

            int cut = MediaLabelLimit - 1;
            // A title can hold an emoji, and cutting one in half would show a broken glyph
            // where the picture used to be.
            if (char.IsHighSurrogate(label[cut - 1])) cut--;
            return label[..cut] + "…";
        }
    }

    /// "Title - Artist", or whichever half the player filled in, or null when it filled in
    /// neither. Upstream puts the artist first behind an option this port does not model yet,
    /// so it follows the default order.
    private static string? TrackLine(MediaSnapshot media) => (media.Artist, media.Title) switch
    {
        ({} artist, {} title) => $"{title} - {artist}",
        ({} artist, _) => artist,
        (_, {} title) => title,
        _ => null,
    };

    private string? MediaTooltip
    {
        get
        {
            var media = _media.Snapshot;
            if (!media.HasSession) return null;

            string track = TrackLine(media) ?? "Nothing playing";
            string who = media.AppId is { } app ? $" ({app})" : "";
            return $"{track}{who} - {StatusWord(media.Status)}, the quick-settings panel has the controls";
        }
    }

    private static string StatusWord(MediaPlaybackStatus status) => status switch
    {
        MediaPlaybackStatus.Playing => "playing",
        MediaPlaybackStatus.Paused => "paused",
        MediaPlaybackStatus.Stopped => "stopped",
        _ => "idle",
    };

    /// What the system's media controls report right now. The bar shows a line of it; the
    /// panel offers the commands.
    public MediaSnapshot Media => _media.Snapshot;

    /// The whole track line, uncut. The bar truncates for width; the panel has room for it.
    public string? MediaTrackLine => TrackLine(_media.Snapshot);

    /// Each command is gated on what the player itself says it will take, so a control that
    /// cannot work is never offered as if it could.
    public void ToggleMedia()
    {
        if (Media is { HasSession: true, CanPlayPause: true }) _media.TogglePlayPause();
    }

    public void NextMedia()
    {
        if (Media is { HasSession: true, CanNext: true }) _media.Next();
    }

    public void PreviousMedia()
    {
        if (Media is { HasSession: true, CanPrevious: true }) _media.Previous();
    }

    private void OnServiceChanged() => _marshal(() => Changed?.Invoke());

    // A format or scale change repaints the labels; geometry is the window's business.
    private void OnConfigChanged(AppConfig _) => OnServiceChanged();

    public void Dispose()
    {
        _status.Changed -= OnServiceChanged;
        _audio.Changed -= OnServiceChanged;
        _media.Changed -= OnServiceChanged;
        _settings.Changed -= OnConfigChanged;
    }
}
