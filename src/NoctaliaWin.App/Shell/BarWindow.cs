// The Noctalia bar as a borderless, per-monitor window: a concave silhouette plus three
// widget slots. Geometry comes entirely from NoctaliaWin.Core; this class only turns it
// into visual elements and keeps them repainted.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Shell;
using NoctaliaWin.Windows;
using Path = System.Windows.Shapes.Path;

namespace NoctaliaWin.App.Shell;

internal sealed class BarWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly BarPlacement _placement;
    private readonly BarWidgets _slots;
    private readonly BarViewModel _model;
    private readonly ThemeBrushes _theme;
    private readonly Canvas _surface = new();
    private readonly List<WidgetView> _views = [];
    private readonly DispatcherTimer _tick;
    private readonly BarInteractions? _interactions;

    public BarWindow(
        MonitorInfo monitor,
        BarPlacement placement,
        BarWidgets slots,
        BarViewModel model,
        ThemeBrushes theme,
        BarInteractions? interactions = null)
    {
        _monitor = monitor;
        _placement = placement;
        _slots = slots;
        _model = model;
        _theme = theme;
        _interactions = interactions;

        Title = "Noctalia bar";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        // A shell strip must never take focus from whatever the user is doing.
        ShowActivated = false;
        Focusable = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // "top" and "overlay" both sit above ordinary windows; the distinction only becomes
        // observable once panels exist to be layered under.
        Topmost = true;

        double sx = Math.Max(monitor.ScaleX, 0.01);
        double sy = Math.Max(monitor.ScaleY, 0.01);
        Width = placement.Surface.Width / sx;
        Height = placement.Surface.Height / sy;

        // These DIP estimates only cover the first frame; the exact physical rectangle is
        // applied in PlaceOnScreen so a monitor above 100% cannot round the bar out of place.
        Left = placement.Surface.X / sx;
        Top = placement.Surface.Y / sy;

        var silhouette = new Path
        {
            Fill = theme.BarBackground,
            Stroke = theme.Outline,
            StrokeThickness = BarSurface.OutlineThickness(monitor),
            Data = BarSurface.Silhouette(placement, monitor),
        };
        _surface.Children.Add(silhouette);

        Content = _surface;
        BuildWidgets();

        _model.Changed += Repaint;
        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => Repaint();
        _tick.Start();

        SourceInitialized += (_, _) => Place(_placement.Surface);
    }

    /// Where the window sits, in physical pixels.
    public PxRect ScreenRect => _placement.Surface;

    /// The strip the panel nests against, and the display it belongs to.
    public BarPlacement Placement => _placement;

    public MonitorInfo Monitor => _monitor;

    public bool AutoHide => _placement.AutoHide;

    /// The rectangle the window slides to when auto-hidden: fully outside its monitor.
    public PxRect HiddenScreenRect => BarAutoHide.HiddenRect(_placement, _monitor);

    /// Thin strip against the docked edge that reveals the bar on pointer entry.
    public PxRect TriggerScreenBand => BarAutoHide.TriggerBand(_placement, _monitor);

    internal void Place(PxRect rect)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // A refused placement leaves the bar where it was. It must never close the window:
        // a mis-placed bar is recoverable, an unusable desktop is not.
        ShellWindowChrome.Place(hwnd, rect, topMost: true);
    }

    private void Repaint()
    {
        foreach (var view in _views)
            view.Update();
    }

    private void BuildWidgets()
    {
        bool vertical = _placement.Position is "left" or "right";
        var body = BarSurface.ToLogical(_placement.Body, _placement, _monitor);
        double padding = _placement.Padding / Math.Max(_monitor.ScaleX, 0.01);

        var content = new Grid
        {
            Width = body.Width,
            Height = body.Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Canvas.SetLeft(content, body.X);
        Canvas.SetTop(content, body.Y);

        if (vertical)
        {
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        else
        {
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        _surface.Children.Add(content);

        AddSlot(content, 0, _slots.Start, vertical, padding, alignCenter: false);
        AddSlot(content, 1, _slots.Center, vertical, padding, alignCenter: true);
        AddSlot(content, 2, _slots.End, vertical, padding, alignCenter: false);
    }

    private void AddSlot(Grid content, int slot, IReadOnlyList<string> widgets, bool vertical, double padding, bool alignCenter)
    {
        var panel = new StackPanel
        {
            Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
            Margin = EdgeMargin(slot, padding, vertical),
            HorizontalAlignment = vertical || alignCenter ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = !vertical || alignCenter ? VerticalAlignment.Center : VerticalAlignment.Top,
        };

        foreach (var widget in widgets)
        {
            var view = BarWidgetView.Create(widget, _model, _placement, _monitor, _theme, vertical, _interactions);
            if (view is null) continue; // no Windows backing, or no data on this machine

            panel.Children.Add(view.Element);
            _views.Add(view);
        }

        if (vertical) Grid.SetRow(panel, slot);
        else Grid.SetColumn(panel, slot);

        content.Children.Add(panel);
    }

    private static Thickness EdgeMargin(int slot, double padding, bool vertical) => slot switch
    {
        // The centre slot is centred by its star-sized cell, so it carries no edge padding.
        0 => vertical ? new Thickness(0, padding, 0, 0) : new Thickness(padding, 0, 0, 0),
        2 => vertical ? new Thickness(0, 0, 0, padding) : new Thickness(0, 0, padding, 0),
        _ => new Thickness(0),
    };

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        _model.Changed -= Repaint;
        base.OnClosed(e);
    }
}
