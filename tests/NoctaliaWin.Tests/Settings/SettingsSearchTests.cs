// Search is the route to a setting nobody can find by browsing, so it has to match the words
// a row actually shows - and stop at upstream's cap instead of rendering a wall.
using NoctaliaWin.Core.Settings;

namespace NoctaliaWin.Tests.Settings;

public class SettingsSearchTests
{
    [Fact]
    public void AWordOnTheRowFindsTheRow()
    {
        var matches = SettingsSearch.Find("opacity");

        Assert.Contains("bar.main.background_opacity", matches.Select(m => m.Field.Id));
    }

    [Fact]
    public void CaseAndSurroundingSpacesDoNotMatter()
    {
        Assert.Equal(
            SettingsSearch.Find("opacity").Select(m => m.Field.Id),
            SettingsSearch.Find("  OPACITY  ").Select(m => m.Field.Id));
    }

    [Fact]
    public void TheConfigPathItselfIsSearchable()
    {
        Assert.Contains(
            "bar.main.margin_ends",
            SettingsSearch.Find("margin_ends").Select(m => m.Field.Id));
    }

    [Fact]
    public void TagsBringUpRowsThatDoNotRepeatTheWord()
    {
        // The launcher's shortcut row never says "shortcut" in its title.
        Assert.Contains(
            "shell.launcher.hotkey",
            SettingsSearch.Find("keyboard").Select(m => m.Field.Id));
    }

    [Fact]
    public void ThePageAndGroupTitlesArePartOfTheMatch()
    {
        Assert.Contains(
            "control_center.width",
            SettingsSearch.Find("control center").Select(m => m.Field.Id));
    }

    [Fact]
    public void AdvancedRowsOnlyAppearWhenAdvancedIsOn()
    {
        Assert.DoesNotContain(
            "bar.main.panel_overlap",
            SettingsSearch.Find("overlap").Select(m => m.Field.Id));

        Assert.Contains(
            "bar.main.panel_overlap",
            SettingsSearch.Find("overlap", includeAdvanced: true).Select(m => m.Field.Id));
    }

    [Fact]
    public void NothingMatchesAQueryNobodyTyped()
    {
        Assert.Empty(SettingsSearch.Find(null));
        Assert.Empty(SettingsSearch.Find("   "));
        Assert.Empty(SettingsSearch.Find("purple-unicorns"));
    }

    [Fact]
    public void ResultsAreCappedAndCarryTheirLocation()
    {
        var matches = SettingsSearch.Find("e", includeAdvanced: true);

        Assert.NotEmpty(matches);
        Assert.True(matches.Count <= SettingsSearch.MaxResults, $"{matches.Count} results");
        Assert.Equal("Appearance \u203a Theme", matches[0].Location);
    }
}
