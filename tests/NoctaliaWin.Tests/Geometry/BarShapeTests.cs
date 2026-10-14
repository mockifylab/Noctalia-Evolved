// Ported-geometry parity tests against Noctalia's barConcaveShape()
// (src/shell/bar/bar_corner_shape.h, MIT License, Copyright (c) 2026 noctalia-dev).
using NoctaliaWin.Core.Geometry;

namespace NoctaliaWin.Tests.Geometry;

public class BarShapeTests
{
    private static BarShapeInput Top(
        bool concave = true,
        int marginEdge = 0,
        int marginEnds = 0,
        int thickness = 34,
        int radius = 12) => new(
            Position: "top",
            ConcaveEdgeCorners: concave,
            MarginEdge: marginEdge,
            MarginEnds: marginEnds,
            Thickness: thickness,
            RadiusTopLeft: radius,
            RadiusTopRight: radius,
            RadiusBottomLeft: radius,
            RadiusBottomRight: radius);

    [Fact]
    public void DefaultConfig_WithEdgeMargin_IsConvexCapsule()
    {
        // Upstream example.toml ships margin_edge = 10, which disables concavity.
        var shape = BarShape.Compute(Top(marginEdge: 10, marginEnds: 180));

        Assert.False(shape.Corners.AnyConcave);
        Assert.Equal(RectInsets.Zero, shape.LogicalInset);
        Assert.Equal(0f, shape.InnerBulge);
        Assert.Equal(new Radii(12, 12, 12, 12), shape.Radii);
    }

    [Fact]
    public void ConcaveDisabled_StaysConvex()
    {
        var shape = BarShape.Compute(Top(concave: false));

        Assert.False(shape.Corners.AnyConcave);
        Assert.Equal(RectInsets.Zero, shape.LogicalInset);
    }

    [Fact]
    public void FullLengthTopBar_CarvesInnerEdgeCorners()
    {
        var shape = BarShape.Compute(Top());

        Assert.Equal(
            new CornerShapes(Br: CornerShape.Concave, Bl: CornerShape.Concave),
            shape.Corners);
        // Bulge is the largest inner-edge radius capped by half the thickness.
        Assert.Equal(12f, shape.InnerBulge);
        Assert.Equal(new RectInsets(Bottom: 12f), shape.LogicalInset);
        // Radii stay convex-shaped values; only the corner flags change.
        Assert.Equal(new Radii(12, 12, 12, 12), shape.Radii);
    }

    [Fact]
    public void FullLengthBottomBar_InsetsTopEdge()
    {
        var shape = BarShape.Compute(Top() with { Position = "bottom" });

        Assert.Equal(
            new CornerShapes(Tl: CornerShape.Concave, Tr: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(new RectInsets(Top: 12f), shape.LogicalInset);
    }

    [Fact]
    public void FullLengthLeftBar_InsetsRightEdge()
    {
        var shape = BarShape.Compute(Top() with { Position = "left" });

        Assert.Equal(
            new CornerShapes(Tr: CornerShape.Concave, Br: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(new RectInsets(Right: 12f), shape.LogicalInset);
    }

    [Fact]
    public void FullLengthRightBar_InsetsLeftEdge()
    {
        var shape = BarShape.Compute(Top() with { Position = "right" });

        Assert.Equal(
            new CornerShapes(Tl: CornerShape.Concave, Bl: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(new RectInsets(Left: 12f), shape.LogicalInset);
    }

    [Fact]
    public void BulgeIsCappedByHalfThickness()
    {
        var shape = BarShape.Compute(Top(thickness: 20, radius: 100));

        Assert.Equal(10f, shape.InnerBulge);
        Assert.Equal(new RectInsets(Bottom: 10f), shape.LogicalInset);
    }

    [Fact]
    public void ScreenEdgeFlare_TopBar_CarvesEndsFromMargins()
    {
        var shape = BarShape.Compute(Top(marginEnds: 100));

        Assert.Equal(
            new CornerShapes(Tl: CornerShape.Concave, Tr: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(0f, shape.InnerBulge);
        // Carve = min(capped radius, margin ends); 12 < 100 so the full radius applies.
        Assert.Equal(12f, shape.Radii.Tl);
        Assert.Equal(12f, shape.Radii.Tr);
        Assert.Equal(new RectInsets(Left: 12f, Right: 12f), shape.LogicalInset);
    }

    [Fact]
    public void ScreenEdgeFlare_CarveIsClampedByMarginEnds()
    {
        var shape = BarShape.Compute(Top(marginEnds: 6));

        Assert.Equal(6f, shape.Radii.Tl);
        Assert.Equal(6f, shape.Radii.Tr);
        Assert.Equal(new RectInsets(Left: 6f, Right: 6f), shape.LogicalInset);
    }

    [Fact]
    public void ScreenEdgeFlare_BottomBar_CarvesBottomCorners()
    {
        var shape = BarShape.Compute(Top(marginEnds: 100) with { Position = "bottom" });

        Assert.Equal(
            new CornerShapes(Br: CornerShape.Concave, Bl: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(new RectInsets(Left: 12f, Right: 12f), shape.LogicalInset);
    }

    [Fact]
    public void ScreenEdgeFlare_LeftBar_CarvesAlongVerticalAxis()
    {
        var shape = BarShape.Compute(Top(marginEnds: 100) with { Position = "left" });

        Assert.Equal(
            new CornerShapes(Tl: CornerShape.Concave, Bl: CornerShape.Concave),
            shape.Corners);
        Assert.Equal(new RectInsets(Top: 12f, Bottom: 12f), shape.LogicalInset);
    }

    [Fact]
    public void NegativeRadii_AreFlooredToZero()
    {
        var shape = BarShape.Compute(new BarShapeInput(
            Position: "top",
            ConcaveEdgeCorners: false,
            MarginEdge: 0,
            MarginEnds: 0,
            Thickness: 34,
            RadiusTopLeft: -5,
            RadiusTopRight: 0,
            RadiusBottomLeft: 0,
            RadiusBottomRight: 0));

        Assert.Equal(0f, shape.Radii.Tl);
    }

    [Fact]
    public void InnerEdgeCorners_UnknownPositionFallsBackToTop()
    {
        Assert.Equal(
            new BarConcaveCorners(BottomLeft: true, BottomRight: true),
            BarShape.InnerEdgeCorners("diagonal"));
    }
}
