using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Vox.App;

public partial class RecordingOverlay : Window
{
    public RecordingOverlay(VoxController controller)
    {
        InitializeComponent(); DataContext = controller;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(handle, -20, GetWindowLongPtr(handle, -20) | 0x08000000 | 0x80 | 0x20);
        };
        SizeChanged += (_, _) => Position();
    }
    public void Position()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight - 28;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
}
