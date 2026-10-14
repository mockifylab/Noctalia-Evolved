using System.Diagnostics;
using NoctaliaWin.Core.Abstractions;

namespace NoctaliaWin.Windows;

/// <summary>
/// Launches applications through the shell (UseShellExecute), so .lnk, .msi,
/// .appref-ms and shell: URIs are resolved by Windows itself rather than by us.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public string? Launch(AppEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Exec))
            return $"{entry.Name}: nothing to launch.";

        var startInfo = new ProcessStartInfo
        {
            FileName = entry.Exec,
            Arguments = entry.Args ?? string.Empty,
            UseShellExecute = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            // Shell-executed launches (documents, .lnk, UWP aliases) can return
            // null or already-exited processes; that is not a failure.
            _ = process;
            return null;
        }
        // A malformed path can reach us straight from a .lnk target or the config
        // file, so nothing here may be allowed to take the launcher down.
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or ArgumentException
            or FileNotFoundException)
        {
            return $"{entry.Name}: {ex.Message}";
        }
    }
}
