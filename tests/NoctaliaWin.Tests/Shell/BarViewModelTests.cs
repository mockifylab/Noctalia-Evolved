using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Shell;

public sealed class BarViewModelTests
{
    private sealed class FakeStatus(SystemStatusSnapshot snapshot) : ISystemStatusService
    {
        public SystemStatusSnapshot Snapshot { get; set; } = snapshot;
        public event Action? Changed;
        public void Raise() => Changed?.Invoke();
    }

    private sealed class FakeAudio : IAudioService
    {
        public int? MasterVolumePercent { get; set; } = 40;
        public bool Muted { get; set; }
        public List<int> Requests { get; } = [];
        public event Action? Changed;

        public void SetMasterVolume(int percent)
        {
            Requests.Add(percent);
            MasterVolumePercent = percent;
            Changed?.Invoke();
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            Changed?.Invoke();
        }
    }

    private sealed class FakeStore(AppConfig? config = null) : ISettingsStore
    {
        public AppConfig Current { get; set; } = config ?? new AppConfig();
        public string ConfigPath { get; } = @"C:\Test\config.toml";
        public IReadOnlyList<ConfigDiagnostic> LastDiagnostics { get; } = [];
        public event Action<AppConfig>? Changed;
        public List<AppConfig> Saves { get; } = [];

        public void Reload() { }
        public bool ReloadIfChanged() => false;
        public void Save(AppConfig config)
        {
            Saves.Add(config);
            Current = config;
            Changed?.Invoke(config);
        }

        public AppConfig RestoreDefaults() => Current = AppConfig.Default();
    }

