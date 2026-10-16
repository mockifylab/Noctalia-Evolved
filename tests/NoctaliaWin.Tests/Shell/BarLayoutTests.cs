using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Geometry;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Shell;

public sealed class BarLayoutTests
{
    private static MonitorInfo Monitor(int workWidth = 1920, int workHeight = 1040, double scale = 1.0) =>
        new("monitor", 0, 0, workWidth, workHeight + 40, 0, 0, workWidth, workHeight, scale, scale, IsPrimary: true);


    [Fact]
    public void TopBar_WithMargins_IsAConvexCapsuleInsideTheWorkArea()
    {
        var bar = new BarSection(); // top, thickness 34, marginEnds 180, marginEdge 10
        var placement = BarLayout.Compute(Monitor(), bar);

        Assert.Equal(new PxRect(180, 10, 1560, 34), placement.Body);
        Assert.Equal(placement.Body, placement.Surface); // convex capsule: no bleed
        Assert.Equal(CornerShapes.AllConvex, placement.Shape.Corners);
    }

    [Fact]
    public void FullLengthTopBar_CarvesItsInnerEdgeAndBleedsDownward()
    {
        var bar = new BarSection { MarginEdge = 0, MarginEnds = 0, Radius = 12 };
        var placement = BarLayout.Compute(Monitor(), bar);

        Assert.Equal(new PxRect(0, 0, 1920, 34), placement.Body);

        // The edge away from the screen docks concave, with a bulge of min(radius, thickness/2).
        Assert.Equal(CornerShape.Concave, placement.Shape.Corners.Bl);
        Assert.Equal(CornerShape.Concave, placement.Shape.Corners.Br);
        Assert.Equal(CornerShape.Convex, placement.Shape.Corners.Tl);
        Assert.Equal(12d, placement.Shape.LogicalInset.Bottom);
        Assert.Equal(new PxRect(0, 0, 1920, 46), placement.Surface);
    }

    [Fact]
    public void EdgeDockedBarWithEndMargins_FlaresIntoThoseMargins()
    {
        var bar = new BarSection { MarginEdge = 0, MarginEnds = 180, Radius = 12 };
        var placement = BarLayout.Compute(Monitor(), bar);

        Assert.Equal(new PxRect(180, 0, 1560, 34), placement.Body);
        Assert.Equal(CornerShape.Concave, placement.Shape.Corners.Tl);
        Assert.Equal(CornerShape.Concave, placement.Shape.Corners.Tr);
        Assert.Equal(12d, placement.Shape.LogicalInset.Left);
        Assert.Equal(12d, placement.Shape.LogicalInset.Right);

        // The window grows by the flare on both ends so the arcs are not clipped.
        Assert.Equal(new PxRect(168, 0, 1584, 34), placement.Surface);
    }

    [Fact]
    public void FlareIsCappedByTheAvailableEndMargin()
    {
        var bar = new BarSection { MarginEdge = 0, MarginEnds = 4, Radius = 12 };
        var placement = BarLayout.Compute(Monitor(), bar);

        // Only 4px of margin exists, so the carve can never exceed it.
        Assert.Equal(4d, placement.Shape.LogicalInset.Left);
        Assert.Equal(new PxRect(0, 0, 1920, 34), placement.Surface);
    }

    [Fact]
    public void ConcaveEdgeCornersOff_FallsBackToPlainConvexRadii()
    {
        var bar = new BarSection { MarginEdge = 0, MarginEnds = 0, ConcaveEdgeCorners = false };
        var placement = BarLayout.Compute(Monitor(), bar);

        Assert.Equal(CornerShapes.AllConvex, placement.Shape.Corners);
        Assert.Equal(0d, placement.Shape.LogicalInset.Bottom);
        Assert.Equal(placement.Body, placement.Surface);
    }

    [Fact]
    public void BottomBar_MirrorsTheTopBarAgainstTheWorkAreaFloor()
    {
        var bar = new BarSection { Position = "bottom", MarginEdge = 10, MarginEnds = 180 };
        var placement = BarLayout.Compute(Monitor(workHeight: 1040), bar);

        Assert.Equal(new PxRect(180, 996, 1560, 34), placement.Body); // 1040 - 34 - 10
        Assert.Equal(CornerShape.Convex, placement.Shape.Corners.Bl);
    }

    [Fact]
    public void SideBars_UseThicknessForWidthAndEndMarginsVertically()
    {
        var left = BarLayout.Compute(Monitor(), new BarSection { Position = "left", MarginEdge = 10, MarginEnds = 180 });
        var right = BarLayout.Compute(Monitor(), new BarSection { Position = "right", MarginEdge = 10, MarginEnds = 180 });

        Assert.Equal(new PxRect(10, 180, 34, 680), left.Body);
        Assert.Equal(new PxRect(1876, 180, 34, 680), right.Body); // 1920 - 34 - 10
    }

    [Fact]
    public void OffsetMonitor_KeepsTheBarInsideItsOwnWorkArea()
    {
        var monitor = new MonitorInfo(
            "second", 1920, 0, 2560, 1440, 1920, 0, 2560, 1400, 1.0, 1.0, IsPrimary: false);

        var placement = BarLayout.Compute(monitor, new BarSection { MarginEdge = 10, MarginEnds = 180 });

        Assert.Equal(new PxRect(2100, 10, 2200, 34), placement.Body);
    }

