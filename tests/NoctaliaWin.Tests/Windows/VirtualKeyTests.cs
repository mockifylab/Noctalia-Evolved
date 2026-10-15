// The config key name -> Win32 virtual-key code table. Codes are asserted as literals from
// winuser.h on purpose: re-deriving them from the table under test would prove nothing.
using NoctaliaWin.Core.Input;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Tests.Windows;

public class VirtualKeyTests
{
    [Theory]
    [InlineData("A", 0x41)]
    [InlineData("Z", 0x5A)]
    [InlineData("0", 0x30)]
    [InlineData("9", 0x39)]
    [InlineData("SPACE", 0x20)]
    [InlineData("ENTER", 0x0D)]
    [InlineData("RETURN", 0x0D)]
    [InlineData("TAB", 0x09)]
    [InlineData("ESC", 0x1B)]
    [InlineData("ESCAPE", 0x1B)]
    [InlineData("BACKSPACE", 0x08)]
    [InlineData("F1", 0x70)]
    [InlineData("F12", 0x7B)]
    [InlineData("F24", 0x87)]
    [InlineData("LEFT", 0x25)]
    [InlineData("UP", 0x26)]
    [InlineData("RIGHT", 0x27)]
    [InlineData("DOWN", 0x28)]
    public void NamesMapToTheDocumentedCodes(string name, uint expected)
    {
        Assert.True(VirtualKeys.TryGet(name, out uint vk));
        Assert.Equal(expected, vk);
    }

    [Fact]
    public void EveryKeyTheParserAcceptsHasACode()
    {
        // The two lists live in different assemblies, so this is the only thing standing
        // between "parseable from config" and "nothing to register".
        foreach (string name in HotkeyGesture.SupportedKeys)
            Assert.True(VirtualKeys.TryGet(name, out _), $"{name} has no virtual-key code");
    }

    [Theory]
    [InlineData("PLUS")]
    [InlineData("MOUSELEFT")]
    [InlineData("F25")]
    [InlineData("F0")]
    [InlineData("AA")]
    [InlineData("")]
    public void NamesWithNoCodeAreRefused(string name)
    {
        Assert.False(VirtualKeys.TryGet(name, out _));
    }
}
