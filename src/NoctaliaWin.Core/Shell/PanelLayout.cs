// Turns a placed bar plus a panel's logical size into the physical-pixel surface the
// pop-out panel window occupies, nesting its concave corners against the bar's own radii.
// Mirrors the attached-panel branch of Noctalia's panel_manager.cpp (MIT, noctalia-dev).
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Geometry;

namespace NoctaliaWin.Core.Shell;

public sealed record PanelPlacement(
    string PanelId,
    Geometry.AttachedPanelGeometry Geometry,
    PxRect Surface,
    PxRect Visual,
    AttachedRevealDirection RevealDirection,
    double ScaleX,
    double ScaleY,
    int BarRadiusStart,
    int BarRadiusEnd,
    bool PanelBorders,
    int RevealDurationMs,
    /// false for a floating card: it touches no bar, so it has no seam to slide out of and
    /// fades instead of revealing.
    bool Attached);

public static class PanelLayout
{
    /// Upstream sizes a panel's height from its tab content; this build's control center has
    /// a known fixed set of rows, so the height is a documented constant instead of a
    /// measure-then-place pass that would fight the reveal animation. It is the height those
    /// rows actually occupy, so the card ends where its last row does.
    public const double ControlCenterLogicalHeight = 260;

    public const double CalendarLogicalHeight = 300;

    /// The calendar popup's width. Upstream derives it from the grid's own measure; a 42-cell
    /// grid at the caption size fits in 320 logical px, so this build fixes it rather than
    /// measuring and re-placing the window mid-reveal.
    public const double CalendarLogicalWidth = 320;

    /// The launcher's rows: a search field, `LauncherModel.VisibleRows` result rows, and the
    /// line under them that reports a failed launch. Fixed heights because the window is
    /// placed before it is measured, and a reveal that resized mid-flight would tear the seam
    /// with the bar.
    public const double LauncherSearchRowHeight = 40;
    public const double LauncherResultRowHeight = 36;
    public const double LauncherStatusRowHeight = 22;

    /// The gap between the launcher's three blocks. The window is placed before it is
    /// measured, so the rendered margins have to add up to this height exactly.
    public const double LauncherGap = StyleTokens.SpaceSm;

    public static double LauncherLogicalHeight =>
        2 * StyleTokens.PanelPadding
        + LauncherSearchRowHeight
        + LauncherGap
        + LauncherModel.VisibleRows * LauncherResultRowHeight
        + LauncherGap
        + LauncherStatusRowHeight;

    /// The logical size a panel asks for, per its id.
    public static (double Width, double Height) LogicalSize(
        string panelId, ControlCenterSection controlCenter, LauncherSection launcher) => panelId switch
        {
            PanelCoordinator.CalendarPanel => (CalendarLogicalWidth, CalendarLogicalHeight),
            PanelCoordinator.LauncherPanel => (launcher.Width, LauncherLogicalHeight),
            _ => (ControlCenterLogicalWidth(controlCenter), ControlCenterLogicalHeight),
        };

    /// The logical width the control center asks for: `control_center.width` is the
    /// full-sidebar size, and the compact sidebar scales it down.
    public static double ControlCenterLogicalWidth(ControlCenterSection section) =>
        section.Width * PanelSection.CompactSidebarWidthFactor;

    /// The bar's radii at the two ends of the edge the panel attaches to. `BarPlacement`
    /// already carries the *rendered* radii, so a carved flare corner nests correctly
    /// instead of leaving a step at the seam.
    public static (int Start, int End) AttachmentEdgeRadii(BarPlacement bar)
    {
        var r = bar.Shape.Radii;
        return bar.Position switch
        {
            "bottom" => ((int)r.Tl, (int)r.Tr),
            "left" => ((int)r.Tr, (int)r.Br),
            "right" => ((int)r.Tl, (int)r.Bl),
            _ => ((int)r.Bl, (int)r.Br),
        };
    }

    /// `logicalWidth`/`logicalHeight` are Noctalia logical px (unscaled). They go through the
    /// same zoom the bar uses — bar scale times UI scale — so the panel's corner cutouts and
    /// the bar's corners stay the same size on screen.
    public static PanelPlacement Compute(
        MonitorInfo monitor,
        BarPlacement bar,
        PanelSection panels,
        string panelId,
        double logicalWidth,
        double logicalHeight,
        PxRect? anchor = null)
    {
        double zoom = bar.ScaleX / Math.Max(monitor.ScaleX, 0.01);
        double sx = Math.Max(monitor.ScaleX, 0.01) * zoom;
        double sy = Math.Max(monitor.ScaleY, 0.01) * zoom;

        int Width(double logical) => Math.Max(1, (int)Math.Round(logical * sx));
        int Height(double logical) => Math.Max(1, (int)Math.Round(logical * sy));

        var surfaceBarRect = bar.Surface;
        int radius = (int)Math.Round(StyleTokens.RadiusXl * sx);
        int overlap = (int)Math.Round(bar.PanelOverlap * sx);

        bool useAnchor = anchor is not null;
        var (start, end) = AttachmentEdgeRadii(bar);

        var geometry = AttachedPanelPlacement.Compute(
            (surfaceBarRect.X, surfaceBarRect.Y, surfaceBarRect.Right, surfaceBarRect.Bottom),
            bar.Position,
            start,
            end,
            Width(logicalWidth),
            Height(logicalHeight),
            radius,
            overlap,
            useAnchor,
            anchor?.X ?? 0,
            anchor?.Y ?? 0);

        // A panel on a bottom or side bar can be taller (or wider) than the space the bar
        // left, so slide it back inside the work area. The seam with the bar is worth
        // losing there: a panel the user cannot read is worse than a panel with a step.
        bool vertical = bar.Position is "left" or "right";
        var unshifted = new PxRect(geometry.SurfaceX, geometry.SurfaceY, geometry.SurfaceWidth, geometry.SurfaceHeight);
        int shift = KeepInsideWorkArea(
            unshifted,
            vertical
                ? (monitor.WorkLeft, monitor.WorkLeft + monitor.WorkWidth)
                : (monitor.WorkTop, monitor.WorkTop + monitor.WorkHeight),
            vertical);

        int deltaX = vertical ? shift : 0;
        int deltaY = vertical ? 0 : shift;
        var surface = new PxRect(
            unshifted.X + deltaX, unshifted.Y + deltaY, unshifted.Width, unshifted.Height);

        return new PanelPlacement(
            PanelId: panelId,
            Geometry: geometry with
            {
                SurfaceX = surface.X,
                SurfaceY = surface.Y,
                VisualX = geometry.VisualX + deltaX,
                VisualY = geometry.VisualY + deltaY,
            },
            Surface: surface,
            Visual: new PxRect(
                geometry.VisualX + deltaX,
                geometry.VisualY + deltaY,
                Width(logicalWidth),
                Height(logicalHeight)),
            RevealDirection: geometry.RevealDirection,
            ScaleX: sx,
            ScaleY: sy,
            BarRadiusStart: start,
            BarRadiusEnd: end,
            PanelBorders: panels.Borders && panels.BorderWidth > 0,
            RevealDurationMs: StyleTokens.AnimNormalMs,
            Attached: true);
    }

