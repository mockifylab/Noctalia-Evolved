// The writes the installer performs, in the order they can be undone: files first, then the
// Start Menu shortcut, then the startup entry last. It never stops a running process, never
// asks for elevation, and never writes outside this user's own folders.
using System.IO;
using System.Linq;
using System.Reflection;
using NoctaliaWin.Core.Installs;
using NoctaliaWin.Core.Updates;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Installer;

/// The stage the installer is in, so the window can name it instead of showing a bar that means
/// nothing.
internal enum InstallStage
{
    Checking,
    Copying,
    Shortcut,
    Startup,
}

internal readonly record struct InstallProgress(InstallStage Stage, int Done, int Total);

/// Whether the writes finished, alongside the text the conclusion step shows. The window cannot
/// tell success from the sentences alone, because a partial install says what went wrong in
/// perfectly good prose.
internal readonly record struct InstallResult(bool Succeeded, string Summary);

internal readonly record struct InstallCheck(string Label, string Detail, bool Passed);

internal static class InstallationRunner
{
    /// What has to be true before anything is copied, one line at a time. The setup window shows
    /// every answer, including the ones that passed, so a person can see what was checked rather
    /// than being told "ready" with no reason.
    public static IReadOnlyList<InstallCheck> Checks(InstallLayout layout)
    {
        string? payload = InstallDecision.CheckPayload(layout.PayloadFolder);
        int files = payload is null ? FileCount(layout.PayloadFolder) : 0;

        string? runtime = DesktopRuntime.MissingWindowsDesktop();
        string found = DesktopRuntime.FoundWindowsDesktop() ?? "an unreadable place";

        bool busy = layout.IsInstalled && IsBusy(layout.ExecutablePath);
        string target = layout.IsInstalled
            ? $"An installed build is here, and its files would be replaced:\n{layout.InstallFolder}"
            : $"Nothing is in the way yet:\n{layout.InstallFolder}";

        return
        [
            new InstallCheck("This bundle",
                payload ?? $"Complete — {files} files, including {InstallLayout.ExecutableName}.",
                payload is null),
            new InstallCheck(".NET Desktop Runtime",
                runtime ?? $"Version {found} is on this machine.",
                runtime is null),
            new InstallCheck("The target folder",
                busy
                    ? "Noctalia Evolved is running from the folder this installer would write to. "
                        + "Quit it from the tray icon first — the installer will not stop a running "
                        + "program for you."
                    : target,
                !busy),
        ];
    }

    /// Copies the payload and wires up the two conveniences a person can opt into. Returns whether
    /// the writes finished alongside the text for the conclusion step, and only leaves a
    /// half-install when the disk said no — the files are copied first, so a failure there means
    /// nothing else was touched.
    public static InstallResult Install(InstallLayout layout, bool startWithWindows,
        IProgress<InstallProgress>? progress = null)
    {
        progress?.Report(new InstallProgress(InstallStage.Checking, 0, 0));

        var probe = Probe(layout);
        if (probe.Blocked) return new InstallResult(false, probe.Message);

        int total = FileCount(layout.PayloadFolder);
        var copies = new CopyProgress(progress, total);

        progress?.Report(new InstallProgress(InstallStage.Copying, 0, total));
        try
        {
            CopyTree(layout.PayloadFolder, layout.InstallFolder, copies);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidOperationException or System.Security.SecurityException)
        {
            return new InstallResult(false,
                $"The files could not be copied: {ex.Message}\n\n"
                + "If Noctalia is running, quit it from the tray icon and try again.");
        }

        progress?.Report(new InstallProgress(InstallStage.Shortcut, total, total));
        string shortcut = CreateShortcut(layout);

        progress?.Report(new InstallProgress(InstallStage.Startup, total, total));
        string startup = startWithWindows ? SetStartup(layout) : "Start with Windows left off.";

        return new InstallResult(true, $"Installed to\n{layout.InstallFolder}\n\n"
            + $"{shortcut}\n{startup}\n\n"
            + "Run Noctalia Evolved from the Start Menu entry, or launch "
            + $"{InstallLayout.ExecutableName} in the folder above.");
    }

    /// What the installer would do right now, before anyone presses the button: whether the
    /// payload is whole, whether the target is writable, and what the desktop runtime situation
    /// looks like.
    public static (bool Blocked, string Message) Probe(InstallLayout layout)
    {
        foreach (InstallCheck check in Checks(layout))
        {
            if (!check.Passed) return (true, check.Detail);
        }

        return (false, string.Empty);
    }

