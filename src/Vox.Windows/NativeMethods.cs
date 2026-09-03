using System.Runtime.InteropServices;

namespace Vox.Windows;

internal static class NativeMethods
{
    internal const nuint VoxInputMarker = 0x564F5831;
    internal delegate nint HookProc(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData { public uint Key, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }

    internal static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    internal static Input Key(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new() { Keyboard = new() { Key = key, Flags = up ? 2u : 0u, Extra = VoxInputMarker } }
    };
}
