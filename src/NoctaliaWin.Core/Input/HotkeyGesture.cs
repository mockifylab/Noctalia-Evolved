// The text form of a global hotkey, as written in config. Parsing lives here because it is
// the part with rules worth testing; turning a key name into a virtual-key code is a Win32
// concern and stays in NoctaliaWin.Windows.
namespace NoctaliaWin.Core.Input;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// A modifier combination plus exactly one key name, e.g. `CTRL+ALT+SPACE`.
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    /// Upstream's keybind syntax names the Super key several ways, and every tool that has
    /// ever written one of these files uses a different alias for it.
    private static readonly Dictionary<string, HotkeyModifiers> Aliases = new(StringComparer.Ordinal)
    {
        ["CTRL"] = HotkeyModifiers.Control,
        ["CONTROL"] = HotkeyModifiers.Control,
        ["ALT"] = HotkeyModifiers.Alt,
        ["MOD1"] = HotkeyModifiers.Alt,
        ["SHIFT"] = HotkeyModifiers.Shift,
        ["SUPER"] = HotkeyModifiers.Win,
        ["WIN"] = HotkeyModifiers.Win,
        ["WINDOWS"] = HotkeyModifiers.Win,
        ["MOD4"] = HotkeyModifiers.Win,
        ["LOGO"] = HotkeyModifiers.Win,
    };

    /// The key names this build can turn into a virtual-key code. Anything else is reported
    /// rather than registered as something else.
    public static readonly IReadOnlySet<string> SupportedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24",
        "SPACE", "ENTER", "RETURN", "TAB", "ESCAPE", "ESC", "BACKSPACE",
        "INSERT", "DELETE", "HOME", "END", "PAGEUP", "PAGEDOWN",
        "LEFT", "UP", "RIGHT", "DOWN",
    };

    /// Empty or whitespace means "no hotkey", which is a valid setting and not an error:
    /// a global binding is something a user has to be able to turn off.
    public static bool TryParse(string? text, out HotkeyGesture? gesture, out string? error)
    {
        gesture = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text)) return true;

        var modifiers = HotkeyModifiers.None;
        string? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.ToUpperInvariant();

            if (Aliases.TryGetValue(token, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (key is not null)
            {
                error = $"\"{text}\" binds more than one key; a hotkey is modifiers plus one key";
                return false;
            }

            if (!SupportedKeys.Contains(token))
            {
                error = $"\"{token}\" is not a key this build can register";
                return false;
            }

            key = token;
        }

        if (key is null)
        {
            error = $"\"{text}\" names no key";
            return false;
        }

        // Without a modifier the binding would swallow a key every application expects to
        // receive, so it is refused here and again in the Win32 layer.
        if (modifiers == HotkeyModifiers.None)
        {
            error = $"\"{text}\" has no modifier; a global hotkey needs at least one";
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    /// The canonical text form - CTRL, ALT, SHIFT, SUPER, then the key - so a parsed gesture can
    /// be shown to a user or written back to config without every caller re-inventing the order.
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("CTRL");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("ALT");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("SHIFT");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("SUPER");
        parts.Add(Key);
        return string.Join('+', parts);
    }
}
