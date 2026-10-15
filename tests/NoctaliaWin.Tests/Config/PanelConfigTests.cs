using NoctaliaWin.Core.Config;
using Xunit;

namespace NoctaliaWin.Tests.Config;

/// The panel sections are the config contract the pop-out surfaces read, so their defaults,
/// round-trip and validation are pinned here rather than trusted from the codec.
public sealed class PanelConfigTests
{
    [Fact]
    public void DefaultsMatchNoctalia()
    {
        var panels = new PanelSection();

        Assert.Equal("solid", panels.TransparencyMode);
        Assert.True(panels.Borders);
        Assert.Equal(1.0, panels.BorderWidth);
        Assert.True(panels.Shadow);
        Assert.Equal("attached", panels.ControlCenterPlacement);
        Assert.Equal("auto", panels.ControlCenterPosition);
        Assert.Equal("floating", panels.LauncherPlacement);
        Assert.Equal("center", panels.LauncherPosition);
        Assert.False(panels.OpenNearClickControlCenter);
        Assert.Equal(8, panels.FloatingOffset);
        Assert.Equal(700, new ControlCenterSection().Width);
    }

    [Fact]
    public void AFullConfigRoundTripsThroughTheCodec()
    {
        var config = AppConfig.Default();
        config.Panels.TransparencyMode = "glass";
        config.Panels.Borders = false;
        config.Panels.BorderWidth = 2;
        config.Panels.ControlCenterPlacement = "floating";
        config.Panels.ControlCenterPosition = "top_right";
        config.Panels.OpenNearClickControlCenter = true;
        config.Panels.FloatingOffset = 24;
        config.ControlCenter.Width = 520;

        var (reloaded, diagnostics) = ConfigToml.Load(ConfigToml.Save(config));

        Assert.DoesNotContain(diagnostics, d => d.IsError);
        Assert.Equal("glass", reloaded.Panels.TransparencyMode);
        Assert.False(reloaded.Panels.Borders);
        Assert.Equal(2.0, reloaded.Panels.BorderWidth);
        Assert.Equal("floating", reloaded.Panels.ControlCenterPlacement);
        Assert.Equal("top_right", reloaded.Panels.ControlCenterPosition);
        Assert.True(reloaded.Panels.OpenNearClickControlCenter);
        Assert.Equal(24, reloaded.Panels.FloatingOffset);
        Assert.Equal(520, reloaded.ControlCenter.Width);
    }

    [Fact]
    public void InvalidValuesFallBackWithADiagnosticInsteadOfFailingToLoad()
    {
        var (config, diagnostics) = ConfigToml.Load("""
            [shell.panel]
            transparency_mode = "frosted"
            control_center_placement = "side"
            border_width = 40
            floating_offset = -9

            [control_center]
            width = 12
            """);

        Assert.Equal("solid", config.Panels.TransparencyMode);
        Assert.Equal("attached", config.Panels.ControlCenterPlacement);
        Assert.Equal(8.0, config.Panels.BorderWidth);
        Assert.Equal(0, config.Panels.FloatingOffset);
        Assert.Equal(240, config.ControlCenter.Width);

        Assert.Contains(diagnostics, d => d.Path == "shell.panel.transparency_mode");
        Assert.Contains(diagnostics, d => d.Path == "shell.panel.control_center_placement");
        Assert.Contains(diagnostics, d => d.Path == "control_center.width");
    }

    [Fact]
    public void AnAbsentPanelSectionLeavesTheDefaultsAlone()
    {
        var (config, diagnostics) = ConfigToml.Load("""
            [shell]
            time_format = "%H:%M"
            """);

        Assert.Equal("solid", config.Panels.TransparencyMode);
        Assert.Equal(700, config.ControlCenter.Width);
        Assert.Equal("%H:%M", config.Shell.TimeFormat);
        Assert.DoesNotContain(diagnostics, d => d.IsError);
    }

    [Fact]
    public void LauncherDefaultsMatchNoctalia()
    {
        var launcher = new LauncherSection();

        Assert.Equal(760, launcher.Width);
        Assert.Equal("top", launcher.SearchPosition);
        Assert.True(launcher.SearchOnTop);
        Assert.Equal("CTRL+ALT+SPACE", launcher.Hotkey);
        Assert.False(new PanelSection().LauncherIsAttached);
    }

    [Fact]
    public void TheLauncherSectionRoundTrips()
    {
        var config = AppConfig.Default();
        config.Launcher.Width = 900;
        config.Launcher.SearchPosition = "bottom";
        config.Launcher.Hotkey = "SUPER+SHIFT+D";
        config.Panels.LauncherPlacement = "attached";
        config.Panels.LauncherPosition = "auto";
        config.Panels.OpenNearClickLauncher = true;

        var (reloaded, diagnostics) = ConfigToml.Load(ConfigToml.Save(config));

        Assert.DoesNotContain(diagnostics, d => d.IsError);
        Assert.Equal(900, reloaded.Launcher.Width);
        Assert.False(reloaded.Launcher.SearchOnTop);
        Assert.Equal("SUPER+SHIFT+D", reloaded.Launcher.Hotkey);
        Assert.True(reloaded.Panels.LauncherIsAttached);
        Assert.Equal("auto", reloaded.Panels.LauncherPosition);
        Assert.True(reloaded.Panels.OpenNearClickLauncher);
    }

