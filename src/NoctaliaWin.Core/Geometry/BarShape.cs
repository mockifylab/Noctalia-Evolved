// Faithful port of barConcaveShape() from Noctalia src/shell/bar/bar_corner_shape.h
// (MIT License, Copyright (c) 2026 noctalia-dev).
namespace NoctaliaWin.Core.Geometry;

public readonly record struct BarConcaveCorners(
    bool TopLeft = false,
    bool TopRight = false,
    bool BottomLeft = false,
    bool BottomRight = false);

public readonly record struct BarShapeInput(
    string Position,
    bool ConcaveEdgeCorners,
    int MarginEdge,
    int MarginEnds,
    int Thickness,
    int RadiusTopLeft,
    int RadiusTopRight,
    int RadiusBottomLeft,
    int RadiusBottomRight);

public readonly record struct BarConcaveShape(
    CornerShapes Corners,
    Radii Radii,
    RectInsets LogicalInset,
    float InnerBulge);

public static class BarShape
{
    /// Corners on the bar's inner edge for a given docked position.
    /// Unknown/empty positions fall through to "top", matching upstream.
    public static BarConcaveCorners InnerEdgeCorners(string position) => position switch
    {
        "bottom" => new BarConcaveCorners(TopLeft: true, TopRight: true),
        "left" => new BarConcaveCorners(TopRight: true, BottomRight: true),
        "right" => new BarConcaveCorners(TopLeft: true, BottomLeft: true),
        _ => new BarConcaveCorners(BottomLeft: true, BottomRight: true),
    };

    public static BarConcaveShape Compute(BarShapeInput cfg)
    {
        float Radius(int v) => Math.Max(0, v);
        float CappedRadius(int v) => Math.Min(cfg.Thickness * 0.5f, Radius(v));

        var radii = new Radii(
            Radius(cfg.RadiusTopLeft),
            Radius(cfg.RadiusTopRight),
            Radius(cfg.RadiusBottomRight),
            Radius(cfg.RadiusBottomLeft));

        if (!cfg.ConcaveEdgeCorners || cfg.MarginEdge > 0)
            return new BarConcaveShape(CornerShapes.AllConvex, radii, RectInsets.Zero, 0f);

        var inner = InnerEdgeCorners(cfg.Position);

        // Inner-edge concavity: full-length bar carves the corners on the edge
        // facing away from the screen.
        if (cfg.MarginEnds == 0)
        {
            var corners = new CornerShapes(
                inner.TopLeft ? CornerShape.Concave : CornerShape.Convex,
                inner.TopRight ? CornerShape.Concave : CornerShape.Convex,
                inner.BottomRight ? CornerShape.Concave : CornerShape.Convex,
                inner.BottomLeft ? CornerShape.Concave : CornerShape.Convex);

            float bulge = 0f;
            if (inner.TopLeft) bulge = Math.Max(bulge, CappedRadius(cfg.RadiusTopLeft));
            if (inner.TopRight) bulge = Math.Max(bulge, CappedRadius(cfg.RadiusTopRight));
            if (inner.BottomLeft) bulge = Math.Max(bulge, CappedRadius(cfg.RadiusBottomLeft));
            if (inner.BottomRight) bulge = Math.Max(bulge, CappedRadius(cfg.RadiusBottomRight));

            var inset = cfg.Position switch
            {
                "bottom" => RectInsets.Zero with { Top = bulge },
                "left" => RectInsets.Zero with { Right = bulge },
                "right" => RectInsets.Zero with { Left = bulge },
                _ => RectInsets.Zero with { Bottom = bulge },
            };

            return new BarConcaveShape(corners, radii, inset, bulge);
        }

        // Screen-edge concavity: the bar flares outward into its end margins.
        // Each concave corner consumes its radius of that margin.
        float endRoom = Radius(cfg.MarginEnds);
        float Carve(int v) => Math.Min(CappedRadius(v), endRoom);

        CornerShapes c;
        float tl = radii.Tl, tr = radii.Tr, br = radii.Br, bl = radii.Bl;
        float iL = 0, iT = 0, iR = 0, iB = 0;
        switch (cfg.Position)
        {
            case "bottom":
                c = new CornerShapes(Bl: CornerShape.Concave, Br: CornerShape.Concave);
                bl = iL = Carve(cfg.RadiusBottomLeft);
                br = iR = Carve(cfg.RadiusBottomRight);
                break;
            case "left":
                c = new CornerShapes(Tl: CornerShape.Concave, Bl: CornerShape.Concave);
                tl = iT = Carve(cfg.RadiusTopLeft);
                bl = iB = Carve(cfg.RadiusBottomLeft);
                break;
            case "right":
                c = new CornerShapes(Tr: CornerShape.Concave, Br: CornerShape.Concave);
                tr = iT = Carve(cfg.RadiusTopRight);
                br = iB = Carve(cfg.RadiusBottomRight);
                break;
            default: // top
                c = new CornerShapes(Tl: CornerShape.Concave, Tr: CornerShape.Concave);
                tl = iL = Carve(cfg.RadiusTopLeft);
                tr = iR = Carve(cfg.RadiusTopRight);
                break;
        }

        return new BarConcaveShape(c, new Radii(tl, tr, br, bl), new RectInsets(iL, iT, iR, iB), 0f);
    }
}
