namespace Vox.Core;

public readonly record struct HotkeyCaptureResult(int VirtualKey, HotkeyModifiers Modifiers, bool Cancelled = false);

/// <summary>Builds one shortcut from raw Windows key edges without depending on WPF focus or key translation.</summary>
public sealed class HotkeyCapture
{
    private const int Escape = 0x1B;
    private HotkeyModifiers _modifiersDown;
    private int? _modifierCandidate;
    private bool _complete;

    public HotkeyCaptureResult? Process(int virtualKey, bool down)
    {
        if (_complete) return null;

        var modifier = ModifierFor(virtualKey);
        if (down) _modifiersDown |= modifier;

        if (virtualKey == Escape && down)
        {
            _complete = true;
            return new(0, HotkeyModifiers.None, Cancelled: true);
        }

        if (modifier != HotkeyModifiers.None)
        {
            if (down) _modifierCandidate = virtualKey;
            else
            {
                _modifiersDown &= ~modifier;
                if (_modifierCandidate == virtualKey)
                {
                    _complete = true;
                    return new(virtualKey, HotkeyModifiers.None);
                }
            }
            return null;
        }

        if (!down) return null;
        _complete = true;
        return new(virtualKey, _modifiersDown);
    }

    public static HotkeyModifiers ModifierFor(int virtualKey) => virtualKey switch
    {
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,
        0x5B or 0x5C => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None
    };
}
