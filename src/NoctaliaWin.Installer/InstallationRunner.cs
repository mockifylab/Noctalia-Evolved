// The writes the installer performs, in the order they can be undone: files first, then the
// Start Menu shortcut, then the startup entry last. It never stops a running process, never
// asks for elevation, and never writes outside this user's own folders.
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using NoctaliaWin.Core.Installs;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Installer;

internal static class InstallationRunner
{
    /// Copies the payload and wires up the two conveniences a person can opt into. Returns the
    /// text for the installer's status line, and only leaves a half-install when the disk said
    /// no — the files are copied first, so a failure there means nothing else was touched.
    public static string Install(InstallLayout layout, bool startWithWindows)
    {
        var probe = Probe(layout);
        if (probe.Blocked) return probe.Message;

        try
        {
            CopyTree(layout.PayloadFolder, layout.InstallFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or InvalidOperationException or System.Security.SecurityException)
        {
            return $"The files could not be copied: {ex.Message}\n\n"
                + "If Noctalia is running, quit it from the tray icon and try again.";
        }

        string shortcut = CreateShortcut(layout);
        string startup = startWithWindows ? SetStartup(layout) : "Start with Windows left off.";

        return $"Installed to\n{layout.InstallFolder}\n\n"
            + $"{shortcut}\n{startup}\n\n"
            + "Run Noctalia Evolved from the Start Menu entry, or launch "
            + $"{InstallLayout.ExecutableName} in the folder above.";
    }

    /// What the installer would do right now, before anyone presses the button: whether the
    /// payload is whole, whether the target is writable, and what the desktop runtime situation
    /// looks like.
    public static (bool Blocked, string Message) Probe(InstallLayout layout)
    {
        string? payload = InstallDecision.CheckPayload(layout.PayloadFolder);
        if (payload is not null) return (true, payload);

        if (layout.IsInstalled && IsBusy(layout.ExecutablePath))
        {
            return (true,
                "Noctalia Evolved is running from the folder this installer would write to. "
                    + "Quit it from the tray icon first — the installer will not stop a running "
                    + "program for you.");
        }

        if (DesktopRuntime.MissingWindowsDesktop() is { } missing)
        {
            return (true, missing);
        }

        return (false, string.Empty);
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

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            string destination = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destination, overwrite: true);
        }

        foreach (string folder in Directory.EnumerateDirectories(source))
        {
            // A framework-dependent publish keeps its layout (runtimes/, the culture folders),
            // so the tree is mirrored as it is rather than flattened.
            CopyTree(folder, Path.Combine(target, Path.GetFileName(folder)));
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
/// is on the machine. The registry's own install record is the documented place to look.
internal static class DesktopRuntime
{
    private const string MinMajor = "10";

    public static string? MissingWindowsDesktop()
    {
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            try
            {
                using var base64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = base64.OpenSubKey(
                    @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");

                if (key is null) continue;
                if (key.GetValueNames().Any(v => v.StartsWith(MinMajor, StringComparison.Ordinal)))
                    return null;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException
                                           or UnauthorizedAccessException or InvalidOperationException)
            {
                // A hive this user cannot read is not the same as a runtime that is missing, so
                // the search continues rather than reporting a problem that may not exist.
            }
        }

        return "The .NET " + MinMajor + " Desktop Runtime is not installed. The app is "
            + "framework-dependent, so it will not start until that runtime is. Nothing has "
            + "been copied.";
    }
}
