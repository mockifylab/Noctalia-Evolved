// The setup wizard's shell: six steps, one idea each, with Go Back and a single accented action
// at the bottom right and a rail of segments at the top so it is always clear how far there is to
// go. The look comes from InstallerLook, the decisions from Core's InstallLayout / InstallDecision,
// and the writes from InstallationRunner — this file only moves between states.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Installs;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Installer;

internal sealed partial class InstallerWindow : Window
{
    private enum Step
    {
        Welcome,
        Machine,
        Destination,
        Choices,
        Installing,
        Done,
    }

    private static readonly Step[] Order =
    [
        Step.Welcome, Step.Machine, Step.Destination, Step.Choices, Step.Installing, Step.Done,
    ];

    private readonly InstallerBundle _bundle;
    private readonly InstallerLook _look;
    private readonly InstallLayout _defaultLayout;
    private readonly ContentControl _host = new();
    private readonly Border[] _segments = new Border[Order.Length];
    private readonly TextBlock _stepLabel;
    private readonly Button _back;
    private readonly Button _primary;

    private InstallLayout _layout;
    private Step _current = Step.Welcome;
    private bool _busy;
    private bool _blockedChecks;
    private bool _failed;
    private string _result = string.Empty;
    private string? _refusedFolder;

    public InstallerWindow(InstallerBundle bundle)
    {
        _bundle = bundle;
        _look = new InstallerLook(BuiltinPalettes.Noctalia.Dark);
        _defaultLayout = bundle.Layout!;
        _layout = bundle.Layout!;

        // A first install turns sign-in on; anything else leaves the switch off, so the installer
        // never re-enables something a person has since turned off themselves.
        _startWithWindows = Action is InstallAction.Fresh;

        Title = "Noctalia Evolved setup";
        Width = 640;
        Height = 600;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = InstallerLook.UiFont;
        Background = _look.Ink(ColorRole.Surface);
        Foreground = _look.Ink(ColorRole.OnSurface);

        _stepLabel = new TextBlock
        {
            FontSize = StyleTokens.FontSizeMini,
            Foreground = _look.Ink(ColorRole.OnSurfaceVariant),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _back = _look.Ghost("Go Back");
        _primary = _look.Primary("Continue");
        _primary.IsDefault = true;

        var root = new DockPanel();

        var rail = Rail();
        DockPanel.SetDock(rail, Dock.Top);
        root.Children.Add(rail);

        var footer = Footer();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        root.Children.Add(new ScrollViewer
        {
            Content = _host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
            Padding = new Thickness(StyleTokens.SpaceLg * 2, StyleTokens.SpaceLg * 2,
                StyleTokens.SpaceLg * 2, StyleTokens.SpaceLg),
        });

        Content = root;

        _back.Click += (_, _) => Back();
        _primary.Click += (_, _) => Next();

        // Escape means "back out", which is what a person expects from a wizard; on the steps
        // where going back is not a thing, it closes the window instead of doing nothing.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || _busy) return;

            if (_current is Step.Welcome or Step.Done) Close();
            else Back();
        };

        SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
                WindowThemeChrome.UseDarkCaption(source.Handle, dark: true);
        };

