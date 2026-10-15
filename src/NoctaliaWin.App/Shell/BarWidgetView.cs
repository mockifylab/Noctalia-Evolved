// One bar widget: a capsule of palette text whose content comes from BarViewModel.
//
// Text labels rather than icon glyphs on purpose: Noctalia's bar uses an icon font that is
// not part of this repository's licence grant, and a label that says "Wi-Fi" is honest
// where a copied glyph would be an unlicensed asset.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Shell;

internal sealed class WidgetView
{
    public required FrameworkElement Element { get; init; }

    /// Repaints the widget from the view model. The window calls this on a tick or when a
    /// service reports a change, instead of rebuilding the whole bar.
    public required Action Update { get; init; }
}

/// How a bar widget reacts to the pointer. The panel-opening widgets route their click to the
/// panel host, and the point of that click travels with it in physical screen pixels so an
/// `open_near_click` panel can follow where the user actually pressed. The window-opening
/// widgets (settings) share the same routing but get their own callback, because raising a
/// window must not close the panel that is currently open.
internal sealed record BarInteractions(
    Func<string, bool> OpensPanel,
    Action<string, Point> Activated,
    Func<string, bool>? OpensWindow = null,
    Action<string, Point>? WindowActivated = null);

internal static class BarWidgetView
{
    // Both families ship with Windows 11; naming a system font is not redistributing an
    // asset. Segoe UI is the fallback on older builds.
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    public static WidgetView? Create(
        string widget,
        BarViewModel model,
        BarPlacement placement,
        MonitorInfo monitor,
        ThemeBrushes theme,
        bool vertical,
        BarInteractions? interactions = null)
    {
        if (!model.IsRendered(widget)) return null;

        double zoom = BarSurface.Zoom(placement, monitor);
        double spacing = placement.WidgetSpacing / Math.Max(monitor.ScaleX, 0.01);

        if (widget.Equals("spacer", StringComparison.OrdinalIgnoreCase))
        {
            var gap = new Border
            {
                Width = vertical
                    ? 1
                    : Math.Max(1, StyleTokens.SpaceMd * zoom),
                Height = vertical
                    ? Math.Max(1, StyleTokens.SpaceMd * zoom)
                    : 1,
            };
            return new WidgetView { Element = gap, Update = () => { } };
        }

        var label = new TextBlock
        {
            FontFamily = UiFont,
            // StyleTokens.FontWeightDefault is 500, which is WPF's Medium.
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        var capsule = new Border
        {
            Child = label,
            Background = Brushes.Transparent,
            // Noctalia's bar capsule: horizontal breathing room around each widget, with
            // the rounding kept small so neighbouring widgets stay readable.
            Padding = vertical
                ? new Thickness(0, StyleTokens.BarCapsulePadding * zoom * 0.5, 0, StyleTokens.BarCapsulePadding * zoom * 0.5)
                : new Thickness(StyleTokens.BarCapsulePadding * zoom * 0.5, 0, StyleTokens.BarCapsulePadding * zoom * 0.5, 0),
            CornerRadius = new CornerRadius(StyleTokens.RadiusSm * zoom),
            Margin = vertical
                ? new Thickness(0, spacing * 0.5, 0, spacing * 0.5)
                : new Thickness(spacing * 0.5, 0, spacing * 0.5, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        bool volume = widget.Equals("volume", StringComparison.OrdinalIgnoreCase);
        bool opensPanel = interactions?.OpensPanel(widget) ?? false;
        bool opensWindow = interactions?.OpensWindow?.Invoke(widget) ?? false;

        if (opensPanel || opensWindow)
        {
            // A bar widget is a button in Noctalia: clicking it raises the panel it owns,
            // and the press point goes with it so the panel can open near the click.
            capsule.Cursor = Cursors.Hand;
            capsule.MouseLeftButtonUp += (_, e) =>
            {
                var point = capsule.PointToScreen(e.GetPosition(capsule));
                if (opensPanel) interactions!.Activated(widget, point);
                else interactions!.WindowActivated!(widget, point);
            };
        }

        if (volume)
        {
            capsule.MouseWheel += (_, e) =>
            {
                if (e.Delta == 0) return;
                model.AdjustVolume(e.Delta > 0 ? 5 : -5);
                e.Handled = true;
            };

            // With no panel configured for it, the click is the only route left to mute.
            if (!opensPanel)
            {
                capsule.Cursor = Cursors.Hand;
                capsule.MouseLeftButtonUp += (_, _) => model.ToggleMute();
            }
        }

        capsule.MouseEnter += (_, _) => capsule.Background = theme.Hover;
        capsule.MouseLeave += (_, _) => capsule.Background = Brushes.Transparent;

        var view = new WidgetView
        {
            Element = capsule,
            Update = () =>
            {
                label.Text = model.Text(widget) ?? string.Empty;
                label.Foreground = model.IsDim(widget) ? theme[ColorRole.OnSurfaceVariant] : theme[ColorRole.OnSurface];
                label.FontSize = FontSize(widget, zoom, placement.FontScale);
                capsule.ToolTip = model.Tooltip(widget);
                capsule.Visibility = string.IsNullOrEmpty(label.Text) && !volume
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            },
        };

        view.Update();
        return view;
    }

    private static double FontSize(string widget, double zoom, double fontScale)
    {
        double baseSize = widget.ToLowerInvariant() switch
        {
            "clock" => StyleTokens.FontSizeBody,
            _ => StyleTokens.FontSizeCaption,
        };

        return baseSize * fontScale * zoom;
    }
}
