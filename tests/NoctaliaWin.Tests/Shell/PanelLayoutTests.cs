using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Geometry;
using NoctaliaWin.Core.Shell;
using Xunit;

namespace NoctaliaWin.Tests.Shell;

public sealed class PanelLayoutTests
{
    private static MonitorInfo Monitor(double scale = 1.0) =>
        new("monitor", 0, 0, 1920, 1080, 0, 0, 1920, 1040, scale, scale, IsPrimary: true);

    private static BarPlacement Bar(
        string position = "top",
        int radius = 12,
        int marginEnds = 180,
        int marginEdge = 10,
        int panelOverlap = 1,
        double scale = 1.0)
    {
        var section = new BarSection
        {
            Position = position,
            Radius = radius,
            MarginEnds = marginEnds,
            MarginEdge = marginEdge,
            PanelOverlap = panelOverlap,
            Scale = scale,
        };
        return BarLayout.Compute(Monitor(), section);
    }

    private static PanelPlacement Compute(
        BarPlacement bar,
        double width = 595,
        double height = 400,
        PxRect? anchor = null,
        PanelSection? panels = null,
        MonitorInfo? monitor = null)
    {
        return PanelLayout.Compute(
            monitor ?? Monitor(), bar, panels ?? new PanelSection(), "control-center", width, height, anchor);
    }

    [Fact]
    public void AttachedPanelHangsBelowATopBar()
    {
        var bar = Bar();
        var panel = Compute(bar);

        Assert.Equal(AttachedRevealDirection.Down, panel.RevealDirection);
        Assert.True(panel.Visual.Y >= bar.Surface.Bottom - 2);
        Assert.True(panel.Visual.Height > 0);
    }

    [Theory]
    [InlineData("top", AttachedRevealDirection.Down)]
    [InlineData("bottom", AttachedRevealDirection.Up)]
    [InlineData("left", AttachedRevealDirection.Right)]
    [InlineData("right", AttachedRevealDirection.Left)]
    public void RevealDirectionAlwaysPointsAwayFromTheBar(string position, AttachedRevealDirection expected)
    {
        Assert.Equal(expected, Compute(Bar(position)).RevealDirection);
    }

    [Fact]
    public void PanelOverlapsTheBarEdgeByTheConfiguredAmount()
    {
        // A 1px overlap hides the seam between the two surfaces, which is why upstream
        // defaults it to 1 rather than 0.
        var none = Compute(Bar(panelOverlap: 0));
        var one = Compute(Bar(panelOverlap: 1));

        Assert.Equal(1, none.Visual.Y - one.Visual.Y);
    }

    [Fact]
    public void NestingUsesTheBarsRenderedCornerRadii()
    {
        // The total inset at each end of the attachment edge is bar radius + panel radius, so
        // an anchored panel pressed against the bar's start cannot get closer than that.
        var small = Compute(Bar(radius: 6), anchor: new PxRect(10, 0, 1, 1));
        var large = Compute(Bar(radius: 18), anchor: new PxRect(10, 0, 1, 1));

        Assert.Equal(6, small.BarRadiusStart);
        Assert.Equal(18, large.BarRadiusStart);
        Assert.Equal(180 + 6 + 12, small.Visual.X);
        Assert.Equal(180 + 18 + 12, large.Visual.X);
    }

    [Fact]
    public void ConcaveCornersSitOnTheBarSide()
    {
        var top = Compute(Bar("top"));
        Assert.Equal(CornerShape.Concave, top.Geometry.Corners.Tl);
        Assert.Equal(CornerShape.Concave, top.Geometry.Corners.Tr);
        Assert.Equal(CornerShape.Convex, top.Geometry.Corners.Br);

        var bottom = Compute(Bar("bottom"));
        Assert.Equal(CornerShape.Concave, bottom.Geometry.Corners.Bl);
        Assert.Equal(CornerShape.Convex, bottom.Geometry.Corners.Tl);
    }

    [Fact]
    public void SurfaceIsWiderThanTheBodySoTheFlareFits()
    {
        var panel = Compute(Bar());

        Assert.True(panel.Surface.Width > panel.Visual.Width);
        Assert.Equal(panel.Visual.X - panel.Surface.X, panel.Geometry.PanelInsetX);
        Assert.Equal(panel.Visual.Y - panel.Surface.Y, panel.Geometry.PanelInsetY);
    }