    /// How many files the bundle holds, which is what makes the progress bar mean something.
    public static int FileCount(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// The copy counter. Progress is reported through IProgress, which hands the number back to
    /// whoever created it — the UI thread — so no WPF element is touched from this thread.
    private sealed class CopyProgress(IProgress<InstallProgress>? progress, int total)
    {
        private int copied;

        public void FileCopied()
        {
            copied++;
            progress?.Report(new InstallProgress(InstallStage.Copying, copied, total));
        }
    }

    private static bool IsBusy(string executable)
    {
        if (!File.Exists(executable)) return false;

        try
        {
            using var _ = File.Open(executable, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            // Windows holds an exclusive-ish lock on a running executable, which is how "it is
            // running" is noticed without asking a process list who else is on this machine.
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void CopyTree(string source, string target, CopyProgress copies)
    {
        Directory.CreateDirectory(target);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            string destination = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
            copies.FileCopied();
        }

        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            // A framework-dependent publish keeps its layout (runtimes/, the culture folders),
            // so the tree is mirrored as it is rather than flattened.
            CopyTree(folder, Path.Combine(target, Path.GetFileName(folder)), copies);
        }
    }

    private static string CreateShortcut(InstallLayout layout)
    {
        // .lnk files are written by the shell's own scripting object; there is no documented
        // managed API for one, and a hand-rolled binary link is not worth the risk of a broken
        // Start Menu entry.
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return "No Start Menu shortcut: this Windows install has no shell scripting object. "
                + $"Launch {layout.ExecutablePath} directly instead.";
        }

        object? shell = null;
        try
        {
            Directory.CreateDirectory(layout.StartMenuFolder);
            shell = Activator.CreateInstance(shellType);

            object? link = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, [layout.ShortcutPath]);
            if (link is null) return "No Start Menu shortcut could be created.";

            Set(link, "TargetPath", layout.ExecutablePath);
            Set(link, "WorkingDirectory", layout.InstallFolder);
            Set(link, "Description", "The Noctalia experience on Windows.");
            link.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);

            return $"Start Menu shortcut created:\n{layout.ShortcutPath}";
        }
        catch (Exception ex) when (ex is TargetInvocationException or IOException
                                       or UnauthorizedAccessException or InvalidOperationException)
        {
            string reason = ex is TargetInvocationException nested && nested.InnerException is { } inner
                ? inner.Message
                : ex.Message;

            return $"No Start Menu shortcut could be created: {reason}. "
                + $"Launch {layout.ExecutablePath} directly instead.";
        }
        finally
        {
            if (shell is IDisposable disposable) disposable.Dispose();
        }

        static void Set(object link, string property, string value) =>
            link.GetType().InvokeMember(property, BindingFlags.SetProperty, null, link, [value]);
    }

    private static string SetStartup(InstallLayout layout)
    {
        try
        {
            // The same value name and argument the running app writes for itself, so the app's
            // own "Start with Windows" row keeps working on an installed copy.
            new RegistryRunKeyStore().Set(
                StartupService.ValueName,
                $"\"{layout.ExecutablePath}\" {StartupService.SilentArgument}");

            return "Starts with Windows (turn it off in the app's Windows settings page).";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException
                                       or InvalidOperationException or UnauthorizedAccessException)
        {
            return $"Could not add the startup entry: {ex.Message}. The app's own "
                + "“Start with Windows” row writes the same value.";
        }
    }
}

/// The one thing an offline installer must not pretend about: whether the runtime the app needs
/// is on the machine. A framework-dependent app has to find its shared framework, so the
/// folders the runtime is installed into are the place to look — a registry install record is
/// written by some installers and not others, and a per-user SDK install leaves it out entirely.
internal static class DesktopRuntime
{
    private const string Relative = @"dotnet\shared\Microsoft.WindowsDesktop.App";
    private const int MinMajor = 10;

    public static string? MissingWindowsDesktop() =>
        FoundWindowsDesktop() is null
            ? ".NET " + MinMajor + " Desktop Runtime is not installed where this installer can "
                + "see it. The app is framework-dependent, so it will not start without that "
                + "runtime, and nothing has been copied."
            : null;

    /// The newest desktop framework this machine has, in the form the setup window can say out
    /// loud. Null means there is none new enough, which is the same answer as the blocker above.
    public static string? FoundWindowsDesktop()
    {
        string? newest = null;
        SemanticVersion? parsedNewest = null;

        foreach (string name in InstalledFrameworkVersions())
        {
            if (MajorOf(name) < MinMajor) continue;

            SemanticVersion? parsed = SemanticVersion.Parse(name);
            if (parsed is null) continue;

            if (parsedNewest is null || parsed.IsNewerThan(parsedNewest))
            {
                parsedNewest = parsed;
                newest = name;
            }
        }

        return newest;
    }

    private static IReadOnlyList<string> InstalledFrameworkVersions()
    {
        var found = new List<string>();

        foreach (string root in Roots())
        {
            string shared = Path.Combine(root, Relative);
            if (!Directory.Exists(shared)) continue;

            try
            {
                foreach (string version in Directory.EnumerateDirectories(shared))
                {
                    found.Add(Path.GetFileName(version));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An unreadable folder says nothing about whether the runtime is elsewhere.
            }
        }

        return found;
    }

    /// Where a Windows machine keeps a shared framework: the machine-wide folders, a
    /// per-user install under the profile, and DOTNET_ROOT when someone set it.
    private static IEnumerable<string> Roots()
    {
        foreach (string? variable in new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is { Length: > 0 } local
                ? Path.Combine(local, "Microsoft")
                : null,
        })
        {
            if (!string.IsNullOrWhiteSpace(variable)) yield return variable;
        }
    }

    /// The major from a folder name like `10.0.2` or `9.0.0-preview.5`. Anything unreadable is
    /// zero, which is "not the runtime we need" rather than a guess that it might be.
    internal static int MajorOf(string? versionText)
    {
        string text = (versionText ?? "").Trim();
        int dot = text.IndexOf('.');
        string head = dot < 0 ? text : text[..dot];

        return int.TryParse(head, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int major) ? major : 0;
    }
}
