// The watcher's own tests cover what a test can assert without waiting on the file system:
// which folders it watches and that tearing it down is safe. What it decides to do when an
// event arrives is SettingsStore.ReloadIfChanged, tested there directly.
using NoctaliaWin.Core.Config;
using NoctaliaWin.Tests.Config;

namespace NoctaliaWin.Tests.Config;

public class ConfigFileWatcherTests
{
    [Fact]
    public void AFolderThatDoesNotExistYetIsWatchedNothing()
    {
        using var dir = new TempConfigDir();
        var missing = Path.Combine(dir.Path, "not-yet");
        var store = new SettingsStore(missing);

        using var watch = new ConfigFileWatcher(store);

        Assert.False(Directory.Exists(missing));
        Assert.False(watch.IsWatching);
    }

    [Fact]
    public void WatchingARealFolderSucceedsAndCanBeTornDownTwice()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());

        var watch = new ConfigFileWatcher(store);
        Assert.True(watch.IsWatching);

        watch.Dispose();
        watch.Dispose();
    }

    [Fact]
    public void AChangedFileIsReloadedThroughTheCallersOwnThread()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());

        var posted = 0;
        using var watch = new ConfigFileWatcher(store, run =>
        {
            // Nothing is handed over until an event arrives, so counting the posts is how the
            // test says "the reload would have happened where the app can do it".
            posted++;
            run();
        });

        File.WriteAllText(dir.ConfigPath,
            File.ReadAllText(dir.ConfigPath).Replace("thickness = 34", "thickness = 26"));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (posted == 0 && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(25);

        Assert.True(posted > 0, "the watcher did not post the edit within five seconds");
        Assert.Equal(26, store.Current.Main.Thickness);
    }
}
