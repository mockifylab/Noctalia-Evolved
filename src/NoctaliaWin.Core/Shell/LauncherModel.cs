// The launcher's search state: what the query matches, which row is selected, and what
// Enter does. Ranking is LauncherSearch's fzy port; this class adds the selection and paging
// the window needs, so both are decided without a display.
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Apps;

namespace NoctaliaWin.Core.Shell;

public sealed class LauncherModel : IDisposable
{
    /// Rows the window shows at once. Upstream pages a grid; a scrolling list window is the
    /// same rule with fewer moving parts, and it keeps the selected row on screen.
    public const int VisibleRows = 8;

    private readonly IApplicationDiscoveryService _apps;
    private readonly IProcessLauncher _launcher;
    private readonly Action<Action> _marshal;
    private List<LauncherResult> _results = [];
    private string _query = string.Empty;
    private int _selected;

    public event Action? Changed;

    public LauncherModel(
        IApplicationDiscoveryService apps,
        IProcessLauncher launcher,
        Action<Action> marshal)
    {
        _apps = apps;
        _launcher = launcher;
        _marshal = marshal;

        _apps.Changed += OnCatalogChanged;
        Search();
    }

    /// The search field. An empty query lists everything, which is what makes this a
    /// dashboard and not only a search box.
    public string Query
    {
        get => _query;
        set
        {
            var next = value ?? string.Empty;
            if (next == _query) return;

            _query = next;
            Search();
        }
    }

    public int Count => _results.Count;

    public int Selected => _selected;

    /// A query that matched nothing is reported as such rather than shown as an empty box.
    public bool NoMatches => _query.Length > 0 && _results.Count == 0;

    /// The first row on screen, kept so the selection is never scrolled out of view.
    public int FirstVisible => Math.Clamp(
        _selected - VisibleRows / 2,
        0,
        Math.Max(0, _results.Count - VisibleRows));

    /// The rows to render, and the index of the selected one among them.
    public IReadOnlyList<LauncherResult> Page()
    {
        int start = FirstVisible;
        int take = Math.Min(VisibleRows, Math.Max(0, _results.Count - start));
        return _results.GetRange(start, take);
    }

    public int SelectedOnPage => _selected - FirstVisible;

    /// Move the selection, wrapping at both ends the way upstream's launcher does.
    public void Move(int delta)
    {
        if (_results.Count == 0) return;

        int wrapped = (_selected + delta) % _results.Count;
        _selected = wrapped < 0 ? wrapped + _results.Count : wrapped;
        Changed?.Invoke();
    }

    /// Select by row on screen, which is what a pointer over a list gives you.
    public void SelectOnPage(int indexOnPage)
    {
        if (_results.Count == 0) return;

        _selected = Math.Clamp(FirstVisible + indexOnPage, 0, _results.Count - 1);
        Changed?.Invoke();
    }

    public AppEntry? SelectedEntry() =>
        _selected < _results.Count ? _results[_selected].Entry : null;

    /// Launch the selected entry. Returns null when it started, or the reason it did not.
    public string? Activate()
    {
        if (SelectedEntry() is not { } entry) return "Nothing to launch";

        return _launcher.Launch(entry);
    }

    /// Re-run the query, e.g. after the installed-app catalog changed under the window.
    public void Refresh() => Search();

    private void Search()
    {
        _results = _query.Length == 0
            ? _apps.Applications
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new LauncherResult(e, 0, []))
                .ToList()
            : LauncherSearch.Search(_query, _apps.Applications).ToList();

        _selected = 0;
        Changed?.Invoke();
    }

    private void OnCatalogChanged() => _marshal(Search);

    public void Dispose() => _apps.Changed -= OnCatalogChanged;
}
