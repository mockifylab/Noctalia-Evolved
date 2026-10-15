// Turns a monitor + a configured bar into the pixel rectangle the shell window
// occupies, the concave outline that goes inside it, and the widget slots to render.
// Pure and physical-pixel based so the WPF layer stays a thin renderer.
using NoctaliaWin.Core.Abstractions;

namespace NoctaliaWin.Core.Shell;

public readonly record struct PxRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public sealed record BarPlacement(
    PxRect Body,
    PxRect Surface,
    Geometry.BarConcaveShape Shape,
    string Position,
    double ScaleX,
    double ScaleY,
    int Padding,
    int WidgetSpacing,
    double FontScale,
    double BackgroundOpacity,
    bool Shadow,
    bool AutoHide,
    int PanelOverlap,
    string Layer);

public sealed record BarWidgets(
    IReadOnlyList<string> Start,
    IReadOnlyList<string> Center,
    IReadOnlyList<string> End,
    IReadOnlyList<string> Skipped);

public static class BarLayout
{
    private static int Scale(double value, double factor) => (int)Math.Round(value * factor);

    /// The bar always lives inside the monitor's work area, so it can never cover the
    /// Windows taskbar. ReserveSpace is therefore not honoured here: shrinking the real
    /// work area needs appbar registration, which is a shell modification.
    ///
    /// `uiScale` is the accessibility UI scale; it multiplies the bar's own scale so one
    /// factor covers both, and it stays separate from `monitor.ScaleX`, which is the
    /// device scale the WPF layer divides back out to get DIPs.
    public static BarPlacement Compute(MonitorInfo monitor, Config.BarSection bar, double uiScale = 1.0)
    {
        var position = bar.Position is "bottom" or "left" or "right" ? bar.Position : "top";
        double zoom = bar.Scale * uiScale;
        double sx = monitor.ScaleX * zoom;
        double sy = monitor.ScaleY * zoom;

        int thicknessX = Scale(bar.Thickness, sx);
        int thicknessY = Scale(bar.Thickness, sy);
        int endsX = Scale(bar.MarginEnds, sx);
        int endsY = Scale(bar.MarginEnds, sy);
        int edgeX = Scale(bar.MarginEdge, sx);
        int edgeY = Scale(bar.MarginEdge, sy);

        var (tl, tr, br, bl) = bar.ResolvedCornerRadii();
        int radius(int value, double factor) => Math.Max(0, Scale(value, factor));

        var shape = Geometry.BarShape.Compute(new Geometry.BarShapeInput(
            Position: position,
            ConcaveEdgeCorners: bar.ConcaveEdgeCorners,
            MarginEdge: position is "left" or "right" ? edgeX : edgeY,
            MarginEnds: position is "top" or "bottom" ? endsX : endsY,
            Thickness: position is "left" or "right" ? thicknessX : thicknessY,
            RadiusTopLeft: radius(tl, sx),
            RadiusTopRight: radius(tr, sx),
            RadiusBottomLeft: radius(bl, sx),
            RadiusBottomRight: radius(br, sx)));

        PxRect body = position switch
        {
            "bottom" => new PxRect(
                monitor.WorkLeft + endsX,
                monitor.WorkTop + monitor.WorkHeight - thicknessY - edgeY,
                Math.Max(1, monitor.WorkWidth - (2 * endsX)),
                Math.Max(1, thicknessY)),
            "left" => new PxRect(
                monitor.WorkLeft + edgeX,
                monitor.WorkTop + endsY,
                Math.Max(1, thicknessX),
                Math.Max(1, monitor.WorkHeight - (2 * endsY))),
            "right" => new PxRect(
                monitor.WorkLeft + monitor.WorkWidth - thicknessX - edgeX,
                monitor.WorkTop + endsY,
                Math.Max(1, thicknessX),
                Math.Max(1, monitor.WorkHeight - (2 * endsY))),
            _ => new PxRect(
                monitor.WorkLeft + endsX,
                monitor.WorkTop + edgeY,
                Math.Max(1, monitor.WorkWidth - (2 * endsX)),
                Math.Max(1, thicknessY)),
        };

        // Shape was computed from physical inputs, so its insets are already pixels.
        var insets = shape.LogicalInset;
        int px(float value) => (int)Math.Round(value);
        var surface = new PxRect(
            body.X - px(insets.Left),
            body.Y - px(insets.Top),
            Math.Max(1, body.Width + px(insets.Left) + px(insets.Right)),
            Math.Max(1, body.Height + px(insets.Top) + px(insets.Bottom)));

        return new BarPlacement(
            Body: body,
            Surface: surface,
            Shape: shape,
            Position: position,
            ScaleX: sx,
            ScaleY: sy,
            Padding: Scale(bar.Padding, sx),
            WidgetSpacing: Scale(bar.WidgetSpacing, sx),
            FontScale: bar.FontScale,
            BackgroundOpacity: bar.BackgroundOpacity,
            Shadow: bar.Shadow,
            AutoHide: bar.AutoHide,
            // Left in logical px: the panel that consumes it scales by the same factor as
            // its own geometry, so it has to start in the same units as the config.
            PanelOverlap: bar.PanelOverlap,
            Layer: bar.Layer);
    }

    /// Unsupported names are skipped and reported instead of rendering a widget with
    /// no Windows backing.
    public static BarWidgets ResolveSlots(Config.BarSection bar)
    {
        var skipped = new List<string>();

        List<string> Filter(IEnumerable<string> names)
        {
            var kept = new List<string>();
            foreach (var name in names)
            {
                if (WidgetCatalog.IsSupported(name)) kept.Add(name);
                else skipped.Add(name);
            }
            return kept;
        }

        return new BarWidgets(
            Start: Filter(bar.Start),
            Center: Filter(bar.Center),
            End: Filter(bar.End),
            Skipped: skipped);
    }
}
