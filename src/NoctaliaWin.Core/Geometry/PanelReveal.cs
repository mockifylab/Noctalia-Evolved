// Ported attached-panel reveal animation from Noctalia
// src/shell/panel/panel_manager.cpp (applyAttachedReveal, publishAttachedPanelGeometry,
// startAttachedOpenAnimation) — MIT License, Copyright (c) 2026 noctalia-dev.
//
// The surface rect never moves during the animation. The panel content slides inside it,
// which is what makes the panel look as if it grows out from behind the bar; the bar-side
// concave bulges only enter the visible clip near the end of the travel.
namespace NoctaliaWin.Core.Geometry;

public readonly record struct PanelReveal(
    double Progress,
    double ContentOffsetX,
    double ContentOffsetY,
    float BulgeRadius)
{
    /// Upstream drops the published geometry entirely below this progress, so the bar
    /// renders no cutouts for a panel that is effectively closed.
    public bool HasVisibleSurface => Progress > MinVisibleProgress;

    public const float MinVisibleProgress = 0.001f;
}

public static class PanelRevealMath
{
    public static double EaseOutCubic(double t)
    {
        double x = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - x, 3);
    }

    public static double EaseInQuad(double t)
    {
        double x = Math.Clamp(t, 0, 1);
        return x * x;
    }

    public static double EaseInOutQuad(double t)
    {
        double x = Math.Clamp(t, 0, 1);
        return x < 0.5 ? 2 * x * x : 1 - Math.Pow(-2 * x + 2, 2) / 2;
    }

    /// Linear progress in `eased` form: the value the renderer should actually apply.
    /// Upstream uses EaseOutCubic to open an attached panel, EaseInOutQuad to close it, and
    /// EaseInQuad to close a detached one.
    public static double OpenEased(double elapsedMs, int durationMs) =>
        EaseOutCubic(Raw(elapsedMs, durationMs));

    public static double AttachedCloseEased(double elapsedMs, int durationMs) =>
        1 - EaseInOutQuad(Raw(elapsedMs, durationMs));

    public static double DetachedCloseEased(double elapsedMs, int durationMs) =>
        1 - EaseInQuad(Raw(elapsedMs, durationMs));

    private static double Raw(double elapsedMs, int durationMs) =>
        durationMs <= 0 ? 1 : Math.Clamp(elapsedMs / durationMs, 0, 1);

    /// Content offset for a reveal at `progress`, in the same units as the size arguments
    /// (DIPs in the WPF layer). The clip stays put; only the content moves.
    public static PanelReveal At(
        double progress,
        double panelWidth,
        double panelHeight,
        float cornerRadius,
        AttachedRevealDirection direction)
    {
        double p = Math.Clamp(progress, 0, 1);
        bool vertical = direction is AttachedRevealDirection.Left or AttachedRevealDirection.Right;
        double travel = (vertical ? panelWidth : panelHeight) * (1 - p);

        // Away from the bar means the content starts parked one panel-dimension short, so
        // its leading edge is at the bar and the rest is clipped away.
        double offsetX = direction switch
        {
            AttachedRevealDirection.Right => -travel,
            AttachedRevealDirection.Left => travel,
            _ => 0,
        };
        double offsetY = direction switch
        {
            AttachedRevealDirection.Down => -travel,
            AttachedRevealDirection.Up => travel,
            _ => 0,
        };

        // The bulges are the last cornerRadius of travel, so the silhouette is a sharp
        // rectangle until then.
        double mainDim = vertical ? panelWidth : panelHeight;
        double bulge = Math.Clamp(cornerRadius - mainDim * (1 - p), 0, Math.Max(0, cornerRadius));

        return new PanelReveal(p, offsetX, offsetY, (float)bulge);
    }
}
