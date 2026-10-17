// The six pages of the setup wizard. Each one carries a single idea and says what it is about to
// do in plain sentences, so nobody has to guess what a button will change. Every path and version
// shown here is read from disk by Core's InstallLayout / InstallDecision and InstallationRunner,
// never typed in — a wizard that lies about where files go is worse than no wizard.
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Installs;

namespace NoctaliaWin.Installer;

internal sealed partial class InstallerWindow
{
    private CheckBox? _startup;
    private CheckBox? _openAfter;
    private bool _startWithWindows;
    private bool _openWhenDone = true;

    private TextBlock? _phase;
    private TextBlock? _counter;
    private ScaleTransform? _barFill;

    private StackPanel Build(Step step) => step switch
    {
        Step.Welcome => WelcomeStep(),
        Step.Machine => MachineStep(),
        Step.Destination => DestinationStep(),
        Step.Choices => ChoicesStep(),
        Step.Installing => InstallingStep(),
        _ => DoneStep(),
    };

    // ── 1. what this is ────────────────────────────────────────────────────────

    private StackPanel WelcomeStep()
    {
        int files = InstallationRunner.FileCount(_layout.PayloadFolder);

        var panel = Stack();
        panel.Children.Add(_look.Tile(62));
        panel.Children.Add(_look.Display(InstallLayout.ProductName));
        panel.Children.Add(_look.Lead("The Noctalia experience on Windows."));
        panel.Children.Add(_look.Body(
            $"Version {_bundle.IncomingVersion ?? "no version reported"} installs for this user "
            + "only. No administrator, no network, and nothing outside the folders this wizard "
            + "shows you before it writes."));
        panel.Children.Add(_look.Caption(
            $"{files} files travel with this setup, and nothing else does."));
        return panel;
    }

    // ── 2. is this machine ready ───────────────────────────────────────────────

    private StackPanel MachineStep()
    {
        var checks = InstallationRunner.Checks(_layout);
        _blockedChecks = checks.Any(check => !check.Passed);

        var rows = checks.Select(CheckRow).ToArray();

        var panel = Stack();
        panel.Children.Add(_look.Display("Your machine"));
        panel.Children.Add(_look.Lead(
            "Three things have to be true before anything is copied. Nothing has been written yet."));
        panel.Children.Add(_look.Card(rows));

        if (_blockedChecks)
        {
            var problem = _look.Body(
                "Continue stays where it is until the line above is sorted out. The installer "
                + "will not stop a running program or install a runtime behind your back.");
            problem.Foreground = _look.Ink(ColorRole.Error);
            panel.Children.Add(problem);
        }

        return panel;
    }

    private Grid CheckRow(InstallCheck check)
    {
        var mark = new TextBlock
        {
            Text = check.Passed ? "✓" : "!",
            FontSize = StyleTokens.FontSizeBody,
            FontWeight = FontWeights.SemiBold,
            Foreground = _look.Ink(check.Passed ? ColorRole.Tertiary : ColorRole.Error),
            Width = 22,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var detail = new TextBlock
        {
            Text = check.Detail,
            FontSize = StyleTokens.FontSizeMini,
            Foreground = _look.Ink(check.Passed ? ColorRole.OnSurfaceVariant : ColorRole.Error),
            TextWrapping = TextWrapping.Wrap,
        };

        var text = new StackPanel { Orientation = Orientation.Vertical };
        text.Children.Add(_look.Named(check.Label, check.Passed ? "ready" : "not ready",
            ColorRole.OnSurface));
        text.Children.Add(detail);

        var grid = new Grid { Margin = new Thickness(0, 0, 0, StyleTokens.SpaceMd) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(mark, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(mark);
        grid.Children.Add(text);
        return grid;
    }

    // ── 3. where it goes ───────────────────────────────────────────────────────

    private StackPanel DestinationStep()
    {
        bool custom = !string.Equals(_layout.InstallFolder, _defaultLayout.InstallFolder,
            StringComparison.OrdinalIgnoreCase);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, StyleTokens.SpaceSm, 0, 0),
        };

        var change = _look.Ghost("Change folder…");
        change.Click += (_, _) => ChooseFolder();
        actions.Children.Add(change);

        if (custom)
        {
            var reset = _look.Ghost("Use the default");
            reset.Click += (_, _) =>
            {
                _layout = _defaultLayout;
                _refusedFolder = null;
                GoTo(Step.Destination);
            };
            reset.Margin = new Thickness(StyleTokens.SpaceSm, 0, 0, 0);
            actions.Children.Add(reset);
        }

        var folder = _look.Card(
            _look.Caption(custom ? "Chosen folder" : "Default folder"),
            _look.PathText(_layout.InstallFolder),
            actions);

        var written = _look.Card(
            _look.Caption("It also writes"),
            _look.Body($"A Start Menu entry, so the app is findable by name:"),
            _look.PathText(_layout.ShortcutPath),
            _look.Body(
                "One value in this user's Run key, and only if the switch two steps from here "
                + "is on."),
            _look.Body("Nothing else. The machine-wide folders are never touched."));

        string decision = InstallDecision.Describe(Action, _layout,
            InstallerBundle.InstalledVersionAt(_layout), _bundle.IncomingVersion);

        var decisionText = _look.Body(decision);
        if (Action is InstallAction.Downgrade or InstallAction.Undetermined)
            decisionText.Foreground = _look.Ink(ColorRole.Error);

        var panel = Stack();
        panel.Children.Add(_look.Display("Where it goes"));
        panel.Children.Add(_look.Lead(
            "The files stay inside your own user folders, which is why this setup never asks for "
            + "an administrator. Choose another folder if you would rather they sat elsewhere."));
        panel.Children.Add(folder);

        if (_refusedFolder is not null)
        {
            var problem = _look.Body(_refusedFolder);
            problem.Foreground = _look.Ink(ColorRole.Error);
            panel.Children.Add(problem);
            _refusedFolder = null;
        }

        panel.Children.Add(written);
        panel.Children.Add(decisionText);
        return panel;
    }

    private void ChooseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where " + InstallLayout.ProductName + " goes",
            Multiselect = false,
            InitialDirectory = Directory.Exists(_layout.InstallFolder)
                ? _layout.InstallFolder
                : Path.GetDirectoryName(_layout.InstallFolder),
        };

