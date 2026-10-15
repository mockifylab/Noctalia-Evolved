// Ported attached-panel surface geometry from Noctalia
// src/shell/panel/attached_panel_context.h and the attached placement math in
// src/shell/panel/panel_manager.cpp (MIT License, Copyright (c) 2026 noctalia-dev).
namespace NoctaliaWin.Core.Geometry;

public enum AttachedRevealDirection
{
    Down,
    Up,
    Right,
    Left,
}

public readonly record struct AttachedPanelGeometry(
    int SurfaceX,
    int SurfaceY,
    int SurfaceWidth,
    int SurfaceHeight,
    int VisualX,
    int VisualY,
    int PanelInsetX,
    int PanelInsetY,
    float CornerRadius,
    CornerShapes Corners,
    Radii Radii,
    RectInsets Insets,
    AttachedRevealDirection RevealDirection);

public static class AttachedPanelPlacement
{
    /// Extra margin so the flare strip around concave bar-side corners fits in the surface.
    public const int FlareSafetyPadding = 2;

    public static CornerShapes CornerShapesFor(string barPosition) => barPosition switch
    {
        "bottom" => new CornerShapes(Bl: CornerShape.Concave, Br: CornerShape.Concave),
        "left" => new CornerShapes(Tl: CornerShape.Concave, Bl: CornerShape.Concave),
        "right" => new CornerShapes(Tr: CornerShape.Concave, Br: CornerShape.Concave),
        _ => new CornerShapes(Tl: CornerShape.Concave, Tr: CornerShape.Concave),
    };

    /// Upstream gives an attached panel the same radius on all four corners
    /// (`bg->setRadii(Radii{radius, radius, radius, radius})` in panel_manager.cpp) and lets
    /// CornerShapesFor decide which pair is concave. Zeroing the away-side corners would
    /// square off the edge of the panel that faces away from the bar.
    public static Radii RadiiFor(float radius) => new(radius);

    public static RectInsets InsetsFor(string barPosition, float radius)
    {
        var vertical = barPosition is "left" or "right";
        return vertical
            ? new RectInsets(0f, radius, 0f, radius)
            : new RectInsets(radius, 0f, radius, 0f);
    }

    public static AttachedRevealDirection RevealDirectionFor(string barPosition) => barPosition switch
    {
        "bottom" => AttachedRevealDirection.Up,
        "left" => AttachedRevealDirection.Right,
        "right" => AttachedRevealDirection.Left,
        _ => AttachedRevealDirection.Down,
    };

    /// Computes the panel surface rect. Units: logical px, screen coords.
    /// barRect is the bar's visible rect; barRadiusStart/End are the bar corner
    /// radii at the two ends of the attachment edge (upstream: total inset =
    /// barR + panelR so concave cutouts nest against the bar's convex ends).
    public static AttachedPanelGeometry Compute(
        (int Left, int Top, int Right, int Bottom) barRect,
        string barPosition,
        int barRadiusStart,
        int barRadiusEnd,
        int panelWidth,
        int panelHeight,
        float cornerRadius,
        int panelOverlap,
        bool useAnchor,
        double anchorX,
        double anchorY)
    {
        bool barIsBottom = barPosition == "bottom";
        bool barIsLeft = barPosition == "left";
        bool barIsRight = barPosition == "right";
        bool barIsVertical = barIsLeft || barIsRight;

        var cornerOutset = (int)Math.Ceiling(cornerRadius);
        // Upstream derives the cross outset from the shadow bleed; shadows are not
        // implemented yet, so the bleed is zero and only the corner wrap + safety
        // padding remain.
        int crossOutset = cornerOutset + FlareSafetyPadding;
        int mainBleedAway = FlareSafetyPadding;

        int barLeft = barRect.Left, barTop = barRect.Top;
        int barRight = barRect.Right, barBottom = barRect.Bottom;

        int totalStartInset = (int)Math.Ceiling(Math.Max(0, barRadiusStart) + cornerRadius);
        int totalEndInset = (int)Math.Ceiling(Math.Max(0, barRadiusEnd) + cornerRadius);

        int visualX, visualY;
        if (barIsVertical)
        {
            var minY = barTop + totalStartInset;
            var maxY = Math.Max(minY, barBottom - panelHeight - totalEndInset);
            var centeredY = barTop + (barBottom - barTop - panelHeight) / 2;
            var desiredY = (int)Math.Round(anchorY - panelHeight * 0.5);
            visualY = useAnchor ? Math.Clamp(desiredY, minY, maxY) : centeredY;
            visualX = barIsLeft ? barRight - panelOverlap : barLeft - panelWidth + panelOverlap;
        }
        else
        {
            var minX = barLeft + totalStartInset;
            var maxX = Math.Max(minX, barRight - panelWidth - totalEndInset);
            var centeredX = barLeft + (barRight - barLeft - panelWidth) / 2;
            var desiredX = (int)Math.Round(anchorX - panelWidth * 0.5);
            visualX = useAnchor ? Math.Clamp(desiredX, minX, maxX) : centeredX;
            visualY = barIsBottom ? barTop - panelHeight + panelOverlap : barBottom - panelOverlap;
        }

        int surfaceX, surfaceY;
        if (barIsVertical)
        {
            surfaceY = visualY - crossOutset;
            surfaceX = barIsLeft ? visualX : visualX - mainBleedAway;
        }
        else
        {
            surfaceX = visualX - crossOutset;
            surfaceY = barIsBottom ? visualY - mainBleedAway : visualY;
        }

        int surfaceWidth = (barIsVertical ? panelWidth + mainBleedAway : panelWidth + 2 * crossOutset);
        int surfaceHeight = (barIsVertical ? panelHeight + 2 * crossOutset : panelHeight + mainBleedAway);

        return new AttachedPanelGeometry(
            surfaceX,
            surfaceY,
            Math.Max(1, surfaceWidth),
            Math.Max(1, surfaceHeight),
            visualX,
            visualY,
            visualX - surfaceX,
            visualY - surfaceY,
            cornerRadius,
            CornerShapesFor(barPosition),
            RadiiFor(cornerRadius),
            InsetsFor(barPosition, cornerRadius),
            RevealDirectionFor(barPosition));
    }
}
