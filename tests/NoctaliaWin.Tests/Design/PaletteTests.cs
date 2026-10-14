// Design-token and palette parity with Noctalia's built-in defaults
// (src/ui/style.h, src/ui/palette.h, src/theme/builtin_palettes.cpp —
// MIT License, Copyright (c) 2026 noctalia-dev).
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Design;

public class PaletteTests
{
    [Theory]
    [InlineData("#070722", 7, 7, 34)]
    [InlineData("#fff59b", 255, 245, 155)]
    [InlineData("#FD4663", 253, 70, 99)]
    public void FromHex_ParsesRgb(string hex, byte r, byte g, byte b)
    {
        Assert.Equal(new Rgb(r, g, b), Rgb.FromHex(hex));
    }

    [Fact]
    public void HexRoundTripsInLowerCase()
    {
        Assert.Equal("#e6e8fa", Rgb.FromHex("#E6E8FA").ToHex());
    }

    [Fact]
    public void FromHex_RejectsMalformedInput()
    {
        Assert.Throws<FormatException>(() => Rgb.FromHex("not-a-color"));
    }

    [Fact]
    public void NoctaliaDarkSurfaceMatchesUpstream()
    {
        var dark = BuiltinPalettes.Noctalia.Dark;

        Assert.Equal(Rgb.FromHex("#070722"), dark[ColorRole.Surface]);
        Assert.Equal(Rgb.FromHex("#fff59b"), dark[ColorRole.Primary]);
        Assert.Equal(Rgb.FromHex("#0e0e43"), dark[ColorRole.OnPrimary]);
        Assert.Equal(Rgb.FromHex("#9BFECE"), dark[ColorRole.Hover]);
    }

    [Fact]
    public void NoctaliaLightSurfaceMatchesUpstream()
    {
        var light = BuiltinPalettes.Noctalia.Light;

        Assert.Equal(Rgb.FromHex("#e6e8fa"), light[ColorRole.Surface]);
        Assert.Equal(Rgb.FromHex("#5d65f5"), light[ColorRole.Primary]);
    }

    [Fact]
    public void EveryColorRoleIsAddressable()
    {
        var palette = BuiltinPalettes.Noctalia.Dark;

        foreach (var role in Enum.GetValues<ColorRole>())
            Assert.NotEqual(default, palette[role]);
    }

    [Fact]
    public void TokenNamesRoundTrip()
    {
        foreach (var (role, token) in ColorRoleTokens.All)
        {
            Assert.Equal(role, ColorRoleTokens.FromToken(token));
            Assert.Equal(token, ColorRoleTokens.ToToken(role));
        }
    }

    [Fact]
    public void ByName_IsCaseInsensitiveAndCoversBuiltins()
    {
        Assert.NotNull(BuiltinPalettes.ByName("noctalia"));
        Assert.NotNull(BuiltinPalettes.ByName("Ayu"));
        Assert.Null(BuiltinPalettes.ByName("rose-pine"));
    }

    [Fact]
    public void PaletteSet_SelectsModeWithoutAmbiguity()
    {
        var set = BuiltinPalettes.Noctalia;

        Assert.Equal(set.Dark, set.For(PaletteMode.Dark));
        Assert.Equal(set.Light, set.For(PaletteMode.Light));
    }

    [Fact]
    public void TokensMatchNoctaliaDefaults()
    {
        Assert.Equal(34, StyleTokens.BarThicknessDefault);
        Assert.Equal(12f, StyleTokens.RadiusXl);
        Assert.Equal(14f, StyleTokens.CardPadding);
        Assert.Equal(6f, StyleTokens.BarCapsulePadding);
        Assert.Equal((100, 200, 400), (StyleTokens.AnimFastMs, StyleTokens.AnimNormalMs, StyleTokens.AnimSlowMs));
        Assert.Equal(15f, StyleTokens.ScaledRadiusXl(1.25f), 3);
    }
}

public class WidgetCatalogTests
{
    [Theory]
    [InlineData("clock")]
    [InlineData("launcher")]
    [InlineData("volume")]
    [InlineData("network")]
    [InlineData("battery")]
    [InlineData("notifications")]
    [InlineData("session")]
    [InlineData("control-center")]
    public void MilestoneOneWidgetsAreSupported(string name)
    {
        Assert.True(WidgetCatalog.IsKnown(name));
        Assert.True(WidgetCatalog.IsSupported(name));
    }

    [Theory]
    [InlineData("workspaces")]   // no Windows virtual-desktop equivalent wired yet
    [InlineData("media")]        // SMTC-backed, planned for M2
    [InlineData("wallpaper")]
    [InlineData("brightness")]
    public void DeferredWidgetsAreKnownButUnsupported(string name)
    {
        Assert.True(WidgetCatalog.IsKnown(name));
        Assert.False(WidgetCatalog.IsSupported(name));
    }

    [Fact]
    public void UnknownWidgetsAreNeitherSupportedNorPlanned()
    {
        Assert.False(WidgetCatalog.IsKnown("totally-made-up"));
        Assert.False(WidgetCatalog.IsSupported("totally-made-up"));
    }

    [Fact]
    public void SupportedAndPlannedDoNotOverlap()
    {
        Assert.Empty(WidgetCatalog.Supported.Intersect(WidgetCatalog.Planned));
    }

    [Fact]
    public void DefaultBarConfigNamesResolveAgainstTheCatalog()
    {
        // Upstream example.toml widget lists must all be recognised names, so the
        // bar can report "not available on Windows" instead of silently dropping them.
        var defaults = new NoctaliaWin.Core.Config.BarSection();

        foreach (var name in defaults.Start.Concat(defaults.Center).Concat(defaults.End))
            Assert.True(WidgetCatalog.IsKnown(name), $"unknown widget '{name}' in default config");
    }
}
