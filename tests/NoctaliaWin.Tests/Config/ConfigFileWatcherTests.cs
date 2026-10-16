// The watcher's own tests cover which folders it watches, that tearing it down is safe, and
// that an edit reaches the store through the caller's thread. What it decides to do when an
// event arrives is SettingsStore.ReloadIfChanged, tested there directly and without waiting on
// the file system.
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

        // Wait for the value rather than for the first event. An editor empties the file on its
        // way to filling it back in, the store ignores that half-written moment, and the event
        // that matters is the one carrying the text.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.Current.Main.Thickness != 26 && DateTime.UtcNow < deadline)
            System.Threading.Thread.Sleep(25);

        Assert.Equal(26, store.Current.Main.Thickness);
        Assert.True(posted > 0, "the watcher did not post the edit within five seconds");
    }
}
