using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Vox.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showRegistration;
    private VoxController? _controller;
    private MainWindow? _settings;
    private RecordingOverlay? _overlay;
    private Forms.NotifyIcon? _tray;
    private Icon? _icon;
    private bool _quitting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (e.Args is ["--render-preview", var path])
            {
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                await using var previewController = new VoxController(Dispatcher, true);
                var preview = new MainWindow(previewController);
                var content = (FrameworkElement)preview.Content;
                preview.Content = null;
                var surface = new System.Windows.Controls.Border
                {
                    Background = (System.Windows.Media.Brush)Resources["BackgroundBrush"],
                    Child = content, DataContext = previewController,
                    Width = 540, Height = 805
                };
                System.Windows.Documents.TextElement.SetForeground(surface, (System.Windows.Media.Brush)Resources["TextBrush"]);
                System.Windows.Documents.TextElement.SetFontFamily(surface, new System.Windows.Media.FontFamily("Segoe UI"));
                System.Windows.Documents.TextElement.SetFontSize(surface, 14);
                using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Vox layout render")
                {
                    Width = 540, Height = 805, WindowStyle = unchecked((int)0x88000000)
                });
                presentation.RootVisual = surface;
                surface.Measure(new System.Windows.Size(540, 805)); surface.Arrange(new Rect(0, 0, 540, 805)); surface.UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var bitmap = new RenderTargetBitmap(540, 805, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(surface);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                using (var stream = File.Create(path)) encoder.Save(stream);
                Shutdown(); return;
            }
            _mutex = new Mutex(true, @"Local\Vox.Dictation", out var firstInstance);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Vox.ShowSettings");
            if (!firstInstance) { _showEvent.Set(); Shutdown(); return; }
            _controller = new VoxController(Dispatcher);
            _settings = new MainWindow(_controller);
            MainWindow = _settings;
            _overlay = new RecordingOverlay(_controller);
            _controller.PropertyChanged += ControllerChanged;
            _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(ShowSettings), null, Timeout.Infinite, false);
            CreateTray();
            if (!e.Args.Contains("--background")) ShowSettings();
            await _controller.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (e.Args is ["--render-preview", var failedPath])
            {
                File.WriteAllText(failedPath + ".error.txt", ex.ToString());
                Shutdown(1); return;
            }
            MessageBox.Show(ex.Message, "Vox could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            await QuitAsync();
        }
    }

    private void CreateTray()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(164, 186, 255));
            var heights = new[] { 8, 17, 25, 14, 9 };
            for (var i = 0; i < heights.Length; i++) graphics.FillRectangle(brush, 3 + i * 6, (32 - heights[i]) / 2, 3, heights[i]);
        }
        var handle = bitmap.GetHicon();
        using (var temporary = Icon.FromHandle(handle)) _icon = (Icon)temporary.Clone();
        DestroyIcon(handle);
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = "Vox · local dictation", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.BeginInvoke(ShowSettings));
        var paste = menu.Items.Add("Paste last again", null, (_, _) => Dispatcher.BeginInvoke(() => _controller?.ArmReplay()));
        menu.Opening += (_, _) => paste.Enabled = _controller?.HasTranscript == true && _controller.CanConfigure;
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit Vox", null, (_, _) => Dispatcher.BeginInvoke(async () => await QuitAsync()));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowSettings);
    }

    private void ShowSettings()
    {
        if (_quitting || _settings is null) return;
        _settings.Show(); _settings.WindowState = WindowState.Normal; _settings.Activate();
    }

    private void ControllerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_controller is null || _overlay is null || _quitting) return;
        if (e.PropertyName == nameof(VoxController.IsOverlayVisible))
        {
            if (_controller.IsOverlayVisible) { _overlay.Show(); _overlay.Position(); }
            else _overlay.Hide();
        }
    }

    public async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true;
        _tray?.Dispose(); _icon?.Dispose(); _overlay?.Close();
        _showRegistration?.Unregister(null);
        try { if (_controller is not null) await _controller.DisposeAsync(); }
        finally { Shutdown(); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showEvent?.Dispose(); _mutex?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
}
