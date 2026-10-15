// Key names from config to Windows virtual-key codes (winuser.h). A table rather than
// VkKeyScanW because that maps keyboard *characters* through the current layout, and a user
// on a non-US keyboard asking for CTRL+ALT+SPACE wants the physical Space key, not whatever
// happens to scan to ' ' there.
namespace NoctaliaWin.Windows.Native;

internal static class VirtualKeys
{
    public static bool TryGet(string name, out uint vk)
    {
        // The letters and digits share their codes with ASCII, so a one-character name is its
        // own virtual key: VK_A is 0x41 and VK_0 is 0x30.
        if (name.Length == 1
            && (name[0] is >= 'A' and <= 'Z' || name[0] is >= '0' and <= '9'))
        {
            vk = name[0];
            return true;
        }

        return WellKnown.TryGetValue(name, out vk);
    }

    private static readonly Dictionary<string, uint> WellKnown = Build();

    private static Dictionary<string, uint> Build()
    {
        var keys = new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["SPACE"] = 0x20,
            ["ENTER"] = 0x0D,
            ["RETURN"] = 0x0D,
            ["TAB"] = 0x09,
            ["ESCAPE"] = 0x1B,
            ["ESC"] = 0x1B,
            ["BACKSPACE"] = 0x08,
            ["INSERT"] = 0x2D,
            ["DELETE"] = 0x2E,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22,
            ["LEFT"] = 0x25,
            ["UP"] = 0x26,
            ["RIGHT"] = 0x27,
            ["DOWN"] = 0x28,
        };

        // F1..F24 are consecutive from VK_F1 = 0x70.
        for (int i = 1; i <= 24; i++) keys[$"F{i}"] = (uint)(0x6F + i);

        return keys;
    }
}
