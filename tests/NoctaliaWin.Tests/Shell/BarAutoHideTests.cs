using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Shell;

public sealed class BarAutoHideTests
{
    private static MonitorInfo Monitor() =>
        new("monitor", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, 1.0, IsPrimary: true);

    private static BarPlacement Placement(string position, int marginEdge = 10, int marginEnds = 180) =>
        BarLayout.Compute(Monitor(), new BarSection
        {
            Position = position,
            MarginEdge = marginEdge,
            MarginEnds = marginEnds,
        });

    [Fact]
    public void TriggerBand_IsAThinStripOnTheDockedWorkEdge()
    {
        Assert.Equal(new PxRect(0, 0, 1920, 2), BarAutoHide.TriggerBand(Placement("top"), Monitor()));
        Assert.Equal(new PxRect(0, 1038, 1920, 2), BarAutoHide.TriggerBand(Placement("bottom"), Monitor()));
        Assert.Equal(new PxRect(0, 0, 2, 1040), BarAutoHide.TriggerBand(Placement("left"), Monitor()));
        Assert.Equal(new PxRect(1918, 0, 2, 1040), BarAutoHide.TriggerBand(Placement("right"), Monitor()));
    }

    [Fact]
    public void TriggerBand_RespectsACustomThickness()
    {
        var band = BarAutoHide.TriggerBand(Placement("top"), Monitor(), triggerThickness: 6);

        Assert.Equal(6, band.Height);
    }

    [Fact]
    public void HiddenRect_MovesTheWholeSurfaceOffTheDockedEdge()
    {
        var monitor = Monitor();

        foreach (var position in new[] { "top", "bottom", "left", "right" })
        {
            var placement = Placement(position);
            var hidden = BarAutoHide.HiddenRect(placement, monitor);

            Assert.Equal(placement.Surface.Width, hidden.Width);
            Assert.Equal(placement.Surface.Height, hidden.Height);

            bool offScreen = position switch
            {
                "top" => hidden.Bottom <= monitor.Top,
                "bottom" => hidden.Y >= monitor.Top + monitor.Height,
                "left" => hidden.Right <= monitor.Left,
                _ => hidden.X >= monitor.Left + monitor.Width,
            };
            Assert.True(offScreen, $"{position} bar is still partly on screen at {hidden}");
        }
    }

    [Fact]
    public void HiddenRect_OnAnOffsetMonitor_LeavesThatMonitorOnly()
    {
        var monitor = new MonitorInfo(
            "second", 1920, 0, 2560, 1440, 1920, 0, 2560, 1400, 1.0, 1.0, IsPrimary: false);
        var placement = BarLayout.Compute(monitor, new BarSection { Position = "top", MarginEdge = 10 });

        var hidden = BarAutoHide.HiddenRect(placement, monitor);

        Assert.True(hidden.Bottom <= monitor.Top);
        Assert.Equal(placement.Body.X, hidden.X); // still aligned with its own bar
    }

    [Fact]
    public void HiddenRect_KeepsTheBarOffScreenWhateverTheEdgeMargin()
    {
        var monitor = Monitor();
        foreach (var marginEdge in new[] { 0, 10, 60 })
        {
            var hidden = BarAutoHide.HiddenRect(Placement("left", marginEdge), monitor);
            Assert.True(hidden.Right <= monitor.Left, $"marginEdge {marginEdge} left {hidden.Right} on screen");
        }
    }

    [Fact]
    public void Contains_IsInclusiveOnTheLeadingEdgesOnly()
    {
        var rect = new PxRect(100, 100, 50, 20);

        Assert.True(BarAutoHide.Contains(rect, 100, 100));
        Assert.True(BarAutoHide.Contains(rect, 149, 119));
        Assert.False(BarAutoHide.Contains(rect, 150, 120)); // exclusive right/bottom
        Assert.False(BarAutoHide.Contains(rect, 99, 110));
    }

    [Fact]
    public void BarsWithoutAutoHide_AreAlwaysShown()
    {
        Assert.True(BarAutoHide.IsShown(autoHide: false, pointerOnBar: false, pointerInTriggerBand: false, panelOpen: false));
    }

    [Fact]
    public void HiddenBar_RevealsOnlyWhenThePointerReachesTheEdge()
    {
        Assert.False(BarAutoHide.IsShown(autoHide: true, pointerOnBar: false, pointerInTriggerBand: false, panelOpen: false));
        Assert.True(BarAutoHide.IsShown(autoHide: true, pointerOnBar: false, pointerInTriggerBand: true, panelOpen: false));
    }

    [Fact]
    public void OpenPanel_KeepsTheBarVisibleEvenWithThePointerElsewhere()
    {
        Assert.True(BarAutoHide.IsShown(autoHide: true, pointerOnBar: false, pointerInTriggerBand: false, panelOpen: true));
    }

    [Fact]
    public void ShownBar_DocksBackWhenThePointerLeavesBothBarAndEdge()
    {
        var top = Placement("top");

        Assert.True(BarAutoHide.Contains(top.Surface, 900, 15)); // pointer inside the bar
        Assert.False(BarAutoHide.Contains(top.Surface, 900, 60));
        Assert.False(BarAutoHide.IsShown(autoHide: true, pointerOnBar: false, pointerInTriggerBand: false, panelOpen: false));
    }
}
