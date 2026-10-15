// Composition root: builds the platform services, then one bar per monitor.
//
// The app has no main window on purpose. Shell strips are derived from the monitor list
// and the config file, so they are created here rather than by a StartupUri, and shutdown
// is explicit (the tray's Exit) so closing one strip cannot end the session.
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NoctaliaWin.App.Design;
using NoctaliaWin.App.Shell;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Shell;
using NoctaliaWin.Windows;

namespace NoctaliaWin.App;

public partial class App : Application
{
    private readonly SettingsStore _settings = new();
    private readonly WindowPlacementService _placement = new();
    private readonly SystemStatusService _status = new();
    private readonly AudioService _audio = new();
    private readonly SystemThemeService _systemTheme = new();
    private readonly TrayService _tray = new();

    private readonly List<BarState> _bars = [];
    private BarViewModel? _model;
    private DispatcherTimer? _pointerPoll;
    private DispatcherTimer? _slide;
    private bool _rebuildQueued;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _tray.ExitRequested += Quit;
        _tray.SetTooltip("Noctalia for Windows");

        // Left-clicking the tray icon has nothing to reopen until the launcher and settings
        // windows exist, so IconActivated is deliberately unwired rather than half-wired.
        _settings.Changed += _ => RebuildLater();
        _systemTheme.Changed += RebuildLater;

        BuildBars();
        StartPointerPolling();
    }

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

        return new ThemeBrushes(paletteSet.For(mode), config.Main.BackgroundOpacity);
    }

    private void BuildBars()
    {
        CloseBars();

        var config = _settings.Current;
        var theme = CurrentTheme();
        _model = new BarViewModel(
            _status,
            _audio,
            _settings,
            action => Dispatcher.BeginInvoke(action));

        foreach (var monitor in _placement.Monitors)
        {
            var placement = BarLayout.Compute(monitor, config.Main, config.Accessibility.UiScale);
            var window = new BarWindow(monitor, placement, BarLayout.ResolveSlots(config.Main), _model, theme);

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

            // panelOpen stays false until the pop-out panels exist.
            bool shown = BarAutoHide.IsShown(autoHide: true, onBar, onBand, panelOpen: false);
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
        foreach (var bar in _bars)
            bar.Window.Close();

        _bars.Clear();
        _model?.Dispose();
        _model = null;
    }

    private void Quit()
    {
        _pointerPoll?.Stop();
        _slide?.Stop();
        CloseBars();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _status.Dispose();
        _audio.Dispose();
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
