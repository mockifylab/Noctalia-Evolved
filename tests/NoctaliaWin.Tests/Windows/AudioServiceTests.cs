using NoctaliaWin.Windows;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Tests.Windows;

public sealed class AudioServiceTests
{
    private sealed class FakeBackend : NoctaliaWin.Windows.IAudioVolumeBackend
    {
        public double? Scalar = 0.5;
        public bool Muted;
        public bool WritesSucceed = true;
        public List<double> WrittenScalars { get; } = [];
        public List<bool> WrittenMutes { get; } = [];

        public double? ReadMasterScalar() => Scalar;
        public bool ReadMuted() => Muted;

        public bool WriteMasterScalar(double scalar)
        {
            if (!WritesSucceed) return false;
            WrittenScalars.Add(scalar);
            Scalar = scalar;
            return true;
        }

        public bool WriteMuted(bool muted)
        {
            if (!WritesSucceed) return false;
            WrittenMutes.Add(muted);
            Muted = muted;
            return true;
        }
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.004, 0)]
    [InlineData(0.005, 1)]
    [InlineData(0.25, 25)]
    [InlineData(0.5, 50)]
    [InlineData(0.999, 100)]
    [InlineData(1.0, 100)]
    [InlineData(-0.3, 0)]   // out-of-range reads are clamped, never shown negative
    [InlineData(1.7, 100)]
    public void ScalarReadings_BecomeThePercentagesTheMixerShows(double scalar, int expected) =>
        Assert.Equal(expected, AudioService.PercentFromScalar(scalar));

    [Fact]
    public void StartsFromTheCurrentEndpointState()
    {
        var backend = new FakeBackend { Scalar = 0.37, Muted = false };
        using var audio = new AudioService(backend);

        Assert.Equal(37, audio.MasterVolumePercent);
        Assert.False(audio.Muted);
    }

    [Fact]
    public void MissingEndpoint_ReportsNoVolumeInsteadOfGuessing()
    {
        var backend = new FakeBackend { Scalar = null };
        using var audio = new AudioService(backend);

        Assert.Null(audio.MasterVolumePercent);
        Assert.False(audio.Muted);
    }

    [Fact]
    public void SettingVolume_WritesTheScalarAndUpdatesImmediately()
    {
        var backend = new FakeBackend();
        using var audio = new AudioService(backend);

        int raised = 0;
        audio.Changed += () => raised++;

        audio.SetMasterVolume(42);

        Assert.Equal(0.42, Assert.Single(backend.WrittenScalars));
        Assert.Equal(42, audio.MasterVolumePercent);
        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(150, 100)]
    public void OutOfRangeRequests_AreClampedBeforeReachingTheDevice(int requested, int expected)
    {
        var backend = new FakeBackend();
        using var audio = new AudioService(backend);

        audio.SetMasterVolume(requested);

        Assert.Equal(expected / 100.0, Assert.Single(backend.WrittenScalars));
        Assert.Equal(expected, audio.MasterVolumePercent);
    }

    [Fact]
    public void ToggleMute_FlipsTheDeviceFlag()
    {
        var backend = new FakeBackend { Muted = false };
        using var audio = new AudioService(backend);

        int raised = 0;
        audio.Changed += () => raised++;
        audio.ToggleMute();

        Assert.True(Assert.Single(backend.WrittenMutes));
        Assert.True(audio.Muted);
        Assert.Equal(1, raised);

        audio.ToggleMute();
        Assert.False(audio.Muted);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void FailedWrites_LeaveTheReportedStateAlone()
    {
        var backend = new FakeBackend { Scalar = 0.6, WritesSucceed = false };
        using var audio = new AudioService(backend);

        int raised = 0;
        audio.Changed += () => raised++;

        audio.SetMasterVolume(10);
        audio.ToggleMute();

        Assert.Equal(60, audio.MasterVolumePercent);
        Assert.False(audio.Muted);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Polling_PicksUpChangesMadeOutsideTheApp()
    {
        var backend = new FakeBackend { Scalar = 0.6 };
        using var audio = new AudioService(backend);

        int raised = 0;
        audio.Changed += () => raised++;

        audio.Poll();
        Assert.Equal(0, raised); // unchanged

        backend.Scalar = 0.2;   // the user drags the Windows mixer
        backend.Muted = true;
        audio.Poll();

        Assert.Equal(1, raised);
        Assert.Equal(20, audio.MasterVolumePercent);
        Assert.True(audio.Muted);
    }

    [Fact]
    public void LiveEndpoint_ReadsThisMachineWithoutMutatingAnything()
    {
        // Real Core Audio call. Deliberately read-only: a test must never move the
        // user's actual volume.
        using var backend = new AudioEndpointVolume();
        using var audio = new AudioService(backend);

        Assert.True(audio.MasterVolumePercent is null or >= 0 and <= 100);
    }
}
