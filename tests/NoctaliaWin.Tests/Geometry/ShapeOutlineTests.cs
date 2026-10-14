// Outline tests pin the silhouette derived from Noctalia's rect SDF shader
// (src/render/programs/rect_program.cpp, MIT License, Copyright (c) 2026 noctalia-dev).
using NoctaliaWin.Core.Geometry;

namespace NoctaliaWin.Tests.Geometry;

public class ShapeOutlineTests
{
    private const float Tolerance = 1e-4f;

    private static void AssertPt(Pt actual, float x, float y)
    {
        Assert.Equal(x, actual.X, Tolerance);
        Assert.Equal(y, actual.Y, Tolerance);
    }

    [Fact]
    public void ConvexRect_HasFourArcsAndCloses()
    {
        var segs = ShapeOutline.Build(100, 50, new Radii(12f), CornerShapes.AllConvex, RectInsets.Zero);

        Assert.Equal(4, segs.Count(s => s.IsArc));
        Assert.Equal(4, segs.Count(s => !s.IsArc));
        Assert.Equal(segs[0].From, segs[^1].To);
    }

    [Fact]
    public void ConvexTopLeft_ArcSweepsFromLeftEdgeToTopEdge()
    {
        var segs = ShapeOutline.Build(100, 50, new Radii(12f), CornerShapes.AllConvex, RectInsets.Zero);

        // Entry point on the left edge, exit on the top edge, centre inside the body.
        AssertPt(segs[0].From, 0, 12);
        AssertPt(segs[0].To, 12, 0);
        Assert.NotNull(segs[0].Center);
        AssertPt(segs[0].Center!.Value, 12, 12);
        Assert.False(segs[0].IsConcave);
    }

    [Fact]
    public void SegmentsAreContiguous()
    {
        var segs = ShapeOutline.Build(
            200, 60,
            new Radii(12, 8, 6, 4),
            new CornerShapes(Tr: CornerShape.Concave),
            new RectInsets(12, 0, 12, 12));

        for (int i = 0; i < segs.Count; i++)
        {
            var next = segs[(i + 1) % segs.Count];
            Assert.Equal(segs[i].To, next.From);
        }
    }

    [Fact]
    public void ConcaveTopLeft_FlaresOutsideTheBody()
    {
        // Concave corners are the bar/panel notch: the outline must leave the body rect.
        var segs = ShapeOutline.Build(
            100, 50, new Radii(12f), new CornerShapes(Tl: CornerShape.Concave), RectInsets.Zero);

        // A concave corner contributes two arcs (the notch + the flare) and two lines.
        Assert.Equal(2, segs.Count(s => s.IsArc && s.IsConcave));

        // Arc: (0,12) -> (-12,0) with centre (-12,12), then the outer strip up to y = -12.
        var first = segs[0];
        Assert.True(first.IsArc);
        AssertPt(first.From, 0, 12);
        AssertPt(first.To, -12, 0);
        AssertPt(first.Center!.Value, -12, 12);
        Assert.True(first.IsConcave);

        var minX = segs.Min(s => Math.Min(s.From.X, s.To.X));
        var minY = segs.Min(s => Math.Min(s.From.Y, s.To.Y));
        Assert.Equal(-12f, minX, Tolerance);
        Assert.Equal(-12f, minY, Tolerance);
    }

    [Fact]
    public void ConcaveTopRight_MirrorsTheFlare()
    {
        var segs = ShapeOutline.Build(
            100, 50, new Radii(12f), new CornerShapes(Tr: CornerShape.Concave), RectInsets.Zero);

        // tl/br/bl stay convex (3 arcs) and the concave tr corner adds 2 more.
        Assert.Equal(5, segs.Count(s => s.IsArc));
        Assert.Equal(2, segs.Count(s => s.IsArc && s.IsConcave));
        Assert.Equal(112f, segs.Max(s => Math.Max(s.From.X, s.To.X)), Tolerance);
        Assert.Equal(-12f, segs.Min(s => Math.Min(s.From.Y, s.To.Y)), Tolerance);
    }

    [Fact]
    public void ZeroRadii_ProduceAPlainRectangle()
    {
        var segs = ShapeOutline.Build(80, 40, Radii.Zero, CornerShapes.AllConvex, RectInsets.Zero);

        Assert.All(segs, s => Assert.False(s.IsArc));
        Assert.Equal(4, segs.Count);
        Assert.Contains(segs, s => s.From == new Pt(0, 0) && s.To == new Pt(80, 0));
        Assert.Contains(segs, s => s.From == new Pt(80, 40) && s.To == new Pt(0, 40));
    }

    [Fact]
    public void Insets_OffsetTheBodyInsideTheSurface()
    {
        // Attached-panel surfaces wrap the body in a flare band (left/right insets).
        var segs = ShapeOutline.Build(
            328, 402, new Radii(12f), CornerShapes.AllConvex, new RectInsets(14, 0, 14, 0));

        var minX = segs.Min(s => Math.Min(s.From.X, s.To.X));
        var maxX = segs.Max(s => Math.Max(s.From.X, s.To.X));
        Assert.Equal(14f, minX, Tolerance);
        Assert.Equal(328f - 14f, maxX, Tolerance);
    }

    [Fact]
    public void Radii_AreClampedToHalfTheSmallestDimension()
    {
        var segs = ShapeOutline.Build(40, 20, new Radii(100f), CornerShapes.AllConvex, RectInsets.Zero);

        // maxRadius = min(40,20)/2 = 10
        AssertPt(segs[0].From, 0, 10);
        AssertPt(segs[0].To, 10, 0);
    }

    [Fact]
    public void Arcs_ReportClockwiseSweepForYDownCoordinates()
    {
        var segs = ShapeOutline.Build(100, 50, new Radii(12f), CornerShapes.AllConvex, RectInsets.Zero);
        var tl = segs[0];

        // (0,12) -> (12,0) around (12,12): clockwise in y-down space.
        Assert.True(tl.Clockwise);
    }
}