    [Fact]
    public void ALauncherWidthOutsideTheRangeIsClampedNotDropped()
    {
        // Upstream keeps a too-wide or too-narrow launcher at the nearest legal size rather
        // than reverting it, so a hand-edited config still renders.
        var (config, diagnostics) = ConfigToml.Load("""
            [shell.launcher]
            width = 4000
            search_position = "middle"
            """);

        Assert.Equal(LauncherSection.MaxWidth, config.Launcher.Width);
        Assert.Equal("top", config.Launcher.SearchPosition);
        Assert.Contains(diagnostics, d => d.Path == "shell.launcher.width");
        Assert.Contains(diagnostics, d => d.Path == "shell.launcher.search_position");
    }

    [Fact]
    public void ControlCenterIsOnlyAttachedWhenTheConfigSaysSo()
    {
        Assert.True(new PanelSection().ControlCenterIsAttached);
        Assert.False(new PanelSection { ControlCenterPlacement = "floating" }.ControlCenterIsAttached);
    }

    [Theory]
    [InlineData("solid", 1.0)]
    [InlineData("soft", 0.92)]
    [InlineData("glass", 0.75)]
    public void TransparencyModesMapToUpstreamCardOpacities(string mode, double expected)
    {
        // Upstream: solid is opaque, soft is base+0.08 clamped to [0.82, 0.92], glass is
        // base+0.10 clamped to [0.62, 0.75]. At the default opacity both hit their ceiling.
        var parsed = new PanelSection { TransparencyMode = mode }.ParsedTransparencyMode;

        Assert.Equal(expected, PanelSection.CardOpacity(parsed, 1.0), 3);
    }

    [Fact]
    public void GlassOpacityHasAFloorSoTextStaysReadable()
    {
        var glass = PanelSection.CardOpacity(PanelTransparencyMode.Glass, 0.1);
        var soft = PanelSection.CardOpacity(PanelTransparencyMode.Soft, 0.0);

        Assert.Equal(0.62, glass, 3);
        Assert.Equal(0.82, soft, 3);
    }

    [Fact]
    public void ModeNamesRoundTripBothWays()
    {
        foreach (var mode in Enum.GetValues<PanelTransparencyMode>())
        {
            var name = PanelSection.ModeName(mode);
            Assert.Equal(mode, new PanelSection { TransparencyMode = name }.ParsedTransparencyMode);
        }
    }

    [Fact]
    public void CloningACofigCarriesThePanelSections()
    {
        var config = AppConfig.Default();
        config.Panels.FloatingOffset = 30;
        config.ControlCenter.Width = 480;
        config.Launcher.Width = 640;
        config.Launcher.Hotkey = "CTRL+ALT+F2";

        var clone = config.Clone();
        clone.Panels.FloatingOffset = 1;
        clone.ControlCenter.Width = 1;
        clone.Launcher.Width = 1;
        clone.Launcher.Hotkey = "x";

        Assert.Equal(30, config.Panels.FloatingOffset);
        Assert.Equal(480, config.ControlCenter.Width);
        Assert.Equal(640, config.Launcher.Width);
        Assert.Equal("CTRL+ALT+F2", config.Launcher.Hotkey);
    }

    [Fact]
    public void AnEmptyHotkeyIsAValidWayToTurnTheBindingOff()
    {
        var (config, diagnostics) = ConfigToml.Load("""
            [shell.launcher]
            hotkey = ""
            """);

        Assert.Equal("", config.Launcher.Hotkey);
        Assert.DoesNotContain(diagnostics, d => d.Path == "shell.launcher.hotkey");

        // ...and it survives a save, so turning it off is not undone by the next write.
        var (reloaded, _) = ConfigToml.Load(ConfigToml.Save(config));
        Assert.Equal("", reloaded.Launcher.Hotkey);
    }

    [Fact]
    public void AnUnusableHotkeyKeepsTheDefaultAndSaysWhy()
    {
        // A bare key would be grabbed out of every application's hands, so the config layer
        // refuses it loudly instead of registering it and swallowing the keystroke.
        var (config, diagnostics) = ConfigToml.Load("""
            [shell.launcher]
            hotkey = "SPACE"
            """);

        Assert.Equal(LauncherSection.DefaultHotkey, config.Launcher.Hotkey);
        var diagnostic = Assert.Single(diagnostics, d => d.Path == "shell.launcher.hotkey");
        Assert.False(diagnostic.IsError);
        Assert.Contains("no modifier", diagnostic.Message);
    }

    [Fact]
    public void AHotkeyIsStoredTheWayTheUserWroteIt()
    {
        var (config, diagnostics) = ConfigToml.Load("""
            [shell.launcher]
            hotkey = "mod4+shift+space"
            """);

        Assert.Equal("mod4+shift+space", config.Launcher.Hotkey);
        Assert.DoesNotContain(diagnostics, d => d.Path == "shell.launcher.hotkey");
    }
}
