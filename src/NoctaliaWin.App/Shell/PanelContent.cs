// What goes inside a pop-out panel. Two surfaces exist in this build, and both are made of
// data the platform services actually report: the calendar panel shows the real month and
// the clock's configured date format, and the control-center panel reads and writes the real
// master volume and reports the real network and battery state.
//
// Every value here is a text label or a slider, never an icon glyph: Noctalia's icon font is
// not part of this repository's licence grant.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Shell;

internal static class PanelContent
{
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    /// The element tree plus the unsubscribe for its repaint subscription. The host detaches
    /// when the window closes, so a panel opened a hundred times does not accumulate a
    /// hundred listeners on the content model.
    public sealed record Surface(FrameworkElement Element, Action Detach);

    /// Build the surface for a panel id. Unknown ids return null so the caller can refuse to
    /// open a panel it cannot fill, rather than showing an empty card.
    public static Surface? Build(
        string panelId,
        PanelContentModel model,
        ThemeBrushes theme,
        BarPlacement bar,
        MonitorInfo monitor)
    {
        double zoom = BarSurface.Zoom(bar, monitor);
        double font = bar.FontScale * zoom;

        return panelId switch
        {
            PanelCoordinator.CalendarPanel => Calendar(model, theme, zoom, font),
            PanelCoordinator.ControlCenterPanel => ControlCenter(model, theme, zoom, font, bar),
            _ => null,
        };
    }

    private static Surface Calendar(PanelContentModel model, ThemeBrushes theme, double zoom, double font)
    {
        var root = Stack(zoom, out var padding);

        var date = Label(model.Bar.DateText, theme, StyleTokens.FontSizeTitle, font, centered: true);
        var grid = new UniformGrid { Rows = 6, Columns = 7, Margin = Space(0, StyleTokens.SpaceSm * zoom, 0, 0) };
        var weekdays = new UniformGrid { Rows = 1, Columns = 7 };
        var month = Label(string.Empty, theme, StyleTokens.FontSizeCaption, font, centered: true);
        double cellRadius = StyleTokens.RadiusMd * zoom;

        void Repaint()
        {
            date.Text = model.Bar.DateText;
            var current = model.Calendar();
            month.Text = current.Title;

            weekdays.Children.Clear();
            foreach (var day in current.WeekdayHeaders)
                weekdays.Children.Add(Label(day, theme, StyleTokens.FontSizeMini, font, centered: true));

            grid.Children.Clear();
            foreach (var cell in current.Cells)
            {
                var cellLabel = Label(cell.Day.ToString(), theme, StyleTokens.FontSizeCaption, font, centered: true);

                if (cell.IsToday)
                {
                    // Upstream marks today with a filled capsule rather than a heavier weight,
                    // which survives any palette. A TextBlock cannot clip its own background to
                    // a radius, so the fill lives on a wrapping border.
                    cellLabel.Foreground = theme[ColorRole.OnPrimary];
                    grid.Children.Add(new Border
                    {
                        Child = cellLabel,
                        Background = theme[ColorRole.Primary],
                        CornerRadius = new CornerRadius(cellRadius),
                        Margin = Space(StyleTokens.SpaceXs * zoom * 0.5, 0, StyleTokens.SpaceXs * zoom * 0.5, 0),
                    });
                    continue;
                }

                cellLabel.Foreground = theme[cell.InMonth ? ColorRole.OnSurface : ColorRole.OnSurfaceVariant];
                if (!cell.InMonth) cellLabel.Opacity = 0.55;
                grid.Children.Add(cellLabel);
            }
        }

        model.Changed += Repaint;
        root.Children.Add(date);
        root.Children.Add(month);
        root.Children.Add(weekdays);
        root.Children.Add(grid);
        root.Children.Add(MonthPager(model, theme, zoom, font));
        Repaint();

        return new Surface(Wrap(root, padding), () => model.Changed -= Repaint);
    }

