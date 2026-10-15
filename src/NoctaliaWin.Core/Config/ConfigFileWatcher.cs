// Watching config.toml for edits made outside this app, so a hand edit reaches the bar without
// a restart. The decision about what counts as an edit lives in SettingsStore.ReloadIfChanged,
// which compares the file's text with what it already loaded — this class only turns events
// into that one call, so the interesting part is testable without a watcher and a clock.
using System.IO;

namespace NoctaliaWin.Core.Config;

public sealed class ConfigFileWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly ISettingsStore _store;
    private readonly Action<Action> _marshal;

    /// `marshal` runs a reload where the store's subscribers are safe to be called — on the
    /// UI thread in the app, inline in a test. File-system events arrive on a thread-pool
    /// thread, which is not somewhere a bar can be rebuilt from.
    public ConfigFileWatcher(ISettingsStore store, Action<Action>? marshal = null)
    {
        _store = store;
        _marshal = marshal ?? (run => run());

        var path = store.ConfigPath;
        var dir = Path.GetDirectoryName(path);
        if (dir is null || !Directory.Exists(dir)) return;

        _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => OnEvent();
        _watcher.Created += (_, _) => OnEvent();
        _watcher.Deleted += (_, _) => OnEvent();
        _watcher.Renamed += (_, _) => OnEvent();
        _watcher.EnableRaisingEvents = true;
    }

    /// True when there is a watcher on a real directory. The one case with none is a config
    /// folder that does not exist yet, where there is nothing to watch.
    public bool IsWatching => _watcher is not null;

    private void OnEvent() => _marshal(() => _store.ReloadIfChanged());

    public void Dispose()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
