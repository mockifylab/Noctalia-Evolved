// Launcher ranking over application entries, using the fzy port.
// Mirrors Noctalia's launcher behavior: fuzzy match against name and path,
// best score wins, matched spans returned for highlighting.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Fuzzy;

namespace NoctaliaWin.Core.Apps;

public sealed record LauncherResult(AppEntry Entry, double Score, int[] NamePositions);

public static class LauncherSearch
{
    public static IReadOnlyList<LauncherResult> Search(
        string query,
        IReadOnlyList<AppEntry> entries,
        int maxResults = 200)
    {
        if (string.IsNullOrEmpty(query))
            return [];

        var results = new List<LauncherResult>(entries.Count);
        foreach (var entry in entries)
        {
            if (!FzyScorer.HasMatch(query, entry.Name) && !FzyScorer.HasMatch(query, entry.Exec))
                continue;

            var (nameScore, namePositions) = FzyScorer.MatchPositions(query, entry.Name);
            var (pathScore, _) = FzyScorer.MatchPositions(query, entry.Exec);
            // Unreasonably long names/paths score -infinity upstream, which already
            // sinks them below every reasonably sized candidate.
            var score = Math.Max(nameScore, pathScore);

            results.Add(new LauncherResult(entry, score, namePositions ?? []));
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        return results.Count > maxResults ? results.GetRange(0, maxResults) : results;
    }
}
