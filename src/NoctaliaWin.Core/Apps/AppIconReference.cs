// Which icon a launcher entry asks the shell for.
//
// Windows stores an icon as "file,index" - a binary that holds many icons and the number of
// the one it wants - and a shortcut either carries that reference or names no icon at all and
// borrows the one belonging to whatever it launches. This decides the file and index from the
// strings discovery hands over, in the order they are worth trying; the shell call itself
// lives in NoctaliaWin.Windows.
using System.Globalization;
using NoctaliaWin.Core.Abstractions;

namespace NoctaliaWin.Core.Apps;

public sealed record AppIconReference(string FilePath, int IconIndex)
{
    /// A shortcut is not a container of icons, so a reference that points at one can only be
    /// answered by the shell resolving it - which is why it is tried last, after the program
    /// the entry actually runs.
    public bool IsShortcut =>
        FilePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    /// null for anything that does not name a real file: a reference has to carry a complete
    /// path, because a bare name would be searched for across the whole machine. A string with
    /// no comma is a plain file at index zero, which is how most shortcuts store their icon.
    public static AppIconReference? Parse(string? iconPath)
    {
        if (iconPath is null) return null;

        string text = iconPath.Trim();
        if (text.Length == 0) return null;

        string file = text;
        int index = 0;

        // The index is whatever follows the last comma, and only when it really is a number:
        // a path may contain a comma itself, and then the whole string is the file. An empty
        // tail is a shortcut that stores its location with the comma and no index after it.
        int comma = text.LastIndexOf(',');
        string tail = comma > 0 ? text[(comma + 1)..].Trim() : string.Empty;
        int parsed = 0;
        bool indexed = tail.Length == 0
            || int.TryParse(tail, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out parsed);

        if (comma > 0 && indexed && Unquote(text[..comma].Trim()) is { Length: > 0 } before)
        {
            file = before;
            index = parsed;
        }
        else
        {
            file = Unquote(text);
        }

        // Shortcuts may name their icon through a variable; the shell expands it before the
        // file is looked for.
        file = Environment.ExpandEnvironmentVariables(file).Trim();
        return file.Length > 0 && Path.IsPathRooted(file)
            ? new AppIconReference(file, index)
            : null;
    }

    /// The references for one entry, best first. A shortcut that names no icon falls back to
    /// the program it starts, because that is where its picture comes from; a bare command
    /// with no path is not offered, since there is nothing to open.
    public static IReadOnlyList<AppIconReference> ForEntry(AppEntry entry)
    {
        var candidates = new List<AppIconReference>(2);

        if (Parse(entry.IconPath) is { } named) candidates.Add(named);

        if (Path.IsPathRooted(entry.Exec)
            && entry.Exec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && new AppIconReference(entry.Exec, 0) is { } own
            && !candidates.Contains(own))
        {
            candidates.Add(own);
        }

        return candidates;
    }

    /// The reference a shortcut carries for its own picture is quoted when the path has a
    /// space in it, which most installed programs' paths do.
    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1].Trim()
            : value;
}
