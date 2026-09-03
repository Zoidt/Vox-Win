using System.ComponentModel;
using System.Runtime.InteropServices;
using Vox.Core;

namespace Vox.Windows;

/// <summary>Hook callbacks only route key edges. Subscribers must post work to their dispatcher.</summary>
public sealed class GlobalKeyboard : IDisposable
{
    private readonly NativeMethods.HookProc _callback;
    private readonly nint _hook;
    private bool _pressed;
    private bool _escapePressed;
    public Hotkey Shortcut { get; set; } = new();
    public bool Suspended { get; set; }
    public bool CanCancel { get; set; }
    public event Action<bool>? ShortcutChanged;
    public event Action? CancelPressed;

    public GlobalKeyboard()
    {
        _callback = Handle;
        _hook = NativeMethods.SetWindowsHookEx(13, _callback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the global keyboard listener.");
    }

    private nint Handle(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(data);
            var down = message == 0x100 || message == 0x104;
            var up = message == 0x101 || message == 0x105;
            if ((key.Flags & 0x10) == 0 && (down || up))
            {
                if (key.Key == 0x1B && (_escapePressed || (CanCancel && !Suspended)))
                {
                    if (down && !_escapePressed) { _escapePressed = true; CancelPressed?.Invoke(); }
                    if (up) _escapePressed = false;
                    return 1;
                }
                if (!Suspended && key.Key == Shortcut.VirtualKey)
                {
                    if (down && !_pressed && ModifiersMatch())
                    {
                        _pressed = true;
                        ShortcutChanged?.Invoke(true);
                        return 1;
                    }
                    if (_pressed)
                    {
                        if (up) { _pressed = false; ShortcutChanged?.Invoke(false); }
                        return 1;
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }

    private bool ModifiersMatch()
    {
        var actual = HotkeyModifiers.None;
        if (NativeMethods.IsDown(0x11)) actual |= HotkeyModifiers.Control;
        if (NativeMethods.IsDown(0x12)) actual |= HotkeyModifiers.Alt;
        if (NativeMethods.IsDown(0x10)) actual |= HotkeyModifiers.Shift;
        if (NativeMethods.IsDown(0x5B) || NativeMethods.IsDown(0x5C)) actual |= HotkeyModifiers.Windows;
        // The primary key can itself be a modifier (e.g. Right Ctrl).
        var primary = Shortcut.VirtualKey switch
        {
            0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,
            0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,
            0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,
            0x5B or 0x5C => HotkeyModifiers.Windows,
            _ => HotkeyModifiers.None
        };
        return (actual & ~primary) == Shortcut.Modifiers;
    }

    public void Dispose() { NativeMethods.UnhookWindowsHookEx(_hook); GC.KeepAlive(_callback); }
}
