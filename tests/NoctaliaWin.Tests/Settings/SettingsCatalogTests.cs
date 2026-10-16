// The settings catalog is a contract: every row must name a real config key, have a default
// that matches the config's own default, and survive a write all the way back through the
// TOML codec without a diagnostic.
using System.Globalization;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Settings;
using NoctaliaWin.Tests.Config;

namespace NoctaliaWin.Tests.Settings;

public class SettingsCatalogTests
{
    [Fact]
    public void PageIdsAndRowIdsAreUnique()
    {
        Assert.Equal(SettingsCatalog.Pages.Count, SettingsCatalog.Pages.Select(p => p.Id).Distinct().Count());

        var ids = SettingsCatalog.All().Select(a => a.Field.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryPageHasGroupsAndGlyphs()
    {
        foreach (var page in SettingsCatalog.Pages)
        {
            Assert.NotEmpty(page.Id);
            Assert.NotEmpty(page.Title);
            Assert.NotEmpty(page.Glyph);
            Assert.NotEmpty(page.Groups);
            foreach (var group in page.Groups)
            {
                Assert.NotEmpty(group.Title);
                Assert.NotEmpty(group.Fields);
            }
        }
    }

    /// A row that does not say what it does is the kind of settings row nobody can use.
    [Fact]
    public void EveryRowIsExplained()
    {
        foreach (var (_, _, field) in SettingsCatalog.All())
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Title));

            // Buttons carry their explanation in the description too; only the read-only
            // display rows are allowed a short one.
            Assert.True(field.Description.Length > 10, $"{field.Id} has no description");
        }
    }

    [Fact]
    public void EveryRowIdIsAConfigPathOrADocumentedWindowsKey()
    {
        // The `system.` rows are the ones that reach outside config.toml: the Run key, the
        // config file's own path, the build version, and the four buttons.
        var outsideConfig = new HashSet<string>(StringComparer.Ordinal)
        {
            "system.autostart", "system.config_path", "system.version",
            "system.open_config_folder", "system.restore_defaults",
            "system.check_updates", "system.open_release_page",
        };

        foreach (var (_, _, field) in SettingsCatalog.All())
        {
            if (field.Id.StartsWith("system.", StringComparison.Ordinal))
                Assert.Contains(field.Id, outsideConfig);
            else
                Assert.DoesNotContain(field.Id, outsideConfig);
        }
    }

    [Fact]
    public void EveryWritableRowStartsAtItsDefault()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        foreach (var (_, _, field) in SettingsCatalog.All())
        {
            if (field.IsReadOnly) continue;
            Assert.Equal(field.DefaultValue, editor.ValueOf(field));
        }
    }

    [Fact]
    public void TheBarPagesCarryTheKeysTheRendererReads()
    {
        var ids = SettingsCatalog.All().Select(a => a.Field.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
        {
            "theme.mode", "theme.builtin", "accessibility.ui_scale",
            "bar.main.position", "bar.main.thickness", "bar.main.radius",
            "bar.main.margin_ends", "bar.main.margin_edge", "bar.main.auto_hide",
            "bar.main.start", "bar.main.center", "bar.main.end",
            "shell.panel.transparency_mode", "shell.panel.control_center_placement",
            "shell.launcher.hotkey", "shell.launcher.width", "control_center.width",
            "shell.time_format", "shell.date_format",
        })
            Assert.Contains(required, ids);
    }

    /// Upstream keeps its inert keys out of the UI; so does this port. A row whose value the
    /// renderer ignores would be a control that appears to work.
    [Fact]
    public void KeysWithNoWindowsSurfaceAreNotOffered()
    {
        var ids = SettingsCatalog.All().Select(a => a.Field.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var inert in new[]
        {
            "bar.main.shadow", "bar.main.reserve_space", "bar.main.layer",
            "shell.panel.shadow", "theme.source", "shell.offline_mode",
        })
            Assert.DoesNotContain(inert, ids);
    }

    [Fact]
    public void AWriteOfEveryRowKindSurvivesTheConfigCodec()
    {
        // Each row is written with a value the catalog itself says it can take, then the file
        // is read back by a second store. That is what proves the catalog and the codec agree:
        // a key the writer forgets, or one the loader clamps away, fails here rather than in
        // the user's hands.
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);

        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (_, _, field) in SettingsCatalog.All())
        {
            // The `system.` rows reach the registry and the filesystem, not config.toml, so a
            // round trip through the codec says nothing about them.
            if (field.IsReadOnly || field.Id.StartsWith("system.", StringComparison.Ordinal))
                continue;

            var current = editor.ValueOf(field);
            var next = NextValue(field, current);
            if (next == current) continue;

            var error = editor.Apply(field, next);
            Assert.True(error is null, $"{field.Id}: {error}");
            written[field.Id] = next;
        }

        Assert.True(written.Count > 20, $"only {written.Count} rows were exercised");

        var reopened = new SettingsStore(dir.Path);
        Assert.DoesNotContain(reopened.LastDiagnostics, d => d.IsError);

        var reloaded = new SettingsEditor(reopened);
        foreach (var (_, _, field) in SettingsCatalog.All())
        {
            if (!written.TryGetValue(field.Id, out var expected)) continue;
            Assert.Equal(expected, reloaded.ValueOf(field));
        }
    }

    /// A different-but-valid value for a row, chosen from what the row itself declares.
    private static string NextValue(SettingsField field, string current) => field.Control switch
    {
        SettingsControlKind.Toggle => current == "true" ? "false" : "true",
        SettingsControlKind.Slider => NextNumber(field, current),
        SettingsControlKind.Pills
            or SettingsControlKind.Dropdown
            or SettingsControlKind.PaletteGrid => field.Choices.Select(c => c.Value).First(v => v != current),
        SettingsControlKind.Text => current == "TEST VALUE" ? "OTHER TEST VALUE" : "TEST VALUE",
        SettingsControlKind.Keybind => "CTRL+ALT+F12",
        SettingsControlKind.WidgetLane =>
            current.Contains("clock", StringComparison.Ordinal) ? "volume" : "clock",
        _ => current,
    };

    private static string NextNumber(SettingsField field, string current)
    {
        var parsed = double.Parse(current, CultureInfo.InvariantCulture);
        var candidate = parsed + field.Step > field.Max ? field.Min + field.Step : parsed + field.Step;

        // A sentinel row (-1 = "no override") needs a real value to be a meaningful test.
        if (field.OffLabel.Length > 0 && candidate < 0)
            candidate = field.Min + field.Step;

        if (field.Decimals == 0)
            candidate = Math.Round(candidate);

        return candidate.ToString("F" + field.Decimals, CultureInfo.InvariantCulture);
    }
}
