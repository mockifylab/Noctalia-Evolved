// Composition root: builds the platform services, then one bar per monitor.
//
// The app has no main window on purpose. Shell strips are derived from the monitor list
// and the config file, so they are created here rather than by a StartupUri, and shutdown
// is explicit (the tray's Exit) so closing one strip cannot end the session.
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NoctaliaWin.App.Design;
using NoctaliaWin.App.Settings;
using NoctaliaWin.App.Shell;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Settings;
using NoctaliaWin.Core.Shell;
using NoctaliaWin.Windows;

namespace NoctaliaWin.App;

public partial class App : Application
{
    private readonly SettingsStore _settings = new();
    private readonly WindowPlacementService _placement = new();
    private readonly SystemStatusService _status = new();
    private readonly AudioService _audio = new();
    private readonly MediaService _media = new();
    private readonly SystemThemeService _systemTheme = new();
    private readonly TrayService _tray = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly StartupService _startup = new();
    private readonly UpdateService _updates = new();

    private readonly List<BarState> _bars = [];
    private BarViewModel? _model;
    private PanelHost? _panels;
    private SettingsEditor? _editor;
    private SettingsWindow? _settingsWindow;
    private ConfigFileWatcher? _configWatch;

    // Built on first use: scanning the Start Menu costs a directory walk, and a session that
    // never opens the launcher should not pay for it at startup.
    private ApplicationDiscoveryService? _apps;
    private LauncherModel? _launcher;

    private DispatcherTimer? _pointerPoll;
    private DispatcherTimer? _slide;
    private bool _rebuildQueued;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _tray.ExitRequested += Quit;
        _tray.SetTooltip("Noctalia for Windows");

        // The tray is the launcher's other door: it opens the same panel the bar's Search
        // button does, against the primary bar's monitor.
        _tray.IconActivated += () => _panels?.ToggleLauncher(CurrentTheme());
        _tray.SettingsRequested += () => OpenSettings();

        _settings.Changed += _ => RebuildLater();
        _systemTheme.Changed += RebuildLater;

        BuildBars();
        StartPointerPolling();

        // An edit made in a text editor reaches the bar too, not only one made here. The
        // events arrive on a file-system thread, so the reload is posted to this dispatcher.
        _configWatch = new ConfigFileWatcher(_settings, run => Dispatcher.BeginInvoke(run));