    [Fact]
    public void ScalingMultipliesEveryGeometryInput()
    {
        var bar = new BarSection
        {
            MarginEdge = 10,
            MarginEnds = 100,
            Thickness = 40,
            Radius = 12,
            Padding = 14,
            WidgetSpacing = 6,
            Scale = 2,
        };
        var placement = BarLayout.Compute(Monitor(scale: 1.5), bar);

        // effective factor 1.5 * 2 = 3: ends 300, edge 30, thickness 120.
        Assert.Equal(new PxRect(300, 30, 1320, 120), placement.Body);
        Assert.Equal(42, placement.Padding);
        Assert.Equal(18, placement.WidgetSpacing);
        Assert.Equal(3.0, placement.ScaleX);
    }

    [Fact]
    public void MarginsWiderThanTheMonitor_StillProduceAPositiveRect()
    {
        var placement = BarLayout.Compute(
            Monitor(workWidth: 1000), new BarSection { MarginEnds = 2000, MarginEdge = 0 });

        Assert.Equal(1, placement.Body.Width);
        Assert.True(placement.Surface.Width >= 1);
    }

    [Fact]
    public void UnknownPosition_IsTreatedAsTopLikeUpstream()
    {
        var placement = BarLayout.Compute(
            Monitor(), new BarSection { Position = "diagonal", MarginEdge = 10, MarginEnds = 180 });

        Assert.Equal("top", placement.Position);
        Assert.Equal(new PxRect(180, 10, 1560, 34), placement.Body);
    }

    [Fact]
    public void NonVisualOptions_AreCarriedThroughForTheRenderer()
    {
        var bar = new BarSection
        {
            BackgroundOpacity = 0.7,
            Shadow = false,
            AutoHide = true,
            Layer = "overlay",
            FontScale = 1.2,
        };
        var placement = BarLayout.Compute(Monitor(), bar);

        Assert.Equal(0.7, placement.BackgroundOpacity);
        Assert.False(placement.Shadow);
        Assert.True(placement.AutoHide);
        Assert.Equal("overlay", placement.Layer);
        Assert.Equal(1.2, placement.FontScale);
    }

    [Fact]
    public void DefaultSlots_SkipUnimplementedWidgetsAndReportThem()
    {
        var slots = BarLayout.ResolveSlots(new BarSection());

        Assert.Equal("launcher", slots.Start[0]);
        Assert.Equal("clock", Assert.Single(slots.Center));
        Assert.Equal("session", slots.End[^1]);

        // Defaults name wallpaper/workspaces, and clipboard/bluetooth/brightness, which must
        // be reported rather than silently dropped.
        Assert.DoesNotContain("wallpaper", slots.Start);
        Assert.DoesNotContain("workspaces", slots.Start);
        Assert.Contains("wallpaper", slots.Skipped);
        // Media has a Windows backing now, so the default lane keeps its slot.
        Assert.Contains("media", slots.End);
        Assert.DoesNotContain("media", slots.Skipped);
        Assert.Contains("bluetooth", slots.Skipped);
        // No documented API exists for another app's status icons or toast content, so
        // those names are reported as skipped instead of rendering an empty tile.
        Assert.DoesNotContain("notifications", slots.End);
        Assert.Contains("notifications", slots.Skipped);
        Assert.Contains("tray", slots.Skipped);
        Assert.Contains("volume", slots.End);
    }

    [Fact]
    public void WidgetLists_AreCaseInsensitiveAndOrderPreserving()
    {
        var bar = new BarSection { Start = ["CLOCK", "spacer", "Launcher"], Center = [], End = [] };
        var slots = BarLayout.ResolveSlots(bar);

        Assert.Equal(new[] { "CLOCK", "spacer", "Launcher" }, slots.Start);
        Assert.Empty(slots.Skipped);
    }

    [Fact]
    public void TextWidgets_AreDeferredWithTheOtherUnbackedNames()
    {
        // "text" needs a per-widget value the config schema does not carry yet, so it
        // would render as an empty label; it is skipped instead.
        var slots = BarLayout.ResolveSlots(new BarSection { Center = ["text"] });

        Assert.Empty(slots.Center);
        Assert.Contains("text", slots.Skipped);
    }

    [Fact]
    public void UiScale_MultipliesDeviceScaleIntoEveryPixelSize()
    {
        var monitor = Monitor();
        var bar = new BarSection { MarginEnds = 0, MarginEdge = 0, Thickness = 34, Radius = 12 };

        var plain = BarLayout.Compute(monitor, bar);
        var scaled = BarLayout.Compute(monitor, bar, uiScale: 1.5);

        // The renderer divides by the device scale only, so the zoom survives as DIPs.
        Assert.Equal(34, plain.Body.Height);
        Assert.Equal(51, scaled.Body.Height);
        Assert.Equal(1.5, scaled.ScaleX / plain.ScaleX);
        Assert.Equal(21, scaled.Padding); // 14 * 1.5
        Assert.Equal(9, scaled.WidgetSpacing); // 6 * 1.5
    }

    [Fact]
    public void UiScale_LiftsTheBarByItsOwnGrowth()
    {
        var bar = new BarSection { MarginEnds = 0, MarginEdge = 10, Thickness = 34 };

        var scaled = BarLayout.Compute(Monitor(), bar, uiScale: 2.0);

        // 10 * 2 = 20 px edge margin, and the bar body still starts below it.
        Assert.Equal(20, scaled.Body.Y);
        Assert.Equal(68, scaled.Body.Height);
    }
}
