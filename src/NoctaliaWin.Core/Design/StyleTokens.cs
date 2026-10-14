// Ported design tokens from Noctalia src/ui/style.h (MIT License, Copyright (c) 2026 noctalia-dev).
namespace NoctaliaWin.Core.Design;

public static class StyleTokens
{
    public const int BarThicknessDefault = 34;

    public const int AnimFastMs = 100;
    public const int AnimNormalMs = 200;
    public const int AnimSlowMs = 400;

    public const float RadiusSm = 3f;
    public const float RadiusMd = 6f;
    public const float RadiusLg = 9f;
    public const float RadiusXl = 12f;

    public const float BorderWidth = 1f;
    public const float EmphasizedBorderWidth = 3f;
    public const float FocusRingWidth = 2f;
    public const float DisabledOutlineAlpha = 0.5f;

    public const float SpaceXs = 4f;
    public const float SpaceSm = 8f;
    public const float SpaceMd = 12f;
    public const float SpaceLg = 16f;

    public const float CardPadding = 14f;
    public const float PanelPadding = 14f;

    public const float BarCapsulePadding = 6f;
    public const float BaseGlyphSize = 16f;

    public const float FontSizeMini = 11f;
    public const float FontSizeCaption = 13f;
    public const float FontSizeBody = 14f;
    public const float FontSizeTitle = 16f;
    public const float FontSizeHeader = 20f;

    public const float ControlHeightSm = 32f;
    public const float ControlHeight = 38f;
    public const float ControlHeightLg = 44f;
    public const float ScrollWheelStep = 56f;
    public const float DragStartThreshold = 6f;

    public const float ScrollbarWidth = 6f;
    public const float ScrollbarHoverWidth = 12f;
    public const float ScrollbarMinThumbHeight = 32f;

    public const float MenuAutoMaxWidth = 420f;

    public const int FontWeightDefault = 500;

    public static float ScaledRadiusXl(float uiScale) => RadiusXl * uiScale;
}