    [Fact]
    public void WithoutAnAnchorThePanelCentresOnTheBar()
    {
        var bar = Bar();
        var panel = Compute(bar);
        int expected = bar.Surface.X + (bar.Surface.Width - panel.Visual.Width) / 2;

        Assert.Equal(expected, panel.Visual.X);
    }

    [Fact]
    public void AnAnchorSlidesThePanelAlongTheBarButNeverPastItsEnds()
    {
        var bar = Bar();

        var anchored = Compute(bar, anchor: new PxRect(900, 0, 1, 1));
        Assert.True(anchored.Visual.X > bar.Surface.X);
        // Centre-on-anchor, within a pixel: an odd panel width makes the exact centre fall
        // on a half-pixel, and which side it lands on is not worth pinning.
        Assert.InRange(anchored.Visual.X + anchored.Visual.Width / 2, 899, 901);

        // Upstream clamps the anchored position to the bar minus the corner insets, so a
        // click near the end cannot push the panel off screen.
        var farRight = Compute(bar, anchor: new PxRect(1900, 0, 1, 1));
        Assert.True(farRight.Visual.Right <= bar.Surface.Right);

        var farLeft = Compute(bar, anchor: new PxRect(10, 0, 1, 1));
        Assert.True(farLeft.Visual.X >= bar.Surface.X);
    }

    [Fact]
    public void ScaleMultipliesEveryPanelDimension()
    {
        // The bar's zoom is applied to the panel too, otherwise the corner cutouts would not
        // match the bar's at 2x.
        var monitor = Monitor(scale: 2.0);
        var bar = BarLayout.Compute(monitor, new BarSection { Scale = 1.0 });
        var panel = PanelLayout.Compute(
            monitor, bar, new PanelSection(), "control-center", 595, 400);

        Assert.Equal(1190, panel.Visual.Width);
        Assert.Equal(800, panel.Visual.Height);
        Assert.Equal(24, (int)panel.Geometry.CornerRadius);
    }

    [Fact]
    public void UiScaleAndBarScaleComposeIntoThePanel()
    {
        var monitor = Monitor();
        var bar = BarLayout.Compute(monitor, new BarSection { Scale = 1.5 }, uiScale: 1.25);
        var panel = PanelLayout.Compute(
            monitor, bar, new PanelSection(), "control-center", 100, 100);

        Assert.Equal(188, panel.Visual.Width);
    }

    [Fact]
    public void CompactSidebarWidthIsTheDocumentedFactorOfTheConfiguredWidth()
    {
        Assert.Equal(
            700 * PanelSection.CompactSidebarWidthFactor,
            PanelLayout.ControlCenterLogicalWidth(new ControlCenterSection()),
            3);

        var custom = PanelLayout.ControlCenterLogicalWidth(new ControlCenterSection { Width = 500 });
        Assert.Equal(425, custom, 3);
    }

    [Fact]
    public void APanelTooTallForTheDisplayIsPulledBackIntoTheWorkArea()
    {
        // A short monitor with a bottom bar leaves less room than the panel needs. Keeping
        // the panel readable matters more than a perfect seam with the bar.
        var shortMonitor = new MonitorInfo(
            "tiny", 0, 0, 1280, 360, 0, 0, 1280, 320, 1.0, 1.0, IsPrimary: true);
        var bar = BarLayout.Compute(shortMonitor, new BarSection { Position = "bottom" });
        var panel = Compute(bar, height: 400, monitor: shortMonitor);

        Assert.True(panel.Visual.Y >= shortMonitor.WorkTop);
        Assert.True(panel.Surface.Y >= shortMonitor.WorkTop);
    }

    [Fact]
    public void APanelThatFitsKeepsItsSeamWithTheBar()
    {
        var bar = Bar();
        var panel = Compute(bar);

        // The default panel is far shorter than the work area, so nothing shifts it and the
        // body still starts at the bar's bottom edge minus the overlap.
        Assert.Equal(bar.Surface.Bottom - 1, panel.Visual.Y);
    }

