// The editor is the settings window's only door to the config: a rejected value must not
// reach the file, an accepted one must reach it once, and reset must mean the config's own
// default rather than a guess.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Settings;
using NoctaliaWin.Tests.Config;

namespace NoctaliaWin.Tests.Settings;

public class SettingsEditorTests
{
    private static SettingsField Field(string id) =>
        SettingsCatalog.All().First(a => a.Field.Id == id).Field;

    private static SettingsPage Page(string id) =>
        SettingsCatalog.PageById(id) ?? throw new InvalidOperationException($"no page {id}");

    [Fact]
    public void AnAcceptedWriteSavesOnceAndRaisesChanged()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);
        var raised = 0;
        store.Changed += _ => raised++;

        var error = editor.Apply(Field("bar.main.thickness"), "52");

        Assert.Null(error);
        Assert.Equal(1, raised);
        Assert.Equal("52", editor.ValueOf(Field("bar.main.thickness")));
        Assert.Null(editor.LastError);
    }

    [Fact]
    public void ARejectedWriteChangesNothingAndSaysWhy()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);
        var raised = 0;
        store.Changed += _ => raised++;

        var error = editor.Apply(Field("bar.main.position"), "middle");

        Assert.NotNull(error);
        Assert.Equal(0, raised);
        Assert.Equal("top", editor.ValueOf(Field("bar.main.position")));
        Assert.False(File.Exists(dir.ConfigPath));

        // The banner names the row the user was looking at, not the config key behind it.
        Assert.NotNull(editor.LastError);
        Assert.Contains("Screen edge", editor.LastError!);
        Assert.Contains("middle", editor.LastError!);
    }

    [Fact]
    public void AValueOutOfRangeIsClampedToWhatTheRendererCanDraw()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        Assert.Null(editor.Apply(Field("bar.main.thickness"), "9999"));
        Assert.Equal("128", editor.ValueOf(Field("bar.main.thickness")));
    }

    [Fact]
    public void ARowThatOnlyDisplaysRefusesToBeWritten()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        Assert.NotNull(editor.Apply(Field("system.config_path"), "somewhere else"));
    }

    [Fact]
    public void ResetTakesARowBackToItsDefaultAndClearsTheOverride()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);
        var radius = Field("bar.main.radius");

        editor.Apply(radius, "24");
        Assert.True(radius.IsOverridden(editor.Scope()));

        Assert.Null(editor.ResetField(radius));
        Assert.Equal(radius.DefaultValue, editor.ValueOf(radius));
        Assert.False(radius.IsOverridden(editor.Scope()));
    }

    [Fact]
    public void ResetPageMovesEveryOverriddenRowOfThatPageInOneSave()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);
        var saves = 0;
        store.Changed += _ => saves++;

        editor.Apply(Field("bar.main.thickness"), "44");
        editor.Apply(Field("bar.main.padding"), "20");
        editor.Apply(Field("theme.mode"), "light");
        saves = 0;

        var reset = editor.ResetPage(Page("bar"));

        Assert.Equal(2, reset);
        Assert.Equal(1, saves);
        Assert.Equal("34", editor.ValueOf(Field("bar.main.thickness")));
        Assert.Equal("14", editor.ValueOf(Field("bar.main.padding")));

        // A different page keeps its own change.
        Assert.Equal("light", editor.ValueOf(Field("theme.mode")));
    }

    [Fact]
    public void RestoreDefaultsRewritesTheWholeFile()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);

        editor.Apply(Field("bar.main.position"), "bottom");
        editor.RestoreDefaults();

        Assert.Equal("top", editor.ValueOf(Field("bar.main.position")));
        var reopened = new SettingsStore(dir.Path);
        Assert.Equal("top", reopened.Current.Main.Position);
    }

    [Fact]
    public void AWidgetTheBarCannotDrawIsRefusedByName()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        var error = editor.Apply(Field("bar.main.center"), "clock, totem");

        Assert.NotNull(error);
        Assert.Contains("'totem'", error!);
        Assert.Equal("clock", editor.ValueOf(Field("bar.main.center")));
    }

    [Fact]
    public void ALaneCanBeReorderedAndEmptied()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));
        var lane = Field("bar.main.start");

        Assert.Null(editor.Apply(lane, "clock, launcher, spacer"));
        Assert.Equal("clock,launcher,spacer", editor.ValueOf(lane));

        Assert.Null(editor.Apply(lane, ""));
        Assert.Equal("", editor.ValueOf(lane));
    }

    [Fact]
    public void AShortcutNeedsAModifierButCanBeLeftUnbound()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));
        var hotkey = Field("shell.launcher.hotkey");

        Assert.NotNull(editor.Apply(hotkey, "D"));
        Assert.NotNull(editor.Apply(hotkey, "SUPER+D+X"));

        Assert.Null(editor.Apply(hotkey, "CTRL+SHIFT+F9"));
        Assert.Equal("CTRL+SHIFT+F9", editor.ValueOf(hotkey));

        Assert.Null(editor.Apply(hotkey, ""));
        Assert.Equal("", editor.ValueOf(hotkey));
    }

    [Fact]
    public void ATimeFormatCannotBeEmptied()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        Assert.NotNull(editor.Apply(Field("shell.time_format"), "   "));
        Assert.Null(editor.Apply(Field("shell.time_format"), "%H:%M"));
    }

    [Fact]
    public void ASchemeOutsideThePortedTableIsRefused()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path));

        Assert.NotNull(editor.Apply(Field("theme.builtin"), "Crust"));
        Assert.Null(editor.Apply(Field("theme.builtin"), "Catppuccin"));
        Assert.Equal("Catppuccin", editor.ValueOf(Field("theme.builtin")));
    }

    [Fact]
    public void TheStartupRowWritesThroughTheInjectedServiceAndNothingElse()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var autostart = new FakeAutostart();
        var editor = new SettingsEditor(store, autostart);
        var row = Field("system.autostart");

        Assert.Equal("false", editor.ValueOf(row));
        Assert.Null(editor.Apply(row, "true"));
        Assert.True(autostart.Enabled);

        // Turning autostart off again still commits the config, which is what rebuilds the
        // shell; the registry value itself is the service's business.
        Assert.Null(editor.Apply(row, "false"));
        Assert.False(autostart.Enabled);
        Assert.Equal(2, autostart.Calls);
    }

    [Fact]
    public void TheDisplayRowsShowTheScopeTheWindowPassedIn()
    {
        using var dir = new TempConfigDir();
        var editor = new SettingsEditor(new SettingsStore(dir.Path), appVersion: "1.0.0");

        Assert.Equal("1.0.0", editor.ValueOf(Field("system.version")));
        Assert.Equal(new SettingsStore(dir.Path).ConfigPath, editor.ValueOf(Field("system.config_path")));
    }

    [Fact]
    public void PerCornerRadiusResetClearsTheOverrideRatherThanZeroingIt()
    {
        using var dir = new TempConfigDir();
        var store = new SettingsStore(dir.Path);
        var editor = new SettingsEditor(store);
        var corner = Field("bar.main.radius_top_left");

        editor.Apply(corner, "6");
        Assert.Equal("6", editor.ValueOf(corner));
        Assert.Equal(6, store.Current.Main.RadiusTopLeft);

        editor.ResetField(corner);
        Assert.Null(store.Current.Main.RadiusTopLeft);
        Assert.Equal("-1", editor.ValueOf(corner));
        Assert.Equal("global", corner.DisplayValueFor(editor.Scope()));
    }

    private sealed class FakeAutostart : IStartupService
    {
        public bool Enabled { get; private set; }
        public int Calls { get; private set; }
        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            Calls++;
        }
    }
}
