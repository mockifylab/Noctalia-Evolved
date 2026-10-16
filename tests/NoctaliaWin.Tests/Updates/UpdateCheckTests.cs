// The release check, decided without a network: version precedence, the repository address a
// paste can produce, GitHub's response body, and the sentences a person gets back.
using NoctaliaWin.Core.Updates;

namespace NoctaliaWin.Tests.Updates;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, "")]
    [InlineData("v1.2.3", 1, 2, 3, "")]
    [InlineData("V1.2.3", 1, 2, 3, "")]
    [InlineData(" 1.2.3 ", 1, 2, 3, "")]
    [InlineData("1.2", 1, 2, 0, "")]
    [InlineData("1", 1, 0, 0, "")]
    [InlineData("1.2.3-rc.1", 1, 2, 3, "rc.1")]
    [InlineData("1.2.3+build.9", 1, 2, 3, "")]
    public void ParsesTheFormsATagArrivesIn(string text, int major, int minor, int patch, string prerelease)
    {
        var version = SemanticVersion.Parse(text);

        Assert.NotNull(version);
        Assert.Equal((major, minor, patch, prerelease),
            (version!.Major, version.Minor, version.Patch, version.Prerelease));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("development build")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("1.x.3")]
    [InlineData("-1.2.3")]
    [InlineData("1.-2.3")]
    [InlineData("1.2.3-")]
    [InlineData("v")]
    [InlineData(null)]
    public void RefusesWhatIsNotAVersion(string? text) =>
        Assert.Null(SemanticVersion.Parse(text));

    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.9", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.0", "1.0.0+build.9", false)]
    // A prerelease never outranks the release it leads up to, so 1.1.0-rc.1 is not offered over
    // 1.1.0 — and the reverse is true.
    [InlineData("1.1.0-rc.1", "1.1.0", false)]
    [InlineData("1.1.0", "1.1.0-rc.1", true)]
    [InlineData("1.1.0-rc.2", "1.1.0-rc.1", true)]
    [InlineData("1.1.0-rc.10", "1.1.0-rc.9", true)]
    [InlineData("1.1.0-rc", "1.1.0-rc.1", false)]
    [InlineData("1.1.0-beta", "1.1.0-alpha.1", true)]
    public void OrdersReleasesTheWayTagsMeanThem(string candidate, string installed, bool newer)
    {
        var a = SemanticVersion.Parse(candidate);
        var b = SemanticVersion.Parse(installed);

        Assert.Equal(newer, a!.IsNewerThan(b));
    }

    [Fact]
    public void KeepsTheTagItReadsWhenPrintedBack() =>
        Assert.Equal("1.2.3-rc.1", SemanticVersion.Parse("v1.2.3-rc.1")!.ToString());
}

