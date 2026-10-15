// Global hotkeys, through the documented RegisterHotKey path.
//
// A binding belongs to a window, and WM_HOTKEY is delivered to that window's thread, so the
// shell cannot hang one off a bar or a panel: those are destroyed and rebuilt on every config
// change and on every display change, which would silently drop the shortcut. The handle here
// is a message-only window - invisible, never activated, nothing but a message target - created
// and torn down with the process, on the UI thread that also owns the WPF dispatcher.
using System.Runtime.InteropServices;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Input;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

public sealed class HotkeyService : IHotkeyService, IDisposable
{
    // RegisterHotKey fails with this when somebody else already owns the combination.
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly HotkeyWindow _window;

    /// Id -> callback. The ids are what WM_HOTKEY comes back with, so they have to stay
    /// unique for the life of the process even though the callbacks are replaced.
    private readonly Dictionary<int, Action> _callbacks = [];

    private int _nextId = 1;
    private bool _disposed;

    public HotkeyService()
    {
        _window = new HotkeyWindow(Dispatch);
    }

    public string? Register(string gesture, Action callback)
    {
        if (_disposed) return "hotkey service is closed";
        if (!HotkeyGesture.TryParse(gesture, out var parsed, out var error))
            return error ?? $"\"{gesture}\" is not a usable hotkey";

        // A valid gesture that came back null is a deliberate "no binding".
        if (parsed is null) return null;

        if (!VirtualKeys.TryGet(parsed.Key, out var vk))
            return $"\"{parsed.Key}\" names a key Windows has no virtual code for";

        var modifiers = Translate(parsed.Modifiers) | NativeMethods.MOD_NOREPEAT;
        int id = _nextId++;

        if (!NativeMethods.RegisterHotKey(_window.Handle, id, modifiers, vk))
        {
            int win32Error = Marshal.GetLastWin32Error();
            return win32Error == ErrorHotkeyAlreadyRegistered
                ? $"{parsed} is already taken by another application"
                : $"{parsed} could not be registered (Windows error {win32Error})";
        }

        _callbacks[id] = callback;
        return null;
    }

    public void UnregisterAll()
    {
        foreach (int id in _callbacks.Keys.ToArray())
            NativeMethods.UnregisterHotKey(_window.Handle, id);

        _callbacks.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unregistering with the handle still alive is the polite half; releasing the handle
        // makes Windows drop whatever is left.
        UnregisterAll();
        _window.DestroyHandle();
    }

    /// The ids are assigned before the window can raise them, so an unknown id is impossible -
    /// but a callback that throws must not take the message loop down with it.
    private void Dispatch(int id)
    {
        try
        {
            if (_callbacks.TryGetValue(id, out var callback)) callback();
        }
        catch
        {
            // A failed toggle is not worth killing the shell over. The next press retries.
        }
    }

    private static uint Translate(HotkeyModifiers modifiers)
    {
        uint flags = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) flags |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) flags |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) flags |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) flags |= NativeMethods.MOD_WIN;
        return flags;
    }

    private sealed class HotkeyWindow : NativeWindow
    {
        private readonly Action<int> _raised;

        public HotkeyWindow(Action<int> raised)
        {
            _raised = raised;
            CreateHandle(new CreateParams
            {
                Caption = "NoctaliaWin.Hotkeys",
                Parent = NativeMethods.HWND_MESSAGE,
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY) _raised(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
}
