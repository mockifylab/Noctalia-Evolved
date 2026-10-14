// Launcher ranking tests over the platform-independent search layer.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Apps;
using NoctaliaWin.Core.Fuzzy;

namespace NoctaliaWin.Tests.Apps;

public class LauncherSearchTests
{
    private static AppEntry Entry(string name, string exec, string? args = null, string kind = "desktop") =>
        new(name, exec, args, null, kind);

    private static readonly AppEntry[] Catalog =
    [
        Entry("Firefox", @"C:\Program Files\Mozilla Firefox\firefox.exe"),
        Entry("Files", @"C:\Program Files\WindowsApps\Files.exe"),
        Entry("Calculator", @"C:\Windows\System32\calc.exe"),
        Entry("Visual Studio Code", @"C:\Program Files\Microsoft VS Code\Code.exe"),
        Entry("Terminal", @"C:\Program Files\Windows Terminal\wt.exe"),
        Entry("Task Manager", @"taskmgr", kind: "run"),
    ];

    [Fact]
    public void EmptyQuery_ReturnsNothing()
    {
        Assert.Empty(LauncherSearch.Search("", Catalog));
        Assert.Empty(LauncherSearch.Search("   ", [Entry("Only", "only.exe")]));
    }

    [Fact]
    public void ExactNameMatch_Wins()
    {
        var results = LauncherSearch.Search("firefox", Catalog);

        Assert.NotEmpty(results);
        Assert.Equal("Firefox", results[0].Entry.Name);
        Assert.True(double.IsPositiveInfinity(results[0].Score));
    }

    [Fact]
    public void QueryOrderIsCaseInsensitive()
    {
        var lower = LauncherSearch.Search("calc", Catalog);
        var upper = LauncherSearch.Search("CALC", Catalog);

        Assert.Equal(lower[0].Entry.Name, upper[0].Entry.Name);
        Assert.Equal("Calculator", lower[0].Entry.Name);
    }

    [Fact]
    public void ConsecutiveMatchesRankAboveScatteredOnes()
    {
        // Same-length candidates whose exec paths cannot match, so only the
        // density of the name match decides the order ("ab" adjacent vs split).
        const string neutralExec = @"C:\Programs\prog.exe";
        AppEntry[] apps = [Entry("abuxda", neutralExec), Entry("axbxcd", neutralExec)];

        var results = LauncherSearch.Search("abd", apps);

        Assert.Equal(2, results.Count);
        Assert.Equal("abuxda", results[0].Entry.Name);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void CommonPrefixRanksByBestScoringField()
    {
        var results = LauncherSearch.Search("firefox", Catalog);

        Assert.Single(results);
        Assert.Equal("Firefox", results[0].Entry.Name);
    }

    [Fact]
    public void PathMatchesAreIncludedWhenTheNameDoesNotMatch()
    {
        // "wt" appears in Terminal's executable path, and "System32" also contains
        // the w->t subsequence for Calculator; neither name matches.
        var results = LauncherSearch.Search("wt", Catalog);

        Assert.Contains(results, r => r.Entry.Name == "Terminal");
        Assert.All(results, r => Assert.False(FzyScorer.HasMatch("wt", r.Entry.Name)));
        Assert.All(results, r => Assert.Empty(r.NamePositions));
    }

    [Fact]
    public void NameHighlightPositionsAreReturnedForNameMatches()
    {
        var results = LauncherSearch.Search("vscode", Catalog);

        var code = results.Single(r => r.Entry.Name == "Visual Studio Code");
        Assert.NotEmpty(code.NamePositions);
        Assert.All(code.NamePositions, p => Assert.InRange(p, 0, "Visual Studio Code".Length - 1));
    }

    [Fact]
    public void NonMatchingEntriesAreExcluded()
    {
        Assert.Empty(LauncherSearch.Search("gimp", Catalog));
    }

    [Fact]
    public void ResultsAreSortedByDescendingScore()
    {
        var results = LauncherSearch.Search("e", Catalog);

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Score >= results[i].Score);
    }

    [Fact]
    public void MaxResultsTruncatesTheList()
    {
        var many = Enumerable.Range(0, 50)
            .Select(i => Entry($"App {i}", $@"C:\Apps\app{i}.exe"))
            .ToArray();

        var results = LauncherSearch.Search("app", many, maxResults: 5);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void OverlongCandidatesStillMatchButRankLast()
    {
        // fzy refuses to score candidates longer than MATCH_MAX_LEN; they must stay
        // reachable but sink below anything reasonably sized.
        var overlong = new string('h', FzyScorer.MatchMaxLen + 10) + "u";
        var hugeName = Entry(overlong, overlong + ".exe");
        var normal = Entry("hu", "hu.exe");

        var results = LauncherSearch.Search("hu", [hugeName, normal]);

        Assert.Equal(2, results.Count);
        Assert.Equal("hu", results[0].Entry.Name);
        Assert.True(double.IsPositiveInfinity(results[0].Score));
        Assert.Same(hugeName, results[1].Entry);
        Assert.True(double.IsNegativeInfinity(results[1].Score));
        Assert.Empty(results[1].NamePositions);
    }

    [Fact]
    public void LaunchTargetCombinesExecutableAndArgs()
    {
        var withArgs = Entry("Editor", @"C:\bin\editor.exe", "--wait");

        Assert.Equal(@"C:\bin\editor.exe --wait", withArgs.LaunchTarget);
        Assert.Equal("taskmgr", Entry("Task Manager", "taskmgr").LaunchTarget);
    }
}
