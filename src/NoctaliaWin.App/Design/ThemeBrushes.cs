// Palette -> WPF brushes.
//
// Built in code rather than a XAML resource dictionary because a palette in Noctalia is
// runtime data (builtin, wallpaper-derived, community) selected by config, and the bar's
// background opacity is a per-bar setting: neither can be a static resource.
using System.Windows;
using System.Windows.Media;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;

namespace NoctaliaWin.App.Design;

internal sealed class ThemeBrushes
{
    private readonly Dictionary<ColorRole, Brush> _brushes = [];

    public ThemeBrushes(Palette palette, double barBackgroundOpacity, PanelTransparencyMode panelMode)
    {
        Palette = palette;

        foreach (var (role, _) in ColorRoleTokens.All)
            _brushes[role] = Freeze(Color.FromRgb(palette[role].R, palette[role].G, palette[role].B));

        // Noctalia's bar keeps the surface colour and dials its alpha, so the widgets
        // never need a second palette.
        var surface = palette[ColorRole.Surface];
        BarBackground = Freeze(Color.FromArgb(
            (byte)Math.Round(Math.Clamp(barBackgroundOpacity, 0, 1) * 255),
            surface.R, surface.G, surface.B));

        // Panel opacity comes from the transparency mode, ported from upstream's card
        // mapping. A soft or glass panel is genuinely translucent here — WPF has no
        // documented backdrop blur behind a layered window, so the blur part of "glass" is
        // not faked and the desktop shows through unblurred.
        PanelOpacity = PanelSection.CardOpacity(panelMode, barBackgroundOpacity);
        PanelBackground = Alpha(surface, PanelOpacity);

        var variant = palette[ColorRole.SurfaceVariant];
        PanelCard = Alpha(variant, PanelOpacity);

        var outline = palette[ColorRole.Outline];
        Outline = Freeze(Color.FromRgb(outline.R, outline.G, outline.B));

        // The capsule a widget lights up under the pointer, kept partly transparent so the
        // bar background still reads through it.
        var hover = palette[ColorRole.Hover];
        Hover = Freeze(Color.FromArgb(90, hover.R, hover.G, hover.B));
    }

    public Palette Palette { get; }
    public Brush BarBackground { get; }
    public Brush PanelBackground { get; }
    public Brush PanelCard { get; }
    public double PanelOpacity { get; }
    public Brush Outline { get; }
    public Brush Hover { get; }

    /// Whether a normal window's own caption should be asked to go dark. Decided from the
    /// palette's surface rather than from the config's mode string, because "auto" resolves to
    /// whichever palette was actually picked.
    public bool UsesDarkCaption
    {
        get
        {
            var surface = Palette[ColorRole.Surface];
            // Rec. 709 luma: what a person reads as light or dark, not raw green.
            return 0.2126 * surface.R + 0.7152 * surface.G + 0.0722 * surface.B < 128;
        }
    }

    public Brush this[ColorRole role] => _brushes[role];

    public static SolidColorBrush Alpha(Rgb color, double opacity) => Freeze(Color.FromArgb(
        (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255),
        color.R, color.G, color.B));

    public static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
