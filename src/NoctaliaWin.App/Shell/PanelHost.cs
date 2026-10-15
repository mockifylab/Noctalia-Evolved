// Owns the pop-out panel windows. The decision of what is open, what replaces what and what
// keeps the bar on screen lives in NoctaliaWin.Core's PanelCoordinator; this class only turns
// that decision into windows, because a panel cannot be tested as a window.
//
// One panel exists at a time. Opening a second one destroys the first with no close
// animation, which is upstream's behaviour, and a panel whose surface this build cannot fill
// is refused rather than shown empty.
using System.Windows;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Shell;

internal sealed class PanelHost : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly PanelContentModel _content;
    private readonly PanelCoordinator _coordinator = new();
    private readonly Func<IReadOnlyList<PxRect>> _barRects;
    private readonly Func<BarWindow?> _primaryBar;
    private readonly Func<LauncherModel> _launcher;
    private PanelWindow? _window;

    /// The coordinator or the window moved, so the shell re-checks auto-hide and puts the
    /// bars back above the panel they just grew out of.
    public event Action? Changed;

    /// `primaryBar` and `launcher` are functions because the launcher's application catalog
    /// costs a directory scan: building a bar must not pay for a panel nobody has opened.
    public PanelHost(
        SettingsStore settings,
        BarViewModel model,
        Func<IReadOnlyList<PxRect>> barRects,
        Func<BarWindow?> primaryBar,
        Func<LauncherModel> launcher)
    {
        _settings = settings;
        _content = new PanelContentModel(model);
        _barRects = barRects;
        _primaryBar = primaryBar;
        _launcher = launcher;
        _coordinator.Changed += () => Changed?.Invoke();
    }

    public bool OpensPanel(string widget) => PanelCoordinator.OpensPanel(widget);

    /// An attached panel hangs off its bar, so that bar must not slide away underneath it.
    /// The window outlives the coordinator's Closed phase while it animates out, and a panel
    /// still on screen is still a panel the bar has to stay visible for.
    public bool KeepsBarShown => _coordinator.KeepsBarShown || _window is not null;

    public string? ActiveId => _coordinator.ActiveId;

    /// `clickPx` is where the widget was pressed, in physical screen pixels.
    public void ToggleFromClick(string widget, BarWindow bar, ThemeBrushes theme, Point clickPx) =>
        Toggle(PanelCoordinator.PanelFor(widget), theme, bar, clickPx);

    /// Open the launcher from anywhere that is not the bar - the tray icon, a future hotkey.
    /// It is the same panel with the same one-open rule, so it closes an open control center
    /// instead of stacking a second window on it.
    public void ToggleLauncher(ThemeBrushes theme) =>
        Toggle(PanelCoordinator.LauncherPanel, theme, _primaryBar(), null);

    private void Toggle(string panelId, ThemeBrushes theme, BarWindow? bar, Point? clickPx)
    {
        // A panel is placed against a monitor, and the monitor comes from a bar. With no bar
        // there is no display to put the panel on, so the request is dropped rather than
        // guessed at.
        if (bar is not { } source) return;

        var config = _settings.Current;

        bool attached = panelId switch
        {
            PanelCoordinator.ControlCenterPanel => config.Panels.ControlCenterIsAttached,
            PanelCoordinator.LauncherPanel => config.Panels.LauncherIsAttached,
            // The clock popup has no placement key upstream: it always hangs off the bar.
            _ => true,
        };

        var (logicalWidth, logicalHeight) =
            PanelLayout.LogicalSize(panelId, config.ControlCenter, config.Launcher);

        // A panel that follows the click is positioned around the press point; without it the
        // panel centres itself against the bar, which is what "auto" means upstream.
        bool followsClick = panelId switch
        {
            PanelCoordinator.CalendarPanel => true,
            PanelCoordinator.ControlCenterPanel => config.Panels.OpenNearClickControlCenter,
            PanelCoordinator.LauncherPanel => config.Panels.OpenNearClickLauncher,
            _ => false,
        };
        PxRect? anchor = attached && followsClick && clickPx is { } click
            ? new PxRect((int)Math.Round(click.X), (int)Math.Round(click.Y), 1, 1)
            : null;

        var placement = attached
            ? PanelLayout.Compute(source.Monitor, source.Placement, config.Panels, panelId,
                logicalWidth, logicalHeight, anchor)
            : PanelLayout.ComputeFloating(source.Monitor, config.Panels, config.ControlCenter,
                config.Launcher, panelId, source.Placement);

        double zoom = BarSurface.Zoom(source.Placement, source.Monitor);
        double font = source.Placement.FontScale * zoom;

        var surface = panelId == PanelCoordinator.LauncherPanel
            ? PanelContent.Launcher(_launcher(), theme, zoom, font, config.Launcher.SearchOnTop,
                () => Close(animated: true))
            : PanelContent.Build(panelId, _content, theme, zoom, font);

        if (surface is null)
        {
            // Nothing to show for this id. Refusing is honest; an empty card is not.
            return;
        }

        if (_coordinator.ActiveId == panelId && _coordinator.Phase != PanelPhase.Closed)
        {
            _coordinator.Toggle(new PanelSurface(panelId, placement.Surface, placement.Visual,
                placement.RevealDirection, placement.Geometry.CornerRadius), _barRects());
            Close(animated: true);
            return;
        }
        // Replacing a different panel: the old window goes now, unanimated, so the new one can
        // own the surface.
        if (_window is { } previous)
        {
            _window = null;
            previous.Close();
        }

        var window = new PanelWindow(source.Monitor, placement, theme, surface);
        window.RevealSettled += _coordinator.FinishOpen;
        window.DismissRequested += () => Close(animated: true);
        window.CloseSettled += () =>
        {
            if (!ReferenceEquals(_window, window)) return;
            _window = null;
            _coordinator.FinishClose();
            window.Close();
            Changed?.Invoke();
        };

        _coordinator.Open(new PanelSurface(panelId, placement.Surface, placement.Visual,
            placement.RevealDirection, placement.Geometry.CornerRadius), _barRects());

        _window = window;
        window.StartOpen();

        // Placed after Show, so the bars have to be re-raised: the panel reveals from behind
        // them, and a panel that paints over its own bar shows no seam at all.
        Changed?.Invoke();
    }

    /// Close without animation, for a config rebuild or display change where the windows are
    /// about to be thrown away anyway.
    public void CloseNow()
    {
        _coordinator.CloseImmediately();
        if (_window is { } window)
        {
            _window = null;
            window.Close();
        }
    }

    private void Close(bool animated)
    {
        if (!animated || _window is not { IsOpen: true } window)
        {
            CloseNow();
            return;
        }

        _coordinator.BeginClose();
        window.StartClose();
    }

    public void Dispose()
    {
        CloseNow();
        _content.Dispose();
    }
}
