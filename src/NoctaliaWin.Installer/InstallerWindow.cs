// The installer's window, in Noctalia's visual language: the dark palette the bar uses, the
// same radii and spacing tokens, and mint-on-charcoal for the one action on the page. Built in
// code like the rest of this port, so the tokens in Core are the only source of the look.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Installs;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Installer;

internal sealed class InstallerWindow : Window
{
    private readonly Palette _palette = BuiltinPalettes.Noctalia.Dark;
    private readonly InstallLayout _layout;
    private readonly CheckBox _startup;
    private readonly TextBlock _status;
    private readonly Button _install;
    private bool _busy;

    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    public InstallerWindow(InstallerBundle bundle)
    {
        _layout = bundle.Layout!;

        Title = "Noctalia Evolved setup";
        Width = 620;
        Height = 620;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = UiFont;
        Background = Ink(ColorRole.Surface);
        Foreground = Ink(ColorRole.OnSurface);

        var action = InstallDecision.Decide(bundle.InstalledVersion, bundle.IncomingVersion);
        var (blocked, blocker) = InstallationRunner.Probe(_layout);

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(StyleTokens.SpaceLg),
        };

        stack.Children.Add(Headline());
        stack.Children.Add(Note(
            "The Noctalia experience on Windows. This installer copies the app beside your own "
            + "files, needs no administrator, and reaches nowhere but this machine."));

        stack.Children.Add(Card(
            Line("This build", bundle.IncomingVersion ?? "version not reported", ColorRole.OnSurface),
            Line("Already installed", bundle.InstalledVersion ?? "nothing at this path", ColorRole.OnSurfaceVariant),
            Note(InstallDecision.Describe(action, _layout, bundle.InstalledVersion, bundle.IncomingVersion))));

        stack.Children.Add(Card(
            Note("Installing writes:"),
            Note($"•  Files in {_layout.InstallFolder}"),
            Note($"•  A Start Menu entry at {_layout.ShortcutPath}"),
            Note("•  This user's Run key, only if the box below is on"),
            Note("•  Nothing else, and nothing that needs an administrator")));

        _startup = new CheckBox
        {
            Content = "Start with Windows when I sign in",
            IsChecked = action is InstallAction.Fresh,
            Foreground = Ink(ColorRole.OnSurface),
            FontSize = StyleTokens.FontSizeBody,
            Margin = new Thickness(0, StyleTokens.SpaceSm, 0, StyleTokens.SpaceSm),
        };
        stack.Children.Add(_startup);

        _status = new TextBlock
        {
            Text = blocked ? blocker : "Ready. Nothing has been copied yet.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Ink(blocked ? ColorRole.Error : ColorRole.OnSurfaceVariant),
            FontSize = StyleTokens.FontSizeCaption,
            Margin = new Thickness(0, StyleTokens.SpaceSm, 0, 0),
        };
        stack.Children.Add(_status);

        _install = new Button
        {
            Content = "Install",
            IsEnabled = !blocked,
            MinHeight = StyleTokens.ControlHeight,
            Padding = new Thickness(StyleTokens.SpaceLg, 0, StyleTokens.SpaceLg, 0),
            FontSize = StyleTokens.FontSizeBody,
            FontWeight = FontWeights.SemiBold,
            Background = Ink(ColorRole.Primary),
            Foreground = Ink(ColorRole.OnPrimary),
            BorderBrush = Ink(ColorRole.Outline),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, StyleTokens.SpaceLg, 0, 0),
        };
        _install.Click += (_, _) => Run();
        stack.Children.Add(_install);

        Content = new ScrollViewer
        {
            Content = stack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
        };

        SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
                WindowThemeChrome.UseDarkCaption(source.Handle, dark: true);
        };
    }

    private void Run()
    {
        if (_busy) return;

        _busy = true;
        _install.IsEnabled = false;
        _status.Foreground = Ink(ColorRole.OnSurfaceVariant);
        _status.Text = "Copying…";

        // The copy runs off the UI thread so the window keeps painting, but nothing is retried
        // or rolled back behind the person's back: whatever the disk did is what the line says.
        Task.Run(() => InstallationRunner.Install(_layout, _startup.IsChecked == true))
            .ContinueWith(
                task =>
                {
                    _busy = false;
                    _status.Text = task.IsFaulted
                        ? $"The installer stopped: {task.Exception?.GetBaseException().Message}"
                        : task.Result;
                    _install.Content = "Install again";
                    _install.IsEnabled = !task.IsFaulted;
                },
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    private Border Headline() => new()
    {
        CornerRadius = new CornerRadius(StyleTokens.RadiusXl),
        Background = Ink(ColorRole.Primary),
        Padding = new Thickness(StyleTokens.SpaceLg),
        Margin = new Thickness(0, 0, 0, StyleTokens.SpaceMd),
        Child = new TextBlock
        {
            Text = InstallLayout.ProductName,
            FontSize = StyleTokens.FontSizeHeader,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink(ColorRole.OnPrimary),
        },
    };

    private Border Card(params UIElement[] rows)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        foreach (UIElement row in rows) panel.Children.Add(row);

        return new Border
        {
            CornerRadius = new CornerRadius(StyleTokens.RadiusLg),
            Background = Ink(ColorRole.SurfaceVariant),
            BorderBrush = Ink(ColorRole.Outline),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            Padding = new Thickness(StyleTokens.CardPadding),
            Margin = new Thickness(0, 0, 0, StyleTokens.SpaceMd),
            Child = panel,
        };
    }

    private TextBlock Line(string label, string value, ColorRole role) => new()
    {
        FontSize = StyleTokens.FontSizeCaption,
        Foreground = Ink(role),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, StyleTokens.SpaceXs),
        Inlines =
        {
            new Run(label + "  ") { FontWeight = FontWeights.SemiBold },
            new Run(value),
        },
    };

    private TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = StyleTokens.FontSizeCaption,
        Foreground = Ink(ColorRole.OnSurface),
        Margin = new Thickness(0, 0, 0, StyleTokens.SpaceXs),
    };

    private SolidColorBrush Ink(ColorRole role) => new(Color.FromRgb(
        _palette[role].R, _palette[role].G, _palette[role].B));
}
