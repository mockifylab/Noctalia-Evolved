using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

public sealed class SystemThemeServiceTests
{
    [Fact]
    public void ReaderValue_IsUsedVerbatim()
    {
        using var dark = new SystemThemeService(() => false, listenForChanges: false);
        using var light = new SystemThemeService(() => true, listenForChanges: false);

        Assert.False(dark.UsesLightTheme);
        Assert.True(light.UsesLightTheme);
    }

    [Fact]
    public void Poll_RaisesOnlyWhenTheAnswerFlips()
    {
        bool light = true;
        using var service = new SystemThemeService(() => light, listenForChanges: false);
        int raises = 0;
        service.Changed += () => raises++;

        service.Poll();
        Assert.Equal(0, raises);

        light = false;
        service.Poll();
        Assert.Equal(1, raises);
        Assert.False(service.UsesLightTheme);

        // A second poll with the same answer must not repaint the whole shell.
        service.Poll();
        Assert.Equal(1, raises);
    }

    [Fact]
    public void LiveRegistryRead_MatchesTheDocumentedValue()
    {
        // The key can legitimately be missing; the service must fall back rather than crash
        // the shell. Comparing against the reader keeps this assertion meaningful on both
        // a light and a dark machine.
        using var service = new SystemThemeService();

        Assert.Equal(SystemThemeService.ReadRegistry(), service.UsesLightTheme);
    }
}
