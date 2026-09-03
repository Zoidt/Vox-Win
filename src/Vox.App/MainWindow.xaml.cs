using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using Vox.Core;
using Vox.Windows;

namespace Vox.App;

public partial class MainWindow : Window
{
    private readonly VoxController _controller;
    private bool _loading = true;
    private bool _capturing;

    public MainWindow(VoxController controller)
    {
        _controller = controller;
        InitializeComponent();
        SourceInitialized += (_, _) => WindowAppearance.UseDarkTitleBar(this);
        DataContext = controller;
        ApplyPreferences();
        _controller.ShortcutCaptureCompleted += EndCapture;
    }

    private void ApplyPreferences()
    {
        _loading = true;
        SoundCheck.IsChecked = _controller.Settings.SoundCues;
        StartupCheck.IsChecked = _controller.Settings.StartWithWindows;
        MicrophoneBox.SelectedItem = _controller.Microphones.FirstOrDefault(m => m.Id == _controller.Settings.MicrophoneId)
            ?? _controller.Microphones[0];
        _loading = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        EndCapture(null);
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void CaptureShortcut(object sender, RoutedEventArgs e)
    {
        if (!_controller.BeginShortcutCapture())
        {
            _controller.SetStatus("Keyboard listener unavailable. Restart Vox to set a shortcut.");
            return;
        }
        _capturing = true;
        ShortcutButton.Content = "Press a key… Esc cancels";
        ShortcutButton.Focus();
    }

    private void EndCapture(Hotkey? shortcut)
    {
        if (!_capturing) return;
        _capturing = false;
        if (shortcut is not null) _controller.SaveSettings(_controller.Settings with { Shortcut = shortcut });
        _controller.CancelShortcutCapture();
        ShortcutButton.SetBinding(ContentProperty, new Binding(nameof(VoxController.ShortcutLabel)));
    }

    private void PreferencesChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _controller.SaveSettings(_controller.Settings with { SoundCues = SoundCheck.IsChecked == true, StartWithWindows = StartupCheck.IsChecked == true });
        ApplyPreferences();
    }
    private void MicrophoneChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading || MicrophoneBox.SelectedItem is not MicrophoneOption option) return;
        _controller.SaveSettings(_controller.Settings with { MicrophoneId = option.Id });
    }
    private void RefreshMicrophones(object sender, RoutedEventArgs e) { _loading = true; _controller.RefreshMicrophones(); ApplyPreferences(); }
    private void EditReplacements(object sender, RoutedEventArgs e)
    {
        if (!_controller.CanConfigure) return;
        EndCapture(null);
        _controller.SuspendDictationHotkey(true);
        try { new TextReplacementsWindow(_controller) { Owner = this }.ShowDialog(); }
        finally { _controller.SuspendDictationHotkey(false); }
    }
    private async void DownloadModel(object sender, RoutedEventArgs e) => await _controller.DownloadModelAsync();
    private void PasteLast(object sender, RoutedEventArgs e) { _controller.ArmReplay(); Hide(); }
    private void CopyLast(object sender, RoutedEventArgs e)
    {
        if (_controller.LastTranscript is not { } text) return;
        try { Clipboard.SetText(text); _controller.SetStatus("Last dictation copied to your clipboard"); }
        catch (ExternalException) { _controller.SetStatus("Clipboard is busy. Try Copy last again."); }
    }
    private void HideWindow(object sender, RoutedEventArgs e) => Hide();
    private async void QuitVox(object sender, RoutedEventArgs e) => await ((App)Application.Current).QuitAsync();
}