    // GetLocalNow is not virtual, so a fixed instant is expressed as a fixed UTC reading
    // plus a local zone with the offset the instant was built with.
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        private TimeZoneInfo? _zone;

        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone =>
            _zone ??= TimeZoneInfo.CreateCustomTimeZone("test", now.Offset, "test", "test");
    }

    /// Records which commands the view model thought the player would take.
    private sealed class FakeMedia : IMediaService
    {
        public MediaSnapshot Snapshot { get; set; } = MediaSnapshot.Empty;
        public int ToggleCount { get; private set; }
        public int NextCount { get; private set; }
        public int PreviousCount { get; private set; }
        public event Action? Changed;

        public void TogglePlayPause() => ToggleCount++;
        public void Next() => NextCount++;
        public void Previous() => PreviousCount++;

        public void Raise() => Changed?.Invoke();
    }

    private static BarViewModel Create(
        FakeAudio audio,
        ISystemStatusService? status = null,
        ISettingsStore? settings = null,
        FakeMedia? media = null) =>
        new(status ?? new FakeStatus(new SystemStatusSnapshot(97, BatteryStatus.Discharging, true, "wifi")),
            audio,
            media ?? new FakeMedia(),
            settings ?? new FakeStore(),
            action => action(),
            new FixedTime(new DateTimeOffset(2026, 10, 15, 9, 37, 0, TimeSpan.FromHours(2))));

    [Fact]
    public void VolumeLabel_ShowsTheEndpointLevel()
    {
        var model = Create(new FakeAudio { MasterVolumePercent = 40 });

        Assert.Equal("40%", model.Text("volume"));
        Assert.True(model.IsRendered("volume"));
        Assert.False(model.IsDim("volume"));
    }

    [Fact]
    public void VolumeLabel_ReportsMuteAndMissingEndpoint()
    {
        var muted = Create(new FakeAudio { MasterVolumePercent = 40, Muted = true });
        var none = Create(new FakeAudio { MasterVolumePercent = null });

        Assert.Equal("Muted", muted.Text("volume"));
        Assert.True(muted.IsDim("volume"));

        // No default endpoint is a real state, and it is labelled rather than blanked.
        Assert.Equal("No output", none.Text("volume"));
        Assert.True(none.IsDim("volume"));
    }

    [Fact]
    public void ClockLabel_UsesTheConfiguredFormat()
    {
        var store = new FakeStore();
        store.Current.Shell.TimeFormat = "{:%H:%M}";
        var model = Create(new FakeAudio(), settings: store);

        Assert.Equal("09:37", model.Text("clock"));
        Assert.Equal("Thursday, 10/15/2026", model.Tooltip("clock"));
    }

    [Theory]
    [InlineData(true, "wifi", "Wi-Fi")]
    [InlineData(true, "ethernet", "Ethernet")]
    [InlineData(true, "cellular", "Cellular")]
    [InlineData(true, null, "Network")]
    [InlineData(false, "wifi", "Offline")]
    [InlineData(false, null, "Offline")]
    public void NetworkLabel_CarriesTheInterfaceKind(bool connected, string? kind, string expected)
    {
        var status = new FakeStatus(new SystemStatusSnapshot(null, BatteryStatus.NoBattery, connected, kind));
        var model = Create(new FakeAudio(), status);

        Assert.Equal(expected, model.Text("network"));
        Assert.Equal(!connected, model.IsDim("network"));
    }

    [Fact]
    public void BatteryWidget_HidesItselfOnAMachineWithoutOne()
    {
        var withBattery = Create(new FakeAudio(), new FakeStatus(
            new SystemStatusSnapshot(64, BatteryStatus.Charging, true, "wifi")));
        var desktop = Create(new FakeAudio(), new FakeStatus(
            new SystemStatusSnapshot(null, BatteryStatus.NoBattery, true, "wifi")));

        Assert.Equal("64%", withBattery.Text("battery"));
        Assert.Equal("Charging, 64%", withBattery.Tooltip("battery"));

        Assert.False(desktop.IsRendered("battery"));
        Assert.Null(desktop.Text("battery"));
    }

    [Fact]
    public void BatteryWithoutPercentage_StillShowsStatusWhenCharging()
    {
        var model = Create(new FakeAudio(), new FakeStatus(
            new SystemStatusSnapshot(null, BatteryStatus.FullyCharged, true, "wifi")));

        Assert.True(model.IsRendered("battery"));
        Assert.Equal("Full", model.Text("battery"));
        Assert.True(model.IsDim("battery"));
    }

    [Theory]
    [InlineData("clock")]
    [InlineData("network")]
    [InlineData("volume")]
    [InlineData("battery")]
    [InlineData("spacer")]
    public void OnlyWidgetsWithRealContentAreRendered(string widget)
    {
        Assert.True(Create(new FakeAudio()).IsRendered(widget));
    }

    [Theory]
    [InlineData("launcher", "Search")]
    [InlineData("control-center", "Quick settings")]
    [InlineData("settings", "Settings")]
    public void BarButtonsCarryAWordInsteadOfAnIcon(string widget, string label)
    {
        // Upstream draws these as icon-font glyphs, which this port does not ship, so the
        // button has to say what it opens.
        var model = Create(new FakeAudio());

        Assert.True(model.IsRendered(widget));
        Assert.Equal(label, model.Text(widget));
        Assert.NotNull(model.Tooltip(widget));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("made-up")]
    public void WidgetsWithoutAPanelYetAreNotRendered(string widget)
    {
        var model = Create(new FakeAudio());

        Assert.False(model.IsRendered(widget));
        Assert.Null(model.Text(widget));
    }

    [Fact]
    public void Spacer_HasNoLabelButTakesItsSlot()
    {
        var model = Create(new FakeAudio());

        Assert.True(model.IsRendered("spacer"));
        Assert.Null(model.Text("spacer"));
    }

    [Fact]
    public void VolumeActions_GoStraightToTheAudioService()
    {
        var audio = new FakeAudio { MasterVolumePercent = 40 };
        var model = Create(audio);

        model.AdjustVolume(5);
        model.AdjustVolume(-60);
        model.ToggleMute();

        Assert.Equal(new[] { 45, 0 }, audio.Requests);
        Assert.True(audio.Muted);
    }

    [Fact]
    public void VolumeWithoutAnEndpoint_IgnoresAdjustment()
    {
        var audio = new FakeAudio { MasterVolumePercent = null };
        var model = Create(audio);

        model.AdjustVolume(10);

        Assert.Empty(audio.Requests);
    }

    [Fact]
    public void ServiceChanges_ReachTheViewThroughTheMarshaller()
    {
        var audio = new FakeAudio();
        var media = new FakeMedia();
        int marshalled = 0;
        var model = new BarViewModel(
            new FakeStatus(new SystemStatusSnapshot(50, BatteryStatus.Discharging, true, "ethernet")),
            audio,
            media,
            new FakeStore(),
            action =>
            {
                marshalled++;
                action();
            },
            new FixedTime(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero)));

        int repaints = 0;
        model.Changed += () => repaints++;

        audio.ToggleMute();

        Assert.Equal(1, marshalled);
        Assert.Equal(1, repaints);

        // The media service reports from whatever thread Windows raised it on, so it goes
        // through the same hand-over as the rest of them.
        media.Raise();

        Assert.Equal(2, marshalled);
        Assert.Equal(2, repaints);
    }

    [Fact]
    public void ConfigChanges_RepaintSoNewFormatsTakeEffect()
    {
        var store = new FakeStore();
        var model = Create(new FakeAudio(), settings: store);
        int repaints = 0;
        model.Changed += () => repaints++;

        store.Current.Shell.TimeFormat = "%H";
        store.Save(store.Current);

        Assert.Equal(1, repaints);
        Assert.Equal("09", model.Text("clock"));
    }

    [Fact]
    public void Dispose_UnsubscribesFromEveryService()
    {
        var audio = new FakeAudio();
        var media = new FakeMedia();
        var model = Create(audio, media: media);
        int repaints = 0;
        model.Changed += () => repaints++;

        model.Dispose();
        audio.ToggleMute();
        media.Raise();

        Assert.Equal(0, repaints);
    }

    private static MediaSnapshot Playing(string? title, string? artist) =>
        new(true, title, artist, MediaPlaybackStatus.Playing, true, true, true, "player.exe");

    [Fact]
    public void MediaLabel_ShowsTheTrackLineThePlayerReports()
    {
        var media = new FakeMedia { Snapshot = Playing("Anvil", "Rush") };
        var model = Create(new FakeAudio(), media: media);

        Assert.Equal("Anvil - Rush", model.Text("media"));
        Assert.False(model.IsDim("media"));
        Assert.Contains("player.exe", model.Tooltip("media"));
    }

    [Fact]
    public void MediaLabel_ShowsWhicheverHalfThePlayerFilledIn()
    {
        var titleOnly = Create(new FakeAudio(),
            media: new FakeMedia { Snapshot = Playing("Anvil", null) });
        var artistOnly = Create(new FakeAudio(),
            media: new FakeMedia { Snapshot = Playing(null, "Rush") });
        // A live session with no metadata at all is normal mid-load, and says so plainly.
        var bare = Create(new FakeAudio(),
            media: new FakeMedia { Snapshot = Playing(null, null) });

        Assert.Equal("Anvil", titleOnly.Text("media"));
        Assert.Equal("Rush", artistOnly.Text("media"));
        Assert.Equal("Playing", bare.Text("media"));
    }

    [Fact]
    public void MediaLabel_TakesNoRoomUntilASessionExists()
    {
        var media = new FakeMedia();
        var model = Create(new FakeAudio(), media: media);

        Assert.True(model.IsRendered("media"));
        Assert.Null(model.Text("media"));
        Assert.Null(model.Tooltip("media"));

        media.Snapshot = Playing("Anvil", "Rush");
        Assert.Equal("Anvil - Rush", model.Text("media"));
    }

    [Fact]
    public void MediaLabel_IsDimmedWhilePaused()
    {
        var model = Create(new FakeAudio(), media: new FakeMedia
        {
            Snapshot = Playing("Anvil", "Rush") with { Status = MediaPlaybackStatus.Paused },
        });

        Assert.True(model.IsDim("media"));
    }

    [Fact]
    public void MediaLabel_CutsWithinTheBarAndWithoutBreakingAnEmoji()
    {
        var longTitle = new string('t', 60);
        var model = Create(new FakeAudio(),
            media: new FakeMedia { Snapshot = Playing(longTitle, "Artist") });

        string cut = model.Text("media")!;
        Assert.Equal(BarViewModel.MediaLabelLimit, cut.Length);
        Assert.EndsWith("…", cut);

        // Cutting at a fixed count can land between the two halves of a surrogate pair, which
        // WPF would draw as a replacement box. Here the emoji straddles the cut.
        var emoji = "x".PadRight(40, 'x') + "🎵y";
        var emojiModel = Create(new FakeAudio(),
            media: new FakeMedia { Snapshot = Playing(emoji, null) });

        string cutEmoji = emojiModel.Text("media")!;
        Assert.EndsWith("…", cutEmoji);
        Assert.Equal(41, cutEmoji.Length);
        Assert.False(char.IsHighSurrogate(cutEmoji[cutEmoji.Length - 2]));
    }

    [Fact]
    public void MediaCommands_SendOnlyWhatThePlayerSaysItWillTake()
    {
        var media = new FakeMedia { Snapshot = Playing("Anvil", "Rush") };
        var model = Create(new FakeAudio(), media: media);

        model.ToggleMedia();
        model.NextMedia();
        model.PreviousMedia();
        Assert.Equal((1, 1, 1), (media.ToggleCount, media.NextCount, media.PreviousCount));

        // A player that accepts nothing is asked for nothing, so no control looks live when
        // it is not.
        media.Snapshot = Playing("Anvil", "Rush") with
        {
            CanPlayPause = false,
            CanNext = false,
            CanPrevious = false,
        };
        model.ToggleMedia();
        model.NextMedia();
        model.PreviousMedia();
        Assert.Equal((1, 1, 1), (media.ToggleCount, media.NextCount, media.PreviousCount));

        // No session at all is the same refusal.
        media.Snapshot = MediaSnapshot.Empty;
        model.ToggleMedia();
        Assert.Equal(1, media.ToggleCount);
    }
}
