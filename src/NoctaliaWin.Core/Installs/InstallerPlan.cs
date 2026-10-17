// The decisions behind the offline installer: where the app goes, what has to be true before
// anything is copied, and what the person is being told they are about to get. Kept in Core so
// the answers are the tested ones, and so no installer UI can quietly decide something worse.
//
// Nothing here touches the network. The payload is the folder the installer was shipped with.
using NoctaliaWin.Core.Updates;

namespace NoctaliaWin.Core.Installs;

/// The three folders an install works between. Built from the environment roots the caller
/// supplies, so the layout is testable and a missing root is a refusal rather than a guess.
public sealed record InstallLayout(string PayloadFolder, string InstallFolder, string StartMenuFolder)
{
    /// The apphost the published payload carries. The name is the assembly's, not a brand, so
    /// it is what a Start Menu shortcut and the Run key both have to point at.
    public const string ExecutableName = "NoctaliaWin.exe";

    public const string ProductName = "Noctalia Evolved";

    public string ExecutablePath => Path.Combine(InstallFolder, ExecutableName);

    public string ShortcutPath => Path.Combine(StartMenuFolder, ProductName + ".lnk");

    /// The per-user install a person running this build gets. Returns null when either folder
    /// the layout is built from is not there, because writing to a made-up location is not an
    /// option: %LOCALAPPDATA% is where an unprivileged app puts itself, and the Start Menu is
    /// where a shortcut belongs.
    public static InstallLayout? ForCurrentUser(string payloadFolder) =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create) is { Length: > 0 } local
        && Environment.GetFolderPath(Environment.SpecialFolder.Programs,
            Environment.SpecialFolderOption.Create) is { Length: > 0 } programs
            ? new InstallLayout(
                payloadFolder,
                Path.Combine(local, "Programs", ProductName),
                Path.Combine(programs, ProductName))
            : null;

    /// The same install with the files moved to a folder the person chose in the setup window.
    /// The Start Menu entry stays where it belongs, so a custom folder never hides the shortcut.
    /// Returns null when the folder is not one this installer may write into.
    public static InstallLayout? ForFolder(string payloadFolder, string? installFolder) =>
        InstallDecision.CheckInstallFolder(installFolder, payloadFolder) is null
        && Environment.GetFolderPath(Environment.SpecialFolder.Programs,
            Environment.SpecialFolderOption.Create) is { Length: > 0 } programs
            ? new InstallLayout(
                payloadFolder,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(installFolder!.Trim())),
                Path.Combine(programs, ProductName))
            : null;

    /// True when the target already holds an install this layout made.
    public bool IsInstalled => File.Exists(ExecutablePath);
}

public enum InstallAction
{
    /// Nothing is in the way; this is a first install.
    Fresh,

    /// An older build is installed and this one is newer.
    Upgrade,

    /// The same version is already installed, so installing again changes nothing but files.
    Same,

    /// An installed build is newer than the payload, which is worth saying out loud before
    /// replacing it.
    Downgrade,

    /// One side or the other does not report a version, so no promise about direction is made.
    Undetermined,
}

public static class InstallDecision
{
    /// What has to be in the payload folder before a copy is offered. Returns null when the
    /// folder is usable, or the reason it is not — the installer shows that reason instead of
    /// failing halfway through.
    public static string? CheckPayload(string payloadFolder)
    {
        if (!Directory.Exists(payloadFolder))
            return $"The installer's payload folder is missing:\n{payloadFolder}";

        if (!File.Exists(Path.Combine(payloadFolder, InstallLayout.ExecutableName)))
            return $"The payload has no {InstallLayout.ExecutableName} in it, so this is not a "
                + "Noctalia bundle. Publish the app into the payload folder first.";

        return null;
    }

