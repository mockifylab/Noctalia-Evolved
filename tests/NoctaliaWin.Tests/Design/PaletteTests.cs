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
    public void ByName_ResolvesEverySpellingAUserMayType()
    {
        Assert.NotNull(BuiltinPalettes.ByName("noctalia"));
        Assert.NotNull(BuiltinPalettes.ByName("Ayu"));
        Assert.NotNull(BuiltinPalettes.ByName("rose-pine"));
        Assert.NotNull(BuiltinPalettes.ByName("ROSE PINE"));
        Assert.NotNull(BuiltinPalettes.ByName("Tokyo Night"));
        Assert.Null(BuiltinPalettes.ByName("crust"));
        Assert.Null(BuiltinPalettes.ByName(null));
    }

    [Fact]
    public void TheWholeUpstreamSetIsOfferedInUpstreamOrder()
    {
        string[] expected =
        [
            "Ayu", "Catppuccin", "Dracula", "Eldritch", "Gruvbox", "Kanagawa",
            "Noctalia", "Nord", "Rosé Pine", "Tokyo-Night",
        ];
        Assert.Equal(expected, BuiltinPalettes.Names);
    }

    [Fact]
    public void BothModesExistAndDifferForEveryPalette()
    {
        foreach (var name in BuiltinPalettes.Names)
        {
            // Upstream ships a light variant for every scheme. A column copied out of the
            // other mode would render the same card twice and make the mode toggle look dead.
            var set = BuiltinPalettes.ByName(name);
            Assert.NotNull(set);
            Assert.NotEqual(set!.Dark.Surface, set.Light.Surface);
            Assert.NotEqual(set.Dark.Primary, set.Light.Primary);
        }
    }

    // The table stores hexes positionally, so a row that is one value short would silently
    // shift every later colour into the wrong role. These spot checks pin role order per
    // palette against the literals this port transcribed from upstream.
    [Theory]
    [InlineData("Ayu", "dark", "#E6B450", "#0B0E14", "#0B0E14", "#565B66")]
    [InlineData("Ayu", "light", "#FF8F40", "#F8F9FA", "#F8F9FA", "#8A9199")]
    [InlineData("Catppuccin", "dark", "#cba6f7", "#11111b", "#1e1e2e", "#4c4f69")]
    [InlineData("Noctalia", "dark", "#fff59b", "#0e0e43", "#070722", "#21215F")]
    [InlineData("Noctalia", "light", "#5d65f5", "#dadcff", "#e6e8fa", "#8288fc")]
    [InlineData("Dracula", "dark", "#bd93f9", "#282A36", "#282A36", "#5a5e77")]
    [InlineData("Gruvbox", "dark", "#b8bb26", "#282828", "#282828", "#57514e")]
    [InlineData("Kanagawa", "dark", "#76946a", "#1f1f28", "#1f1f28", "#363646")]
    [InlineData("Nord", "dark", "#8fbcbb", "#2e3440", "#2e3440", "#505a70")]
    [InlineData("Rosé Pine", "dark", "#ebbcba", "#191724", "#191724", "#403d52")]
    [InlineData("Tokyo-Night", "dark", "#7aa2f7", "#16161e", "#1a1b26", "#353D57")]
    public void RolesLandWhereUpstreamPutThem(
        string name, string mode, string primary, string onPrimary, string surface, string outline)
    {
        var set = BuiltinPalettes.ByName(name);
        Assert.NotNull(set);
        var palette = mode == "dark" ? set!.Dark : set.Light;

        Assert.Equal(Rgb.FromHex(primary), palette[ColorRole.Primary]);
        Assert.Equal(Rgb.FromHex(onPrimary), palette[ColorRole.OnPrimary]);
        Assert.Equal(Rgb.FromHex(surface), palette[ColorRole.Surface]);
        Assert.Equal(Rgb.FromHex(outline), palette[ColorRole.Outline]);
    }

    [Fact]
    public void SlugCollapsesAccentsCaseAndSeparators()
    {
        Assert.Equal("rosepine", BuiltinPalettes.Slug("Rosé Pine"));
        Assert.Equal("rosepine", BuiltinPalettes.Slug("ROSE-PINE"));
        Assert.Equal("tokyonight", BuiltinPalettes.Slug("Tokyo Night"));
        Assert.Equal(string.Empty, BuiltinPalettes.Slug("   "));
    }

    [Fact]
    public void SchemeCardsShowFourDotsOverTheSurface()
    {
        ColorRole[] expected =
            [ColorRole.Primary, ColorRole.Secondary, ColorRole.Tertiary, ColorRole.Error];
        Assert.Equal(expected, BuiltinPalettes.PreviewRoles);
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
    [InlineData("tray")]         // other processes' status icons have no documented reader
    [InlineData("notifications")] // toast content needs the panel, not the bar alone
    [InlineData("text")]         // needs a per-widget value the schema does not model
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
