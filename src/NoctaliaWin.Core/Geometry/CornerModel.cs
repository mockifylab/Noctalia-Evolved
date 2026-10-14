// Ported corner/radii/insets model from Noctalia src/render/core/render_styles.h
// (MIT License, Copyright (c) 2026 noctalia-dev).
namespace NoctaliaWin.Core.Geometry;

public enum CornerShape
{
    Convex,
    Concave,
}

public readonly record struct CornerShapes(
    CornerShape Tl = CornerShape.Convex,
    CornerShape Tr = CornerShape.Convex,
    CornerShape Br = CornerShape.Convex,
    CornerShape Bl = CornerShape.Convex)
{
    public static readonly CornerShapes AllConvex = new();

    public bool AnyConcave =>
        Tl == CornerShape.Concave || Tr == CornerShape.Concave
        || Br == CornerShape.Concave || Bl == CornerShape.Concave;
}

/// Per-corner radii: top-left, top-right, bottom-right, bottom-left.
public readonly record struct Radii(float Tl, float Tr, float Br, float Bl)
{
    public Radii(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public static readonly Radii Zero = new(0f);

    public float this[int cornerIndex] => cornerIndex switch
    {
        0 => Tl,
        1 => Tr,
        2 => Br,
        3 => Bl,
        _ => throw new IndexOutOfRangeException(),
    };

    public Radii Clamped(float maxRadius) =>
        new(Math.Max(0, Math.Min(Tl, maxRadius)),
            Math.Max(0, Math.Min(Tr, maxRadius)),
            Math.Max(0, Math.Min(Br, maxRadius)),
            Math.Max(0, Math.Min(Bl, maxRadius)));
}

/// Left, top, right, bottom insets (logical px from surface edge to body rect).
public readonly record struct RectInsets(float Left = 0f, float Top = 0f, float Right = 0f, float Bottom = 0f)
{
    public static readonly RectInsets Zero = new();
}
