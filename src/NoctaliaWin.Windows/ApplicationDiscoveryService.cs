// Installed-application discovery for the launcher.
//
// Sources, all documented and read-only:
//   1. Start Menu Program folders (per-user, then all-users) — .lnk shortcuts,
//      which is what the Windows Start menu itself enumerates.
//   2. App Paths registry keys (HKLM + HKCU, 32-bit view included) — the registration
//      mechanism installers use to make an executable resolvable by name.
//
// Not covered in this milestone: packaged (UWP/MSIX) apps. Enumerating them needs the
// Appx deployment API, so the launcher currently lists desktop apps only, and that is
// documented in the README instead of being faked with a Start-menu handoff.
using System.Security;
using Microsoft.Win32;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

public interface IShortcutResolver
{
    /// Resolved target of a .lnk, or null when it cannot be read.
    ShortcutTarget? Resolve(string shortcutPath);
}

public sealed class ShellLinkResolver : IShortcutResolver
{
    public ShortcutTarget? Resolve(string shortcutPath)
    {
        var target = ShellLinkReader.Read(shortcutPath);
        return target.Path is null ? null : target;
    }
}

public sealed class ApplicationDiscoveryService : IApplicationDiscoveryService
{
    public const string UserStartMenuRelative = @"Microsoft\Windows\Start Menu\Programs";
    public const string AppPathsRelative = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    private readonly IReadOnlyList<(string Root, int Priority)> _startMenuRoots;
    private readonly IShortcutResolver _resolver;
    private readonly Func<IEnumerable<AppCandidate>> _appPathsReader;
    private List<AppEntry> _entries = [];

    public event Action? Changed;

    public ApplicationDiscoveryService()
        : this(DefaultStartMenuRoots(), new ShellLinkResolver(), ReadAppPaths)
    {
    }

    internal ApplicationDiscoveryService(
        IReadOnlyList<(string Root, int Priority)> startMenuRoots,
        IShortcutResolver resolver,
        Func<IEnumerable<AppCandidate>> appPathsReader)
    {
        _startMenuRoots = startMenuRoots;
        _resolver = resolver;
        _appPathsReader = appPathsReader;
        Refresh();
    }

    public IReadOnlyList<AppEntry> Applications => _entries;

    public static IReadOnlyList<(string Root, int Priority)> DefaultStartMenuRoots()
    {
        var roots = new List<(string, int)>();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (appData.Length > 0) roots.Add((Path.Combine(appData, UserStartMenuRelative), 0));
        if (common.Length > 0) roots.Add((Path.Combine(common, UserStartMenuRelative), 1));
        return roots;
    }

    public void Refresh()
    {
        var next = Build();
        if (_entries.Count == next.Count && _entries.SequenceEqual(next)) return;
        _entries = next;
        Changed?.Invoke();
    }

    internal List<AppEntry> Build()
    {
        // priority 0 (per-user) wins over 1 (all-users); shortcuts win over App Paths.
        var byTarget = new Dictionary<string, (int Rank, AppEntry Entry)>(StringComparer.OrdinalIgnoreCase);
        void Offer(int rank, AppEntry entry, string key)
        {
            if (!byTarget.TryGetValue(key, out var existing) || rank < existing.Rank)
                byTarget[key] = (rank, entry);
        }

        foreach (var (root, priority) in _startMenuRoots)
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> links;
            try
            {
                links = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                continue; // a single unreadable folder must not empty the launcher
            }

            foreach (var link in links)
            {
                var name = Path.GetFileNameWithoutExtension(link);
                if (IsHousekeepingShortcut(name)) continue;

                var target = _resolver.Resolve(link);
                if (target?.Path is not { Length: > 0 } path) continue;
                if (!IsLaunchable(path)) continue;

                Offer(10 + priority, new AppEntry(
                    Name: name,
                    Exec: path,
                    Args: string.IsNullOrWhiteSpace(target.Arguments) ? null : target.Arguments,
                    IconPath: string.IsNullOrWhiteSpace(target.IconLocation) ? link : target.IconLocation,
                    Kind: "desktop"), path + "\n" + target.Arguments);
            }
        }

        foreach (var candidate in _appPathsReader())
        {
            if (!IsLaunchable(candidate.ExePath)) continue;
            var name = string.IsNullOrWhiteSpace(candidate.DisplayName)
                ? Path.GetFileNameWithoutExtension(candidate.ExePath)
                : candidate.DisplayName;
            Offer(20, new AppEntry(name, candidate.ExePath, null, candidate.IconPath, "desktop"),
                candidate.ExePath + "\n");
        }

        return byTarget.Values
            .Select(v => v.Entry)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// Uninstallers, help readers and link-to-web pages are noise in a launcher.
    internal static bool IsHousekeepingShortcut(string name) =>
        name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Visit ", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Documentation", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Help", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLaunchable(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".exe" or ".bat" or ".cmd" or ".com" or ".msi" or ".lnk" or ".appref-ms"))
            return false;
        try
        {
            return File.Exists(path);
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// HKLM first (machine-wide installs), then HKCU, then the 32-bit registry view.
    internal static IReadOnlyList<AppCandidate> ReadAppPaths()
    {
        var results = new List<AppCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Default),
                     (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Default),
                 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var appPaths = baseKey.OpenSubKey(AppPathsRelative);
                if (appPaths is null) continue;

                foreach (var sub in appPaths.GetSubKeyNames())
                {
                    using var key = appPaths.OpenSubKey(sub);
                    if (key?.GetValue(null) is not string exe || string.IsNullOrWhiteSpace(exe)) continue;

                    var expanded = Environment.ExpandEnvironmentVariables(exe.Trim('"'));
                    if (!seen.Add(expanded)) continue;

                    results.Add(new AppCandidate(
                        ExePath: expanded,
                        DisplayName: Path.GetFileNameWithoutExtension(sub),
                        IconPathName: key.GetValue("IconPath") as string));
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException)
            {
                // Missing or locked hives are normal on a clean machine.
            }
        }

        return results;
    }
}

public sealed record AppCandidate(string ExePath, string DisplayName, string? IconPathName)
{
    public string? IconPath => string.IsNullOrWhiteSpace(IconPathName) ? null : IconPathName;
}
