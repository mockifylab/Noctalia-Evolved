// What "is there a newer release, and where does it come from" means, kept away from HTTP so
// the answers a person sees are decided by code the tests can exercise. Nothing here reaches
// the network, and nothing here installs: the outcome is a sentence and a couple of links.
using System.Globalization;
using System.Text.Json;

namespace NoctaliaWin.Core.Updates;

/// A release number in the form GitHub tags carry: `v1.2.3`, `1.2.3-rc.1`, `1.2`.
public sealed record SemanticVersion(int Major, int Minor, int Patch, string Prerelease)
    : IComparable<SemanticVersion>, IComparable
{
    public static SemanticVersion? Parse(string? text)
    {
        if (text is null) return null;

        string value = text.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value[1..];

        // A build metadata suffix ("+noise") is not part of the precedence the tags compare by.
        int plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];

        string core = value, prerelease = "";
        int dash = value.IndexOf('-');
        if (dash >= 0)
        {
            core = value[..dash];
            prerelease = value[(dash + 1)..];

            // "1.2.3-" is a half-typed tag, not a release with no prerelease name.
            if (prerelease.Length == 0) return null;
        }

        // Empty segments stay empty so "1..3" and "-1.2.3" are refused rather than read as
        // numbers with the gap taken out.
        string[] parts = core.Split('.');
        if (parts.Length > 3) return null;

        int[] numbers = [0, 0, 0];
        for (int i = 0; i < parts.Length; i++)
        {
            // Int32.TryParse alone would accept "+3" and leading whitespace, which no tag uses.
            if (!IsUnsignedInteger(parts[i], out int number)) return null;
            numbers[i] = number;
        }

        return new SemanticVersion(numbers[0], numbers[1], numbers[2], prerelease);
    }

    /// True when this release is the one to offer the person running `installed`.
    public bool IsNewerThan(SemanticVersion? installed) => CompareTo(installed) > 0;

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;

        int byNumber = Major != other.Major
            ? Major.CompareTo(other.Major)
            : Minor != other.Minor
                ? Minor.CompareTo(other.Minor)
                : Patch.CompareTo(other.Patch);

        if (byNumber != 0) return byNumber;

        // A release outranks its own prereleases, which is why 1.1.0-rc.1 must not be offered
        // over 1.1.0.
        if (Prerelease.Length == 0) return other.Prerelease.Length == 0 ? 0 : 1;
        if (other.Prerelease.Length == 0) return -1;

        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    public int CompareTo(object? obj) => CompareTo(obj as SemanticVersion);

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}" + (Prerelease.Length > 0 ? $"-{Prerelease}" : "");

    private static bool IsUnsignedInteger(string text, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 9) return false;
        foreach (char c in text)
        {
            if (c < '0' || c > '9') return false;
            value = value * 10 + (c - '0');
        }
        return true;
    }

    /// Identifier by identifier, numbers compared numerically and words ordinally, with a
    /// shorter list ranking lower — the order semver defines and the one tags in the wild use.
    private static int ComparePrerelease(string left, string right)
    {
        string[] a = left.Split('.'), b = right.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool isNumber(string text, out int value) =>
                int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

            bool leftNumber = isNumber(a[i], out int x);
            bool rightNumber = isNumber(b[i], out int y);

            // A number beats a word where both sides agree on being one; otherwise the two are
            // read as words, which is the order tags in the wild sort by.
            int result = leftNumber && rightNumber
                ? x.CompareTo(y)
                : string.CompareOrdinal(a[i], b[i]);

            if (result != 0) return result;
        }

        return a.Length.CompareTo(b.Length);
    }
}

/// Which repository's releases to ask, and the two addresses that follow from it. The owner and
/// name are validated before they go anywhere near a URL, and the scheme is fixed to HTTPS
/// rather than taken from the input.
public sealed record GitHubRepository(string Owner, string Name)
{
    /// `owner/name`, or a GitHub URL for the same repository. Returns null for anything that is
    /// not one of those, so a mistyped value fails loudly instead of querying a wrong host.
    public static GitHubRepository? Parse(string? text)
    {
        if (text is null) return null;

        string value = text.Trim().TrimEnd('/');
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            string host = value["https://".Length..];
            int slash = host.IndexOf('/');
            host = slash < 0 ? host : host[..slash];

            // Only the canonical hosts, so "https://evil.example/mockifylab/x" is refused rather
            // than being trusted because it looked like a URL.
            if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && !host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                return null;

            value = value[("https://" + host).Length..].TrimStart('/');
            if (value.StartsWith("repos/", StringComparison.OrdinalIgnoreCase))
                value = value["repos/".Length..];
        }
        else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A pasted release page is the same repository, so its tail is not part of the name.
        foreach (string tail in new[] { "/releases/latest", "/releases" })
        {
            if (value.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                value = value[..^tail.Length];
        }

        // Split without dropping anything, so "owner//name" is the malformed paste it looks
        // like instead of a repository with a blank half.
        string[] parts = value.Split('/');
        if (parts.Length != 2) return null;

        return IsSafeSegment(parts[0]) && IsSafeSegment(parts[1])
            ? new GitHubRepository(parts[0], parts[1])
            : null;
    }

