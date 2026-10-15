// Persistent settings store: %APPDATA%-style user data dir, atomic writes,
// corrupt-file tolerance, restore-defaults.
namespace NoctaliaWin.Core.Config;

public interface ISettingsStore
{
    AppConfig Current { get; }

    /// Where the config lives, so the settings window can show the path a row writes to.
    string ConfigPath { get; }
    IReadOnlyList<ConfigDiagnostic> LastDiagnostics { get; }
    event Action<AppConfig>? Changed;
    void Reload();

    /// Re-reads the file only when its text differs from what this store last loaded or wrote,
    /// and raises `Changed` only then. That is what lets a file-system event — which fires
    /// several times for one save, and fires for the app's own writes too — be safe to act on.
    bool ReloadIfChanged();

    void Save(AppConfig config);
    AppConfig RestoreDefaults();
}

public sealed class SettingsStore : ISettingsStore
{
    public const string AppFolderName = "NoctaliaWin";
    public const string ConfigFileName = "config.toml";

    private readonly string _dir;
    private AppConfig _current;

    /// The exact text behind `Current`. A hand edit is only an edit if the bytes differ, which
    /// is also what makes the duplicate events of an atomic replace harmless.
    private string _lastText = "";

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public SettingsStore(string? directory = null)
    {
        _dir = directory ?? DefaultDirectory;
        _current = AppConfig.Default();
        Reload();
    }

    public string ConfigPath => Path.Combine(_dir, ConfigFileName);
    public AppConfig Current => _current;
    public IReadOnlyList<ConfigDiagnostic> LastDiagnostics { get; private set; } = [];
    public event Action<AppConfig>? Changed;

    public void Reload()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _current = AppConfig.Default();
                _lastText = "";
                LastDiagnostics = [];
                return;
            }
            var text = File.ReadAllText(ConfigPath);
            var (config, diags) = ConfigToml.Load(text);
            _current = config;
            _lastText = text;
            LastDiagnostics = diags;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _current = AppConfig.Default();
            LastDiagnostics = [new("", $"could not read config: {ex.Message}", IsError: true)];
        }
    }

    public bool ReloadIfChanged()
    {
        string text;
        try
        {
            text = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A save in flight is not a lost edit: the watcher fires again when the file is
            // replaced, and a store that cannot read it keeps showing what it already has.
            return false;
        }

        if (text == _lastText) return false;

        Reload();
        Changed?.Invoke(_current);
        return true;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_dir);
        var text = ConfigToml.Save(config);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(ConfigPath))
            File.Replace(tmp, ConfigPath, null);
        else
            File.Move(tmp, ConfigPath);
        _current = config.Clone();
        _lastText = text;
        Changed?.Invoke(_current);
    }

    public AppConfig RestoreDefaults()
    {
        var defaults = AppConfig.Default();
        Save(defaults);
        return defaults;
    }
}
