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
        public IReadOnlyList<ConfigDiagnostic> LastDiagnostics { get; } = [];
        public event Action<AppConfig>? Changed;
        public List<AppConfig> Saves { get; } = [];

        public void Reload() { }
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

    private static BarViewModel Create(
        FakeAudio audio,
        ISystemStatusService? status = null,
        ISettingsStore? settings = null) =>
        new(status ?? new FakeStatus(new SystemStatusSnapshot(97, BatteryStatus.Discharging, true, "wifi")),
            audio,
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
    [InlineData("launcher")]
    [InlineData("control-center")]
    [InlineData("session")]
    [InlineData("settings")]
    [InlineData("media")]
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
        int marshalled = 0;
        var model = new BarViewModel(
            new FakeStatus(new SystemStatusSnapshot(50, BatteryStatus.Discharging, true, "ethernet")),
            audio,
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
        var model = Create(audio);
        int repaints = 0;
        model.Changed += () => repaints++;

        model.Dispose();
        audio.ToggleMute();

        Assert.Equal(0, repaints);
    }
}
