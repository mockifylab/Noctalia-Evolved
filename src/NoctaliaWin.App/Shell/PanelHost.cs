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
    private PanelWindow? _window;

    /// The coordinator or the window moved, so the shell re-checks auto-hide and puts the
    /// bars back above the panel they just grew out of.
    public event Action? Changed;

    public PanelHost(SettingsStore settings, BarViewModel model, Func<IReadOnlyList<PxRect>> barRects)
    {
        _settings = settings;
        _content = new PanelContentModel(model);
        _barRects = barRects;
        _coordinator.Changed += () => Changed?.Invoke();
    }

    public bool OpensPanel(string widget) => PanelCoordinator.OpensPanel(widget);

    /// An attached panel hangs off its bar, so that bar must not slide away underneath it.
    /// The window outlives the coordinator's Closed phase while it animates out, and a panel
    /// still on screen is still a panel the bar has to stay visible for.
    public bool KeepsBarShown => _coordinator.KeepsBarShown || _window is not null;

    public string? ActiveId => _coordinator.ActiveId;

    /// `clickPx` is where the widget was pressed, in physical screen pixels.
    public void ToggleFromClick(string widget, BarWindow bar, ThemeBrushes theme, Point clickPx)
    {
        var config = _settings.Current;
        string panelId = PanelCoordinator.PanelFor(widget);

        // Only the control center has a placement setting; the clock popup is a bar-attached
        // panel in every upstream style this port models.
        bool attached = panelId != PanelCoordinator.ControlCenterPanel
            || config.Panels.ControlCenterIsAttached;

        double width = panelId == PanelCoordinator.CalendarPanel
            ? PanelLayout.CalendarLogicalWidth
            : PanelLayout.ControlCenterLogicalWidth(config.ControlCenter);
        double height = panelId == PanelCoordinator.CalendarPanel
            ? PanelLayout.CalendarLogicalHeight
            : PanelLayout.ControlCenterLogicalHeight;

        // A panel that follows the click is positioned around the press point; without it the
        // panel centres itself against the bar, which is what "auto" means upstream.
        bool followsClick = panelId == PanelCoordinator.CalendarPanel
            || config.Panels.OpenNearClickControlCenter;
        PxRect? anchor = attached && followsClick
            ? new PxRect((int)Math.Round(clickPx.X), (int)Math.Round(clickPx.Y), 1, 1)
            : null;

        var placement = attached
            ? PanelLayout.Compute(bar.Monitor, bar.Placement, config.Panels, panelId, width, height, anchor)
            : PanelLayout.ComputeFloating(bar.Monitor, config.Panels, config.ControlCenter, panelId, bar.Placement);

        var surface = PanelContent.Build(panelId, _content, theme, bar.Placement, bar.Monitor);
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

        var window = new PanelWindow(bar.Monitor, placement, theme, surface);
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
