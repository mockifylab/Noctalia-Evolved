// The window's only route to a setting. A row's write is validated first, applied to a clone
// of the stored config, and only then saved - so a rejected value leaves the file untouched
// and the shell never rebuilds around a half-applied change.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;

namespace NoctaliaWin.Core.Settings;

public sealed class SettingsEditor
{
    private readonly ISettingsStore _store;
    private readonly IStartupService? _autostart;
    private readonly string? _appVersion;

    public SettingsEditor(
        ISettingsStore store,
        IStartupService? autostart = null,
        string? appVersion = null)
    {
        _store = store;
        _autostart = autostart;
        _appVersion = appVersion;
    }

    public AppConfig Config => _store.Current;

    public IReadOnlyList<ConfigDiagnostic> Diagnostics => _store.LastDiagnostics;

    /// Why the last write did not land, or null. The window shows this as its status banner
    /// and clears it as soon as a write succeeds.
    public string? LastError { get; private set; }

    public SettingsScope Scope() => NewScope(_store.Current);

    public string ValueOf(SettingsField field) => field.ValueFor(Scope());

    /// Applies a value and persists it. Returns null when it took, or the reason it did not.
    public string? Apply(SettingsField field, string value)
    {
        if (field.IsReadOnly)
        {
            LastError = $"{field.Title} cannot be changed here";
            return LastError;
        }

        var config = _store.Current.Clone();
        var error = field.WriteValue(NewScope(config), value);
        if (error is not null)
        {
            LastError = $"{field.Title}: {error}";
            return LastError;
        }

        _store.Save(config);
        LastError = null;
        return null;
    }

    public string? ResetField(SettingsField field) => Apply(field, field.DefaultValue);

    /// Writes every overridden row of one page back to its default, as a single save, so the
    /// shell rebuilds once instead of once per row. Returns how many rows moved.
    public int ResetPage(SettingsPage page)
    {
        var config = _store.Current.Clone();
        var scope = NewScope(config);
        var reset = 0;

        foreach (var field in page.Fields)
        {
            if (field.IsReadOnly || !field.IsOverridden(scope)) continue;
            if (field.Reset(scope) is not null) continue;
            reset++;
        }

        if (reset == 0) return 0;

        _store.Save(config);
        LastError = null;
        return reset;
    }

    public void RestoreDefaults() => _store.RestoreDefaults();

    private SettingsScope NewScope(AppConfig config) =>
        new(config, _autostart, _store.ConfigPath, _appVersion);
}
