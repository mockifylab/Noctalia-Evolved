using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

/// <summary>
/// Only the failure paths are exercised here: a successful launch would open a real
/// window on this machine, which is verified manually in the launcher stage.
/// </summary>
public sealed class ProcessLauncherTests
{
    [Fact]
    public void MissingExecutableTarget_ReportsWithoutThrowing()
    {
        var launcher = new ProcessLauncher();
        var error = launcher.Launch(new AppEntry(
            "Ghost", "", null, null, "desktop"));

        Assert.NotNull(error);
        Assert.StartsWith("Ghost", error);
    }

    [Fact]
    public void UnresolvableTarget_ReportsAnErrorAndNeverThrows()
    {
        var launcher = new ProcessLauncher();
        var error = launcher.Launch(new AppEntry(
            "Nowhere",
            @"C:\Program Files\NoctaliaWin Tests\definitely-not-installed.exe",
            null, null, "desktop"));

        Assert.NotNull(error);
        Assert.Contains("Nowhere", error);
    }

}
