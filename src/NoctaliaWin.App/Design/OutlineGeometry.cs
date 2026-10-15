// Core's outline segments -> a WPF geometry, shared by the bar and the panels.
//
// Both surfaces are the same thing: a body rect with per-corner convex or concave radii,
// inset inside a larger surface. Only the scale factors differ, so the arc construction lives
// here once instead of being copied per window.
using System.Windows;
using System.Windows.Media;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Geometry;
using Path = System.Windows.Shapes.Path;

namespace NoctaliaWin.App.Design;

internal static class OutlineGeometry
{
    /// Points outside the surface are kept on purpose: the window boundary clips them, which
    /// is what makes a concave flare against a screen edge look correct.
    public static Geometry Build(
        double surfaceWidth,
        double surfaceHeight,
        Radii radii,
        CornerShapes corners,
        RectInsets insets,
        double scaleX,
        double scaleY)
    {
        var segments = ShapeOutline.Build(
            (float)surfaceWidth, (float)surfaceHeight, radii, corners, insets);

        if (segments.Count == 0)
            return Geometry.Empty;

        double sx = Math.Max(scaleX, 0.01);
        double sy = Math.Max(scaleY, 0.01);
        Point At(Pt p) => new(p.X / sx, p.Y / sy);

        var figure = new PathFigure { IsClosed = true, IsFilled = true, StartPoint = At(segments[0].From) };
        foreach (var segment in segments)
        {
            if (segment.Center is not { } center)
            {
                figure.Segments.Add(new LineSegment(At(segment.To), true));
                continue;
            }

            // A quarter arc: the radius is the distance to the centre, which is the same for
            // both endpoints. Each axis divides by its own scale, so a non-square DPI setting
            // makes the arc elliptical instead of distorting the surface.
            double radius = MathF.Sqrt(
                MathF.Pow(segment.From.X - center.X, 2) + MathF.Pow(segment.From.Y - center.Y, 2));

            figure.Segments.Add(new ArcSegment(
                At(segment.To),
                new Size(radius / sx, radius / sy),
                rotationAngle: 0,
                isLargeArc: false,
                segment.Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                isStroked: true));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    /// One device pixel of outline, expressed in the window's DIP space.
    public static double StrokeThickness(double scaleX) => StyleTokens.BorderWidth / Math.Max(scaleX, 0.01);

    public static Path FilledPath(Geometry data, Brush fill, Brush? stroke, double strokeWidth) => new()
    {
        Data = data,
        Fill = fill,
        Stroke = stroke,
        StrokeThickness = strokeWidth,
    };
}
