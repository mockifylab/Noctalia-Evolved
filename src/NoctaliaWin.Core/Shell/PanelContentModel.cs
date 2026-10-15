// The state behind the panel surfaces: what month the calendar shows, and the live values
// the quick-settings rows read. Kept in Core so paging, "back to today" and the repaint
// plumbing are tested without a display.
namespace NoctaliaWin.Core.Shell;

public sealed class PanelContentModel : IDisposable
{
    private readonly BarViewModel _bar;
    private DateAnchor _visibleMonth;
    private bool _pinnedToToday = true;

    public event Action? Changed;

    public PanelContentModel(BarViewModel bar)
    {
        _bar = bar;
        _visibleMonth = DateAnchor.Of(DateOnly.FromDateTime(_bar.Now.Date));
        _bar.Changed += OnBarChanged;
    }

    public bool CalendarShowsToday => _pinnedToToday;

    public MonthGrid Calendar()
    {
        var today = _bar.Now;
        // While the user has not paged away, the grid follows the clock across a month
        // boundary instead of sticking on the month the panel opened in.
        if (_pinnedToToday) _visibleMonth = DateAnchor.Of(DateOnly.FromDateTime(today.Date));

        return MonthCalendar.Build(_visibleMonth, today);
    }

    public void PageMonths(int delta)
    {
        if (delta == 0) return;

        _visibleMonth = _visibleMonth.ShiftMonths(delta);
        _pinnedToToday = false;
        Changed?.Invoke();
    }

    public void BackToToday()
    {
        _pinnedToToday = true;
        _visibleMonth = DateAnchor.Of(DateOnly.FromDateTime(_bar.Now.Date));
        Changed?.Invoke();
    }

    /// Re-render when a service changed, the clock moved, or the config was edited.
    public BarViewModel Bar => _bar;

    public void Dispose()
    {
        _bar.Changed -= OnBarChanged;
    }

    private void OnBarChanged() => Changed?.Invoke();
}
