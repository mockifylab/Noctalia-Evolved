using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Tests.Windows;

/// <summary>
/// Discovery is tested against real temporary folders and fake .lnk files, with the
/// shell-link read stubbed out: the logic under test is source merging, ranking and
/// filtering, not IShellLink itself.
/// </summary>
public sealed class ApplicationDiscoveryServiceTests : IDisposable
{
    private sealed class TempDir : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "noctalia-win-discovery-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Root);

        public string SubDirectory(string name)
        {
            var path = System.IO.Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { /* leave temp dirs to the OS cleaner */ }
        }
    }

    private readonly TempDir _temp = new();
    private readonly string _bin;

    public ApplicationDiscoveryServiceTests()
    {
        // Targets have to exist on disk, otherwise IsLaunchable correctly rejects them.
        _bin = _temp.SubDirectory("bin");
        foreach (var exe in new[] { "firefox", "code", "terminal", "notepad", "helper", "old" })
            File.WriteAllText(Path.Combine(_bin, $"{exe}.exe"), "");
        File.WriteAllText(Path.Combine(_bin, "notes.txt"), "");
    }

    private sealed class FakeResolver : IShortcutResolver
    {
        public Dictionary<string, ShortcutTarget?> ByLinkName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ShortcutTarget? Resolve(string shortcutPath) =>
            ByLinkName.GetValueOrDefault(Path.GetFileNameWithoutExtension(shortcutPath));
    }

    private string Exe(string name) => Path.Combine(_bin, $"{name}.exe");

    private string WriteLink(string root, string name)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{name}.lnk");
        File.WriteAllText(path, "fake shortcut");
        return path;
    }

    private (string Root, int Priority) Root(string name) =>
        (_temp.SubDirectory(name), name == "user" ? 0 : 1);

    private ApplicationDiscoveryService Service(
        IShortcutResolver resolver,
        IReadOnlyList<(string Root, int Priority)> roots,
        params AppCandidate[] appPaths) =>
        new(roots, resolver, () => appPaths);

    [Fact]
    public void Shortcuts_AreEnumeratedRecursivelyAndSortedByName()
    {
        var (root, priority) = Root("user");
        var nested = Path.Combine(root, "Accessories");
        var resolver = new FakeResolver();

        WriteLink(root, "Firefox");
        WriteLink(nested, "Terminal");
        WriteLink(root, "Code");
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", null);
        resolver.ByLinkName["Terminal"] = new(Exe("terminal"), "", null);
        resolver.ByLinkName["Code"] = new(Exe("code"), "", null);

        var apps = Service(resolver, [(root, priority)]).Applications;

        Assert.Equal(["Code", "Firefox", "Terminal"], apps.Select(a => a.Name));
        Assert.All(apps, a => Assert.Equal("desktop", a.Kind));
        Assert.Equal(Exe("terminal"), apps.Single(a => a.Name == "Terminal").Exec);
    }

    [Fact]
    public void HousekeepingShortcuts_AreRecognised()
    {
        Assert.True(ApplicationDiscoveryService.IsHousekeepingShortcut("Uninstall Firefox"));
        Assert.True(ApplicationDiscoveryService.IsHousekeepingShortcut("Visit Firefox.com"));
        Assert.True(ApplicationDiscoveryService.IsHousekeepingShortcut("Help"));
        Assert.True(ApplicationDiscoveryService.IsHousekeepingShortcut("documentation"));
        Assert.False(ApplicationDiscoveryService.IsHousekeepingShortcut("Firefox"));
        Assert.False(ApplicationDiscoveryService.IsHousekeepingShortcut("Visual Studio Code"));
    }

    [Fact]
    public void HousekeepingShortcuts_NeverReachTheShellLinkResolver()
    {
        var (root, priority) = Root("user");
        var resolver = new CountingResolver();
        WriteLink(root, "Uninstall Firefox");
        WriteLink(root, "Help");
        WriteLink(root, "Firefox");

        var service = Service(resolver, [(root, priority)]);

        // Only the real app is resolved; the filtered names are skipped first.
        Assert.Equal(1, resolver.Calls);
        Assert.Empty(service.Applications); // and its guessed target does not exist on disk
    }

    private sealed class CountingResolver : IShortcutResolver
    {
        public int Calls;
        public ShortcutTarget? Resolve(string shortcutPath)
        {
            Calls++;
            return new(shortcutPath.Replace(".lnk", ".exe"), "", null);
        }
    }

    [Fact]
    public void UnreadableTargetsAndNonExecutables_AreDropped()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        WriteLink(root, "Notes");
        WriteLink(root, "Missing");
        WriteLink(root, "Firefox");

        resolver.ByLinkName["Notes"] = new(Path.Combine(_bin, "notes.txt"), "", null);
        resolver.ByLinkName["Missing"] = new(Path.Combine(_bin, "ghost.exe"), "", null);
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", null);

        var apps = Service(resolver, [(root, priority)]).Applications;

        Assert.Equal("Firefox", Assert.Single(apps).Name);
    }

    [Fact]
    public void ResolverReturningNull_SkipsTheShortcut()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        WriteLink(root, "Broken");
        WriteLink(root, "Firefox");
        resolver.ByLinkName["Broken"] = null;
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", null);

        Assert.Equal("Firefox", Assert.Single(Service(resolver, [(root, priority)]).Applications).Name);
    }

    [Fact]
    public void SameTargetInBothRoots_KeepsThePerUserShortcutName()
    {
        var user = Root("user");
        var allUsers = Root("allusers");
        var resolver = new FakeResolver();

        WriteLink(user.Item1, "Firefox (user)");
        WriteLink(allUsers.Item1, "Firefox (machine)");
        resolver.ByLinkName["Firefox (user)"] = new(Exe("firefox"), "", null);
        resolver.ByLinkName["Firefox (machine)"] = new(Exe("firefox"), "", null);

        var apps = Service(resolver, [user, allUsers]).Applications;

        var entry = Assert.Single(apps);
        Assert.Equal("Firefox (user)", entry.Name);
    }

    [Fact]
    public void AllUsersRoot_IsUsedWhenThePerUserRootDoesNotExist()
    {
        var allUsers = Root("allusers");
        var resolver = new FakeResolver();
        WriteLink(allUsers.Item1, "Firefox");
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", null);

        var apps = Service(resolver, [(@"C:\Definitely\Not\Here", 0), allUsers]).Applications;

        Assert.Equal("Firefox", Assert.Single(apps).Name);
    }

    [Fact]
    public void SameExeWithDifferentArguments_KeepsBothEntries()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        WriteLink(root, "Terminal");
        WriteLink(root, "Terminal (new window)");
        resolver.ByLinkName["Terminal"] = new(Exe("terminal"), "", null);
        resolver.ByLinkName["Terminal (new window)"] = new(Exe("terminal"), "--new-window", null);

        var apps = Service(resolver, [(root, priority)]).Applications;

        Assert.Equal(2, apps.Count);
        Assert.Equal("--new-window", apps.Single(a => a.Args is not null).Args);
    }

    [Fact]
    public void AppPathsFillGapsButNeverOutrankShortcuts()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        WriteLink(root, "Firefox Browser");
        resolver.ByLinkName["Firefox Browser"] = new(Exe("firefox"), "", null);

        var service = Service(
            resolver,
            [(root, priority)],
            new AppCandidate(Exe("firefox"), "firefox", null),
            new AppCandidate(Exe("notepad"), "notepad", null));

        var apps = service.Applications;
        Assert.Equal(["Firefox Browser", "notepad"], apps.Select(a => a.Name));
        Assert.Equal(Exe("firefox"), apps.Single(a => a.Name == "Firefox Browser").Exec);
    }

    [Fact]
    public void AppPathsCandidates_AreFilteredTheSameWay()
    {
        var resolver = new FakeResolver();
        var service = Service(
            resolver,
            [(Root("user").Item1, 0)],
            new AppCandidate(Path.Combine(_bin, "notes.txt"), "notes", null),
            new AppCandidate(Path.Combine(_bin, "ghost.exe"), "ghost", null),
            new AppCandidate(Exe("helper"), "helper", @"%SystemRoot%\helper.exe,0"));

        var apps = Assert.Single(service.Applications);
        Assert.Equal("helper", apps.Name);
        Assert.Equal(@"%SystemRoot%\helper.exe,0", apps.IconPath);
    }

    [Fact]
    public void IconPath_FallsBackToTheShortcutItself()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        var link = WriteLink(root, "Firefox");
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", "");

        var apps = Service(resolver, [(root, priority)]).Applications;

        Assert.Equal(link, Assert.Single(apps).IconPath);
    }

    [Fact]
    public void Refresh_IsSilentWhenNothingChangedAndFiresOnceOtherwise()
    {
        var (root, priority) = Root("user");
        var resolver = new FakeResolver();
        WriteLink(root, "Firefox");
        resolver.ByLinkName["Firefox"] = new(Exe("firefox"), "", null);

        var service = Service(resolver, [(root, priority)]);
        int raised = 0;
        service.Changed += () => raised++;

        service.Refresh();
        Assert.Equal(0, raised);

        WriteLink(root, "Code");
        resolver.ByLinkName["Code"] = new(Exe("code"), "", null);
        service.Refresh();
        Assert.Equal(1, raised);
        Assert.Equal(2, service.Applications.Count);
    }

    [Theory]
    [InlineData("firefox.exe", true)]   // installer-registered executables
    [InlineData("setup.msi", true)]
    [InlineData("build.cmd", true)]
    [InlineData("tool.bat", true)]
    [InlineData("notes.txt", false)]   // documents are not apps
    [InlineData("manual.pdf", false)]
    [InlineData("ghost.exe", false)]   // right extension, missing file
    public void LaunchableFilter_AcceptsExistingExecutablesOnly(string fileName, bool expected)
    {
        var path = Path.Combine(_bin, fileName);
        if (expected) File.WriteAllText(path, "");
        Assert.Equal(expected, ApplicationDiscoveryService.IsLaunchable(path));
    }

    public void Dispose() => _temp.Dispose();
}
