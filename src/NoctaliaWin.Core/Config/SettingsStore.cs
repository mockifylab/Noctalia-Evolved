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
    void Save(AppConfig config);
    AppConfig RestoreDefaults();
}

public sealed class SettingsStore : ISettingsStore
{
    public const string AppFolderName = "NoctaliaWin";
    public const string ConfigFileName = "config.toml";

    private readonly string _dir;
    private AppConfig _current;

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
                LastDiagnostics = [];
                return;
            }
            var (config, diags) = ConfigToml.Load(File.ReadAllText(ConfigPath));
            _current = config;
            LastDiagnostics = diags;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _current = AppConfig.Default();
            LastDiagnostics = [new("", $"could not read config: {ex.Message}", IsError: true)];
        }
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
        Changed?.Invoke(_current);
    }

    public AppConfig RestoreDefaults()
    {
        var defaults = AppConfig.Default();
        Save(defaults);
        return defaults;
    }
}
