// Where the installer's payload is, and what version each side reports. Both come from the
// files on disk rather than from anything the installer remembers, so a moved or half-copied
// bundle is reported instead of being installed anyway.
using System.Diagnostics;
using System.IO;
using NoctaliaWin.Core.Installs;

namespace NoctaliaWin.Installer;

internal sealed record InstallerBundle(InstallLayout? Layout, string? Problem)
{
    /// The folders a bundle may ship with the setup executable, in the order they are tried.
    private static readonly string[] PayloadNames = ["payload", "app"];

    public bool HasPayload => Layout is not null && Problem is null;

    /// Resolves the bundle from the setup executable's own directory, or from a path given on
    /// the command line. A bundle without a payload is a problem to state, not a reason to
    /// offer an install that would copy nothing.
    public static InstallerBundle Read(string? explicitPayload)
    {
        string here = AppContext.BaseDirectory;

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPayload)) candidates.Add(explicitPayload.Trim());
        foreach (string name in PayloadNames) candidates.Add(Path.Combine(here, name));

        foreach (string candidate in candidates)
        {
            string? problem = InstallDecision.CheckPayload(candidate);
            if (problem is null)
            {
                var layout = InstallLayout.ForCurrentUser(candidate);
                return layout is null
                    ? new InstallerBundle(null, "This user's profile folders are not available, "
                        + "so there is nowhere to install to.")
                    : new InstallerBundle(layout, null);
            }

            if (explicitPayload is null) continue;

            // An explicit path that does not work is answered with its own reason, because the
            // person pointed at it deliberately and a generic "no payload" would be unhelpful.
            return new InstallerBundle(null, problem);
        }

        return new InstallerBundle(null,
            $"No published app was found beside this installer.\n\nLooked in:\n"
            + string.Join("\n", PayloadNames.Select(name => Path.Combine(here, name)))
            + "\n\nPublish the app into one of those folders, or pass the payload path as an "
            + "argument: NoctaliaEvolved-Setup.exe C:\\path\\to\\payload");
    }

    public string PayloadFolder => Layout!.PayloadFolder;

    /// The version the payload's apphost carries, or null when there is nothing to read it from.
    public string? IncomingVersion => VersionOf(Layout?.PayloadFolder);

    /// The version already on disk at the install path, or null for a first install.
    public string? InstalledVersion => Layout is { IsInstalled: true } installed
        ? VersionOf(installed.InstallFolder)
        : null;

    private static string? VersionOf(string? folder)
    {
        if (folder is null) return null;

        string executable = Path.Combine(folder, InstallLayout.ExecutableName);
        if (!File.Exists(executable)) return null;

        try
        {
            // The apphost's file metadata is what a person sees in Explorer's properties, which
            // makes it the honest thing to compare: it is set from the same single version
            // number the settings window shows.
            string? product = FileVersionInfo.GetVersionInfo(executable).ProductVersion;
            return string.IsNullOrWhiteSpace(product) ? null : product;
        }
        catch (ArgumentException)
        {
            // A file that is not a real executable — a zero-length copy from an interrupted
            // install, say — reports no version instead of taking the installer down with it.
            return null;
        }
    }
}