public sealed class GitHubRepositoryTests
{
    [Theory]
    [InlineData("mockifylab/Noctalia-Evolved", "mockifylab", "Noctalia-Evolved")]
    [InlineData(" mockifylab/noctalia ", "mockifylab", "noctalia")]
    [InlineData("https://github.com/mockifylab/Noctalia-Evolved", "mockifylab", "Noctalia-Evolved")]
    [InlineData("https://github.com/mockifylab/Noctalia-Evolved/", "mockifylab", "Noctalia-Evolved")]
    [InlineData("https://github.com/mockifylab/Noctalia-Evolved/releases/latest", "mockifylab", "Noctalia-Evolved")]
    [InlineData("https://api.github.com/repos/mockifylab/Noctalia-Evolved", "mockifylab", "Noctalia-Evolved")]
    public void ReadsWhatAPersonWouldPaste(string text, string owner, string name)
    {
        var repo = GitHubRepository.Parse(text);

        Assert.NotNull(repo);
        Assert.Equal((owner, name), (repo!.Owner, repo.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("mockifylab")]
    [InlineData("mockifylab/one/two/three")]
    [InlineData("mockifylab/Noctalia-Evolved/branches")]
    [InlineData("https://example.com/mockifylab/Noctalia-Evolved")]
    [InlineData("http://github.com/mockifylab/Noctalia-Evolved")]
    [InlineData("../../etc/passwd")]
    [InlineData("mockify lab/noctalia")]
    [InlineData("owner/name?token=secret")]
    [InlineData("owner//name")]
    [InlineData(null)]
    public void RefusesAnythingThatIsNotAGitHubRepository(string? text) =>
        Assert.Null(GitHubRepository.Parse(text));

    [Fact]
    public void BuildsHttpsAddressesAndNothingElse()
    {
        var repo = GitHubRepository.Parse("mockifylab/Noctalia-Evolved")!;

        Assert.Equal("https", repo.LatestReleaseApi.Scheme);
        Assert.Equal(
            "https://api.github.com/repos/mockifylab/Noctalia-Evolved/releases/latest",
            repo.LatestReleaseApi.ToString());
        Assert.Equal(
            "https://github.com/mockifylab/Noctalia-Evolved/releases/latest",
            repo.LatestReleasePage);
    }
}

public sealed class ReleaseInfoTests
{
    private const string Body = """
        {
          "tag_name": "v1.2.0",
          "name": "Noctalia Evolved 1.2.0",
          "html_url": "https://github.com/mockifylab/Noctalia-Evolved/releases/tag/v1.2.0",
          "assets": [
            { "name": "installer.exe", "browser_download_url": "https://objects.example/NoctaliaEvolved-Setup.exe" }
          ]
        }
        """;

    [Fact]
    public void ReadsAGitHubReleaseBody()
    {
        var release = ReleaseInfo.FromGitHubJson(Body);

        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release!.TagName);
        Assert.Equal("Noctalia Evolved 1.2.0", release.Name);
        Assert.Equal("1.2.0", release.Version!.ToString());
        Assert.Equal("https://objects.example/NoctaliaEvolved-Setup.exe", release.AssetUrl);
    }

    [Fact]
    public void TakesTheFirstAssetThatCarriesAUrl()
    {
        const string withEmptyFirst = """
            { "tag_name": "1.0.0", "assets": [
              { "name": "notes" },
              { "browser_download_url": "" },
              { "browser_download_url": "https://objects.example/second.exe" }
            ] }
            """;

        Assert.Equal("https://objects.example/second.exe", ReleaseInfo.FromGitHubJson(withEmptyFirst)!.AssetUrl);
    }

    [Fact]
    public void AReleaseWithoutAssetsIsStillARelease()
    {
        var release = ReleaseInfo.FromGitHubJson("""{ "tag_name": "1.0.0", "assets": [] }""");

        Assert.NotNull(release);
        Assert.Equal("", release!.AssetUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("<html>404</html>")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    [InlineData("""{ "tag_name": 12 }""")]
    [InlineData("""{ "tag_name": "" }""")]
    public void SaysNothingUsefulRatherThanGuessing(string? json) =>
        Assert.Null(ReleaseInfo.FromGitHubJson(json));

    [Fact]
    public void AnUnreadableAssetsFieldStillLeavesARelease() =>
        Assert.Equal("", ReleaseInfo.FromGitHubJson("""{ "tag_name": "1.0.0", "assets": "oops" }""")!.AssetUrl);
}

public sealed class UpdateEvaluatorTests
{
    private static readonly ReleaseInfo Newest = new(
        "v1.2.0", "Noctalia Evolved 1.2.0",
        "https://github.com/mockifylab/Noctalia-Evolved/releases/tag/v1.2.0",
        "https://objects.example/NoctaliaEvolved-Setup.exe");

    [Fact]
    public void OffersANewerReleaseWithoutPromisingToInstallIt()
    {
        var check = UpdateEvaluator.Evaluate("1.0.0", Newest);

        Assert.Equal(UpdateOutcome.UpdateAvailable, check.Outcome);
        Assert.Equal("1.2.0", check.LatestVersion);
        Assert.Equal(Newest.PageUrl, check.ReleasePageUrl);
        Assert.Contains("Nothing has been downloaded", check.Message);
        Assert.Contains("until you run the installer yourself", check.Message);
    }

    [Fact]
    public void ReportsUpToDateWhenThisBuildIsTheNewest()
    {
        var check = UpdateEvaluator.Evaluate("1.2.0", Newest);

        Assert.Equal(UpdateOutcome.UpToDate, check.Outcome);
        Assert.Contains("1.2.0", check.Message);
    }

    [Fact]
    public void ReportsUpToDateWhenThisBuildIsAheadOfEveryPublish()
    {
        // An unreleased build from source must not be told a published tag is an update.
        Assert.Equal(UpdateOutcome.UpToDate, UpdateEvaluator.Evaluate("1.3.0", Newest).Outcome);
    }

    [Fact]
    public void SaysPlainlyWhenTheRunningBuildHasNoVersion()
    {
        var check = UpdateEvaluator.Evaluate("development build", Newest);

        Assert.Equal(UpdateOutcome.UnknownInstalledVersion, check.Outcome);
        Assert.Contains("development build", check.Message);
        Assert.Contains("1.2.0", check.Message);
    }

    [Fact]
    public void SaysPlainlyWhenAnEmptyVersionReportsItselfAsNothing()
    {
        var check = UpdateEvaluator.Evaluate("", Newest);

        Assert.Equal(UpdateOutcome.UnknownInstalledVersion, check.Outcome);
        Assert.Contains("nothing", check.Message);
    }

    [Fact]
    public void SaysPlainlyWhenTheTagIsNotAVersion()
    {
        var check = UpdateEvaluator.Evaluate("1.0.0", Newest with { TagName = "nightly-41" });

        Assert.Equal(UpdateOutcome.Failed, check.Outcome);
        Assert.Contains("nightly-41", check.Message);
    }

    [Fact]
    public void SaysPlainlyWhenNothingCameBack()
    {
        var check = UpdateEvaluator.Evaluate("1.0.0", null);

        Assert.Equal(UpdateOutcome.Failed, check.Outcome);
        Assert.False(check.HasReleasePage);
    }
}