        Loaded += (_, _) => GoTo(Step.Welcome);
    }

    private Border Rail()
    {
        var rail = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(StyleTokens.SpaceLg * 2, StyleTokens.SpaceLg,
                StyleTokens.SpaceLg * 2, 0),
        };

        for (int i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new Border
            {
                Width = 30,
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, StyleTokens.SpaceXs, 0),
                Background = _look.Ink(ColorRole.Outline, 0.45),
            };
            rail.Children.Add(_segments[i]);
        }

        return new Border { Child = rail };
    }

    private Border Footer()
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(_back);
        buttons.Children.Add(new Border { Width = StyleTokens.SpaceMd });
        buttons.Children.Add(_primary);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_stepLabel, 0);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(_stepLabel);
        grid.Children.Add(buttons);

        return new Border
        {
            BorderBrush = _look.Ink(ColorRole.Outline, 0.4),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(StyleTokens.SpaceLg * 2, StyleTokens.SpaceMd,
                StyleTokens.SpaceLg * 2, StyleTokens.SpaceLg),
            Child = grid,
        };
    }

    private void GoTo(Step step)
    {
        _current = step;
        _host.Content = Build(step);

        int index = Array.IndexOf(Order, step);

        for (int i = 0; i < _segments.Length; i++)
        {
            _segments[i].Background = i <= index
                ? _look.Ink(ColorRole.Tertiary)
                : _look.Ink(ColorRole.Outline, 0.45);
        }

        _stepLabel.Text = $"Step {index + 1} of {Order.Length}";

        _back.Visibility = step is Step.Welcome or Step.Installing or Step.Done
            ? Visibility.Collapsed
            : Visibility.Visible;

        _primary.IsEnabled = step switch
        {
            Step.Machine => !_blockedChecks,
            Step.Installing => false,
            _ => true,
        };
        _primary.Content = step switch
        {
            Step.Choices => "Install",
            Step.Installing => "Installing…",
            Step.Done => "Close",
            _ => "Continue",
        };

        FadeIn();
        _primary.Focus();
    }

    private void FadeIn()
    {
        _host.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(StyleTokens.AnimNormalMs),
        });
    }

    private void Back()
    {
        int index = Array.IndexOf(Order, _current);
        if (index > 0) GoTo(Order[index - 1]);
    }

    private void Next()
    {
        switch (_current)
        {
            case Step.Choices:
                BeginInstall();
                break;

            case Step.Done:
                Close();
                break;

            default:
                GoTo(Order[Array.IndexOf(Order, _current) + 1]);
                break;
        }
    }

    private void BeginInstall()
    {
        if (_busy) return;
        _busy = true;

        // Read the switches here: a WPF element cannot be touched from the copy thread.
        bool startWithWindows = _startup?.IsChecked == true;
        bool openAfter = _openAfter?.IsChecked == true;

        GoTo(Step.Installing);

        var progress = new Progress<InstallProgress>(OnProgress);

        Task.Run(() => InstallationRunner.Install(_layout, startWithWindows, progress))
            .ContinueWith(
                task =>
                {
                    _busy = false;

                    if (task.IsFaulted)
                    {
                        _failed = true;
                        _result = "The installer stopped: "
                            + task.Exception?.GetBaseException().Message;
                    }
                    else
                    {
                        _failed = !task.Result.Succeeded;
                        _result = task.Result.Summary;
                    }

                    if (!_failed && openAfter) _result += "\n\n" + Launch();

                    GoTo(Step.Done);
                },
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void OnProgress(InstallProgress value)
    {
        if (_current != Step.Installing) return;

        if (_phase is not null)
        {
            _phase.Text = value.Stage switch
            {
                InstallStage.Checking => "Checking this machine…",
                InstallStage.Copying => "Copying the bundle…",
                InstallStage.Shortcut => "Adding the Start Menu entry…",
                InstallStage.Startup => "Setting up signing in…",
                _ => "Working…",
            };
        }

        if (_barFill is not null && value.Stage is InstallStage.Copying && value.Total > 0)
        {
            double fraction = Math.Clamp((double)value.Done / value.Total, 0, 1);
            _barFill.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = fraction,
                Duration = TimeSpan.FromMilliseconds(StyleTokens.AnimFastMs),
            });
        }
        else if (_barFill is not null && value.Stage is not InstallStage.Copying)
        {
            _barFill.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(StyleTokens.AnimFastMs),
            });
        }

        if (_counter is not null)
        {
            _counter.Text = value.Stage switch
            {
                InstallStage.Copying when value.Total > 0 => $"{value.Done} of {value.Total} files",
                InstallStage.Checking => "nothing copied yet",
                _ => "finishing up",
            };
        }
    }

    /// The only thing this installer ever starts, and only when the person asked for it. A failure
    /// to launch is said in the summary rather than hidden, because the files did land.
    private string Launch()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _layout.ExecutablePath,
                WorkingDirectory = _layout.InstallFolder,
                UseShellExecute = true,
            });

            return "Noctalia Evolved is starting. The bar appears at the top of your screen.";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                       or InvalidOperationException
                                       or System.Security.SecurityException)
        {
            return $"It was installed but could not be started: {ex.Message}. "
                + $"Launch {InstallLayout.ExecutableName} from the folder above.";
        }
    }

    private InstallAction Action => InstallDecision.Decide(
        InstallerBundle.InstalledVersionAt(_layout), _bundle.IncomingVersion);
}