        // A second door to the same window, for a person whose bar has no Settings widget in it
        // yet: `NoctaliaWin.exe --settings [section]`, where the section is one of the sidebar's
        // own ids, so a shortcut can land on the page it is about.
        int at = Array.IndexOf(e.Args, "--settings");
        if (at >= 0)
        {
            string? page = at + 1 < e.Args.Length && !e.Args[at + 1].StartsWith('-')
                ? e.Args[at + 1]
                : null;
            OpenSettings(page);
        }
    }

    /// The launcher's model, built the first time a launcher panel is actually asked for and
    /// kept across config rebuilds, because rescanning the installed applications on every
    /// settings change would throw away results the user is typing against.
    private LauncherModel RequireLauncher()
    {
        _apps ??= new ApplicationDiscoveryService();
        return _launcher ??= new LauncherModel(
            _apps, new ProcessLauncher(), action => Dispatcher.BeginInvoke(action));
    }

    /// Re-bound on every rebuild, because the binding itself is configurable and a rebuild
    /// has to be able to pick up a changed key. Unregistering first is what makes that safe:
    /// a second RegisterHotKey for the same combination would come back as "already taken",
    /// blamed on some other application.
    private void BindLauncherHotkey()
    {
        _hotkeys.UnregisterAll();

        var error = _hotkeys.Register(
            _settings.Current.Launcher.Hotkey,
            () => _panels?.ToggleLauncher(CurrentTheme()));

        // The full reason belongs in the settings window; the tray can only say that the
        // shortcut is not live, which is the part a user would otherwise have to guess at.
        _tray.SetTooltip(error is null
            ? "Noctalia for Windows"
            : "Noctalia for Windows - launcher hotkey unavailable");
    }

    /// One settings window per app: asking again raises the window that is already open rather
    /// than stacking a second editor over the same file. `page` is the optional deep link from
    /// the command line; an id the catalog does not have opens the first page.
    private void OpenSettings(string? page = null)
    {
        var config = _settings.Current;
        var theme = CurrentTheme();

        if (_settingsWindow is { } existing)
        {
            existing.Reveal(theme, config.Accessibility.UiScale, config.Accessibility.UiScale);
            return;
        }

        _editor ??= new SettingsEditor(_settings, _startup, AppVersion);

        var window = new SettingsWindow(
            _editor, theme, config.Accessibility.UiScale, config.Accessibility.UiScale, page, _updates);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, window)) _settingsWindow = null;
        };

        _settingsWindow = window;
        window.Show();
        window.Activate();
    }

    /// The version this build was compiled with, from Directory.Build.props. Read from the
    /// running assembly rather than the apphost's file metadata, because an unversioned build
    /// leaves that field holding the .NET SDK's own number, which the settings window would
    /// then show as if it were ours.
    private static string AppVersion =>
        typeof(App).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(App).Assembly.GetName().Version?.ToString(3)
        ?? "development build";

    private ThemeBrushes CurrentTheme()
    {
        var config = _settings.Current;
        var paletteSet = BuiltinPalettes.ByName(config.Theme.Builtin) ?? BuiltinPalettes.Noctalia;

        var mode = config.Theme.Mode switch
        {
            "light" => PaletteMode.Light,
            "auto" => _systemTheme.UsesLightTheme ? PaletteMode.Light : PaletteMode.Dark,
            _ => PaletteMode.Dark,
        };

        return new ThemeBrushes(
            paletteSet.For(mode),
            config.Main.BackgroundOpacity,
            config.Panels.ParsedTransparencyMode);
    }

    private void BuildBars()
    {
        CloseBars();

        var config = _settings.Current;
        var theme = CurrentTheme();

        // An open settings window is repainted with the palette the bars were just rebuilt
        // with, instead of being left showing a theme the file no longer holds.
        _settingsWindow?.Reveal(theme, config.Accessibility.UiScale, config.Accessibility.UiScale);

        _model = new BarViewModel(
            _status,
            _audio,
            _media,
            _settings,
            action => Dispatcher.BeginInvoke(action));

        _panels = new PanelHost(
            _settings,
            _model,
            () => [.. _bars.Select(b => b.Current)],
            () => _bars.Count > 0 ? _bars[0].Window : null,
            RequireLauncher);
        _panels.Changed += RaiseBars;

        BindLauncherHotkey();

        foreach (var monitor in _placement.Monitors)
        {
            var placement = BarLayout.Compute(monitor, config.Main, config.Accessibility.UiScale);
            var slots = BarLayout.ResolveSlots(config.Main);

            // The click handler needs the window it belongs to, and the window needs the
            // handler, so the window lands in `built` before any click can reach the lambda.
            BarWindow? built = null;
            var interactions = new BarInteractions(
                _panels.OpensPanel,
                (widget, point) =>
                {
                    if (built is { } bar) _panels?.ToggleFromClick(widget, bar, theme, point);
                },
                PanelCoordinator.OpensWindow,
                (widget, _) =>
                {
                    if (PanelCoordinator.OpensWindow(widget)) OpenSettings();
                });

            var window = new BarWindow(monitor, placement, slots, _model, theme, interactions);
            built = window;

            // Positioned before Show so the first painted frame is already on the right
            // monitor; WPF's DIP Left/Top cannot be trusted across mixed-DPI displays.
            window.Show();
            var state = new BarState(window);
            _bars.Add(state);

            // An auto-hidden bar starts parked outside its monitor: the state already says
            // hidden, so the window has to move there instead of staying where Show put it.
            if (!state.Shown)
            {
                state.Current = window.HiddenScreenRect;
                window.Place(state.Current);
            }

            if (PresentationSource.FromVisual(window) is HwndSource source)
                source.AddHook(WndProc);
        }

        // Nothing to show means nothing to keep the process alive visibly; the tray still
        // owns an exit path, so leave the bars alone and let the user fix the config.
        if (_bars.Count == 0)
            _tray.SetTooltip("Noctalia for Windows - no displays detected");
    }

    // WM_DISPLAYCHANGE arrives on the window's own thread, which is the UI thread here.
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == ShellWindowChrome.WmDisplayChange)
            RebuildLater();

        return IntPtr.Zero;
    }

    /// Rebuilding destroys windows, so never do it from inside a window message.
    private void RebuildLater()
    {
        if (_rebuildQueued) return;

        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _rebuildQueued = false;
            BuildBars();
        }, DispatcherPriority.Background);
    }

    private void StartPointerPolling()
    {
        // Only auto-hidden bars need the pointer, and a bar that never hides should not
        // cost a poll.
        if (_bars.All(b => !b.AutoHide)) return;

        _pointerPoll ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _pointerPoll.Tick += OnPointerPoll;
        _pointerPoll.Start();
    }

    private void OnPointerPoll(object? sender, EventArgs e)
    {
        var pointer = ShellWindowChrome.Pointer();

        foreach (var bar in _bars)
        {
            if (!bar.AutoHide) continue;

            bool onBar = pointer is { } p && BarAutoHide.Contains(bar.Window.ScreenRect, p.X, p.Y);
            bool onBand = pointer is { } q && BarAutoHide.Contains(bar.Window.TriggerScreenBand, q.X, q.Y);

            // An open panel keeps its bar on screen: the panel is drawn against the bar's
            // corners, so a bar that slid away would leave the panel hanging in space.
            bool shown = BarAutoHide.IsShown(
                autoHide: true, onBar, onBand, panelOpen: _panels?.KeepsBarShown ?? false);
            if (shown == bar.Shown) continue;

            // Re-targeting mid-slide is allowed: the new slide starts from wherever the
            // window currently is, so a quick in-and-out flick never teleports it.
            bar.Shown = shown;
            bar.From = bar.Current;
            bar.To = shown ? bar.Window.ScreenRect : bar.Window.HiddenScreenRect;
            bar.StartedAt = DateTimeOffset.Now;
            bar.Animating = true;
        }

        if (_bars.Any(b => b.Animating)) StartSlide();
    }

    private void StartSlide()
    {
        if (_slide is not null) return;

        _slide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _slide.Tick += OnSlideTick;
        _slide.Start();
    }

    private void OnSlideTick(object? sender, EventArgs e)
    {
        foreach (var bar in _bars)
        {
            if (!bar.Animating) continue;

            double progress = (DateTimeOffset.Now - bar.StartedAt).TotalMilliseconds
                / StyleTokens.AnimNormalMs;

            bar.Current = BarAutoHide.Interpolate(bar.From, bar.To, progress);
            bar.Window.Place(bar.Current);

            if (progress < 1) continue;

            bar.Animating = false;
            bar.Current = bar.To;
            bar.Window.Place(bar.To);
        }

        if (_bars.All(b => !b.Animating) && _slide is not null)
        {
            _slide.Tick -= OnSlideTick;
            _slide.Stop();
            _slide = null;
        }
    }

    private void CloseBars()
    {
        if (_panels is not null)
        {
            _panels.Changed -= RaiseBars;
            _panels.Dispose();
            _panels = null;
        }

        foreach (var bar in _bars)
            bar.Window.Close();

        _bars.Clear();
        _model?.Dispose();
        _model = null;
    }

    /// Put every bar back on top of its own z-band. A panel places itself topmost when it
    /// opens, so the bars have to be re-raised after it or the reveal shows no seam at all.
    private void RaiseBars()
    {
        foreach (var bar in _bars)
            bar.Window.Place(bar.Current);
    }

    private void Quit()
    {
        _pointerPoll?.Stop();
        _slide?.Stop();
        _configWatch?.Dispose();
        CloseBars();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _launcher?.Dispose();
        _hotkeys.Dispose();
        _status.Dispose();
        _audio.Dispose();
        _media.Dispose();
        _systemTheme.Dispose();
        _tray.Dispose();
        base.OnExit(e);
    }

    /// Mutable per-window auto-hide state, kept out of the window so the window stays a
    /// renderer.
    private sealed class BarState(BarWindow window)
    {
        public BarWindow Window { get; } = window;
        public bool AutoHide { get; } = window.AutoHide;

        /// The state the window is being driven toward, so a mid-slide reversal is exact.
        public bool Shown { get; set; } = !window.AutoHide;
        public bool Animating { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public PxRect Current { get; set; } = window.ScreenRect;
        public PxRect From { get; set; } = window.ScreenRect;
        public PxRect To { get; set; } = window.ScreenRect;
    }
}
