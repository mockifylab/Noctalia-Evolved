// Search over the settings catalog, the way upstream does it: a plain lowercase substring
// across the section, the group, the row's title, description, config path and tags, capped
// at 50 matches (src/settings/settings_search.cpp — MIT License, Copyright (c) 2026
// noctalia-dev). Fuzzy scoring is the launcher's job; a settings query should match what the
// user can see on screen.
namespace NoctaliaWin.Core.Settings;

public sealed record SettingsMatch(SettingsPage Page, SettingsGroup Group, SettingsField Field)
{
    /// What upstream shows beside a result: where the row lives, so a jump back out of search
    /// lands on the right page.
    public string Location => $"{Page.Title} \u203a {Group.Title}";
}

public static class SettingsSearch
{
    public const int MaxResults = 50;

    public static IReadOnlyList<SettingsMatch> Find(string? query, bool includeAdvanced = false)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var needle = query.Trim().ToLowerInvariant();
        var results = new List<SettingsMatch>();

        foreach (var (page, group, field) in SettingsCatalog.All())
        {
            if (field.Advanced && !includeAdvanced) continue;
            if (!field.SearchText(page.Title, group.Title).Contains(needle, StringComparison.Ordinal))
                continue;

            results.Add(new SettingsMatch(page, group, field));
            if (results.Count >= MaxResults) break;
        }

        return results;
    }
}