    /// Whether a folder the person picked is one this installer may write into. The setup window
    /// promises that no administrator is needed, and that only holds inside this user's own
    /// profile — so anything outside it, and anything that is not a plain folder path, is answered
    /// with the reason instead of being tried and failing halfway through.
    public static string? CheckInstallFolder(string? installFolder, string? payloadFolder = null)
    {
        if (string.IsNullOrWhiteSpace(installFolder))
            return "Choose a folder to install into.";

        string text = installFolder.Trim();
        if (text.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return "That is not a usable folder name.";

        if (!Path.IsPathRooted(text))
            return "Choose a complete folder path, starting with a drive.";

        string full;
        try
        {
            full = Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                       or PathTooLongException)
        {
            return "That is not a usable folder path.";
        }

        full = Path.TrimEndingDirectorySeparator(full);

        if (Path.GetPathRoot(full) is { Length: > 0 } drive
            && string.Equals(full, Path.TrimEndingDirectorySeparator(drive),
                StringComparison.OrdinalIgnoreCase))
        {
            return "Choose a folder on a drive, not the whole drive.";
        }

        if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            is not { Length: > 0 } profile)
        {
            return "This user's profile folder is not available, so there is nowhere this "
                + "installer can write without an administrator.";
        }

        if (!IsInside(full, profile))
        {
            return full + " is outside this user's profile (" + profile + "). Writing there "
                + "would need an administrator, and this installer never asks for one.";
        }

        if (payloadFolder is { Length: > 0 })
        {
            string payload = Path.TrimEndingDirectorySeparator(Path.GetFullPath(payloadFolder));
            if (IsInside(payload, full) || IsInside(full, payload))
            {
                return "Choose a folder that is not the installer's own bundle, and does not "
                    + "contain it — copying over the bundle would destroy what is being installed.";
            }
        }

        return null;
    }

    /// True when `path` sits below `parent`, the way Windows paths behave. A folder is never
    /// inside itself, which is what keeps a person from picking their profile root and having the
    /// app loose in it.
    private static bool IsInside(string path, string parent)
    {
        string head = Path.TrimEndingDirectorySeparator(parent);
        if (path.Length <= head.Length) return false;
        if (!string.Equals(path[..head.Length], head, StringComparison.OrdinalIgnoreCase)) return false;

        char boundary = path[head.Length];
        return boundary == Path.DirectorySeparatorChar || boundary == Path.AltDirectorySeparatorChar;
    }

    /// Which direction this install moves in, from the two version strings the installer reads:
    /// the one already on disk and the one in the payload. Unreadable text is reported as
    /// undetermined rather than being read as "older" or "newer".
    public static InstallAction Decide(string? installedVersion, string? incomingVersion)
    {
        var incoming = SemanticVersion.Parse(incomingVersion);
        if (incoming is null) return InstallAction.Undetermined;

        var installed = SemanticVersion.Parse(installedVersion);
        if (installed is null)
        {
            // A first install has nothing on disk, and that is not the same as not knowing.
            return string.IsNullOrEmpty(installedVersion)
                ? InstallAction.Fresh
                : InstallAction.Undetermined;
        }

        if (incoming.IsNewerThan(installed)) return InstallAction.Upgrade;
        if (installed.IsNewerThan(incoming)) return InstallAction.Downgrade;
        return InstallAction.Same;
    }

    /// The sentence the installer shows for that decision. A downgrade is stated as what it is,
    /// because silently replacing a newer build with an older one is how people lose a fix they
    /// were relying on.
    public static string Describe(InstallAction action, InstallLayout layout, string? installed, string? incoming) =>
        action switch
        {
            InstallAction.Fresh => $"Installs {ProductName(incoming)} for this user in\n{layout.InstallFolder}",
            InstallAction.Upgrade => $"Replaces {ProductName(installed)} with {ProductName(incoming)} in\n{layout.InstallFolder}",
            InstallAction.Same => $"{ProductName(incoming)} is already installed here. Re-installing overwrites the same files.",
            InstallAction.Downgrade => $"{ProductName(installed)} is already installed here, and it is newer than {ProductName(incoming)}. "
                + "Installing would replace it with the older build.",
            _ => "Installed: " + (string.IsNullOrWhiteSpace(installed) ? "unknown" : installed.Trim())
                + ", this bundle: " + (string.IsNullOrWhiteSpace(incoming) ? "unknown" : incoming.Trim())
                + ". The versions cannot be compared, so nothing is promised about which is newer.",
        };

    private static string ProductName(string? version) =>
        InstallLayout.ProductName + " " + (string.IsNullOrWhiteSpace(version) ? "(version not reported)" : version.Trim());

    /// The folders the copied payload needs, and the ones the installer creates. Split out so
    /// the GUI can say what it writes before a press does it.
    public static IReadOnlyList<string> FoldersCreated(InstallLayout layout) =>
    [
        layout.InstallFolder,
        layout.StartMenuFolder,
    ];
}