    /// A floating panel is a plain rounded card in the work area, with no concave corner:
    /// upstream only nests the flare against a bar for `*_placement = "attached"`.
    /// `control_center_position` values beyond "auto" (below the bar) and "center" are parsed
    /// but land on the centred card, which is what the config codec documents.
    public static PanelPlacement ComputeFloating(
        MonitorInfo monitor,
        PanelSection panels,
        ControlCenterSection controlCenter,
        LauncherSection launcher,
        string panelId,
        BarPlacement? bar = null)
    {
        double zoom = (bar?.ScaleX ?? monitor.ScaleX) / Math.Max(monitor.ScaleX, 0.01);
        double sx = Math.Max(monitor.ScaleX, 0.01) * zoom;
        double sy = Math.Max(monitor.ScaleY, 0.01) * zoom;

        var (logicalWidth, logicalHeight) = LogicalSize(panelId, controlCenter, launcher);
        string position = panelId == PanelCoordinator.LauncherPanel
            ? panels.LauncherPosition
            : panels.ControlCenterPosition;

        int width = Math.Max(1, (int)Math.Round(logicalWidth * sx));
        int height = Math.Max(1, (int)Math.Round(logicalHeight * sy));
        int radius = (int)Math.Round(StyleTokens.RadiusXl * sx);
        int offset = (int)Math.Round(panels.FloatingOffset * sx);

        // "auto" hangs the card off the edge the bar docks on; "center" (and a build with no
        // bar to hang from) centres it in the work area.
        bool centerBoth = position == "center" || bar is null;
        bool barIsVertical = bar is { Position: "left" or "right" };

        int left = monitor.WorkLeft + Math.Max(0, (monitor.WorkWidth - width) / 2);
        int top = monitor.WorkTop + Math.Max(0, (monitor.WorkHeight - height) / 2);

        if (!centerBoth)
        {
            if (barIsVertical)
                left = bar!.Position == "left" ? bar.Surface.Right + offset : bar.Surface.X - offset - width;
            else if (bar!.Position == "bottom")
                top = bar.Surface.Y - offset - height;
            else
                top = bar.Surface.Bottom + offset;
        }

        // Never let the card hang off the work area, whichever way it was placed.
        int clampedLeft = Math.Max(monitor.WorkLeft,
            Math.Min(left, Math.Max(monitor.WorkLeft, monitor.WorkLeft + monitor.WorkWidth - width)));
        int clampedTop = Math.Max(monitor.WorkTop,
            Math.Min(top, Math.Max(monitor.WorkTop, monitor.WorkTop + monitor.WorkHeight - height)));

        var surface = new PxRect(clampedLeft, clampedTop, width, height);

        return new PanelPlacement(
            PanelId: panelId,
            Geometry: new AttachedPanelGeometry(
                SurfaceX: clampedLeft,
                SurfaceY: clampedTop,
                SurfaceWidth: width,
                SurfaceHeight: height,
                VisualX: clampedLeft,
                VisualY: clampedTop,
                PanelInsetX: 0,
                PanelInsetY: 0,
                CornerRadius: radius,
                Corners: CornerShapes.AllConvex,
                Radii: new Radii(radius),
                Insets: RectInsets.Zero,
                RevealDirection: AttachedRevealDirection.Down),
            Surface: surface,
            Visual: surface,
            RevealDirection: AttachedRevealDirection.Down,
            ScaleX: sx,
            ScaleY: sy,
            BarRadiusStart: 0,
            BarRadiusEnd: 0,
            PanelBorders: panels.Borders && panels.BorderWidth > 0,
            RevealDurationMs: StyleTokens.AnimNormalMs,
            Attached: false);
    }

    /// How far the surface has to move along the bar's main axis to stay inside the band,
    /// or zero when it already does.
    private static int KeepInsideWorkArea(PxRect surface, (int Low, int High) band, bool alongX)
    {
        int start = alongX ? surface.X : surface.Y;
        int end = alongX ? surface.Right : surface.Bottom;

        int earliest = band.Low - start;
        int latest = band.High - end;
        if (earliest > latest) return earliest;

        return Math.Clamp(0, earliest, latest);
    }
}
