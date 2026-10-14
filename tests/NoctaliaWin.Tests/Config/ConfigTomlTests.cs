// Config codec tests: schema validation, clamping, migration, round-tripping
// through the self-contained TOML reader/writer.
using NoctaliaWin.Core.Config;

namespace NoctaliaWin.Tests.Config;

public class ConfigTomlTests
{
    private static ConfigDiagnostic? Find(List<ConfigDiagnostic> diags, string path) =>
        diags.FirstOrDefault(d => d.Path == path);

    [Fact]
    public void EmptyText_YieldsDefaultsWithoutErrors()
    {
        var (config, diags) = ConfigToml.Load("");

        Assert.DoesNotContain(diags, d => d.IsError);
        Assert.Equal("dark", config.Theme.Mode);
        Assert.Equal(34, config.Main.Thickness);
        Assert.Equal("top", config.Main.Position);
        Assert.Equal(["launcher", "wallpaper", "workspaces"], config.Main.Start);
        Assert.Equal(["clock"], config.Main.Center);
        Assert.Single(config.Bars);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryModelledValue()
    {
        var config = AppConfig.Default();
        config.Theme.Mode = "light";
        config.Theme.Builtin = "Ayu";
        config.Accessibility.UiScale = 1.25;
        config.Shell.TimeFormat = "{:%H:%M}";
        config.Shell.OfflineMode = true;
        var bar = config.Bars["main"];
        bar.Position = "bottom";
        bar.Thickness = 40;
        bar.BackgroundOpacity = 0.6;
        bar.Radius = 8;
        bar.RadiusTopLeft = 0;
        bar.ConcaveEdgeCorners = false;
        bar.MarginEnds = 200;
        bar.MarginEdge = 0;
        bar.AutoHide = true;
        bar.Layer = "overlay";
        bar.Start = ["launcher", "clock"];
        bar.End = ["volume", "battery"];

        var (loaded, diags) = ConfigToml.Load(ConfigToml.Save(config));

        Assert.DoesNotContain(diags, d => d.IsError);
        Assert.Equal(config.Theme.Mode, loaded.Theme.Mode);
        Assert.Equal(config.Theme.Builtin, loaded.Theme.Builtin);
        Assert.Equal(config.Accessibility.UiScale, loaded.Accessibility.UiScale, 3);
        Assert.Equal(config.Shell.OfflineMode, loaded.Shell.OfflineMode);
        var b = loaded.Bars["main"];
        Assert.Equal("bottom", b.Position);
        Assert.Equal(40, b.Thickness);
        Assert.Equal(0.6, b.BackgroundOpacity, 3);
        Assert.Equal(8, b.Radius);
        Assert.Equal(0, b.RadiusTopLeft);
        Assert.Null(b.RadiusTopRight);
        Assert.False(b.ConcaveEdgeCorners);
        Assert.Equal(200, b.MarginEnds);
        Assert.True(b.AutoHide);
        Assert.Equal("overlay", b.Layer);
        Assert.Equal(["launcher", "clock"], b.Start);
        Assert.Equal(["volume", "battery"], b.End);
    }

    [Fact]
    public void Load_ReadsUpstreamExampleStyleKeys()
    {
        const string toml = """
            schema_version = 1

            [theme]
            mode = "dark"
            source = "builtin"
            builtin = "Noctalia"

            [bar.main]
            position = "top"
            thickness = 34
            radius = 12
            margin_ends = 180
            margin_edge = 10
            concave_edge_corners = true
            start = ["launcher", "clock"]
            center = [
              "clock",
              "battery",
            ]
            end = ["volume"]
            """;

        var (config, diags) = ConfigToml.Load(toml);

        Assert.DoesNotContain(diags, d => d.IsError);
        Assert.Equal(["launcher", "clock"], config.Main.Start);
        // Multi-line arrays with a trailing comma must parse.
        Assert.Equal(["clock", "battery"], config.Main.Center);
        Assert.Equal(["volume"], config.Main.End);
        Assert.Equal(180, config.Main.MarginEnds);
    }

    [Fact]
    public void Load_InvalidEnumValue_KeepsDefaultAndWarns()
    {
        var (config, diags) = ConfigToml.Load("""
            [bar.main]
            position = "middle"
            layer = "bottom-most"

            [theme]
            mode = "purple"
            """);

        Assert.Equal("top", config.Main.Position);
        Assert.Equal("top", config.Main.Layer);
        Assert.Equal("dark", config.Theme.Mode);
        Assert.NotNull(Find(diags, "bar.main.position"));
        Assert.NotNull(Find(diags, "bar.main.layer"));
        Assert.NotNull(Find(diags, "theme.mode"));
        Assert.All(diags, d => Assert.False(d.IsError));
    }

    [Theory]
    [InlineData(999, 128)]
    [InlineData(-5, 20)]
    public void OutOfRangeIntegers_AreClampedWithADiagnostic(int input, int expected)
    {
        var (config, diags) = ConfigToml.Load($"[bar.main]{Environment.NewLine}thickness = {input}");

        Assert.Equal(expected, config.Main.Thickness);
        var diag = Find(diags, "bar.main.thickness");
        Assert.NotNull(diag);
        Assert.Contains("clamped", diag!.Message);
    }

    [Fact]
    public void InRangeIntegers_AreTakenVerbatimWithoutADiagnostic()
    {
        var (config, diags) = ConfigToml.Load("[bar.main]\nthickness = 40");

        Assert.Equal(40, config.Main.Thickness);
        Assert.Empty(diags);
    }

    [Fact]
    public void WrongTypes_FallBackToDefaultsWithADiagnostic()
    {
        var (config, diags) = ConfigToml.Load("""
            [bar.main]
            thickness = "wide"
            background_opacity = true
            shadow = 3
            start = "launcher"
            """);

        Assert.Equal(34, config.Main.Thickness);
        Assert.Equal(1.0, config.Main.BackgroundOpacity, 3);
        Assert.True(config.Main.Shadow);
        Assert.Equal(["launcher", "wallpaper", "workspaces"], config.Main.Start);
        Assert.Equal(4, diags.Count);
    }

    [Fact]
    public void OlderSchema_IsMigratedInPlace()
    {
        var (config, diags) = ConfigToml.Load("schema_version = 0");

        Assert.Equal(AppConfig.CurrentSchemaVersion, config.SchemaVersion);
        var diag = diags.Single();
        Assert.False(diag.IsError);
        Assert.Contains("migrated", diag.Message);
    }

    [Fact]
    public void NewerSchema_WarnsButStillLoads()
    {
        var (config, diags) = ConfigToml.Load("""
            schema_version = 7
            [bar.main]
            thickness = 44
            """);

        Assert.Equal(44, config.Main.Thickness);
        Assert.Contains(diags, d => !d.IsError && d.Message.Contains("newer"));
    }

    [Fact]
    public void UnknownSections_AreReportedAndIgnored()
    {
        var (config, diags) = ConfigToml.Load("""
            [mixer]
            volume = 50

            [dock]
            position = "center"
            """);

        Assert.Equal(AppConfig.Default().Main.Thickness, config.Main.Thickness);
        Assert.Equal(2, diags.Count(d => d.Message.Contains("unknown section")));
        Assert.All(diags, d => Assert.False(d.IsError));
    }

    [Fact]
    public void MalformedInput_ProducesErrorDiagnosticsAndDefaults()
    {
        var (config, diags) = ConfigToml.Load("this is not toml at all");

        Assert.Contains(diags, d => d.IsError);
        Assert.Equal(AppConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal("top", config.Main.Position);
    }

    [Fact]
    public void DuplicateKey_IsReportedOnceAndFirstValueWins()
    {
        var (config, diags) = ConfigToml.Load("""
            [bar.main]
            thickness = 30
            thickness = 99
            """);

        Assert.Contains(diags, d => d.IsError && d.Message.Contains("duplicate"));
        Assert.Equal(30, config.Main.Thickness);
    }

    [Fact]
    public void ExtraBars_KeepMainPresent()
    {
        var (config, diags) = ConfigToml.Load("""
            [bar.main]
            thickness = 30

            [bar.secondary]
            position = "bottom"
            thickness = 28
            """);

        Assert.DoesNotContain(diags, d => d.IsError);
        Assert.Equal(2, config.Bars.Count);
        Assert.Equal(30, config.Bars["main"].Thickness);
        Assert.Equal("bottom", config.Bars["secondary"].Position);
        Assert.Equal(28, config.Bars["secondary"].Thickness);
        // Non-main bars start from fresh defaults, not from main.
        Assert.Equal(180, config.Bars["secondary"].MarginEnds);
    }

    [Fact]
    public void Save_WritesHeaderAndSchemaVersion()
    {
        var text = ConfigToml.Save(AppConfig.Default());

        Assert.StartsWith("#", text);
        Assert.Contains($"schema_version = {AppConfig.CurrentSchemaVersion}", text);
        Assert.Contains("[bar.main]", text);
        Assert.Contains("[theme]", text);
    }

    [Fact]
    public void ResolvedCornerRadii_FallBackToTheUniformRadius()
    {
        var bar = new BarSection { Radius = 12, RadiusTopLeft = 0 };

        Assert.Equal((0, 12, 12, 12), bar.ResolvedCornerRadii());
    }

    [Fact]
    public void Clone_IsDeepEnoughToMutateIndependently()
    {
        var original = AppConfig.Default();
        var clone = original.Clone();
        clone.Bars["main"].Start.Add("extra");
        clone.Bars["main"].Position = "left";

        Assert.Equal(3, original.Bars["main"].Start.Count);
        Assert.Equal("top", original.Bars["main"].Position);
    }
}
