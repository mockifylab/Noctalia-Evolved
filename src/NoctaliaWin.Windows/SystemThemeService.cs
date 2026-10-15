// The system's light/dark choice for app chrome, read from the documented per-user
// value the Settings app writes, and refreshed through SystemEvents.
//
// Deliberately not used: the UWP UI.ViewManagement UTCoreFrameworksSettings WinRT class,
// which is not a documented contract for a desktop process.
using Microsoft.Win32;
using NoctaliaWin.Core.Abstractions;

namespace NoctaliaWin.Windows;

public sealed class SystemThemeService : ISystemThemeService, IDisposable
{
    public const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string LightThemeValueName = "AppsUseLightTheme";

    private readonly Func<bool?>? _reader; // test seam
    private bool _listensForChanges;

    public event Action? Changed;

    public SystemThemeService() : this(reader: null, listenForChanges: true)
    {
    }

    internal SystemThemeService(Func<bool?>? reader, bool listenForChanges)
    {
        _reader = reader;
        UsesLightTheme = Read();

        if (!listenForChanges) return;

        _listensForChanges = true;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool UsesLightTheme { get; private set; }

    /// Re-reads the value; raises Changed only when the answer actually flipped.
    internal void Poll()
    {
        var next = Read();
        if (next == UsesLightTheme) return;

        UsesLightTheme = next;
        Changed?.Invoke();
    }

    // SystemEvents can raise on a non-UI thread; the subscriber is responsible for
    // marshalling, which is what the WPF app does for every other service event too.
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        Poll();
    }

    private bool Read() => _reader?.Invoke() ?? ReadRegistry();

    internal static bool ReadRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
            // Missing key or value means the OS default, which is light.
            return key?.GetValue(LightThemeValueName) is not int value || value != 0;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (!_listensForChanges) return;

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _listensForChanges = false;
    }
}