    public string Slug => $"{Owner}/{Name}";

    public Uri LatestReleaseApi => new($"https://api.github.com/repos/{Owner}/{Name}/releases/latest");

    public string LatestReleasePage => $"https://github.com/{Owner}/{Name}/releases/latest";

    /// GitHub's own rules are looser, but these characters are all a paste can hold in practice,
    /// and refusing anything else keeps a dot-dot or a query mark out of the address.
    private static bool IsSafeSegment(string text) =>
        text.Length is > 0 and <= 100
        && text[0] != '-'
        && text[^1] != '.'
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}

/// One release as GitHub's API describes it.
public sealed record ReleaseInfo(
    string TagName, string Name, string PageUrl, string AssetUrl)
{
    /// Reads GitHub's `releases/latest` body. Malformed or partial JSON yields null, because a
    /// checker that guessed at a version would be worse than one that says it could not read
    /// the answer.
    public static ReleaseInfo? FromGitHubJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!TryString(root, "tag_name", out string? tag) || string.IsNullOrEmpty(tag)) return null;

            TryString(root, "name", out string? name);
            TryString(root, "html_url", out string? page);

            return new ReleaseInfo(tag, name ?? tag, page ?? string.Empty, FirstAssetUrl(root));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public SemanticVersion? Version => SemanticVersion.Parse(TagName);

    private static bool TryString(JsonElement element, string key, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(key, out var property)
            || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return value is not null;
    }

    /// The first asset's download address. A release with no assets is still a release, so this
    /// is empty rather than fatal — the page link works on its own.
    private static string FirstAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
            return string.Empty;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind == JsonValueKind.Object
                && asset.TryGetProperty("browser_download_url", out var url)
                && url.ValueKind == JsonValueKind.String
                && url.GetString() is { Length: > 0 } address)
                return address;
        }

        return string.Empty;
    }
}

public enum UpdateOutcome
{
    /// A newer release exists, and the person decides what to do about it.
    UpdateAvailable,

    /// This build is at or ahead of the newest published release.
    UpToDate,

    /// The running build does not carry a version a release can be compared to.
    UnknownInstalledVersion,

    /// No repository to ask, or one that cannot be read as `owner/name`.
    NotConfigured,

    /// Nothing usable came back — offline, refused, or an unreadable body.
    Failed,
}

public sealed record UpdateCheck(
    UpdateOutcome Outcome,
    string Message,
    string? LatestVersion = null,
    string? ReleasePageUrl = null,
    string? AssetUrl = null)
{
    public bool HasReleasePage => !string.IsNullOrEmpty(ReleasePageUrl);
}

/// Decides what a check result means in words, from the installed version and whatever the
/// repository answered.
public static class UpdateEvaluator
{
    public static UpdateCheck Evaluate(string? installedVersion, ReleaseInfo? release)
    {
        if (release is null)
        {
            return new UpdateCheck(
                UpdateOutcome.Failed,
                "GitHub's latest release could not be read. That is what an offline machine, "
                    + "a refused request, or a body we cannot parse looks like from here.");
        }

        var latest = release.Version;
        if (latest is null)
        {
            return new UpdateCheck(
                UpdateOutcome.Failed,
                $"The newest release is tagged “{release.TagName}”, which is not a version "
                    + "this build can compare against.");
        }

        var installed = SemanticVersion.Parse(installedVersion);
        if (installed is null)
        {
            return new UpdateCheck(
                UpdateOutcome.UnknownInstalledVersion,
                $"The newest release is {latest}, but this build reports its version as "
                    + "“" + Show(installedVersion) + "”, which is not a number to compare.",
                latest.ToString(),
                release.PageUrl,
                release.AssetUrl);
        }

        if (!latest.IsNewerThan(installed))
        {
            return new UpdateCheck(
                UpdateOutcome.UpToDate,
                $"This is the newest release: {installed} installed, {latest} published.",
                latest.ToString(),
                release.PageUrl,
                release.AssetUrl);
        }

        return new UpdateCheck(
            UpdateOutcome.UpdateAvailable,
            $"{latest} is out and this build is {installed}. Nothing has been downloaded and "
                + "nothing will be installed until you run the installer yourself.",
            latest.ToString(),
            release.PageUrl,
            release.AssetUrl);
    }

    private static string Show(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "nothing" : text.Trim();
}
