// Core's physical-pixel bar placement -> WPF's DIP space.
//
// The split matters: NoctaliaWin.Core does all the geometry in pixels (that is what the
// monitor APIs report), and WPF lays out in device-independent units of the monitor the
// window is on. Dividing by that monitor's scale is the whole conversion, and it keeps the
// user's bar scale and UI scale as DIPs, which is how typography and padding are meant to
// grow.
using System.Windows;
using System.Windows.Media;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Geometry;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Shell;

internal static class BarSurface
{
    /// The zoom the bar carries in DIPs: bar scale times accessibility UI scale, with the
    /// device scale divided back out.
    public static double Zoom(BarPlacement placement, MonitorInfo monitor) =>
        monitor.ScaleX <= 0 ? 1.0 : placement.ScaleX / monitor.ScaleX;

    /// A placement rect converted to DIPs relative to the bar window's own surface.
    public static Rect ToLogical(PxRect rect, BarPlacement placement, MonitorInfo monitor)
    {
        var surface = placement.Surface;
        return new Rect(
            (rect.X - surface.X) / monitor.ScaleX,
            (rect.Y - surface.Y) / monitor.ScaleY,
            Math.Max(0, rect.Width) / monitor.ScaleX,
            Math.Max(0, rect.Height) / monitor.ScaleY);
    }

    /// The bar's fill silhouette, including the concave flare. Points outside the surface
    /// are kept: a bar docked flush with a screen edge has its flare there, and the window
    /// boundary is what clips it, exactly like the upstream shader's surface.
    public static Geometry Silhouette(BarPlacement placement, MonitorInfo monitor)
    {
        var surface = placement.Surface;
        var shape = placement.Shape;

        return Design.OutlineGeometry.Build(
            surface.Width, surface.Height,
            shape.Radii, shape.Corners, shape.LogicalInset,
            monitor.ScaleX, monitor.ScaleY);
    }

    /// Stroke thickness in DIPs for the bar's outline: Noctalia draws it one device pixel
    /// wide, so it must not grow with DPI.
    public static double OutlineThickness(MonitorInfo monitor) =>
        Design.OutlineGeometry.StrokeThickness(monitor.ScaleX);
}