        if (dialog.ShowDialog(this) != true) return;

        string? problem = InstallDecision.CheckInstallFolder(dialog.FolderName, _layout.PayloadFolder);
        if (problem is null) problem = InstallLayout.ForFolder(_layout.PayloadFolder, dialog.FolderName)
            is { } chosen ? null : "That folder could not be used.";

        if (problem is not null)
        {
            // The refusal is the whole answer: the folder on screen stays the one that would be
            // written to, and nothing is half-applied.
            _refusedFolder = problem;
            GoTo(Step.Destination);
            return;
        }

        _layout = InstallLayout.ForFolder(_layout.PayloadFolder, dialog.FolderName)!;
        GoTo(Step.Destination);
    }

    // ── 4. the two switches ────────────────────────────────────────────────────

    private StackPanel ChoicesStep()
    {
        _startup = _look.Toggle(
            "Start with Windows when I sign in",
            "One value in this user's Run key. The app's own Windows page turns it back off.",
            _startWithWindows);
        _startup.Checked += (_, _) => _startWithWindows = true;
        _startup.Unchecked += (_, _) => _startWithWindows = false;

        _openAfter = _look.Toggle(
            "Open " + InstallLayout.ProductName + " when the install finishes",
            "The bar appears at the top of the screen; its tray icon is how you quit it.",
            _openWhenDone);
        _openAfter.Checked += (_, _) => _openWhenDone = true;
        _openAfter.Unchecked += (_, _) => _openWhenDone = false;

        var panel = Stack();
        panel.Children.Add(_look.Display("Choices"));
        panel.Children.Add(_look.Lead(
            "Both of these are settings the app keeps as well, so either one can change later "
            + "without reinstalling."));
        panel.Children.Add(_look.Card(_startup, _look.Caption(string.Empty), _openAfter));
        panel.Children.Add(_look.Caption(
            "Neither switch reaches outside this user's own folders."));
        return panel;
    }

    // ── 5. the copy itself ─────────────────────────────────────────────────────

    private StackPanel InstallingStep()
    {
        _phase = _look.Body("Checking this machine…");
        _counter = _look.Caption("nothing copied yet");
        _barFill = new ScaleTransform(0, 1);

        var fill = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = _look.Ink(ColorRole.Tertiary),
            RenderTransform = _barFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var track = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = _look.Ink(ColorRole.SurfaceVariant, 0.9),
            BorderBrush = _look.Ink(ColorRole.Outline, 0.5),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, StyleTokens.SpaceSm),
            Child = fill,
        };

        var panel = Stack();
        panel.Children.Add(_look.Display("Installing"));
        panel.Children.Add(_look.Lead(
            "Copying now. The work happens in the background, so this window keeps painting and "
            + "the lines below say what has actually been written."));
        panel.Children.Add(track);
        panel.Children.Add(_phase);
        panel.Children.Add(_counter);
        return panel;
    }

    // ── 6. what happened ──────────────────────────────────────────────────────

    private StackPanel DoneStep()
    {
        var panel = Stack();
        panel.Children.Add(_look.Display(_failed
            ? "Installation stopped"
            : InstallLayout.ProductName + " is installed"));
        panel.Children.Add(_look.Lead(_failed
            ? "The lines below say exactly what happened. Nothing was retried or rolled back "
              + "behind your back."
            : "Here is what it wrote, and how to take it back."));

        panel.Children.Add(_look.Card(ResultRows().ToArray()));

        if (_failed)
        {
            var retry = _look.Ghost("Go back");
            retry.Click += (_, _) => GoTo(Step.Choices);
            retry.Margin = new Thickness(0, 0, 0, StyleTokens.SpaceSm);
            panel.Children.Add(retry);
            return panel;
        }

        var open = _look.Ghost("Open the folder");
        open.Click += (_, _) => OpenFolder(open);
        open.Margin = new Thickness(0, 0, 0, StyleTokens.SpaceSm);
        panel.Children.Add(open);

        panel.Children.Add(_look.Caption(
            "To remove it: quit " + InstallLayout.ProductName + " from its tray icon, delete the "
            + "folder and the Start Menu entry above, and turn off Start with Windows on the "
            + "app's Windows settings page."));
        return panel;
    }

    private IEnumerable<UIElement> ResultRows()
    {
        foreach (string line in _result.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return new Border { Height = StyleTokens.SpaceSm };
                continue;
            }

            yield return IsPathLine(line) ? _look.PathText(line.Trim()) : _look.Body(line.Trim());
        }
    }

    private static bool IsPathLine(string line) =>
        line.Trim().Length > 3 && char.IsLetter(line.TrimStart()[0]) && line.TrimStart()[1] == ':';

    private void OpenFolder(Button button)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _layout.InstallFolder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                       or InvalidOperationException)
        {
            button.Content = "The folder could not be opened";
            button.IsEnabled = false;
        }
    }

    private StackPanel Stack() => new() { Orientation = Orientation.Vertical };
}
