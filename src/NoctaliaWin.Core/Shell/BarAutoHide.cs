// Auto-hide policy: where the pointer has to go to reveal a hidden bar, and where the
// window moves when it hides. Pure so the slide targets are testable without a desktop.
namespace NoctaliaWin.Core.Shell;

public static class BarAutoHide
{
    /// Noctalia's reveal strip: a thin band against the docked screen edge.
    public const int DefaultTriggerThickness = 2;

    public static PxRect TriggerBand(BarPlacement placement, Abstractions.MonitorInfo monitor, int triggerThickness = DefaultTriggerThickness)
        => placement.Position switch
        {
            "bottom" => new PxRect(monitor.WorkLeft, monitor.WorkTop + monitor.WorkHeight - triggerThickness, monitor.WorkWidth, triggerThickness),
            "left" => new PxRect(monitor.WorkLeft, monitor.WorkTop, triggerThickness, monitor.WorkHeight),
            "right" => new PxRect(monitor.WorkLeft + monitor.WorkWidth - triggerThickness, monitor.WorkTop, triggerThickness, monitor.WorkHeight),
            _ => new PxRect(monitor.WorkLeft, monitor.WorkTop, monitor.WorkWidth, triggerThickness),
        };

    /// The hidden window sits entirely outside its monitor, in the direction it docks
    /// to, so no pixel stays visible and it can never cover the taskbar. Translating by
    /// the surface size alone is not enough when the bar carries an edge margin.
    public static PxRect HiddenRect(BarPlacement placement, Abstractions.MonitorInfo monitor)
    {
        var surface = placement.Surface;
        return placement.Position switch
        {
            "bottom" => surface with { Y = monitor.Top + monitor.Height },
            "left" => surface with { X = surface.X - surface.Width - Math.Max(0, surface.X - monitor.Left) },
            "right" => surface with { X = monitor.Left + monitor.Width },
            _ => surface with { Y = surface.Y - surface.Height - Math.Max(0, surface.Y - monitor.Top) },
        };
    }

    public static bool Contains(PxRect rect, int x, int y) =>
        x >= rect.X && x < rect.X + rect.Width && y >= rect.Y && y < rect.Y + rect.Height;

    /// Shown while the pointer is on the bar or in the reveal band, and while a panel
    /// owned by the bar is open; hidden otherwise. A bar with auto-hide off is always shown.
    public static bool IsShown(
        bool autoHide,
        bool pointerOnBar,
        bool pointerInTriggerBand,
        bool panelOpen) =>
        !autoHide || panelOpen || pointerOnBar || pointerInTriggerBand;
}
