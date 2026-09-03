using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Vox.Core;

namespace Vox.Windows;

/// <summary>Hook callbacks only route key edges. Subscribers must post work to their dispatcher.</summary>
public sealed class GlobalKeyboard : IDisposable
{
    private readonly NativeMethods.HookProc _callback;
    private readonly nint _hook;
    private bool _pressed;
    private bool _escapePressed;
    private HotkeyCapture? _capture;
    private HotkeyModifiers _modifiersDown;
    public Hotkey Shortcut { get; set; } = new();
    public bool Suspended { get; set; }
    public bool CanCancel { get; set; }
    public event Action<bool>? ShortcutChanged;
    public event Action? CancelPressed;
    public event Action<Hotkey?>? ShortcutCaptureCompleted;

    public GlobalKeyboard()
    {
        _callback = Handle;
        _hook = NativeMethods.SetWindowsHookEx(13, _callback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the global keyboard listener.");
        if (NativeMethods.IsDown(0x11)) _modifiersDown |= HotkeyModifiers.Control;
        if (NativeMethods.IsDown(0x12)) _modifiersDown |= HotkeyModifiers.Alt;
        if (NativeMethods.IsDown(0x10)) _modifiersDown |= HotkeyModifiers.Shift;
        if (NativeMethods.IsDown(0x5B) || NativeMethods.IsDown(0x5C)) _modifiersDown |= HotkeyModifiers.Windows;
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
                UpdateModifierState((int)key.Key, down);
                if (_capture is not null)
                {
                    var result = _capture.Process((int)key.Key, down);
                    if (result is { } completed)
                    {
                        _capture = null;
                        ShortcutCaptureCompleted?.Invoke(completed.Cancelled ? null : CreateHotkey(completed));
                    }
                    return 1;
                }
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
        // The primary key can itself be a modifier (e.g. Right Ctrl).
        var primary = HotkeyCapture.ModifierFor(Shortcut.VirtualKey);
        return (_modifiersDown & ~primary) == Shortcut.Modifiers;
    }

    public void BeginShortcutCapture()
    {
        _capture = new HotkeyCapture();
        Suspended = true;
    }

    public void EndShortcutCapture()
    {
        _capture = null;
        Suspended = false;
    }

    private void UpdateModifierState(int virtualKey, bool down)
    {
        var modifier = HotkeyCapture.ModifierFor(virtualKey);
        if (modifier == HotkeyModifiers.None) return;
        if (down) _modifiersDown |= modifier;
        else _modifiersDown &= ~modifier;
    }

    private static Hotkey CreateHotkey(HotkeyCaptureResult result)
    {
        var labels = new List<string>();
        if (result.Modifiers.HasFlag(HotkeyModifiers.Control)) labels.Add("Ctrl");
        if (result.Modifiers.HasFlag(HotkeyModifiers.Alt)) labels.Add("Alt");
        if (result.Modifiers.HasFlag(HotkeyModifiers.Shift)) labels.Add("Shift");
        if (result.Modifiers.HasFlag(HotkeyModifiers.Windows)) labels.Add("Win");
        labels.Add(KeyLabel(result.VirtualKey));
        return new(result.VirtualKey, result.Modifiers, string.Join(" + ", labels));
    }

    private static string KeyLabel(int virtualKey) => virtualKey switch
    {
        0x11 => "Ctrl", 0x12 => "Alt", 0x10 => "Shift",
        0xA2 => "Left Ctrl", 0xA3 => "Right Ctrl", 0xA4 => "Left Alt", 0xA5 => "Right Alt",
        0xA0 => "Left Shift", 0xA1 => "Right Shift", 0x5B => "Left Win", 0x5C => "Right Win",
        _ => KeyInterop.KeyFromVirtualKey(virtualKey).ToString()
    };

    public void Dispose() { NativeMethods.UnhookWindowsHookEx(_hook); GC.KeepAlive(_callback); }
}
