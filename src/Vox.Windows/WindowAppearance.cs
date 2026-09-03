using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Vox.Windows;

public static class WindowAppearance
{
    public static void UseDarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;

        var enabled = 1;
        // Windows 10 20H1 and newer use attribute 20; older compatible builds used 19.
        if (NativeMethods.DwmSetWindowAttribute(handle, 20, ref enabled, Marshal.SizeOf<int>()) != 0)
            NativeMethods.DwmSetWindowAttribute(handle, 19, ref enabled, Marshal.SizeOf<int>());
    }
}
