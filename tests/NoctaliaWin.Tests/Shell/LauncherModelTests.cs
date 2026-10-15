// The launcher's search state, selection and paging, decided without a window.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Shell;

public sealed class LauncherModelTests
{
    private sealed class FakeApps : IApplicationDiscoveryService
    {
        public List<AppEntry> Entries = [];
        public event Action? Changed;
        public IReadOnlyList<AppEntry> Applications => Entries;
        public void Refresh() => Changed?.Invoke();
        public void Raise() => Changed?.Invoke();
    }

    private sealed class FakeLauncher : IProcessLauncher
    {
        public List<AppEntry> Launched = [];
        public string? Error { get; set; }
        public string? Launch(AppEntry entry)
        {
            Launched.Add(entry);
            return Error;
        }
    }

    private static AppEntry Entry(string name, string exec = "") =>
        new(name, exec.Length > 0 ? exec : $"C:\\Windows\\System32\\{name}.exe", null, null, "desktop");

    private static (LauncherModel Model, FakeApps Apps, FakeLauncher Launcher) Build(params string[] names)
    {
        var apps = new FakeApps();
        apps.Entries.AddRange(names.Select(name => Entry(name)));
        var launcher = new FakeLauncher();
        return (new LauncherModel(apps, launcher, action => action()), apps, launcher);
    }

    [Fact]
    public void EmptyQueryListsEverythingAlphabetically()
    {
        var (model, _, _) = Build("Zebra", "Calculator", "Notepad");

        var page = model.Page();

        Assert.Equal(3, model.Count);
        Assert.Equal(new[] { "Calculator", "Notepad", "Zebra" }, page.Select(r => r.Entry.Name));
    }

    [Fact]
    public void QueryFiltersToNameMatchesAndKeepsPositionsForHighlighting()
    {
        var (model, _, _) = Build("Calculator", "Calendar", "Terminal");
        model.Query = "calc";

        Assert.Equal(new[] { "Calculator" }, model.Page().Select(r => r.Entry.Name));
        Assert.NotEmpty(model.Page()[0].NamePositions);
    }

    [Fact]
    public void ChangingTheQueryResetsTheSelection()
    {
        var (model, _, _) = Build("Calculator", "Calendar", "Terminal");
        model.Move(2);
        Assert.Equal(2, model.Selected);

        model.Query = "cal";

        Assert.Equal(0, model.Selected);
    }

    [Fact]
    public void MoveWrapsAtBothEnds()
    {
        var (model, _, _) = Build("a", "b", "c");

        model.Move(1);
        model.Move(1);
        model.Move(1);

        Assert.Equal(0, model.Selected);

        model.Move(-1);
        Assert.Equal(2, model.Selected);
    }

    [Fact]
    public void MoveDoesNothingWithoutResults()
    {
        var (model, _, _) = Build();
        model.Move(1);
        Assert.Equal(0, model.Selected);
        Assert.Null(model.SelectedEntry());
    }

    [Fact]
    public void FirstVisibleKeepsTheSelectionOnScreen()
    {
        var (model, _, _) = Build([.. Enumerable.Range(0, 20).Select(i => $"app{i:00}")]);

        model.Move(12);

        Assert.Equal(8, model.FirstVisible);
        Assert.Equal(4, model.SelectedOnPage);
        Assert.Equal(LauncherModel.VisibleRows, model.Page().Count);
        Assert.Equal("app12", model.Page()[model.SelectedOnPage].Entry.Name);
    }

    [Fact]
    public void FirstVisibleStopsSoTheLastPageIsFull()
    {
        var (model, _, _) = Build([.. Enumerable.Range(0, 20).Select(i => $"app{i:00}")]);

        model.Move(19); // the last row
        Assert.Equal(19, model.Selected);
        Assert.Equal(20 - LauncherModel.VisibleRows, model.FirstVisible);
        Assert.Equal("app19", model.Page()[^1].Entry.Name);
    }

    [Fact]
    public void ShortListsShowOnOnePageWithoutAnOffset()
    {
        var (model, _, _) = Build("a", "b");
        model.Move(1);

        Assert.Equal(0, model.FirstVisible);
        Assert.Equal(1, model.SelectedOnPage);
        Assert.Equal(2, model.Page().Count);
    }

    [Fact]
    public void SelectOnPageMapsAScreenRowToTheCatalog()
    {
        var (model, _, _) = Build([.. Enumerable.Range(0, 20).Select(i => $"app{i:00}")]);
        model.Move(12);

        model.SelectOnPage(0);

        Assert.Equal(8, model.Selected);
        Assert.Equal("app08", model.SelectedEntry()!.Name);
    }

    [Fact]
    public void SelectOnPageIsClampedToTheCatalog()
    {
        var (model, _, _) = Build("a", "b", "c");
        model.SelectOnPage(50);
        Assert.Equal(2, model.Selected);
        model.SelectOnPage(-3);
        Assert.Equal(0, model.Selected);
    }

    [Fact]
    public void NoMatchesNeedsAQuery()
    {
        var (model, _, _) = Build();

        // An empty catalog with an empty query is a dashboard waiting to be typed into,
        // not a failed search.
        Assert.False(model.NoMatches);

        model.Query = "nothing";
        Assert.True(model.NoMatches);
    }

    [Fact]
    public void ActivateLaunchesTheSelectedEntry()
    {
        var (model, _, launcher) = Build("Calculator", "Notepad");
        model.Move(1);

        Assert.Null(model.Activate());
        Assert.Equal("Notepad", Assert.Single(launcher.Launched).Name);
    }

    [Fact]
    public void ActivateReportsTheLauncherError()
    {
        var (model, _, launcher) = Build("Calculator");
        launcher.Error = "Access is denied";

        Assert.Equal("Access is denied", model.Activate());
    }

    [Fact]
    public void ActivateWithoutResultsDoesNotLaunch()
    {
        var (model, _, launcher) = Build();

        Assert.Equal("Nothing to launch", model.Activate());
        Assert.Empty(launcher.Launched);
    }

    [Fact]
    public void CatalogChangesReSearchAgainstTheNewEntries()
    {
        var (model, apps, _) = Build("Calculator");
        model.Query = "calc";
        Assert.Equal(1, model.Count);

        apps.Entries.Add(Entry("Calc Pad"));
        apps.Raise();

        Assert.Equal(2, model.Count);
        Assert.Equal(0, model.Selected);
    }

    [Fact]
    public void DisposeStopsRespondingToCatalogChanges()
    {
        var (model, apps, _) = Build("Calculator");
        model.Dispose();

        apps.Entries.Add(Entry("Calc Pad"));
        apps.Raise();

        Assert.Equal(1, model.Count);
    }

    [Fact]
    public void ChangedFiresForQueriesAndSelection()
    {
        var (model, _, _) = Build("a", "b");
        int changes = 0;
        model.Changed += () => changes++;

        model.Query = "a";
        model.Move(1);
        model.Query = "a"; // no change, no notification

        Assert.Equal(2, changes);
    }
}
