// Surface outline generation matching Noctalia's rect shader silhouette:
// per-corner Convex/Concave quarter arcs with RectInsets, replicated from the
// boundary rules in src/render/programs/rect_program.cpp (MIT License,
// Copyright (c) 2026 noctalia-dev).
namespace NoctaliaWin.Core.Geometry;

public readonly record struct Pt(float X, float Y);

/// A closed-outline segment: straight line when Center is null, otherwise a
/// quarter arc of radius dist(From,Center) == dist(To,Center).
public readonly record struct OutlineSegment(Pt From, Pt To, Pt? Center, bool IsConcave)
{
    public bool IsArc => Center.HasValue;

    /// Sweep direction for WPF-style arc construction: positive cross product
    /// (v1 × v2 of start/end relative to center) means clockwise in y-down coords.
    public bool Clockwise =>
        Center is { } c &&
        (From.X - c.X) * (To.Y - c.Y) - (From.Y - c.Y) * (To.X - c.X) > 0;
}

public static class ShapeOutline
{
    /// Builds the clockwise outline (y-down, logical/surface px) of a body rect
    /// inset within the surface, with per-corner convex or concave radii.
    /// Concave corners flare outward into the inset band exactly like the shader:
    /// two quarter arcs plus the outer strip, which may extend past the body edge.
    public static IReadOnlyList<OutlineSegment> Build(
        float surfaceWidth,
        float surfaceHeight,
        Radii radii,
        CornerShapes corners,
        RectInsets insets)
    {
        var safeInset = new RectInsets(
            Math.Max(0, insets.Left), Math.Max(0, insets.Top),
            Math.Max(0, insets.Right), Math.Max(0, insets.Bottom));

        var l = Math.Min(safeInset.Left, surfaceWidth);
        var t = Math.Min(safeInset.Top, surfaceHeight);
        var r = Math.Max(l, surfaceWidth - safeInset.Right);
        var b = Math.Max(t, surfaceHeight - safeInset.Bottom);

        float maxRadius = Math.Max(Math.Min(r - l, b - t) * 0.5f, 0f);
        var rad = radii.Clamped(maxRadius);

        var segs = new List<OutlineSegment>();
        // Figure starts at the tl corner's entry point on the left edge.
        var tlEntry = new Pt(l, t + rad.Tl);
        var start = TlCorner(segs, l, t, r, b, rad.Tl, corners.Tl);

        // Top edge to tr.
        var trEntry = new Pt(r - rad.Tr, t);
        if (corners.Tr == CornerShape.Concave)
            trEntry = new Pt(r - rad.Tr, t);
        LineTo(segs, start, trEntry);
        var trExit = TrCorner(segs, l, t, r, b, rad.Tr, corners.Tr);

        // Right edge to br.
        LineTo(segs, trExit, new Pt(r, b - rad.Br));
        var brExit = BrCorner(segs, l, t, r, b, rad.Br, corners.Br);

        // Bottom edge to bl.
        LineTo(segs, brExit, new Pt(l + rad.Bl, b));
        var blExit = BlCorner(segs, l, t, r, b, rad.Bl, corners.Bl);

        // Left edge back to the tl entry point.
        LineTo(segs, blExit, tlEntry);
        return segs;
    }

    private static void LineTo(List<OutlineSegment> segs, Pt from, Pt to)
    {
        if (from != to)
            segs.Add(new OutlineSegment(from, to, null, false));
    }

    // Each corner function appends its clockwise segments and returns the exit point.

    private static Pt TlCorner(List<OutlineSegment> s, float l, float t, float r, float b, float radius, CornerShape shape)
    {
        if (radius <= 0f)
            return new Pt(l, t);

        if (shape == CornerShape.Convex)
        {
            s.Add(new OutlineSegment(new Pt(l, t + radius), new Pt(l + radius, t), new Pt(l + radius, t + radius), false));
            return new Pt(l + radius, t);
        }

        var p0 = new Pt(l, t + radius);
        var p1 = new Pt(l - radius, t);
        var p2 = new Pt(l - radius, t - radius);
        var p3 = new Pt(l, t - radius);
        var exit = new Pt(l + radius, t);
        s.Add(new OutlineSegment(p0, p1, new Pt(l - radius, t + radius), true));
        s.Add(new OutlineSegment(p1, p2, null, true));
        s.Add(new OutlineSegment(p2, p3, null, true));
        s.Add(new OutlineSegment(p3, exit, new Pt(l + radius, t - radius), true));
        return exit;
    }

    private static Pt TrCorner(List<OutlineSegment> s, float l, float t, float r, float b, float radius, CornerShape shape)
    {
        if (radius <= 0f)
            return new Pt(r, t);

        if (shape == CornerShape.Convex)
        {
            s.Add(new OutlineSegment(new Pt(r - radius, t), new Pt(r, t + radius), new Pt(r - radius, t + radius), false));
            return new Pt(r, t + radius);
        }

        var p0 = new Pt(r - radius, t);
        var p1 = new Pt(r, t - radius);
        var p2 = new Pt(r + radius, t - radius);
        var p3 = new Pt(r + radius, t);
        var exit = new Pt(r, t + radius);
        s.Add(new OutlineSegment(p0, p1, new Pt(r - radius, t - radius), true));
        s.Add(new OutlineSegment(p1, p2, null, true));
        s.Add(new OutlineSegment(p2, p3, null, true));
        s.Add(new OutlineSegment(p3, exit, new Pt(r + radius, t + radius), true));
        return exit;
    }

    private static Pt BrCorner(List<OutlineSegment> s, float l, float t, float r, float b, float radius, CornerShape shape)
    {
        if (radius <= 0f)
            return new Pt(r, b);

        if (shape == CornerShape.Convex)
        {
            s.Add(new OutlineSegment(new Pt(r, b - radius), new Pt(r - radius, b), new Pt(r - radius, b - radius), false));
            return new Pt(r - radius, b);
        }

        var p0 = new Pt(r, b - radius);
        var p1 = new Pt(r + radius, b);
        var p2 = new Pt(r + radius, b + radius);
        var p3 = new Pt(r, b + radius);
        var exit = new Pt(r - radius, b);
        s.Add(new OutlineSegment(p0, p1, new Pt(r + radius, b - radius), true));
        s.Add(new OutlineSegment(p1, p2, null, true));
        s.Add(new OutlineSegment(p2, p3, null, true));
        s.Add(new OutlineSegment(p3, exit, new Pt(r - radius, b + radius), true));
        return exit;
    }

    private static Pt BlCorner(List<OutlineSegment> s, float l, float t, float r, float b, float radius, CornerShape shape)
    {
        if (radius <= 0f)
            return new Pt(l, b);

        if (shape == CornerShape.Convex)
        {
            s.Add(new OutlineSegment(new Pt(l + radius, b), new Pt(l, b - radius), new Pt(l + radius, b - radius), false));
            return new Pt(l, b - radius);
        }

        var p0 = new Pt(l + radius, b);
        var p1 = new Pt(l, b + radius);
        var p2 = new Pt(l - radius, b + radius);
        var p3 = new Pt(l - radius, b);
        var exit = new Pt(l, b - radius);
        s.Add(new OutlineSegment(p0, p1, new Pt(l + radius, b + radius), true));
        s.Add(new OutlineSegment(p1, p2, null, true));
        s.Add(new OutlineSegment(p2, p3, null, true));
        s.Add(new OutlineSegment(p3, exit, new Pt(l - radius, b - radius), true));
        return exit;
    }
}
