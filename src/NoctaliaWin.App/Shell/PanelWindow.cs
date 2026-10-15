// A Noctalia pop-out panel: a borderless layered window whose painted shape is the concave
// silhouette from Core, nested against the bar's own corners.
//
// The window rect is the panel's *final* surface and never moves. The reveal animation
// translates the painted body and its content inside that rect, so the panel appears to grow
// out from behind the bar, which is how upstream does it (applyAttachedReveal). Keeping the
// window still is what lets the concave cutouts line up with the bar on every frame.
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Geometry;
using NoctaliaWin.Core.Shell;
using NoctaliaWin.Windows;
using Path = System.Windows.Shapes.Path;

namespace NoctaliaWin.App.Shell;

internal sealed class PanelWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly PanelPlacement _placement;
    private readonly ThemeBrushes _theme;
    private readonly PanelContent.Surface _content;
    private readonly Canvas _surface = new();
    private readonly TranslateTransform _reveal = new();
    private readonly DispatcherTimer _ticker;
    private readonly Stopwatch _clock = new();
    private Rect _bodyDip;
    private double _progress;
    private bool _closing;

    /// Finished sliding out. The coordinator uses this to mark the panel fully open.
    public event Action? RevealSettled;

    /// Finished sliding back, or nothing left to animate. The owner hides the window.
    public event Action? CloseSettled;

    /// Escape, or the window losing activation to another application.
    public event Action? DismissRequested;

    public PanelWindow(
        MonitorInfo monitor,
        PanelPlacement placement,
        ThemeBrushes theme,
        PanelContent.Surface content)
    {
        _monitor = monitor;
        _placement = placement;
        _theme = theme;
        _content = content;

        Title = "Noctalia panel";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;

        // Unlike the bar, a panel takes focus: Escape has to reach it, and losing activation
        // is how a click on another application dismisses it.
        ShowActivated = true;
        Focusable = true;

        double sx = Math.Max(monitor.ScaleX, 0.01);
        double sy = Math.Max(monitor.ScaleY, 0.01);
        var surface = placement.Surface;

        Width = surface.Width / sx;
        Height = surface.Height / sy;
        Left = surface.X / sx;
        Top = surface.Y / sy;

        var geometry = placement.Geometry;
        var silhouette = OutlineGeometry.Build(
            surface.Width, surface.Height,
            geometry.Radii, geometry.Corners, geometry.Insets,
            sx, sy);

        // Body and content move together; the window boundary is the clip.
        var moving = new Canvas();
        var body = OutlineGeometry.FilledPath(
            silhouette,
            theme.PanelBackground,
            placement.PanelBorders ? theme.Outline : null,
            placement.PanelBorders ? OutlineGeometry.StrokeThickness(sx) : 0);
        moving.Children.Add(body);

        // The painted body sits at the visual rect, inset from the surface by the flare.
        var bodyDip = new Rect(
            geometry.PanelInsetX / sx,
            geometry.PanelInsetY / sy,
            placement.Visual.Width / sx,
            placement.Visual.Height / sy);

        var bodyElement = content.Element;
        Canvas.SetLeft(bodyElement, bodyDip.X);
        Canvas.SetTop(bodyElement, bodyDip.Y);
        bodyElement.Width = bodyDip.Width;
        bodyElement.Height = bodyDip.Height;
        moving.Children.Add(bodyElement);
        moving.RenderTransform = _reveal;

        _surface.Children.Add(moving);
        Content = _surface;
        _bodyDip = bodyDip;

        // The content subscribes itself to the model's repaints; dropping the window drops
        // the subscription, so reopened panels never accumulate listeners.
        Closed += (_, _) => content.Detach();

        // Start collapsed so the first painted frame cannot flash the full panel.
        ApplyReveal(0);

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _ticker.Tick += OnTick;

        MouseLeftButtonDown += (_, e) =>
        {
            // A click inside the body is a click on a control. One on the transparent flare
            // around it is a click on nothing, and dismisses, which is what upstream's click
            // shield does by leaving the bar rects out of its input region.
            if (!_bodyDip.Contains(e.GetPosition(this)))
                DismissRequested?.Invoke();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DismissRequested?.Invoke();
                e.Handled = true;
            }
        };

        // Only an open window can be dismissed by losing focus: before the first Show the
        // window has nothing to lose, and dismissing then would destroy the panel mid-reveal.
        Deactivated += (_, _) =>
        {
            if (IsOpen) DismissRequested?.Invoke();
        };

        SourceInitialized += (_, _) => Place(surface);
    }

    public PxRect ScreenRect => _placement.Surface;

    public bool IsOpen { get; private set; }

    public string PanelId => _placement.PanelId;

    internal void Place(PxRect rect)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        ShellWindowChrome.Place(hwnd, rect, topMost: true);
    }

    /// Slide out from behind the bar.
    public void StartOpen()
    {
        _closing = false;
        _progress = 0;
        ApplyReveal(0);
        Show();
        IsOpen = true;
        _clock.Restart();
        _ticker.Start();
        Activate();

        // A panel that takes focus has to hand it to something: the launcher's search field
        // asks for it, and typing works on the first frame instead of the second click.
        _content.Focus?.Invoke();
    }

    /// Slide back. The window hides itself when it is done, so an interrupted animation
    /// cannot leave a half-visible panel on screen.
    public void StartClose()
    {
        if (!IsOpen) return;

        _closing = true;
        _clock.Restart();
        _ticker.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _clock.ElapsedMilliseconds;
        int duration = _placement.RevealDurationMs;

        if (!_closing)
        {
            _progress = PanelRevealMath.OpenEased(elapsed, duration);
            ApplyReveal(_progress);

            if (elapsed >= duration)
            {
                _ticker.Stop();
                ApplyReveal(1);
                RevealSettled?.Invoke();
            }

            return;
        }

        // Closing runs from wherever the panel currently is, so interrupting a reveal
        // reverses smoothly instead of jumping.
        double from = _progress;
        double t = duration <= 0 ? 1 : Math.Clamp(elapsed / (double)duration, 0, 1);
        ApplyReveal(_placement.Attached
            ? from * (1 - PanelRevealMath.EaseInOutQuad(t))
            : from * PanelRevealMath.DetachedCloseEased(elapsed, duration));

        if (t >= 1)
        {
            _ticker.Stop();
            ApplyReveal(0);
            IsOpen = false;
            Hide();
            CloseSettled?.Invoke();
        }
    }

    private void ApplyReveal(double progress)
    {
        if (!_placement.Attached)
        {
            // A floating card touches no bar, so it has no seam to grow out of. Upstream's
            // detached panels fade in place, which is what the window opacity does here.
            _reveal.X = 0;
            _reveal.Y = 0;
            Opacity = Math.Clamp(progress, 0, 1);
            return;
        }

        double sx = Math.Max(_monitor.ScaleX, 0.01);
        double sy = Math.Max(_monitor.ScaleY, 0.01);
        double widthDip = _placement.Visual.Width / sx;
        double heightDip = _placement.Visual.Height / sy;

        var reveal = PanelRevealMath.At(
            progress, widthDip, heightDip, (float)(_placement.Geometry.CornerRadius / sx), _placement.RevealDirection);

        _reveal.X = reveal.ContentOffsetX;
        _reveal.Y = reveal.ContentOffsetY;
        Opacity = reveal.HasVisibleSurface ? 1.0 : 0.0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _ticker.Stop();
        base.OnClosed(e);
    }
}
