// The settings window, laid out the way Noctalia's settings page is (src/ui/sections/
// settings_page.cpp — MIT License, Copyright (c) 2026 noctalia-dev): a search field and the
// section list down the left, grouped cards of rows on the right, and the page title giving
// way to search.
//
// What is not upstream's, and is said plainly here rather than in the UI: the rows come from
// SettingsCatalog, which lists only the keys this build can act on, and every write goes
// through SettingsEditor so a refused value never reaches config.toml.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Settings;
using NoctaliaWin.Windows;

namespace NoctaliaWin.App.Settings;

internal sealed class SettingsWindow : Window
{
    private readonly SettingsEditor _editor;
    private readonly UpdateService? _updates;
    private bool _gone;
    private ThemeBrushes _theme;
    private double _zoom;
    private double _font;

    private readonly TextBox _search;
    private readonly StackPanel _nav = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel _body = new() { Orientation = Orientation.Vertical };
    private readonly Border _banner;
    private readonly TextBlock _bannerText;

    private string _pageId = SettingsCatalog.Pages[0].Id;
    private bool _showAdvanced;

    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    public SettingsWindow(
        SettingsEditor editor, ThemeBrushes theme, double zoom, double font,
        string? page = null, UpdateService? updates = null)
    {
        _editor = editor;
        _updates = updates;
        _theme = theme;
        _zoom = zoom;
        _font = font;
        Closed += (_, _) => _gone = true;
        if (page is not null && SettingsCatalog.PageById(page) is { } requested) _pageId = requested.Id;

        Title = "Noctalia settings";
        Width = 960;
        Height = 680;
        MinWidth = 720;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Background = theme[ColorRole.Surface];
        FontFamily = UiFont;

        _bannerText = new TextBlock
        {
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * font,
            Foreground = theme[ColorRole.OnSurface],
            TextWrapping = TextWrapping.Wrap,
        };
        _banner = new Border
        {
            Child = _bannerText,
            CornerRadius = new CornerRadius(StyleTokens.RadiusMd * zoom),
            Padding = Space(StyleTokens.SpaceSm * zoom),
            Margin = Space(0, 0, 0, StyleTokens.SpaceMd * zoom),
            Visibility = Visibility.Collapsed,
        };

        _search = SettingsControls.SearchBox("Search settings", Ctx(), out var searchHost);
        _search.TextChanged += (_, _) => Render();

        var sidebar = new StackPanel { Orientation = Orientation.Vertical };
        sidebar.Children.Add(searchHost);
        sidebar.Children.Add(new ScrollViewer
        {
            Content = _nav,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = Space(0, StyleTokens.SpaceMd * zoom, 0, 0),
        });

        var column = new StackPanel { Orientation = Orientation.Vertical };
        column.Children.Add(_banner);
        column.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
        });

        var root = new Grid { Margin = Space(StyleTokens.SpaceLg * zoom) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SidebarWidth * zoom) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StyleTokens.SpaceMd * zoom) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(sidebar, 0);
        Grid.SetColumn(column, 2);
        root.Children.Add(sidebar);
        root.Children.Add(column);

        Content = root;
        Render();
    }

    /// Upstream's sidebar is 200 logical px wide; the search box sits above the list and the
    /// advanced switch rides at its foot, so the list can scroll without moving either.
    private const double SidebarWidth = 200;

    /// Re-called on every render, so a palette or scale change picked up from config.toml is
    /// reflected without rebuilding the window.
    private SettingsContext Ctx() => new(
        _theme, _zoom, _font, _editor, Committed: Render, Notice: Notify, RunAction: Run);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
            WindowThemeChrome.UseDarkCaption(source.Handle, _theme.UsesDarkCaption);

        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _search.Text.Length > 0)
        {
            _search.Clear();
            e.Handled = true;
        }

        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _search.Focus();
            _search.SelectAll();
            e.Handled = true;
        }
    }

    // ── rendering ────────────────────────────────────────────────────────────

    private void Render()
    {
        var ctx = Ctx();
        RenderNav(ctx);
        RenderBody(ctx);
    }

    private void RenderNav(SettingsContext ctx)
    {
        bool searching = Query.Length > 0;

        _nav.Children.Clear();
        foreach (var page in SettingsCatalog.Pages)
        {
            // While searching there is no one page, so the highlight goes away and every row
            // is on show at once.
            _nav.Children.Add(SettingsControls.NavButton(
                page.Title, !searching && page.Id == _pageId, ctx,
                () =>
                {
                    _pageId = page.Id;
                    // Clearing the query already repaints through the text handler, so a click
                    // on a section never renders the same page twice.
                    if (searching) _search.Clear();
                    else Render();
                }));
        }

        _nav.Children.Add(new Border { Height = StyleTokens.SpaceMd * ctx.Zoom });
        _nav.Children.Add(SettingsControls.SwitchRow(
            "show advanced", _showAdvanced, ctx,
            () => { _showAdvanced = !_showAdvanced; Render(); }));
    }

    private void RenderBody(SettingsContext ctx)
    {
        _body.Children.Clear();

        if (Query.Length > 0)
        {
            RenderResults(ctx);
        }
        else
        {
            var page = SettingsCatalog.PageById(_pageId) ?? SettingsCatalog.Pages[0];
            _body.Children.Add(Header(page.Title, ctx));
            foreach (var group in page.Groups)
            {
                var rows = group.Fields.Where(f => _showAdvanced || !f.Advanced).ToList();
                if (rows.Count == 0) continue;
                _body.Children.Add(Card(ctx, group.Title, rows.Select(f => SettingsControls.Row(f, ctx))));
            }
        }

        RenderDiagnostics(ctx);
    }

    private void RenderResults(SettingsContext ctx)
    {
        var matches = SettingsSearch.Find(Query, _showAdvanced);

        if (matches.Count == 0)
        {
            _body.Children.Add(Empty(ctx, $"Nothing matches “{Query}”."));
            return;
        }

        // Grouped by where the row lives, in catalog order, so a result reads like the page it
        // came from rather than like a search engine's list.
        foreach (var cluster in matches.GroupBy(m => (m.Page.Id, m.Group.Id)))
        {
            var first = cluster.First();
            _body.Children.Add(Card(
                ctx, first.Location, cluster.Select(m => SettingsControls.Row(m.Field, ctx))));
        }

        _body.Children.Add(new TextBlock
        {
            Text = matches.Count >= SettingsSearch.MaxResults
                ? $"{matches.Count} matches (the list stops at {SettingsSearch.MaxResults})"
                : $"{matches.Count} matches",
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
            Margin = Space(0, StyleTokens.SpaceSm * ctx.Zoom, 0, 0),
        });
    }

    /// The loader's own notes about the file on disk: a key it had to clamp or a name it did not
    /// recognise belongs in front of the person who edited it by hand.
    private void RenderDiagnostics(SettingsContext ctx)
    {
        var problems = _editor.Diagnostics.Where(d => !string.IsNullOrWhiteSpace(d.Message)).ToList();
        if (problems.Count == 0) return;

        _body.Children.Add(Card(ctx, "Config notes", problems.Select(d =>
        {
            var text = new TextBlock
            {
                Text = $"{d.Path} - {d.Message}",
                FontFamily = UiFont,
                FontSize = StyleTokens.FontSizeCaption * ctx.Font,
                TextWrapping = TextWrapping.Wrap,
                Foreground = d.IsError
                    ? ctx.Theme[ColorRole.Error]
                    : ctx.Theme[ColorRole.OnSurfaceVariant],
            };
            return (UIElement)new Border
            {
                Child = text,
                Padding = Space(0, 2 * ctx.Zoom, 0, 2 * ctx.Zoom),
            };
        })));
    }

    private static UIElement Header(string title, SettingsContext ctx) => new TextBlock
    {
        Text = title,
        FontFamily = UiFont,
        FontSize = StyleTokens.FontSizeHeader * ctx.Font,
        FontWeight = FontWeights.Bold,
        Foreground = ctx.Theme[ColorRole.OnSurface],
        Margin = Space(0, 0, 0, StyleTokens.SpaceMd * ctx.Zoom),
    };

    /// A group's rows inside one card: the palette's surface variant, mostly transparent, with
    /// the rounding upstream gives a settings group.
    private static UIElement Card(
        SettingsContext ctx, string title, IEnumerable<UIElement> rows)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeTitle * ctx.Font,
            FontWeight = FontWeights.Bold,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            Margin = Space(0, 0, 0, StyleTokens.SpaceSm * ctx.Zoom),
        });

        foreach (var row in rows) stack.Children.Add(row);

        return new Border
        {
            Child = stack,
            Background = ThemeBrushes.Alpha(ctx.Theme.Palette[ColorRole.SurfaceVariant], 0.4),
            CornerRadius = new CornerRadius(StyleTokens.RadiusLg * ctx.Zoom),
            Padding = Space(StyleTokens.SpaceSm * ctx.Zoom, StyleTokens.SpaceMd * ctx.Zoom,
                StyleTokens.SpaceSm * ctx.Zoom, StyleTokens.SpaceMd * ctx.Zoom),
            Margin = Space(0, 0, StyleTokens.SpaceLg * ctx.Zoom, StyleTokens.SpaceMd * ctx.Zoom),
        };
    }

    private static UIElement Empty(SettingsContext ctx, string text) => new Border
    {
        Child = new TextBlock
        {
            Text = text,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        },
        Background = ThemeBrushes.Alpha(ctx.Theme.Palette[ColorRole.SurfaceVariant], 0.24),
        CornerRadius = new CornerRadius(StyleTokens.RadiusLg * ctx.Zoom),
        MinHeight = 160 * ctx.Zoom,
        MinWidth = 360 * ctx.Zoom,
        Padding = Space(StyleTokens.SpaceLg * ctx.Zoom),
    };

    // ── writing ──────────────────────────────────────────────────────────────

    private string Query => _search.Text.Trim();

    private void Notify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _banner.Visibility = Visibility.Collapsed;
            return;
        }

        _bannerText.Text = message;
        _bannerText.Foreground = _theme[ColorRole.OnSurface];
        _banner.Background = ThemeBrushes.Alpha(_theme.Palette[ColorRole.Primary], 0.16);
        _banner.Visibility = Visibility.Visible;
    }

    /// The Button rows the catalog offers. Anything else answers honestly instead of doing
    /// nothing quietly.
    private string? Run(string action) => action switch
    {
        SettingsCatalog.OpenConfigFolder => OpenConfigFolder(),
        SettingsCatalog.RestoreDefaults => RestoreDefaults(),
        SettingsCatalog.CheckForUpdates => CheckForUpdates(),
        SettingsCatalog.OpenLatestRelease => _updates?.OpenLatestRelease()
            ?? "This build was started without an update source.",
        _ => $"Nothing is wired to “{action}”.",
    };

    /// Starts the check and answers straight away, because the request belongs off the UI
    /// thread. The outcome arrives later in the same banner, and the person decides what to do
    /// with it — nothing here downloads or installs.
    private string? CheckForUpdates()
    {
        if (_updates is not { } updates) return "This build was started without an update source.";

        var version = _editor.Scope().AppVersion;
        _ = CheckInBackground(updates, version);
        return $"Asking GitHub for the newest release ({updates.Repository})…";
    }

    private async Task CheckInBackground(UpdateService updates, string? version)
    {
        string message;
        try
        {
            message = (await updates.CheckAsync(version)).Message;
        }
        catch (Exception ex)
        {
            // The service swallows the network's own failures; anything reaching here is a bug,
            // and the banner is the only place a person would ever see it.
            message = $"The update check stopped unexpectedly: {ex.Message}";
        }

        if (_gone) return;
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_gone) Notify(message);
            });
        }
        catch (InvalidOperationException)
        {
            // The window's dispatcher shut down between the two checks.
        }
    }

    private string? OpenConfigFolder()
    {
        var path = _editor.Scope().ConfigPath;
        var folder = path is null ? null : Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return "The config folder does not exist yet.";

        try
        {
            // Opening the directory itself, not an `explorer.exe` command line, so a path with
            // spaces or quotes in it cannot be misread as arguments.
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            return $"Opened {folder}";
        }
        catch (Exception e)
        {
            return $"Could not open the config folder: {e.Message}";
        }
    }

    private string? RestoreDefaults()
    {
        _editor.RestoreDefaults();
        Render();
        return "Every row is back at its default.";
    }

    // ── refresh ──────────────────────────────────────────────────────────────

    /// Called when the config rebuild changed the palette or the scale, so the window follows
    /// the same theme the bar was redrawn with.
    public void Reveal(ThemeBrushes theme, double zoom, double font)
    {
        _theme = theme;
        _zoom = zoom;
        _font = font;

        Background = theme[ColorRole.Surface];
        if (PresentationSource.FromVisual(this) is HwndSource source)
            WindowThemeChrome.UseDarkCaption(source.Handle, theme.UsesDarkCaption);

        Show();
        Activate();
        Render();
    }

    private static Thickness Space(double all) => new(all);
    private static Thickness Space(double left, double top, double right, double bottom) =>
        new(left, top, right, bottom);
}