    [Fact]
    public void VerticalBarsAttachOnTheSideTheyFace()
    {
        var left = Compute(Bar("left", marginEnds: 20, marginEdge: 10));
        var monitor = Monitor();

        Assert.Equal(AttachedRevealDirection.Right, left.RevealDirection);
        Assert.True(left.Visual.X >= monitor.WorkLeft);
        Assert.True(left.Visual.Height > 0);

        var right = Compute(Bar("right", marginEnds: 20, marginEdge: 10));
        Assert.Equal(AttachedRevealDirection.Left, right.RevealDirection);
        Assert.True(right.Visual.Right <= monitor.WorkLeft + monitor.WorkWidth);
    }

    [Fact]
    public void AttachedPanelsAreMarkedAttached()
    {
        Assert.True(Compute(Bar()).Attached);
    }

    [Fact]
    public void FloatingCardIsAPlainRoundedRectWithNoFlare()
    {
        var panel = PanelLayout.ComputeFloating(
            Monitor(), new PanelSection(), new ControlCenterSection(), "control-center", Bar());

        Assert.False(panel.Attached);
        Assert.Equal(CornerShapes.AllConvex, panel.Geometry.Corners);
        Assert.Equal(RectInsets.Zero, panel.Geometry.Insets);
        Assert.Equal(0, panel.Geometry.PanelInsetX);
        Assert.Equal(0, panel.Geometry.PanelInsetY);

        // No flare band, so the surface and the painted body are the same rect.
        Assert.Equal(panel.Surface, panel.Visual);
    }

    [Fact]
    public void FloatingCenterPositionCentresTheCardInTheWorkArea()
    {
        var monitor = Monitor();
        var panels = new PanelSection { ControlCenterPosition = "center" };
        var panel = PanelLayout.ComputeFloating(
            monitor, panels, new ControlCenterSection(), "control-center", Bar());

        Assert.Equal(monitor.WorkLeft + (monitor.WorkWidth - panel.Visual.Width) / 2, panel.Visual.X);
        Assert.Equal(monitor.WorkTop + (monitor.WorkHeight - panel.Visual.Height) / 2, panel.Visual.Y);
    }

    [Fact]
    public void FloatingAutoPositionHangsOffTheEdgeTheBarDockedOn()
    {
        var bar = Bar();
        var panel = PanelLayout.ComputeFloating(
            Monitor(), new PanelSection(), new ControlCenterSection(), "control-center", bar);

        Assert.Equal(bar.Surface.Bottom + new PanelSection().FloatingOffset, panel.Visual.Y);

        var bottomBar = Bar("bottom");
        var above = PanelLayout.ComputeFloating(
            Monitor(), new PanelSection(), new ControlCenterSection(), "control-center", bottomBar);
        Assert.Equal(above.Visual.Bottom + new PanelSection().FloatingOffset, bottomBar.Surface.Y);
    }

    [Fact]
    public void FloatingCardIsClampedIntoTheWorkArea()
    {
        // A card wider than the display starts at the work area's left edge rather than at a
        // negative x the user cannot reach, and the axis that does fit stays inside.
        var cramped = new MonitorInfo(
            "tiny", 0, 0, 800, 600, 0, 0, 800, 560, 1.0, 1.0, IsPrimary: true);
        var bar = BarLayout.Compute(cramped, new BarSection { Position = "left", MarginEdge = 10 });
        var panel = PanelLayout.ComputeFloating(
            cramped, new PanelSection(), new ControlCenterSection { Width = 1400 }, "control-center", bar);

        Assert.Equal(cramped.WorkLeft, panel.Visual.X);
        Assert.True(panel.Visual.Y >= cramped.WorkTop);
        Assert.True(panel.Visual.Bottom <= cramped.WorkTop + cramped.WorkHeight);
    }

    [Fact]
    public void FloatingCalendarUsesTheCalendarSizeNotTheControlCenterSize()
    {
        var panel = PanelLayout.ComputeFloating(
            Monitor(), new PanelSection(), new ControlCenterSection(), "calendar", Bar());

        Assert.Equal((int)PanelLayout.CalendarLogicalWidth, panel.Visual.Width);
        Assert.Equal((int)PanelLayout.CalendarLogicalHeight, panel.Visual.Height);
        Assert.Equal("calendar", panel.PanelId);
    }
}
