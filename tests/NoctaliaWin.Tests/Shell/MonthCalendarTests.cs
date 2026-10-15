using System.Globalization;
using NoctaliaWin.Core.Shell;
using Xunit;

namespace NoctaliaWin.Tests.Shell;

public sealed class MonthCalendarTests
{
    private static readonly CultureInfo EnGb = new("en-GB");

    private static MonthGrid Build(int year, int month, string today, CultureInfo? culture = null) =>
        MonthCalendar.Build(
            new DateAnchor(year, month),
            DateTimeOffset.Parse(today, CultureInfo.InvariantCulture),
            culture ?? EnGb);

    [Fact]
    public void TheGridIsAlwaysSixWeeksSoThePanelNeverResizes()
    {
        Assert.Equal(MonthCalendar.CellCount, Build(2026, 2, "2026-02-10").Cells.Count);
        Assert.Equal(MonthCalendar.CellCount, Build(2026, 12, "2026-12-01").Cells.Count);
    }

    [Fact]
    public void HeadersFollowTheCulturesFirstDayOfWeek()
    {
        // en-GB starts the week on Monday; en-US on Sunday. The shell follows the user's
        // locale rather than picking one.
        Assert.Equal("Mon", Build(2026, 10, "2026-10-15").WeekdayHeaders[0]);
        Assert.Equal("Sun", Build(2026, 10, "2026-10-15", new CultureInfo("en-US")).WeekdayHeaders[0]);
    }

    [Fact]
    public void DaysOutsideTheMonthAreMarkedNotHidden()
    {
        // October 2026 starts on a Thursday, so the Monday-led grid opens with September.
        var grid = Build(2026, 10, "2026-10-15");

        Assert.False(grid.Cells[0].InMonth);
        Assert.Equal(28, grid.Cells[0].Day);
        Assert.Equal(1, grid.Cells.First(c => c.InMonth).Day);
    }

    [Fact]
    public void TodayIsFlaggedOnlyInsideTheShownMonth()
    {
        var current = Build(2026, 10, "2026-10-15");
        Assert.Single(current.Cells, c => c.IsToday);
        Assert.Equal(15, current.Cells.Single(c => c.IsToday).Day);

        var otherMonth = Build(2026, 11, "2026-10-15");
        Assert.DoesNotContain(otherMonth.Cells, c => c.IsToday);
    }

    [Fact]
    public void TheTitleIsTheMonthAndYear()
    {
        Assert.Equal("October 2026", Build(2026, 10, "2026-10-15").Title);
    }

    [Fact]
    public void PagingForwardsAndBackwardsCrossesYearBoundaries()
    {
        var december = new DateAnchor(2026, 12);

        Assert.Equal(new DateAnchor(2027, 1), december.ShiftMonths(1));
        Assert.Equal(new DateAnchor(2026, 12), december.ShiftMonths(12).ShiftMonths(-12));
        Assert.Equal(new DateAnchor(2027, 1), new DateAnchor(2026, 1).ShiftMonths(12));
        Assert.Equal(new DateAnchor(2025, 12), new DateAnchor(2026, 1).ShiftMonths(-1));
    }

    [Fact]
    public void PagingManyMonthsStaysInValidRange()
    {
        var anchor = new DateAnchor(2026, 3);

        for (int delta = -60; delta <= 60; delta++)
        {
            var moved = anchor.ShiftMonths(delta);
            Assert.InRange(moved.Month, 1, 12);
            Assert.True(moved.Year >= 1);
        }
    }

    [Fact]
    public void TheFirstVisibleCellIsAlwaysTheGridStartForThatWeekday()
    {
        // February 2028 starts on a Tuesday, so a Monday-led grid shows January 31 first.
        var grid = Build(2028, 2, "2028-02-01");

        Assert.Equal(31, grid.Cells[0].Day);
        Assert.False(grid.Cells[0].InMonth);
        Assert.Equal(1, grid.Cells[1].Day);
        Assert.True(grid.Cells[1].IsToday);
    }
}
