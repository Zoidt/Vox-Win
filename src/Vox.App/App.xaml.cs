using System.ComponentModel;
using System.Drawing;
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
            if (e.Args is ["--render-preview" or "--render-replacements-preview", var path])
            {
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                await using var previewController = new VoxController(Dispatcher, true);
                Window preview = e.Args[0] == "--render-replacements-preview"
                    ? new TextReplacementsWindow(previewController) : new MainWindow(previewController);
                if (preview is TextReplacementsWindow dictionary)
                {
                    dictionary.SampleInput.Text = "I use open ai with vox.";
                    dictionary.SampleOutput.Text = new Vox.Core.TextRewriter(previewController.Settings.Replacements).Apply(dictionary.SampleInput.Text);
                }
                var width = (int)preview.Width;
                var height = (int)preview.Height;
                var content = (FrameworkElement)preview.Content;
                preview.Content = null;
                var surface = new System.Windows.Controls.Border
                {
                    Background = preview.Background,
                    Child = content, DataContext = previewController,
                    Width = width, Height = height
                };
                System.Windows.Documents.TextElement.SetForeground(surface, preview.Foreground);
                System.Windows.Documents.TextElement.SetFontFamily(surface, preview.FontFamily);
                System.Windows.Documents.TextElement.SetFontSize(surface, preview.FontSize);
                using var presentation = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Vox layout render")
                {
                    Width = width, Height = height, WindowStyle = unchecked((int)0x88000000)
                });
                presentation.RootVisual = surface;
                surface.Measure(new System.Windows.Size(width, height)); surface.Arrange(new Rect(0, 0, width, height)); surface.UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
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
            var checkStartup = e.Args is ["--check-startup", _];
            if (!e.Args.Contains("--background") && !checkStartup) ShowSettings();
            await _controller.InitializeAsync();
            if (checkStartup)
            {
                var report = new
                {
                    ModelReady = _controller.IsReady,
                    KeyboardListenerReady = _controller.HotkeyAvailable,
                    MicrophoneChoices = _controller.Microphones.Count,
                    Status = _controller.Status
                };
                File.WriteAllText(e.Args[1], System.Text.Json.JsonSerializer.Serialize(report));
                if (!report.ModelReady || !report.KeyboardListenerReady) Environment.ExitCode = 1;
                await QuitAsync();
            }
        }
        catch (Exception ex)
        {
            if (e.Args is ["--render-preview" or "--render-replacements-preview", var failedPath])
            {
                File.WriteAllText(failedPath + ".error.txt", ex.ToString());
                Shutdown(1); return;
            }
            if (e.Args is ["--check-startup", var checkPath])
            {
                File.WriteAllText(checkPath, System.Text.Json.JsonSerializer.Serialize(new { Error = ex.Message }));
                await QuitAsync(); Environment.ExitCode = 1; return;
            }
            MessageBox.Show(ex.Message, "Vox could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            await QuitAsync();
        }
    }

    private void CreateTray()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/vox.ico"))
            ?? throw new InvalidOperationException("The Vox application icon is missing.");
        using (resource.Stream)
        using (var loaded = new Icon(resource.Stream)) _icon = (Icon)loaded.Clone();
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
}