    private static FrameworkElement MonthPager(PanelContentModel model, ThemeBrushes theme, double zoom, double font)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = Space(0, StyleTokens.SpaceSm * zoom, 0, 0),
        };

        row.Children.Add(TextButton("Previous month", "Prev", model.PageMonths, -1, theme, zoom, font));
        row.Children.Add(TextButton("Back to this month", "Today", _ => model.BackToToday(), 0, theme, zoom, font));
        row.Children.Add(TextButton("Next month", "Next", model.PageMonths, 1, theme, zoom, font));
        return row;
    }

    private static Surface ControlCenter(
        PanelContentModel model, ThemeBrushes theme, double zoom, double font, BarPlacement bar)
    {
        var root = Stack(zoom, out var padding);
        var audio = model.Bar;

        var volumeLabel = Label(string.Empty, theme, StyleTokens.FontSizeCaption, font, centered: false);
        var volume = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            Margin = Space(0, StyleTokens.SpaceXs * zoom, 0, StyleTokens.SpaceSm * zoom),
        };
        volume.ValueChanged += (_, e) => audio.SetVolume((int)Math.Round(e.NewValue));

        var mute = TextButton("Mute or unmute the output device", "Mute", _ => audio.ToggleMute(), 0, theme, zoom, font);
        var network = Label(string.Empty, theme, StyleTokens.FontSizeCaption, font, centered: false);
        var battery = Label(string.Empty, theme, StyleTokens.FontSizeCaption, font, centered: false);

        // The panel must follow a volume change made by another app, but must not fight the
        // user while they are dragging.
        void Repaint()
        {
            var percent = audio.VolumePercent;
            volumeLabel.Text = percent is { } value
                ? $"{value}%"
                : "No output device";
            volume.IsEnabled = percent is not null && !audio.IsMuted;
            if (!volume.IsMouseCaptured && !volume.IsKeyboardFocused && percent is { } synced)
                volume.Value = synced;

            mute.IsEnabled = audio.VolumePercent is not null;
            mute.Content = audio.IsMuted ? "Unmute" : "Mute";
            network.Text = "Network: " + audio.NetworkSummary;
            battery.Text = "Battery: " + audio.BatterySummary;
        }

        model.Changed += Repaint;
        root.Children.Add(Card(theme, zoom, volumeLabel, volume, mute));
        root.Children.Add(Card(theme, zoom, network));
        root.Children.Add(Card(theme, zoom, battery));

        var note = Label(
            "Media, notifications and brightness need Windows APIs this build does not use yet.",
            theme, StyleTokens.FontSizeMini, font, centered: false);
        note.Opacity = 0.7;
        note.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(note);

        Repaint();
        return new Surface(Wrap(root, padding), () => model.Changed -= Repaint);
    }

    // ── small builders ───────────────────────────────────────────────────────

    private static StackPanel Stack(double zoom, out Thickness padding)
    {
        padding = new Thickness(StyleTokens.PanelPadding * zoom);
        return new StackPanel { Orientation = Orientation.Vertical };
    }

    private static FrameworkElement Wrap(StackPanel content, Thickness padding) => new Border
    {
        Child = content,
        Padding = padding,
    };

    private static Border Card(ThemeBrushes theme, double zoom, params UIElement[] rows)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var row in rows) stack.Children.Add(row);

        return new Border
        {
            Child = stack,
            Background = theme.PanelCard,
            CornerRadius = new CornerRadius(StyleTokens.RadiusMd * zoom),
            Padding = new Thickness(StyleTokens.CardPadding * zoom * 0.7),
            Margin = Space(0, 0, 0, StyleTokens.SpaceSm * zoom),
        };
    }

    private static TextBlock Label(
        string text, ThemeBrushes theme, double size, double fontScale, bool centered) => new()
    {
        Text = text,
        FontFamily = UiFont,
        FontWeight = FontWeights.Medium,
        FontSize = size * fontScale,
        Foreground = theme[ColorRole.OnSurface],
        Margin = Space(0, 0, 0, StyleTokens.SpaceXs * fontScale),
        TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left,
        HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
    };

    private static Button TextButton(
        string tooltip, string text, Action<int> onClick, int argument, ThemeBrushes theme, double zoom, double font)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontWeight = FontWeights.Medium,
            FontSize = StyleTokens.FontSizeCaption * font,
            Foreground = theme[ColorRole.OnSurface],
            Background = theme.PanelCard,
            BorderBrush = theme.Outline,
            BorderThickness = new Thickness(0),
            ToolTip = tooltip,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = Space(0, 0, StyleTokens.SpaceSm * zoom, 0),
            Padding = new Thickness(StyleTokens.SpaceMd * zoom * 0.6, StyleTokens.SpaceXs * zoom, StyleTokens.SpaceMd * zoom * 0.6, StyleTokens.SpaceXs * zoom),
        };
        button.Template = FlatTemplate(theme, zoom);
        button.Click += (_, _) => onClick(argument);
        return button;
    }

    /// WPF's default button chrome is a Windows-vista-era gradient that no palette can make
    /// look like Noctalia, so the control template is replaced with a rounded card whose fill
    /// comes from the palette. Hovering swaps in the palette's hover capsule, the same
    /// treatment the bar widgets get.
    private static ControlTemplate FlatTemplate(ThemeBrushes theme, double zoom)
    {
        // The name goes through the factory constructor: that is what registers it in the
        // template's namescope, so the hover trigger's TargetName can resolve.
        var border = new FrameworkElementFactory(typeof(Border), "root");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(StyleTokens.RadiusSm * zoom));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, theme.Hover) { TargetName = "root" });

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(hover);
        return template;
    }

    private static Thickness Space(double l, double t, double r, double b) => new(l, t, r, b);
}
