// Placement parity tests for attached (bar-triggered) panels, ported from
// Noctalia src/shell/panel/panel_manager.cpp (MIT License, noctalia-dev).
using NoctaliaWin.Core.Geometry;

namespace NoctaliaWin.Tests.Geometry;

public class AttachedPanelPlacementTests
{
    private static (int Left, int Top, int Right, int Bottom) TopBar => (0, 0, 1920, 34);
    private static (int Left, int Top, int Right, int Bottom) BottomBar => (0, 1046, 1920, 1080);
    private static (int Left, int Top, int Right, int Bottom) LeftBar => (0, 0, 34, 1080);

    private static AttachedPanelGeometry Place(
        (int Left, int Top, int Right, int Bottom) bar,
        string position,
        double anchorX = 0,
        double anchorY = 0,
        bool useAnchor = true,
        int panelWidth = 300,
        int panelHeight = 400,
        float radius = 12,
        int barRadiusStart = 12,
        int barRadiusEnd = 12,
        int overlap = 1) =>
        AttachedPanelPlacement.Compute(
            bar, position, barRadiusStart, barRadiusEnd,
            panelWidth, panelHeight, radius, overlap, useAnchor, anchorX, anchorY);

    [Fact]
    public void TopBar_AnchoredPanel_NestsAgainstBarEndRadii()
    {
        var g = Place(TopBar, "top", anchorX: 100);

        // totalStartInset = ceil(barRadiusStart + panelRadius) = 24 -> the anchor at x=100
        // would put the panel at -50, so it clamps to the bar's start inset.
        Assert.Equal(24, g.VisualX);
        Assert.Equal(33, g.VisualY);          // barBottom - panelOverlap
        Assert.Equal(10, g.SurfaceX);         // visualX - crossOutset (12 + 2)
        Assert.Equal(33, g.SurfaceY);
        Assert.Equal(328, g.SurfaceWidth);    // panel + 2 * crossOutset
        Assert.Equal(402, g.SurfaceHeight);   // panel + bleed away from the bar
        Assert.Equal(14, g.PanelInsetX);
        Assert.Equal(0, g.PanelInsetY);
    }

    [Fact]
    public void TopBar_ClampsPanelInsideTheBarEnds()
    {
        var g = Place(TopBar, "top", anchorX: 1900);

        // maxX = barRight - panelWidth - totalEndInset = 1920 - 300 - 24
        Assert.Equal(1596, g.VisualX);
    }

    [Fact]
    public void TopBar_WithoutAnchor_CentersOnTheBar()
    {
        var g = Place(TopBar, "top", useAnchor: false);

        Assert.Equal((1920 - 300) / 2, g.VisualX);
        Assert.Equal(AttachedRevealDirection.Down, g.RevealDirection);
    }

    [Fact]
    public void TopBar_CornerRadiusZero_ShrinksOutsetToOneSide()
    {
        var g = Place(TopBar, "top", anchorX: 500, radius: 0);

        Assert.Equal(500 - 150, g.VisualX);
        // cornerOutset = 0, crossOutset = FlareSafetyPadding
        Assert.Equal(2, g.PanelInsetX);
        Assert.Equal(304, g.SurfaceWidth);
    }

    [Fact]
    public void BottomBar_RevealsUpwards()
    {
        var g = Place(BottomBar, "bottom", useAnchor: false);

        Assert.Equal(AttachedRevealDirection.Up, g.RevealDirection);
        Assert.Equal(647, g.VisualY);         // barTop - panelHeight + overlap
        Assert.Equal(645, g.SurfaceY);        // minus mainBleedAway
        Assert.Equal(402, g.SurfaceHeight);
        Assert.Equal(328, g.SurfaceWidth);
        Assert.Equal(14, g.PanelInsetX);      // crossOutset wraps the flare
        Assert.Equal(2, g.PanelInsetY);
    }

    [Fact]
    public void LeftBar_AttachesToTheBarsRightEdge()
    {
        var g = Place(LeftBar, "left", anchorY: 200);

        Assert.Equal(AttachedRevealDirection.Right, g.RevealDirection);
        Assert.Equal(33, g.VisualX);          // barRight - overlap
        Assert.Equal(24, g.VisualY);          // clamped to barTop + totalStartInset
        Assert.Equal(0, g.PanelInsetX);
        Assert.Equal(14, g.PanelInsetY);
        Assert.Equal(302, g.SurfaceWidth);
        Assert.Equal(428, g.SurfaceHeight);
    }

    [Fact]
    public void RightBar_CornersAreOnItsLeftSide()
    {
        var g = Place(LeftBar, "right", anchorY: 500);

        Assert.Equal(AttachedRevealDirection.Left, g.RevealDirection);
        Assert.Equal(-299, g.VisualX);        // barLeft - panelWidth + overlap
    }

    [Theory]
    [InlineData("top", CornerShape.Concave, CornerShape.Concave, CornerShape.Convex, CornerShape.Convex)]
    [InlineData("bottom", CornerShape.Convex, CornerShape.Convex, CornerShape.Concave, CornerShape.Concave)]
    [InlineData("left", CornerShape.Concave, CornerShape.Convex, CornerShape.Convex, CornerShape.Concave)]
    [InlineData("right", CornerShape.Convex, CornerShape.Concave, CornerShape.Concave, CornerShape.Convex)]
    public void BarSideCorners_AreConcaveForEachPosition(
        string position, CornerShape tl, CornerShape tr, CornerShape br, CornerShape bl)
    {
        Assert.Equal(
            new CornerShapes(tl, tr, br, bl),
            AttachedPanelPlacement.CornerShapesFor(position));
    }

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void HorizontalBars_InsetsWrapTheCrossAxisLeftRight(string position)
    {
        Assert.Equal(new RectInsets(9, 0, 9, 0), AttachedPanelPlacement.InsetsFor(position, 9));
    }

    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    public void VerticalBars_InsetsWrapTheCrossAxisTopBottom(string position)
    {
        Assert.Equal(new RectInsets(0, 9, 0, 9), AttachedPanelPlacement.InsetsFor(position, 9));
    }

    [Fact]
    public void Radii_AreAssignedToTheAwaySideCorners()
    {
        // Upstream keeps the bar-side radius separate (it animates with reveal
        // progress), so static radii land on the away-side convex corners.
        Assert.Equal(new Radii(0, 0, 12, 12), AttachedPanelPlacement.RadiiFor("bottom", 12));
        Assert.Equal(new Radii(12, 12, 0, 0), AttachedPanelPlacement.RadiiFor("top", 12));
        Assert.Equal(new Radii(0, 12, 12, 0), AttachedPanelPlacement.RadiiFor("left", 12));
        Assert.Equal(new Radii(12, 0, 0, 12), AttachedPanelPlacement.RadiiFor("right", 12));
    }

    [Fact]
    public void NegativeBarRadii_AreFloored()
    {
        var g = Place(TopBar, "top", anchorX: 960, barRadiusStart: -20, barRadiusEnd: -20);

        // totalStart/EndInset = ceil(0 + 12) = 12
        Assert.Equal(960 - 150, g.VisualX);
        Assert.Equal(12f, g.Insets.Left);
    }

    [Fact]
    public void OversizedPanel_StillProducesPositiveSurface()
    {
        var g = Place(TopBar, "top", useAnchor: false, panelWidth: 4000, panelHeight: 2000);

        Assert.True(g.SurfaceWidth >= 1);
        Assert.True(g.SurfaceHeight >= 1);
    }
}
