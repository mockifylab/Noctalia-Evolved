using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

public sealed class TrayServiceTests
{
    [Theory]
    [InlineData("Noctalia")]
    [InlineData("")]
    [InlineData("Noctalia - 100% - Wi-Fi")]
    public void Tooltips_WithinTheDocumentedLimit_ArePassedThrough(string text) =>
        Assert.Equal(text, TrayService.ClampTooltip(text));

    [Fact]
    public void Tooltips_OverTheLimit_AreTruncatedInsteadOfThrowing()
    {
        var longText = new string('x', TrayService.MaxTooltipLength + 40);

        var clamped = TrayService.ClampTooltip(longText);

        Assert.Equal(TrayService.MaxTooltipLength, clamped.Length);
        // The limit is a documented shell constraint: passing more would raise
        // ArgumentException from inside a widget update, on the app's hot path.
        Assert.Equal(63, TrayService.MaxTooltipLength);
    }

    [Fact]
    public void CreatingAndRemovingTheIcon_OnThisMachine_DoesNotThrow()
    {
        // The real shell notification icon: created, updated and removed again.
        using var tray = new TrayService();
        tray.SetTooltip("Noctalia — 100% · Wi-Fi");
    }
}
