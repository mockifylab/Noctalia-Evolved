// Renders Noctalia's strftime-ish format strings (config `shell.time_format`,
// `shell.date_format`) against a DateTimeOffset.
//
// Implemented as a direct evaluator instead of a translation to a .NET custom format
// string: .NET would reinterpret unknown literal characters, and the config file is
// user-editable, so an unrecognised token has to survive visibly rather than corrupt
// the surrounding text.
using System.Globalization;
using System.Text;

namespace NoctaliaWin.Core.Shell;

public static class Strftime
{
    /// Noctalia writes clock formats as chrono format specs, e.g. "{:%H:%M}".
    /// The wrapper is stripped so both "{:%H:%M}" and "%H:%M" work.
    public static string Unwrap(string format)
    {
        var s = format.Trim();
        if (s.StartsWith("{:", StringComparison.Ordinal) && s.EndsWith('}'))
            return s[2..^1];
        return s;
    }

    public static string Format(string format, DateTimeOffset when, IFormatProvider? provider = null)
    {
        var culture = provider as CultureInfo ?? CultureInfo.CurrentCulture;
        var pattern = Unwrap(format);

        var sb = new StringBuilder(pattern.Length * 2);
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != '%')
            {
                sb.Append(pattern[i]);
                continue;
            }

            // A trailing '%' has nothing to consume; keep it visible.
            if (i + 1 >= pattern.Length)
            {
                sb.Append('%');
                break;
            }

            char token = pattern[++i];
            if (!TryAppend(token, when, culture, sb))
            {
                // Unknown token: emit it verbatim rather than guessing.
                sb.Append('%').Append(token);
            }
        }

        return sb.ToString();
    }

    private static bool TryAppend(char token, DateTimeOffset when, CultureInfo culture, StringBuilder sb)
    {
        switch (token)
        {
            case 'Y': sb.Append(when.Year.ToString("D4", CultureInfo.InvariantCulture)); return true;
            case 'y': sb.Append((when.Year % 100).ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'm': sb.Append(when.Month.ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'd': sb.Append(when.Day.ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'e': sb.Append(when.Day.ToString("D2", CultureInfo.InvariantCulture).TrimStart('0').PadLeft(2, ' ')); return true;
            case 'H': sb.Append(when.Hour.ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'I': sb.Append(TwelveHour(when).ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'M': sb.Append(when.Minute.ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'S': sb.Append(when.Second.ToString("D2", CultureInfo.InvariantCulture)); return true;
            case 'j': sb.Append(when.DayOfYear.ToString("D3", CultureInfo.InvariantCulture)); return true;
            case 'p': sb.Append(Designator(when, culture).ToUpperInvariant()); return true;
            case 'P': sb.Append(Designator(when, culture).ToLowerInvariant()); return true;
            case 'A': sb.Append(culture.DateTimeFormat.GetDayName(when.DayOfWeek)); return true;
            case 'a': sb.Append(culture.DateTimeFormat.GetAbbreviatedDayName(when.DayOfWeek)); return true;
            case 'B': sb.Append(culture.DateTimeFormat.GetMonthName(when.Month)); return true;
            case 'b': sb.Append(culture.DateTimeFormat.GetAbbreviatedMonthName(when.Month)); return true;
            case 'n': sb.Append('\n'); return true;
            case 't': sb.Append('\t'); return true;
            case '%': sb.Append('%'); return true;
            // Locale-appropriate representations: the culture's own .NET patterns are the
            // closest documented equivalent of strftime's %x, %X and %c.
            case 'x': sb.Append(when.ToString(culture.DateTimeFormat.ShortDatePattern, culture)); return true;
            case 'X': sb.Append(when.ToString(culture.DateTimeFormat.LongTimePattern, culture)); return true;
            case 'c': sb.Append(when.ToString(culture.DateTimeFormat.FullDateTimePattern, culture)); return true;
            case 'D': sb.Append(Format("%m/%d/%y", when, culture)); return true;
            case 'F': sb.Append(Format("%Y-%m-%d", when, culture)); return true;
            case 'T': sb.Append(Format("%H:%M:%S", when, culture)); return true;
            default: return false;
        }
    }

    private static int TwelveHour(DateTimeOffset when) => when.Hour % 12 is 0 ? 12 : when.Hour % 12;

    private static string Designator(DateTimeOffset when, CultureInfo culture) =>
        when.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
}
