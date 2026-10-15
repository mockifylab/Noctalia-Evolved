// Palette -> WPF brushes.
//
// Built in code rather than a XAML resource dictionary because a palette in Noctalia is
// runtime data (builtin, wallpaper-derived, community) selected by config, and the bar's
// background opacity is a per-bar setting: neither can be a static resource.
using System.Windows;
using System.Windows.Media;
using NoctaliaWin.Core.Design;

namespace NoctaliaWin.App.Design;

internal sealed class ThemeBrushes
{
    private readonly Dictionary<ColorRole, Brush> _brushes = [];

    public ThemeBrushes(Palette palette, double barBackgroundOpacity)
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

        var outline = palette[ColorRole.Outline];
        Outline = Freeze(Color.FromRgb(outline.R, outline.G, outline.B));

        // The capsule a widget lights up under the pointer, kept partly transparent so the
        // bar background still reads through it.
        var hover = palette[ColorRole.Hover];
        Hover = Freeze(Color.FromArgb(90, hover.R, hover.G, hover.B));
    }

    public Palette Palette { get; }
    public Brush BarBackground { get; }
    public Brush Outline { get; }
    public Brush Hover { get; }

    public Brush this[ColorRole role] => _brushes[role];

    public static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
