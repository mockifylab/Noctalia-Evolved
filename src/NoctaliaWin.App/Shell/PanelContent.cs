// What goes inside a pop-out panel. Three surfaces exist in this build, and each is made of
// data the platform services actually report: the calendar panel shows the real month and the
// clock's configured date format, the control-center panel reads and writes the real master
// volume, drives the system's real media controls and reports the real network and battery
// state, and the launcher lists and searches the applications Windows actually has installed.
//
// Every value here is a text label, a slider or an edit box, never an icon-font glyph:
// Noctalia's icon font is not part of this repository's licence grant. The only pictures are
// the applications' own icons, which the shell is asked for directly.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Shell;

internal static class PanelContent
{
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    /// The element tree plus the unsubscribe for its repaint subscription, and the focus
    /// request the window makes once it is active. The host detaches when the window closes,
    /// so a panel opened a hundred times does not accumulate a hundred listeners.
    public sealed record Surface(FrameworkElement Element, Action Detach, Action? Focus = null);

    /// Build the surface for a control-center or calendar panel id. Unknown ids return null so
    /// the caller can refuse to open a panel it cannot fill, rather than showing an empty card.
    ///
    /// `zoom` is the logical-pixel-to-DIP factor for this monitor and bar, and `font` adds the
    /// bar's font scale, so a bar-less panel is built from the monitor scale alone.
    public static Surface? Build(
        string panelId,
        PanelContentModel model,
        ThemeBrushes theme,
        double zoom,
        double font) => panelId switch
    {
        PanelCoordinator.CalendarPanel => Calendar(model, theme, zoom, font),
        PanelCoordinator.ControlCenterPanel => ControlCenter(model, theme, zoom, font),
        _ => null,
    };

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
        PanelContentModel model, ThemeBrushes theme, double zoom, double font)
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

        // The media controls belong to whichever app currently holds them, so this card is
        // built once and then shown, hidden and re-labelled as sessions come and go.
        var mediaTrack = Label(string.Empty, theme, StyleTokens.FontSizeCaption, font, centered: false);
        mediaTrack.TextWrapping = TextWrapping.Wrap;
        var playPause = TextButton("Play or pause the current player", "Play", _ => audio.ToggleMedia(), 0, theme, zoom, font);
        var mediaPrevious = TextButton("Previous track", "Back", _ => audio.PreviousMedia(), 0, theme, zoom, font);
        var mediaNext = TextButton("Next track", "Next", _ => audio.NextMedia(), 0, theme, zoom, font);
        var mediaButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = Space(0, StyleTokens.SpaceSm * zoom, 0, 0),
        };
        mediaButtons.Children.Add(mediaPrevious);
        mediaButtons.Children.Add(playPause);
        mediaButtons.Children.Add(mediaNext);
        var mediaCard = Card(theme, zoom, mediaTrack, mediaButtons);

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

            var media = audio.Media;
            mediaCard.Visibility = media.HasSession ? Visibility.Visible : Visibility.Collapsed;
            mediaTrack.Text = audio.MediaTrackLine ?? "Nothing playing";
            playPause.Content = media.Status == MediaPlaybackStatus.Playing ? "Pause" : "Play";
            playPause.IsEnabled = media.CanPlayPause;
            mediaPrevious.IsEnabled = media.CanPrevious;
            mediaNext.IsEnabled = media.CanNext;
        }

        model.Changed += Repaint;
        root.Children.Add(Card(theme, zoom, volumeLabel, volume, mute));
        root.Children.Add(mediaCard);
        root.Children.Add(Card(theme, zoom, network));
        root.Children.Add(Card(theme, zoom, battery));

        var note = Label(
            "Notifications and brightness need Windows APIs this build does not use yet.",
            theme, StyleTokens.FontSizeMini, font, centered: false);
        note.Opacity = 0.7;
        note.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(note);

        Repaint();
        return new Surface(Wrap(root, padding), () => model.Changed -= Repaint);
    }

    /// The launcher: a search field, the rows it matches and a status line.
    ///
    /// Ranking, paging, wrapping and what Enter does all live in LauncherModel, which is
    /// tested without a display; this draws that model's state and forwards keys to it.
    public static Surface Launcher(
        LauncherModel model,
        ThemeBrushes theme,
        double zoom,
        double font,
        bool searchOnTop,
        IconCache icons,
        Action dismiss)
    {
        var root = Stack(zoom, out var padding);

        var search = new TextBox
        {
            FontFamily = UiFont,
            FontWeight = FontWeights.Medium,
            FontSize = StyleTokens.FontSizeBody * font,
            Foreground = theme[ColorRole.OnSurface],
            CaretBrush = theme[ColorRole.OnSurface],
            // The card behind the field is drawn by its container, so the field itself stays
            // transparent rather than stacking a second panel-coloured box on it.
            Background = Brushes.Transparent,
            Template = FlatSearchTemplate(theme, zoom),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Type to search installed applications",
            Text = model.Query,
        };

        var list = new StackPanel { Orientation = Orientation.Vertical };

        // A row's picture is its height less the space around it, asked of the shell in
        // physical pixels so a scaled display gets the size it will actually draw.
        double iconSize = (PanelLayout.LauncherResultRowHeight - StyleTokens.SpaceMd) * zoom;
        int iconPx = (int)Math.Round(iconSize);

        var status = Label(string.Empty, theme, StyleTokens.FontSizeMini, font, centered: false);
        status.TextWrapping = TextWrapping.Wrap;
        status.Opacity = 0.75;
        status.Margin = Space(0, StyleTokens.SpaceSm * zoom, 0, 0);

        // A launch failure is shown rather than swallowed, and goes away as soon as the
        // query moves again.
        string? failure = null;
        search.TextChanged += (_, _) =>
        {
            failure = null;
            model.Query = search.Text;
        };

        search.PreviewKeyDown += (_, e) =>
        {
            // Tab is consumed here because WPF would otherwise treat it as focus traversal
            // and move the caret out of a panel that has nothing else to focus.
            switch (e.Key)
            {
                case Key.Down:
                    model.Move(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    model.Move(-1);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    model.Move(e.KeyboardDevice.Modifiers == ModifierKeys.Shift ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    e.Handled = true;
                    if (model.Activate() is { } error)
                    {
                        failure = error;
                        Repaint();
                    }
                    else
                    {
                        dismiss();
                    }
                    break;
            }
        };

        void Repaint()
        {
            list.Children.Clear();
            var page = model.Page();
            int selected = model.SelectedOnPage;

            for (int i = 0; i < page.Count; i++)
            {
                int row = i;
                var entry = page[i];
                bool isSelected = row == selected;

                // The application's own icon, drawn as soon as the shell has read it. The space
                // is reserved either way, so a file with no picture cannot push its name out of
                // line with every other row.
                var glyph = new Image
                {
                    Width = iconSize,
                    Height = iconSize,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = Space(0, 0, StyleTokens.SpaceSm * zoom, 0),
                    // The row is the clickable thing; a picture in it must not swallow the press.
                    IsHitTestVisible = false,
                };
                icons.Ready(entry.Entry, iconPx, source => glyph.Source = source);

                var name = new TextBlock
                {
                    FontFamily = UiFont,
                    FontWeight = FontWeights.Medium,
                    FontSize = StyleTokens.FontSizeCaption * font,
                    Foreground = theme[ColorRole.OnSurface],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                AppendMatch(name, entry.Entry.Name, entry.NamePositions, theme[ColorRole.Primary]);

                var path = new TextBlock
                {
                    FontFamily = UiFont,
                    FontSize = StyleTokens.FontSizeMini * font,
                    Foreground = theme[ColorRole.OnSurfaceVariant],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = Space(StyleTokens.SpaceMd * zoom, 0, 0, 0),
                    MaxWidth = 220 * zoom,
                };
                path.Text = entry.Entry.Exec;

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(glyph, 0);
                Grid.SetColumn(name, 1);
                Grid.SetColumn(path, 2);
                grid.Children.Add(glyph);
                grid.Children.Add(name);
                grid.Children.Add(path);

                var capsule = new Border
                {
                    Child = grid,
                    Background = isSelected ? theme[ColorRole.SurfaceVariant] : Brushes.Transparent,
                    CornerRadius = new CornerRadius(StyleTokens.RadiusSm * zoom),
                    Padding = new Thickness(StyleTokens.SpaceMd * zoom * 0.5, 0, StyleTokens.SpaceMd * zoom * 0.5, 0),
                    Height = PanelLayout.LauncherResultRowHeight * zoom,
                    Cursor = Cursors.Hand,
                    // The rows are a pointer over the list, not stops on the tab ring: the
                    // search field keeps the keyboard so typing is never one click away.
                    Focusable = false,
                };
                capsule.MouseEnter += (_, _) => capsule.Background = theme.Hover;
                capsule.MouseLeave += (_, _) =>
                    capsule.Background = row == model.SelectedOnPage
                        ? theme[ColorRole.SurfaceVariant]
                        : Brushes.Transparent;
                capsule.MouseLeftButtonUp += (_, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        model.SelectOnPage(row);
                        if (model.Activate() is { } error) failure = error;
                        else dismiss();
                    }
                    else
                    {
                        model.SelectOnPage(row);
                    }

                    search.Focus();
                    Repaint();
                };

                list.Children.Add(capsule);
            }

            status.Text = failure is { } error
                ? error
                : model.NoMatches
                    ? $"No match for \"{model.Query}\""
                    : model.Query.Length == 0
                        ? $"{model.Count} applications - start typing to filter"
                        : $"{model.Count} match{(model.Count == 1 ? "" : "es")} - Enter opens, Esc closes";
        }

        var searchCard = new Border
        {
            Height = PanelLayout.LauncherSearchRowHeight * zoom,
            Background = theme.PanelCard,
            CornerRadius = new CornerRadius(StyleTokens.RadiusMd * zoom),
            Padding = new Thickness(StyleTokens.SpaceMd * zoom * 0.6, 0, StyleTokens.SpaceMd * zoom * 0.6, 0),
            Margin = searchOnTop
                ? Space(0, 0, 0, StyleTokens.SpaceSm * zoom)
                : Space(0, StyleTokens.SpaceSm * zoom, 0, 0),
        };

        // WPF's TextBox has no placeholder, and an empty field in a launcher nobody has typed
        // in yet reads as a broken window rather than an invitation.
        var hint = new TextBlock
        {
            Text = "Search applications",
            FontFamily = UiFont,
            FontWeight = FontWeights.Medium,
            FontSize = StyleTokens.FontSizeBody * font,
            Foreground = theme[ColorRole.OnSurfaceVariant],
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        var field = new Grid();
        field.Children.Add(search);
        field.Children.Add(hint);
        searchCard.Child = field;

        void SyncHint() => hint.Visibility = search.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        search.TextChanged += (_, _) => SyncHint();
        SyncHint();

        model.Changed += Repaint;
        if (searchOnTop)
        {
            root.Children.Add(searchCard);
            root.Children.Add(list);
        }
        else
        {
            root.Children.Add(list);
            root.Children.Add(searchCard);
        }
        root.Children.Add(status);
        Repaint();

        return new Surface(
            Wrap(root, padding),
            () => model.Changed -= Repaint,
            () =>
            {
                search.Focus();
                search.SelectAll();
            });
    }

    /// The matched characters in bold and in the accent colour, the rest in the surface
    /// colour. This is what makes a fuzzy match legible instead of mysterious.
    private static void AppendMatch(TextBlock target, string text, int[] positions, Brush matched)
    {
        var hits = positions.Where(p => p >= 0 && p < text.Length).OrderBy(p => p).ToHashSet();

        // Emit maximal runs of matched / unmatched characters so the emphasis is per span,
        // not per character.
        target.Inlines.Clear();
        int start = 0;
        while (start < text.Length)
        {
            bool hit = hits.Contains(start);
            int end = start;
            while (end < text.Length && hits.Contains(end) == hit) end++;

            var piece = new Run(text[start..end]);
            if (hit)
            {
                piece.FontWeight = FontWeights.Bold;
                piece.Foreground = matched;
            }
            target.Inlines.Add(piece);
            start = end;
        }
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

    /// The search field's chrome. WPF's default TextBox template brings a Windows-forms-era
    /// white box with a focus glow that no palette can tint, so the whole template is a
    /// rounded card of the panel's own card colour with the text host inside it.
    private static ControlTemplate FlatSearchTemplate(ThemeBrushes theme, double zoom)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(StyleTokens.RadiusMd * zoom));
        border.SetValue(Border.PaddingProperty, new Thickness(StyleTokens.SpaceSm * zoom, 0, StyleTokens.SpaceSm * zoom, 0));

        // TextBox finds its editor by this exact name, so it goes through the factory
        // constructor: that is what registers it in the template's namescope.
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(FrameworkElement.FocusableProperty, false);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        border.AppendChild(host);

        return new ControlTemplate(typeof(TextBox)) { VisualTree = border };
    }

    private static Thickness Space(double l, double t, double r, double b) => new(l, t, r, b);
}
