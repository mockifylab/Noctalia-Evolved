// The one place this build talks to GitHub: a single HTTPS GET for the repository's latest
// release. No settings, no paths, no identifiers leave the machine — the request carries only
// the standard User-Agent GitHub requires. The result is turned into words by Core, so what a
// person reads is the tested code's answer, and installing is never part of it.
using System.Diagnostics;
using NoctaliaWin.Core.Updates;

namespace NoctaliaWin.Windows;

public sealed class UpdateService
{
    /// This project's own repository. The check reads what GitHub publishes for it, so a fork
    /// or a rename is pointed elsewhere with NOCTALIA_REPOSITORY rather than by editing code.
    public const string DefaultRepository = "mockifylab/Noctalia-Evolved";

    public const string RepositoryVariable = "NOCTALIA_REPOSITORY";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public UpdateService()
    {
        // GitHub's API answers 403 to a request with no User-Agent, and setting it on the
        // client once keeps every call honest about who is asking.
        if (!Http.DefaultRequestHeaders.Contains("User-Agent"))
            Http.DefaultRequestHeaders.Add("User-Agent", "Noctalia-Evolved-for-Windows");
    }

    /// Where updates come from right now. A value that is set but unreadable is reported as
    /// unconfigured rather than falling back quietly, because a silent fallback would point a
    /// person at the wrong repository without telling them.
    public string Repository =>
        Environment.GetEnvironmentVariable(RepositoryVariable) is { Length: > 0 } configured
            ? configured
            : DefaultRepository;

    public GitHubRepository? Target => GitHubRepository.Parse(Repository);

    public async Task<UpdateCheck> CheckAsync(
        string? installedVersion, CancellationToken cancel = default)
    {
        var target = Target;
        if (target is null)
        {
            return new UpdateCheck(
                UpdateOutcome.NotConfigured,
                $"“{Repository}” is not a GitHub repository, so there is nothing to ask. "
                    + $"Set {RepositoryVariable} to owner/name, or unset it to use "
                    + $"{DefaultRepository}.");
        }

        try
        {
            using var response = await Http.GetAsync(target.LatestReleaseApi, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheck(
                    UpdateOutcome.Failed,
                    $"GitHub answered {(int)response.StatusCode} for {target.Slug}.");
            }

            string body = await response.Content.ReadAsStringAsync(cancel);
            var release = ReleaseInfo.FromGitHubJson(body);
            if (release is null)
            {
                return new UpdateCheck(
                    UpdateOutcome.Failed, $"The response from {target.LatestReleaseApi} could not be read.");
            }

            // The page link comes from the body, but the address was built from a validated
            // owner/name, so filling it in when GitHub omits it stays on the same repository.
            var checkedRelease = release.PageUrl.Length == 0
                ? release with { PageUrl = target.LatestReleasePage }
                : release;

            return UpdateEvaluator.Evaluate(installedVersion, checkedRelease);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return new UpdateCheck(
                UpdateOutcome.Failed,
                $"Could not reach GitHub: {ex.Message}");
        }
    }

    /// Opens the release page in the person's browser. Only ever the latest-release page for the
    /// validated repository, and only on request — downloading and installing stay manual.
    public string? OpenLatestRelease()
    {
        var target = Target;
        if (target is null) return $"“{Repository}” is not a GitHub repository.";

        try
        {
            Process.Start(new ProcessStartInfo(target.LatestReleasePage) { UseShellExecute = true });
            return $"Opened {target.LatestReleasePage}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"Could not open the release page: {ex.Message}";
        }
    }

    private static bool IsExpected(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or OperationCanceledException
            or NotSupportedException or UriFormatException;
}
