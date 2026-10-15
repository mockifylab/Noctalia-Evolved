// The config text -> gesture rules. The Win32 half (virtual-key codes) is covered in
// Windows/VirtualKeyTests.cs; nothing here touches the operating system.
using NoctaliaWin.Core.Input;

namespace NoctaliaWin.Tests.Input;

public class HotkeyGestureTests
{
    [Fact]
    public void TheDefaultLauncherBindingParses()
    {
        Assert.True(HotkeyGesture.TryParse("CTRL+ALT+SPACE", out var gesture, out var error));
        Assert.Null(error);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, gesture!.Modifiers);
        Assert.Equal("SPACE", gesture.Key);
    }

    [Theory]
    [InlineData("ctrl+alt+space")]
    [InlineData("  Ctrl + Alt + Space  ")]
    [InlineData("CONTROL+MOD1+SPACE")]
    [InlineData("MOD4+SHIFT+F12")]
    public void ModifiersAreSpelledManyWays(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture, out var error));
        Assert.Null(error);
        Assert.NotNull(gesture);
    }

    [Theory]
    [InlineData("SUPER+ALT+F12", "ALT+SUPER+F12")]
    [InlineData("mod4+shift+ctrl+a", "CTRL+SHIFT+SUPER+A")]
    [InlineData("WIN+D", "SUPER+D")]
    [InlineData("windows+shift+enter", "SHIFT+SUPER+ENTER")]
    public void ToStringWritesTheCanonicalForm(string text, string expected)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture, out _));
        Assert.Equal(expected, gesture!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankMeansNoBindingAndThatIsNotAnError(string? text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture, out var error));
        Assert.Null(gesture);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("CTRL+ALT+A+B")]
    [InlineData("CTRL+ENTER+TAB")]
    public void MoreThanOneKeyIsRefused(string text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _, out var error));
        Assert.Contains("more than one key", error);
    }

    [Theory]
    [InlineData("CTRL+PLUS", "PLUS")]
    [InlineData("SUPER+MEDIAPLAY", "MEDIAPLAY")]
    [InlineData("CTRL+MOUSELEFT", "MOUSELEFT")]
    public void AKeyThisBuildCannotRegisterIsNamed(string text, string key)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _, out var error));
        Assert.Contains(key, error);
    }

    [Fact]
    public void AGestureNeedsAKey()
    {
        Assert.False(HotkeyGesture.TryParse("CTRL+ALT", out _, out var error));
        Assert.Contains("names no key", error);
    }

    [Fact]
    public void AGestureNeedsAModifier()
    {
        // A bare key would be stolen from every application on the desktop, so it is not
        // offered as a configuration even though Win32 would accept it.
        Assert.False(HotkeyGesture.TryParse("SPACE", out _, out var error));
        Assert.Contains("no modifier", error);
    }

    [Fact]
    public void TheOfferedKeysAreTheWholeSet()
    {
        // Windows/VirtualKeyTests.cs proves every one of these has a virtual-key code; this
        // count is here so a name cannot quietly disappear from the list the parser accepts.
        Assert.Equal(77, HotkeyGesture.SupportedKeys.Count);
        Assert.Contains("ESC", HotkeyGesture.SupportedKeys);
        Assert.Contains("ESCAPE", HotkeyGesture.SupportedKeys);
    }
}
