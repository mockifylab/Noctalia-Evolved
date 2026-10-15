// The one-open-panel rule and the dismissal hit tests, ported from the behaviour of
// Noctalia's panel_manager (MIT, Copyright (c) 2026 noctalia-dev): opening a second panel
// destroys the first with no close animation, an attached panel keeps its bar revealed, and
// clicks on the bar itself must not dismiss the panel that is open.
//
// The window layer drives the clock; this class only decides what is open and where.
namespace NoctaliaWin.Core.Shell;

public enum PanelPhase
{
    Closed,
    Opening,
    Open,
    Closing,
}

/// A panel the shell can open, and where its surface sits in physical pixels.
public sealed record PanelSurface(
    string PanelId,
    PxRect SurfaceRect,
    PxRect VisualRect,
    Geometry.AttachedRevealDirection RevealDirection,
    float CornerRadius);

public sealed class PanelCoordinator
{
    public const string CalendarPanel = "calendar";
    public const string ControlCenterPanel = "control-center";

    /// The bar widgets that open a panel on click. Anything else in the bar is a label.
    public static bool OpensPanel(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => true,
        "volume" => true,
        "network" => true,
        "battery" => true,
        "control-center" => true,
        _ => false,
    };

    /// Which panel a bar widget opens. The status widgets all reach the same quick-settings
    /// surface, the way upstream's audio/network/battery buttons route to the control center.
    public static string PanelFor(string widget) => widget.ToLowerInvariant() switch
    {
        "clock" => CalendarPanel,
        _ => ControlCenterPanel,
    };

    public PanelPhase Phase { get; private set; } = PanelPhase.Closed;

    public PanelSurface? Active { get; private set; }

    public string? ActiveId => Active?.PanelId;

    public bool IsTransitioning => Phase is PanelPhase.Opening or PanelPhase.Closing;

    /// While a panel is attached to a bar, that bar must not slide away underneath it.
    public bool KeepsBarShown => Phase != PanelPhase.Closed;

    public event Action? Changed;

    /// True when the panel is now open (or starting to open).
    public bool Toggle(PanelSurface surface, IReadOnlyList<PxRect> barRects)
    {
        if (Phase is PanelPhase.Open or PanelPhase.Opening && ActiveId == surface.PanelId)
        {
            CloseImmediately();
            return false;
        }

        Open(surface, barRects);
        return true;
    }

    public void Open(PanelSurface surface, IReadOnlyList<PxRect> barRects)
    {
        // Upstream tears the previous panel down without animating it, so the new one can
        // take the surface immediately.
        Active = surface;
        _barRects = barRects;
        Phase = PanelPhase.Opening;
        Changed?.Invoke();
    }

    public void FinishOpen()
    {
        if (Phase != PanelPhase.Opening) return;
        Phase = PanelPhase.Open;
        Changed?.Invoke();
    }

    public void BeginClose()
    {
        if (Phase is PanelPhase.Opening or PanelPhase.Open)
        {
            Phase = PanelPhase.Closing;
            Changed?.Invoke();
        }
    }

    public void FinishClose()
    {
        Phase = PanelPhase.Closed;
        Active = null;
        _barRects = [];
        Changed?.Invoke();
    }

    public void CloseImmediately()
    {
        FinishClose();
    }

    /// A click inside the panel's own visual body is absorbed by the panel. A click in the
    /// transparent band around it (the flare margin) or on the bar dismisses it, which is
    /// what upstream's click shield does by excluding the bar rects from its input region.
    public bool AbsorbsPointer(int x, int y) =>
        Active is { } active && active.VisualRect.Contains(x, y);

    public bool DismissesAt(int x, int y) =>
        Active is { } active
        && !active.VisualRect.Contains(x, y)
        && !IsOnBar(x, y);

    private bool IsOnBar(int x, int y)
    {
        foreach (var rect in _barRects)
            if (rect.Contains(x, y))
                return true;

        return false;
    }

    private IReadOnlyList<PxRect> _barRects = [];
}
