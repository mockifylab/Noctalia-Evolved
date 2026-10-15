// The calendar panel's month grid. Kept in Core because the layout rules are the part
// Noctalia's users see and argue about (week starts Monday, six rows so heights never
// jump), and none of it needs a display.
namespace NoctaliaWin.Core.Shell;

public readonly record struct MonthCell(int Day, bool InMonth, bool IsToday);

public sealed record MonthGrid(
    string Title,
    IReadOnlyList<string> WeekdayHeaders,
    IReadOnlyList<MonthCell> Cells);

public static class MonthCalendar
{
    /// Six rows always: a short month would otherwise make the panel resize under the user
    /// when they page between months.
    public const int CellCount = 42;

    public static MonthGrid Build(DateAnchor anchor, DateTimeOffset today, IFormatProvider? provider = null)
    {
        var culture = provider as System.Globalization.CultureInfo
            ?? System.Globalization.CultureInfo.CurrentCulture;

        // DayOfWeek follows CultureInfo, which is what the upstream shell does too.
        int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        var headers = Enumerable.Range(0, 7)
            .Select(i => culture.DateTimeFormat.AbbreviatedDayNames[(first + i) % 7])
            .ToList();

        var monthStart = new DateTime(anchor.Year, anchor.Month, 1);
        var gridStart = monthStart.AddDays(-((int)monthStart.DayOfWeek - first + 7) % 7);

        var cells = Enumerable.Range(0, CellCount)
            .Select(i => gridStart.AddDays(i))
            .Select(d => new MonthCell(
                d.Day,
                InMonth: d.Month == monthStart.Month && d.Year == monthStart.Year,
                IsToday: d.Date == today.Date))
            .ToList();

        return new MonthGrid(
            monthStart.ToString("MMMM yyyy", culture),
            headers,
            cells);
    }
}

/// The month being shown, independent of "today" so the panel can page without mutating a
/// date.
public readonly record struct DateAnchor(int Year, int Month)
{
    public static DateAnchor Of(DateOnly date) => new(date.Year, date.Month);

    public DateAnchor ShiftMonths(int delta)
    {
        // Arithmetic happens on a zero-based month index and is floored, so paging back
        // from January lands in December of the previous year rather than in month 0.
        int index = Year * 12 + (Month - 1) + delta;
        int year = Math.DivRem(index, 12, out int shifted);
        if (shifted < 0)
        {
            year -= 1;
            shifted += 12;
        }

        return new DateAnchor(Math.Max(1, year), shifted + 1);
    }
}
