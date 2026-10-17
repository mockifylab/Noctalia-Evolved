// Icon-reference parsing tests: the string shapes Windows shortcuts actually carry, and the
// order the candidates for an entry are worth trying.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Apps;

namespace NoctaliaWin.Tests.Apps;

public class AppIconReferenceTests
{
    private static AppEntry Entry(string name, string exec, string? iconPath = null,
        string kind = "desktop") =>
        new(name, exec, null, iconPath, kind);

    [Fact]
    public void ReadsAPlainPathAsIndexZero()
    {
        var icon = AppIconReference.Parse(@"C:\Program Files\Mozilla Firefox\firefox.exe");

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Program Files\Mozilla Firefox\firefox.exe", icon.FilePath);
        Assert.Equal(0, icon.IconIndex);
    }

    [Fact]
    public void ReadsAFileAndIndexPair()
    {
        var icon = AppIconReference.Parse(@"C:\Windows\System32\shell32.dll,137");

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Windows\System32\shell32.dll", icon.FilePath);
        Assert.Equal(137, icon.IconIndex);
    }

    [Fact]
    public void KeepsANegativeIndexBecauseTheShellReadsItAsAResourceLocation()
    {
        var icon = AppIconReference.Parse(@"C:\Windows\System32\shell32.dll,-204");

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Windows\System32\shell32.dll", icon.FilePath);
        Assert.Equal(-204, icon.IconIndex);
    }

    [Fact]
    public void KeepsAQuotedPathThatContainsAComma()
    {
        var icon = AppIconReference.Parse("\"C:\\Icons, Extra\\app.dll\", 2");

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Icons, Extra\app.dll", icon.FilePath);
        Assert.Equal(2, icon.IconIndex);
    }

    [Fact]
    public void TreatsACommaThatIsNotFollowedByANumberAsPartOfThePath()
    {
        var icon = AppIconReference.Parse(@"C:\Program Files\Some, Odd\app.dll");

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Program Files\Some, Odd\app.dll", icon.FilePath);
        Assert.Equal(0, icon.IconIndex);
    }

    [Theory]
    [InlineData(@"C:\Windows\calc.exe,")]
    [InlineData(@"C:\Windows\calc.exe, ")]
    public void AcceptsATrailingCommaWithNoIndexAfterIt(string reference)
    {
        var icon = AppIconReference.Parse(reference);

        Assert.NotNull(icon);
        Assert.Equal(@"C:\Windows\calc.exe", icon.FilePath);
        Assert.Equal(0, icon.IconIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(",0")]
    [InlineData("\"\"")]
    [InlineData("firefox.exe")]
    public void RefusesAnythingThatIsNotACompleteFilePath(string? reference) =>
        Assert.Null(AppIconReference.Parse(reference));

    [Fact]
    public void ExpandsTheVariablesAShortcutCanUseToNameItsFolder()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var icon = AppIconReference.Parse(@"%WINDIR%\System32\shell32.dll,3");

        Assert.NotNull(icon);
        Assert.Equal(Path.Combine(windows, @"System32\shell32.dll"), icon.FilePath);
    }

    [Fact]
    public void KnowsWhichReferencesHaveToGoThroughTheShell()
    {
        Assert.True(AppIconReference.Parse(@"C:\Start Menu\Files.lnk")!.IsShortcut);
        Assert.True(AppIconReference.Parse(@"C:\Start Menu\FILES.LNK")!.IsShortcut);
        Assert.False(AppIconReference.Parse(@"C:\Windows\explorer.exe")!.IsShortcut);
    }

    [Fact]
    public void OffersTheNamedIconBeforeTheProgramItself()
    {
        var candidates = AppIconReference.ForEntry(Entry(
            "Firefox",
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files\Mozilla Firefox\firefox.exe,1"));

        Assert.Equal(2, candidates.Count);
        Assert.Equal(1, candidates[0].IconIndex);
        Assert.Equal(0, candidates[1].IconIndex);
    }

    [Fact]
    public void FallsBackToTheProgramWhenTheShortcutNamesNoIcon()
    {
        var candidates = AppIconReference.ForEntry(Entry(
            "Calculator",
            @"C:\Windows\System32\calc.exe",
            @"C:\Start Menu\Calculator.lnk"));

        Assert.Equal(
        [
            new AppIconReference(@"C:\Start Menu\Calculator.lnk", 0),
            new AppIconReference(@"C:\Windows\System32\calc.exe", 0),
        ],
            candidates);
    }

    [Fact]
    public void DoesNotAskTwiceForTheSameFile()
    {
        var candidates = AppIconReference.ForEntry(Entry(
            "Code",
            @"C:\Program Files\Microsoft VS Code\Code.exe",
            @"C:\Program Files\Microsoft VS Code\Code.exe,0"));

        Assert.Single(candidates);
    }

    [Fact]
    public void DoesNotFallBackToACommandWithNoPath()
    {
        var candidates = AppIconReference.ForEntry(Entry("Task Manager", "taskmgr",
            @"C:\Windows\System32\shell32.dll,10"));

        Assert.Equal([new AppIconReference(@"C:\Windows\System32\shell32.dll", 10)], candidates);
    }

    [Fact]
    public void DoesNotFallBackToATargetThatIsNotAProgram()
    {
        var candidates = AppIconReference.ForEntry(Entry("Notes",
            @"C:\Users\someone\Notes.txt", @"C:\Start Menu\Notes.lnk"));

        Assert.Equal([new AppIconReference(@"C:\Start Menu\Notes.lnk", 0)], candidates);
    }

    [Fact]
    public void AnEntryWithNothingToTryYieldsNoCandidates()
    {
        Assert.Empty(AppIconReference.ForEntry(Entry("Task Manager", "taskmgr")));
    }
}
