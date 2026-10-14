using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

public sealed class WindowPlacementServiceTests
{
    private static MonitorInfo Monitor(
        int left, int width, bool primary = false, string name = "monitor", double scale = 1.0) =>
        new(name, left, 0, width, 1080, left, 0, width, 1040, scale, scale, primary);

    [Fact]
    public void MonitorAt_ReturnsTheMonitorContainingThePoint()
    {
        var service = new WindowPlacementService(() =>
        [
            Monitor(0, 1920, primary: true),
            Monitor(1920, 2560, name: "right"),
        ]);

        Assert.Equal("monitor", service.MonitorAt(500, 400).DeviceName);
        Assert.Equal("right", service.MonitorAt(3000, 400).DeviceName);
    }

    [Fact]
    public void MonitorAt_ExclusiveRightAndBottomEdges()
    {
        var service = new WindowPlacementService(() =>
        [
            Monitor(0, 1920, primary: true),
            Monitor(1920, 2560, name: "right"),
        ]);

        Assert.Equal("right", service.MonitorAt(1920, 0).DeviceName);
        // x == 1920 belongs to the second display, so the first one ends at 1919.
        Assert.Equal("monitor", service.MonitorAt(1919, 1079).DeviceName);
    }

    [Fact]
    public void MonitorAt_OutsideEveryDisplay_FallsBackToPrimary()
    {
        var service = new WindowPlacementService(() =>
        [
            Monitor(-2560, 2560, name: "left"),
            Monitor(0, 1920, primary: true),
        ]);

        Assert.Equal("monitor", service.MonitorAt(999_999, 999_999).DeviceName);
    }

    [Fact]
    public void PrimaryMonitor_IsTheFlaggedDisplayEvenWhenItIsNotFirst()
    {
        var service = new WindowPlacementService(() =>
        [
            Monitor(-2560, 2560, name: "left"),
            Monitor(0, 1920, primary: true, name: "main"),
        ]);

        Assert.Equal("main", service.PrimaryMonitor.DeviceName);
    }

    [Fact]
    public void PrimaryMonitor_WithoutAnyFlaggedDisplay_UsesTheFirst()
    {
        var service = new WindowPlacementService(() =>
        [
            Monitor(0, 1920, name: "only"),
        ]);

        Assert.Equal("only", service.PrimaryMonitor.DeviceName);
    }

    [Fact]
    public void EmptyEnumeration_StillProducesAUsableFallbackDisplay()
    {
        var service = new WindowPlacementService(() => []);

        var fallback = Assert.Single(service.Monitors);
        Assert.True(fallback.IsPrimary);
        Assert.Equal((0, 0, 1920, 1080), (fallback.Left, fallback.Top, fallback.Width, fallback.Height));
        Assert.Equal(1.0, fallback.ScaleX);
    }

    [Fact]
    public void ScaleComesFromMonitorDpi()
    {
        var service = new WindowPlacementService(() => [Monitor(0, 1920, scale: 1.5, primary: true)]);

        Assert.Equal(1.5, service.PrimaryMonitor.ScaleX);
        Assert.Equal(1.5, service.PrimaryMonitor.ScaleY);
    }

    [Fact]
    public void Refresh_WithIdenticalLayout_DoesNotRaiseDisplaysChanged()
    {
        var monitors = new List<MonitorInfo> { Monitor(0, 1920, primary: true) };
        var service = new WindowPlacementService(() => monitors);

        int raised = 0;
        service.DisplaysChanged += () => raised++;

        service.Refresh();
        service.Refresh();

        Assert.Equal(0, raised);

        monitors.Add(Monitor(1920, 1080));
        service.Refresh();
        Assert.Equal(1, raised);

        monitors.RemoveAt(1);
        service.Refresh();
        Assert.Equal(2, raised);
    }

    [Fact]
    public void LiveEnumeration_ReturnsAtLeastOneDisplayOnThisMachine()
    {
        // The untested-on-purpose path: real EnumDisplayMonitors on the current desktop.
        var service = new WindowPlacementService();

        Assert.NotEmpty(service.Monitors);
        var primary = service.PrimaryMonitor;
        Assert.True(primary.Width > 0 && primary.Height > 0);
        Assert.True(primary.ScaleX > 0.5 && primary.ScaleX <= 8);
        Assert.NotNull(service.MonitorAt(primary.Left + 1, primary.Top + 1));
    }
}
