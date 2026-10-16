// SettingsStore behaviour on the real filesystem: persistence, atomic replace,
// corrupt-file tolerance and restore-defaults.
using NoctaliaWin.Core.Config;

namespace NoctaliaWin.Tests.Config;

public sealed class TempConfigDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "noctalia-win-tests-" + Guid.NewGuid().ToString("N"));

    public TempConfigDir() => Directory.CreateDirectory(Path);

    public string ConfigPath => System.IO.Path.Combine(Path, SettingsStore.ConfigFileName);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* leave temp dirs to the OS cleaner */ }
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void MissingFile_LoadsDefaultsWithoutDiagnostics()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);

        Assert.Equal(34, store.Current.Main.Thickness);
        Assert.Empty(store.LastDiagnostics);
        Assert.False(File.Exists(dir.ConfigPath));
    }

    [Fact]
    public void SaveThenReload_PersistsAcrossInstances()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);

        var config = store.Current.Clone();
        config.Bars["main"].Position = "bottom";
        config.Bars["main"].Thickness = 48;
        config.Theme.Mode = "light";
        store.Save(config);

        Assert.True(File.Exists(dir.ConfigPath));

        var reloaded = new SettingsStore(dir.Path);
        Assert.Equal("bottom", reloaded.Current.Main.Position);
        Assert.Equal(48, reloaded.Current.Main.Thickness);
        Assert.Equal("light", reloaded.Current.Theme.Mode);
        Assert.DoesNotContain(reloaded.LastDiagnostics, d => d.IsError);
    }

    [Fact]
    public void Save_IsVisibleThroughCurrentAndRaisesChanged()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        AppConfig? notified = null;
        store.Changed += c => notified = c;

        var config = store.Current.Clone();
        config.Bars["main"].MarginEnds = 250;
        store.Save(config);

        Assert.Equal(250, store.Current.Main.MarginEnds);
        Assert.NotNull(notified);
        Assert.Equal(250, notified!.Main.MarginEnds);
        // The store keeps its own copy: mutating the caller's object must not leak in.
        config.Bars["main"].MarginEnds = 999;
        Assert.Equal(250, store.Current.Main.MarginEnds);
    }

    [Fact]
    public void CreateDirectory_WhenItDoesNotExistYet()
    {
        using var dir = new TempConfigDir();
        var nested = System.IO.Path.Combine(dir.Path, "depth", "deeper");
        var store = new SettingsStore(nested);

        store.Save(store.Current.Clone());

        Assert.True(File.Exists(System.IO.Path.Combine(nested, SettingsStore.ConfigFileName)));
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultsWithADiagnosticAndDoesNotThrow()
    {
        using var dir = new TempConfigDir();
        File.WriteAllText(dir.ConfigPath, "]]] not toml [[[ garbage \x01");

        var store = new SettingsStore(dir.Path);

        Assert.Equal(34, store.Current.Main.Thickness);
        Assert.Contains(store.LastDiagnostics, d => d.IsError);

        // The bad file is preserved until the user saves over it.
        Assert.Contains("garbage", File.ReadAllText(dir.ConfigPath));
    }

    [Fact]
    public void PartiallyValidFile_LoadsGoodValuesAndReportsBadOnes()
    {
        using var dir = new TempConfigDir();
        File.WriteAllText(dir.ConfigPath, """
            [theme]
            mode = "light"

            [bar.main]
            thickness = 44
            position = "sideways"
            """);

        var store = new SettingsStore(dir.Path);

        Assert.Equal("light", store.Current.Theme.Mode);
        Assert.Equal(44, store.Current.Main.Thickness);
        Assert.Equal("top", store.Current.Main.Position);
        Assert.Contains(store.LastDiagnostics, d => d.Path == "bar.main.position" && !d.IsError);
    }

    [Fact]
    public void RestoreDefaults_WritesAndReturnsTheDefaultConfig()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);

        var custom = store.Current.Clone();
        custom.Bars["main"].Thickness = 60;
        store.Save(custom);

        var restored = store.RestoreDefaults();

        Assert.Equal(34, restored.Main.Thickness);
        Assert.Equal(34, store.Current.Main.Thickness);
        Assert.Equal(34, new SettingsStore(dir.Path).Current.Main.Thickness);
    }

    [Fact]
    public void Reload_PicksUpExternalEdits()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());

        File.WriteAllText(dir.ConfigPath, File.ReadAllText(dir.ConfigPath).Replace("thickness = 34", "thickness = 26"));
        store.Reload();

        Assert.Equal(26, store.Current.Main.Thickness);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());
        store.Save(store.Current.Clone());

        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void ReloadIfChanged_PicksUpAnExternalEditAndRaisesChanged()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());

        var raised = 0;
        store.Changed += _ => raised++;

        File.WriteAllText(dir.ConfigPath,
            File.ReadAllText(dir.ConfigPath).Replace("thickness = 34", "thickness = 26"));

        Assert.True(store.ReloadIfChanged());
        Assert.Equal(26, store.Current.Main.Thickness);
        Assert.Equal(1, raised);
    }

    /// An editor that empties the file before writing the new text back is caught by the watcher
    /// at that empty moment. Reloading there would put the bar back to its factory settings for
    /// an instant, so a present-but-empty file counts as a write in progress rather than a
    /// config that was cleared. The next event carries the text and is acted on as normal.
    [Fact]
    public void ReloadIfChanged_IgnoresAFileThatIsPresentButEmpty()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var custom = store.Current.Clone();
        custom.Bars["main"].Thickness = 26;
        store.Save(custom);
        var written = File.ReadAllText(dir.ConfigPath);

        File.WriteAllText(dir.ConfigPath, "");

        Assert.False(store.ReloadIfChanged());
        Assert.Equal(26, store.Current.Main.Thickness);

        File.WriteAllText(dir.ConfigPath, written.Replace("thickness = 26", "thickness = 30"));

        Assert.True(store.ReloadIfChanged());
        Assert.Equal(30, store.Current.Main.Thickness);
    }

    /// One save arrives as several file-system events, and the app's own writes are events too.
    /// A second look at unchanged text has to be silent, or every setting change would rebuild
    /// the bar a handful of times.
    [Fact]
    public void ReloadIfChanged_IsSilentWhenTheTextHasNotChanged()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        store.Save(store.Current.Clone());

        var raised = 0;
        store.Changed += _ => raised++;

        Assert.False(store.ReloadIfChanged());
        Assert.False(store.ReloadIfChanged());
        Assert.Equal(0, raised);

        store.Save(store.Current.Clone());
        Assert.Equal(1, raised);   // the write itself, and nothing else

        // The events an atomic replace leaves behind must not read as somebody else's edit.
        Assert.False(store.ReloadIfChanged());
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ReloadIfChanged_FallsBackToDefaultsWhenTheFileGoesAway()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var custom = store.Current.Clone();
        custom.Bars["main"].Thickness = 60;
        store.Save(custom);

        var raised = 0;
        store.Changed += _ => raised++;

        File.Delete(dir.ConfigPath);

        Assert.True(store.ReloadIfChanged());
        Assert.Equal(34, store.Current.Main.Thickness);
        Assert.Equal(1, raised);
    }
}
