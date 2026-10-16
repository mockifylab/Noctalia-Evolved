// What the offline installer decides before it writes: whether a payload is installable, which
// direction an install moves, and the sentences the window shows. The copying itself is in the
// installer project; these are the answers it is built on.
using NoctaliaWin.Core.Installs;

namespace NoctaliaWin.Tests.Installs;

public sealed class InstallerPlanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "noctalia-win-installer-" + Guid.NewGuid().ToString("N"));

    public InstallerPlanTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* left for the OS's temp cleanup */ }
    }

    private string Payload(params string[] files)
    {
        string folder = Path.Combine(_root, "payload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        foreach (string file in files)
        {
            string target = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, file.EndsWith(".exe", StringComparison.Ordinal) ? "MZ" : "");
        }

        return folder;
    }

    private static InstallLayout Layout(string payload) => new(payload,
        @"C:\Users\someone\AppData\Local\Programs\Noctalia Evolved",
        @"C:\Users\someone\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Noctalia Evolved");

    [Fact]
    public void APayloadIsUsableWhenTheApphostIsInIt() =>
        Assert.Null(InstallDecision.CheckPayload(Payload(InstallLayout.ExecutableName, "NoctaliaWin.dll")));

    [Fact]
    public void AMissingFolderIsNamedInTheMessage()
    {
        string problem = InstallDecision.CheckPayload(Path.Combine(_root, "not-here"))!;

        Assert.Contains("payload folder is missing", problem);
        Assert.Contains("not-here", problem);
    }

    [Fact]
    public void AFolderWithoutTheApphostIsNotABundle()
    {
        string problem = InstallDecision.CheckPayload(Payload("README.txt"))!;

        Assert.Contains(InstallLayout.ExecutableName, problem);
        Assert.Contains("not a Noctalia bundle", problem);
    }

    [Fact]
    public void TheInstalledPathsAreTheOnesTheShortcutAndRunKeyUse()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        Assert.Equal(
            @"C:\Users\someone\AppData\Local\Programs\Noctalia Evolved\NoctaliaWin.exe",
            layout.ExecutablePath);
        Assert.Equal(
            @"C:\Users\someone\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Noctalia Evolved\Noctalia Evolved.lnk",
            layout.ShortcutPath);
        Assert.Equal(
            new[] { layout.InstallFolder, layout.StartMenuFolder },
            InstallDecision.FoldersCreated(layout));
        Assert.False(layout.IsInstalled);
    }

    [Fact]
    public void AnInstallFolderThatHoldsTheApphostCountsAsInstalled()
    {
        string install = Payload(InstallLayout.ExecutableName);
        var layout = Layout("unused") with { InstallFolder = install, StartMenuFolder = install };

        Assert.True(layout.IsInstalled);
    }

    [Theory]
    [InlineData(null, "1.0.0", InstallAction.Fresh)]
    [InlineData("", "1.0.0", InstallAction.Fresh)]
    [InlineData("1.0.0", "1.0.0", InstallAction.Same)]
    [InlineData("0.9.0", "1.0.0", InstallAction.Upgrade)]
    [InlineData("1.0.0", "0.9.0", InstallAction.Downgrade)]
    [InlineData("v1.0.0", "1.1.0", InstallAction.Upgrade)]
    [InlineData("nightly", "1.0.0", InstallAction.Undetermined)]
    [InlineData("1.0.0", "nightly", InstallAction.Undetermined)]
    [InlineData(null, null, InstallAction.Undetermined)]
    public void DecidesWhichDirectionTheInstallMoves(string? installed, string? incoming, InstallAction expected) =>
        Assert.Equal(expected, InstallDecision.Decide(installed, incoming));

    [Fact]
    public void APayloadWithoutAVersionIsNeverCalledAFreshInstall()
    {
        // Nothing on disk and nothing readable in the payload is "unknown", not "first install":
        // the window must not promise a direction it cannot see.
        Assert.Equal(InstallAction.Undetermined, InstallDecision.Decide(null, null));
    }

    [Fact]
    public void DescribesAFirstInstallWithTheFolderItWritesTo()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        string text = InstallDecision.Describe(InstallAction.Fresh, layout, null, "1.0.0");

        Assert.Contains("Noctalia Evolved 1.0.0", text);
        Assert.Contains(layout.InstallFolder, text);
        Assert.DoesNotContain("Start Menu", text);
    }

    [Fact]
    public void NamesAnUpgradeAndWhatItReplaces()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        string text = InstallDecision.Describe(InstallAction.Upgrade, layout, "0.9.0", "1.0.0");

        Assert.Contains("Replaces Noctalia Evolved 0.9.0 with Noctalia Evolved 1.0.0", text);
    }

    [Fact]
    public void SaysSoWhenThisBundleIsTheOlderOne()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        string text = InstallDecision.Describe(InstallAction.Downgrade, layout, "1.2.0", "1.0.0");

        Assert.Contains("newer than Noctalia Evolved 1.0.0", text);
        Assert.Contains("replace it with the older build", text);
    }

    [Fact]
    public void RefusesToPromiseADirectionItCannotSee()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        string text = InstallDecision.Describe(InstallAction.Undetermined, layout, "nightly", "1.0.0");

        Assert.Contains("cannot be compared", text);
        Assert.Contains("nightly", text);
    }

    [Fact]
    public void ReportsAnEmptyVersionAsNotReportedRatherThanBlank()
    {
        var layout = Layout(Payload(InstallLayout.ExecutableName));

        string text = InstallDecision.Describe(InstallAction.Undetermined, layout, "", "");

        Assert.Contains("unknown", text);
        Assert.DoesNotContain("Noctalia Evolved \"\"", text);
    }
}
