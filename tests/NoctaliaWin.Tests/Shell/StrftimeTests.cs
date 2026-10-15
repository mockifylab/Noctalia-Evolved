using System.Globalization;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Tests.Shell;

public sealed class StrftimeTests
{
    // 2026-10-15 is a Thursday.
    private static readonly DateTimeOffset Thursday = new(2026, 10, 15, 14, 7, 9, TimeSpan.FromHours(2));

    private static string Format(string pattern, DateTimeOffset when = default) =>
        Strftime.Format(pattern, when == default ? Thursday : when, CultureInfo.InvariantCulture);

    [Fact]
    public void DefaultTimeFormat_RendersTwoDigitHoursAndMinutes()
    {
        Assert.Equal("14:07", Format("{:%H:%M}"));
    }

    [Fact]
    public void PlainPatterns_DoNotNeedTheChronoWrapper()
    {
        Assert.Equal("14:07", Format("%H:%M"));
        Assert.Equal("%H:%M", Strftime.Unwrap("{:%H:%M}"));
        Assert.Equal("%H:%M", Strftime.Unwrap("%H:%M"));
        Assert.Equal("{unclosed", Strftime.Unwrap("{unclosed"));
    }

    [Fact]
    public void WeekdayAndLocaleDate_UseTheProvidedCulture()
    {
        // InvariantCulture is the .NET equivalent of C's locale for %x.
        Assert.Equal("Thursday, 10/15/2026", Format("%A, %x"));
        Assert.Equal("Thu, 14:07:09", Format("%a, %X"));
    }

    [Theory]
    [InlineData(0, "12", "AM")]
    [InlineData(11, "11", "AM")]
    [InlineData(12, "12", "PM")]
    [InlineData(13, "01", "PM")]
    [InlineData(23, "11", "PM")]
    public void TwelveHourClockAndMeridiem(int hour, string expectedHours, string expectedMeridiem)
    {
        var when = new DateTimeOffset(2026, 10, 15, hour, 30, 0, TimeSpan.Zero);

        Assert.Equal(expectedHours, Format("%I", when));
        Assert.Equal(expectedMeridiem, Format("%p", when));
        Assert.Equal(expectedMeridiem.ToLowerInvariant(), Format("%P", when));
    }

    [Fact]
    public void DateCombinations_MatchStrftimeDefinitions()
    {
        Assert.Equal("2026-10-15", Format("%F"));
        Assert.Equal("10/15/26", Format("%D"));
        Assert.Equal("14:07:09", Format("%T"));
        // InvariantCulture's full pattern is day-before-month with a 24-hour clock.
        Assert.Equal("Thursday, 15 October 2026 14:07:09", Format("%c"));
    }

    [Fact]
    public void FieldWidthsArePaddedAsDocumented()
    {
        Assert.Equal("26", Format("%y"));
        Assert.Equal("07", Format("%M"));
        Assert.Equal("09", Format("%S"));
    }

    [Fact]
    public void DayOfYear_IsThreeDigits()
    {
        var when = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("005", Format("%j", when));
    }

    [Fact]
    public void DayOfMonth_SpacePadsSingleDigits()
    {
        var early = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(" 5", Format("%e", early));
        Assert.Equal("15", Format("%e", late));
    }

    [Fact]
    public void WhitespaceAndLiteralPercentArePassedThrough()
    {
        Assert.Equal("a\nb", Format("a%nb"));
        Assert.Equal("a\tb", Format("a\tb"));
        Assert.Equal("100%", Format("100%%"));
        Assert.Equal("no tokens here", Format("no tokens here"));
    }

    [Fact]
    public void UnrecognisedTokens_SurviveInsteadOfCorruptingTheLabel()
    {
        // A hand-edited config must stay readable: an unknown token is echoed verbatim,
        // and a trailing percent sign is not swallowed.
        Assert.Equal("time: %q 14:07", Format("time: %q %H:%M"));
        Assert.Equal("14:07%", Format("%H:%M%"));
    }

    [Fact]
    public void MonthNamesUseTheCulture()
    {
        Assert.Equal("October Oct 10", Format("%B %b %m"));
    }
}
