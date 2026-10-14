using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

public sealed class StartupServiceTests
{
    private sealed class FakeStore : IRunKeyStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Removed { get; } = [];

        public string? Get(string valueName) => Values.GetValueOrDefault(valueName);
        public void Set(string valueName, string command) => Values[valueName] = command;
        public void Remove(string valueName)
        {
            Removed.Add(valueName);
            Values.Remove(valueName);
        }
    }

    [Fact]
    public void Enabled_ReflectsWhetherTheRunKeyHasOurValue()
    {
        var store = new FakeStore();
        var startup = new StartupService(store, () => @"C:\Apps\NoctaliaWin.exe");

        Assert.False(startup.Enabled);

        store.Values["OneDrive"] = "C:\\OneDrive.exe";
        Assert.False(startup.Enabled); // another app's entry must not count as ours

        store.Values[StartupService.ValueName] = "\"C:\\Apps\\NoctaliaWin.exe\" --startup";
        Assert.True(startup.Enabled);
    }

    [Fact]
    public void Enabling_WritesAQuotedPathWithTheSilentArgument()
    {
        var store = new FakeStore();
        var startup = new StartupService(store, () => @"C:\Program Files\Noctalia\Noctalia Win.exe");

        startup.SetEnabled(true);

        Assert.Equal(
            $"\"C:\\Program Files\\Noctalia\\Noctalia Win.exe\" {StartupService.SilentArgument}",
            store.Values[StartupService.ValueName]);
        Assert.True(startup.Enabled);
    }

    [Fact]
    public void Disabling_RemovesOnlyOurOwnValue()
    {
        var store = new FakeStore();
        store.Values["OneDrive"] = "C:\\OneDrive.exe";
        store.Values[StartupService.ValueName] = "anything";
        var startup = new StartupService(store, () => @"C:\Apps\NoctaliaWin.exe");

        startup.SetEnabled(false);

        Assert.False(startup.Enabled);
        Assert.Equal(StartupService.ValueName, Assert.Single(store.Removed));
        Assert.Equal("C:\\OneDrive.exe", store.Values["OneDrive"]);
    }

    [Fact]
    public void Enabling_WithoutAKnownExecutablePath_FailsLoudly()
    {
        var store = new FakeStore();
        var startup = new StartupService(store, () => null);

        Assert.Throws<InvalidOperationException>(() => startup.SetEnabled(true));
        Assert.Empty(store.Values);
    }

    [Fact]
    public void Command_IsTheStoredLaunchLine()
    {
        var store = new FakeStore();
        var startup = new StartupService(store, () => @"C:\Apps\NoctaliaWin.exe");

        Assert.Null(startup.Command);
        startup.SetEnabled(true);
        Assert.Contains("NoctaliaWin.exe", startup.Command);
    }

    [Fact]
    public void RealStore_UsesThePerUserRunKeyOnly()
    {
        // HKCU, never HKLM: the app must not need admin rights to start with Windows.
        Assert.Equal(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            RegistryRunKeyStore.RunKeyPath);
    }

    [Fact]
    public void RealStore_ReadingOnThisMachineNeverThrows()
    {
        var store = new RegistryRunKeyStore();
        _ = store.Get(StartupService.ValueName);
        store.Remove("NoctaliaWin-Definitely-Absent-Test-Value");
    }
}
