// "Start with Windows" via the documented per-user Run key. Reversible: disabling
// removes only our own value name and never touches anything else in the key.
using Microsoft.Win32;

namespace NoctaliaWin.Windows;

public interface IRunKeyStore
{
    string? Get(string valueName);
    void Set(string valueName, string command);
    void Remove(string valueName);
}

public sealed class RegistryRunKeyStore : IRunKeyStore
{
    // HKCU (not HKLM): no admin rights, and it only affects the current user.
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Get(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void Set(string valueName, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException($"could not open {RunKeyPath} for writing");
        key.SetValue(valueName, command, Microsoft.Win32.RegistryValueKind.ExpandString);
    }

    public void Remove(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(valueName) is null) return;
        key.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

public sealed class StartupService : Core.Abstractions.IStartupService
{
    public const string ValueName = "NoctaliaWin";

    /// Launch argument that starts the app without stealing focus on login.
    public const string SilentArgument = "--startup";

    private readonly IRunKeyStore _store;
    private readonly Func<string?> _executablePath;

    public StartupService() : this(new RegistryRunKeyStore(), () => Environment.ProcessPath)
    {
    }

    internal StartupService(IRunKeyStore store, Func<string?> executablePath)
    {
        _store = store;
        _executablePath = executablePath;
    }

    public bool Enabled => _store.Get(ValueName) is not null;

    public string? Command => _store.Get(ValueName);

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            _store.Remove(ValueName);
            return;
        }

        var path = _executablePath();
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("cannot determine this app's executable path");

        _store.Set(ValueName, $"\"{path}\" {SilentArgument}");
    }
}
